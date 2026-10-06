using System.Collections.Immutable;
using CsWSC;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace CsWSC.LanguageServer;

/// <summary>
/// .csws を CsWSC の実行時と同じ条件 (スクリプト形式・ScriptGlobals がホスト・同じ using) で解析する Roslyn ワークスペース。
/// スクリプトは互いに独立なので、ファイルごとに 1 プロジェクトを作る。
/// </summary>
internal sealed class ScriptWorkspace
{
    private readonly AdhocWorkspace _workspace = new(MefHostServices.DefaultHost);
    private readonly Dictionary<string, DocumentId> _documents = [];
    private readonly ImmutableArray<MetadataReference> _references = LoadReferences();

    /// <summary>uri のドキュメントを最新の内容で返す。</summary>
    public Document GetDocument(string uri, string text)
    {
        if (!_documents.TryGetValue(uri, out var id))
        {
            var projectId = ProjectId.CreateNewId();
            id = DocumentId.CreateNewId(projectId);
            var project = ProjectInfo.Create(projectId, VersionStamp.Create(), uri, "csws", LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: ScriptRunner.Imports),
                parseOptions: new CSharpParseOptions(LanguageVersion.Latest, kind: SourceCodeKind.Script),
                metadataReferences: _references,
                isSubmission: true,
                hostObjectType: typeof(ScriptGlobals));
            _workspace.AddProject(project);
            _workspace.AddDocument(DocumentInfo.Create(id, Path.GetFileName(uri), sourceCodeKind: SourceCodeKind.Script,
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(""), VersionStamp.Create()))));
            _documents[uri] = id;
        }
        var solution = _workspace.CurrentSolution.WithDocumentText(id, SourceText.From(text));
        _workspace.TryApplyChanges(solution);
        return _workspace.CurrentSolution.GetDocument(id)!;
    }

    public void Close(string uri)
    {
        if (_documents.Remove(uri, out var id)) _workspace.TryApplyChanges(_workspace.CurrentSolution.RemoveProject(id.ProjectId));
    }

    // 実行時と同じ共有フレームワーク (.NET + WindowsDesktop) と CsWSC を参照する
    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        var frameworkDirs = ScriptRunner.References
            .Select(a => Path.GetDirectoryName(a.Location)!)
            .Where(d => d.Contains("shared", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tpa = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator);
        var files = tpa.Where(f => frameworkDirs.Contains(Path.GetDirectoryName(f), StringComparer.OrdinalIgnoreCase))
            .Append(typeof(ScriptGlobals).Assembly.Location);

        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { builder.Add(MetadataReference.CreateFromFile(file, documentation: FindDocumentation(file))); }
            catch (Exception e) when (e is BadImageFormatException or IOException) { }
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// XML ドキュメント (説明文) を探す。CsWSC は dll の隣にある。
    /// .NET の実装 dll (System.Private.CoreLib など) は説明文の XML と名前が違う (System.Runtime.xml など) ため、
    /// SDK の参照パック (packs/*.Ref) の XML をまとめて検索する。
    /// </summary>
    private static DocumentationProvider? FindDocumentation(string dll)
    {
        var xml = Path.ChangeExtension(dll, ".xml");
        if (File.Exists(xml)) return XmlDocumentationProvider.CreateFromFile(xml);
        return FrameworkDocumentation.Value;
    }

    private static readonly Lazy<string[]> RefPackDirs = new(() =>
    {
        // ...\dotnet\shared\Microsoft.NETCore.App\10.0.x → ...\dotnet\packs
        var shared = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var packs = Path.GetFullPath(Path.Combine(shared, "..", "..", "..", "packs"));
        if (!Directory.Exists(packs)) return [];
        var major = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
        return new[] { "Microsoft.NETCore.App.Ref", "Microsoft.WindowsDesktop.App.Ref" }
            .Select(p => Path.Combine(packs, p))
            .Where(Directory.Exists)
            .Select(p => Directory.GetDirectories(p)
                .OrderByDescending(v => Version.TryParse(Path.GetFileName(v), out var ver) ? ver : new Version())
                .FirstOrDefault())
            .Where(v => v != null)
            .Select(v => Path.Combine(v!, "ref", major))
            .Where(Directory.Exists)
            .ToArray();
    });

    private static readonly Lazy<DocumentationProvider?> FrameworkDocumentation = new(() =>
    {
        var files = RefPackDirs.Value.SelectMany(d => Directory.GetFiles(d, "*.xml"))
            // よく使うものを先に探す
            .OrderBy(f => Path.GetFileName(f) switch
            {
                "System.Runtime.xml" => 0, "System.Linq.xml" => 1, "System.Collections.xml" => 2,
                "System.Windows.Forms.xml" => 3, "System.Drawing.Primitives.xml" => 4, _ => 9,
            })
            .ToArray();
        return files.Length == 0 ? null : new CompositeDocumentationProvider(files);
    });

    private sealed class CompositeDocumentationProvider(string[] files) : DocumentationProvider
    {
        // ファイルは必要になった時点で読み、メンバー ID → <member> 要素の XML を覚える
        private readonly Lazy<Dictionary<string, string>>[] _files =
            files.Select(f => new Lazy<Dictionary<string, string>>(() => Load(f))).ToArray();

        protected override string? GetDocumentationForSymbol(string documentationMemberID, System.Globalization.CultureInfo preferredCulture, CancellationToken cancellationToken = default)
        {
            foreach (var f in _files)
                if (f.Value.TryGetValue(documentationMemberID, out var xml)) return xml;
            return null;
        }

        private static Dictionary<string, string> Load(string file)
        {
            var map = new Dictionary<string, string>();
            try
            {
                using var reader = System.Xml.XmlReader.Create(file);
                while (!reader.EOF)
                {
                    if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.Name == "member" && reader.GetAttribute("name") is { } name)
                        map[name] = reader.ReadOuterXml();
                    else reader.Read();
                }
            }
            catch (System.Xml.XmlException) { }
            return map;
        }

        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
}
