using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Tags;

namespace CsWSC.LanguageServer;

// ---- 拡張機能とやりとりするデータ (位置はすべて UTF-16 のオフセット) ----

internal sealed record CompletionEntry(int Index, string Label, string Kind, string? Detail, string? SortText, string? FilterText, string InsertText);
internal sealed record CompletionResult(int Start, int End, CompletionEntry[] Items);
internal sealed record HoverResult(string Signature, string? Documentation, int Start, int End);
internal sealed record SignatureEntry(string Label, string? Documentation, ParameterEntry[] Parameters);
internal sealed record ParameterEntry(string Label, string? Documentation);
internal sealed record SignatureResult(SignatureEntry[] Signatures, int ActiveSignature, int ActiveParameter);
internal sealed record DiagnosticEntry(int Start, int End, string Severity, string Code, string Message);

/// <summary>補完・ホバー・引数ヒント・診断。</summary>
internal sealed class LanguageService(ScriptWorkspace workspace)
{
    // 詳細説明 (resolve) 用に直前の補完結果を覚えておく
    private (Document Document, CompletionList List)? _lastCompletion;

    public async Task<CompletionResult?> CompleteAsync(string uri, string text, int offset)
    {
        var doc = workspace.GetDocument(uri, text);
        var service = CompletionService.GetService(doc);
        if (service == null) return null;
        var list = await service.GetCompletionsAsync(doc, offset);
        _lastCompletion = (doc, list);
        var items = list.ItemsList.Select((item, i) => new CompletionEntry(
            i,
            item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
            KindOf(item),
            string.IsNullOrEmpty(item.InlineDescription) ? null : item.InlineDescription,
            // ラベルと同じなら省略して応答を小さくする (候補が数千件になるため)
            item.SortText == item.DisplayText ? null : item.SortText,
            item.FilterText == item.DisplayText ? null : item.FilterText,
            item.DisplayText)).ToArray();
        return new CompletionResult(list.Span.Start, list.Span.End, items);
    }

    /// <summary>補完候補の説明 (シグネチャと XML ドキュメント)。</summary>
    public async Task<string?> ResolveCompletionAsync(int index)
    {
        if (_lastCompletion is not var (doc, list) || index < 0 || index >= list.ItemsList.Count) return null;
        var item = list.ItemsList[index];
        var service = CompletionService.GetService(doc);
        var description = service == null ? null : await service.GetDescriptionAsync(doc, item);
        if (description == null) return null;

        // 1 行目 (シグネチャ) は Roslyn の説明を使い、本文は XML ドキュメントの remarks・example まで含めたものに差し替える
        var lines = description.Text.Split('\n', 2);
        var symbol = await FindCompletionSymbolAsync(doc, list.Span.Start, item.DisplayText);
        var body = symbol == null ? null : Summary(symbol);
        return body == null ? description.Text : $"{lines[0].TrimEnd()}\n{body}";
    }

    /// <summary>補完候補の名前から、その位置で参照できるシンボルを探す (a.b の b なら a の型のメンバーから)。</summary>
    private static async Task<ISymbol?> FindCompletionSymbolAsync(Document doc, int position, string name)
    {
        var root = await doc.GetSyntaxRootAsync();
        var model = await doc.GetSemanticModelAsync();
        if (root == null || model == null) return null;
        INamespaceOrTypeSymbol? container = null;
        var token = root.FindToken(Math.Max(0, position - 1));
        if (token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.DotToken) && token.Parent is MemberAccessExpressionSyntax access)
        {
            var info = model.GetSymbolInfo(access.Expression).Symbol;
            container = info as INamespaceOrTypeSymbol ?? model.GetTypeInfo(access.Expression).Type;
            if (container == null) return null;
        }
        return model.LookupSymbols(position, container, name).FirstOrDefault();
    }

    public async Task<HoverResult?> HoverAsync(string uri, string text, int offset)
    {
        var doc = workspace.GetDocument(uri, text);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(doc, offset);
        if (symbol == null) return null;
        var root = await doc.GetSyntaxRootAsync();
        var token = root!.FindToken(offset);
        var display = symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor
            ? $"new {ctor.ContainingType.ToDisplayString(Format)}({string.Join(", ", ctor.Parameters.Select(p => p.ToDisplayString(Format)))})"
            : symbol.ToDisplayString(Format);
        return new HoverResult(display, Summary(symbol), token.Span.Start, token.Span.End);
    }

    public async Task<SignatureResult?> SignatureHelpAsync(string uri, string text, int offset)
    {
        var doc = workspace.GetDocument(uri, text);
        var root = await doc.GetSyntaxRootAsync();
        var model = await doc.GetSemanticModelAsync();
        if (root == null || model == null) return null;

        // カーソルを含む最も内側の呼び出し (引数リストの括弧の内側)
        var node = root.FindToken(Math.Max(0, offset - 1)).Parent;
        ArgumentListSyntax? args = null;
        IMethodSymbol[] methods = [];
        for (; node != null; node = node.Parent)
        {
            if (node is InvocationExpressionSyntax inv && Inside(inv.ArgumentList, offset))
            {
                args = inv.ArgumentList;
                methods = model.GetMemberGroup(inv.Expression).OfType<IMethodSymbol>().ToArray();
                break;
            }
            if (node is BaseObjectCreationExpressionSyntax create && create.ArgumentList is { } al && Inside(al, offset))
            {
                args = al;
                methods = model.GetSymbolInfo(create).CandidateSymbols.Prepend(model.GetSymbolInfo(create).Symbol)
                    .OfType<IMethodSymbol>().SelectMany(m => m.ContainingType.InstanceConstructors).Distinct<IMethodSymbol>(SymbolEqualityComparer.Default).ToArray();
                if (methods.Length == 0 && model.GetTypeInfo(create).Type is INamedTypeSymbol t) methods = [.. t.InstanceConstructors];
                break;
            }
        }
        if (args == null || methods.Length == 0) return null;

        var activeParam = args.Arguments.GetSeparators().Count(s => s.SpanStart < offset);
        var argCount = args.Arguments.Count;
        var chosen = model.GetSymbolInfo(args.Parent!).Symbol as IMethodSymbol;
        var signatures = methods.Select(ToSignature).ToArray();
        var active = chosen != null ? Array.FindIndex(methods, m => SymbolEqualityComparer.Default.Equals(m, chosen)) : -1;
        if (active < 0) active = Array.FindIndex(methods, m => m.Parameters.Length >= Math.Max(argCount, activeParam + 1) || m.Parameters.LastOrDefault()?.IsParams == true);
        return new SignatureResult(signatures, Math.Max(0, active), activeParam);
    }

    public async Task<DiagnosticEntry[]> DiagnoseAsync(string uri, string text)
    {
        var doc = workspace.GetDocument(uri, text);
        var model = await doc.GetSemanticModelAsync();
        if (model == null) return [];
        return model.GetDiagnostics()
            .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning && d.Location.IsInSource)
            .Select(d => new DiagnosticEntry(d.Location.SourceSpan.Start, d.Location.SourceSpan.End,
                d.Severity == DiagnosticSeverity.Error ? "error" : "warning", d.Id, d.GetMessage()))
            .ToArray();
    }

    public void Close(string uri) => workspace.Close(uri);

    // ---- ヘルパー ----

    private static readonly SymbolDisplayFormat Format = SymbolDisplayFormat.MinimallyQualifiedFormat
        .AddMemberOptions(SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType)
        .AddParameterOptions(SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut)
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat ParameterFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
                          SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static bool Inside(ArgumentListSyntax list, int offset) =>
        offset > list.OpenParenToken.SpanStart && (list.CloseParenToken.IsMissing || offset <= list.CloseParenToken.SpanStart);

    private static SignatureEntry ToSignature(IMethodSymbol m)
    {
        var docXml = ParseDoc(m);
        var parameters = m.Parameters.Select(p => new ParameterEntry(p.ToDisplayString(ParameterFormat),
            docXml?.Elements("param").FirstOrDefault(e => (string?)e.Attribute("name") == p.Name) is { } pe ? Clean(pe) : null)).ToArray();
        var name = m.MethodKind == MethodKind.Constructor ? m.ContainingType.Name : m.Name;
        var ret = m.MethodKind == MethodKind.Constructor ? "" : m.ReturnType.ToDisplayString(ParameterFormat) + " ";
        var label = $"{ret}{name}({string.Join(", ", parameters.Select(p => p.Label))})";
        return new SignatureEntry(label, Summary(m), parameters);
    }

    private static XElement? ParseDoc(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: true);
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try { return XElement.Parse(xml.Trim().StartsWith("<member") ? xml : $"<member>{xml}</member>"); }
        catch (System.Xml.XmlException) { return null; }
    }

    /// <summary>summary・remarks・returns・example を Markdown にまとめる。</summary>
    private static string? Summary(ISymbol symbol)
    {
        var doc = ParseDoc(symbol is IMethodSymbol { ReducedFrom: { } r } ? r : symbol);
        if (doc == null) return null;
        var parts = new List<string>();
        if (doc.Element("summary") is { } s) parts.Add(Clean(s));
        if (doc.Element("remarks") is { } rem) parts.Add(Clean(rem));
        if (doc.Element("returns") is { } ret) parts.Add($"**戻り値**: {Clean(ret)}");
        foreach (var ex in doc.Elements("example")) parts.Add($"**例**\n{Clean(ex)}");
        parts.RemoveAll(string.IsNullOrWhiteSpace);
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    /// <summary>XML ドキュメントの要素を Markdown にする。code はコードブロック、see などはインラインコードにする。</summary>
    private static string Clean(XElement e)
    {
        var sb = new StringBuilder();
        foreach (var n in e.Nodes())
        {
            switch (n)
            {
                case XText t: AppendProse(sb, t.Value); break;
                case XElement { Name.LocalName: "see" or "seealso" } see:
                    var cref = (string?)see.Attribute("cref") ?? (string?)see.Attribute("langword") ?? (string?)see.Attribute("href") ?? see.Value;
                    sb.Append('`').Append(cref.Contains(':') ? ShortName(cref[(cref.IndexOf(':') + 1)..]) : cref).Append('`');
                    break;
                case XElement { Name.LocalName: "paramref" or "typeparamref" } pr: sb.Append('`').Append((string?)pr.Attribute("name")).Append('`'); break;
                case XElement { Name.LocalName: "c" } c: sb.Append('`').Append(c.Value).Append('`'); break;
                case XElement { Name.LocalName: "code" } code: sb.Append("\n```csharp\n").Append(Dedent(code.Value)).Append("\n```\n"); break;
                case XElement { Name.LocalName: "para" or "br" } p: sb.Append("\n\n").Append(Clean(p)); break;
                case XElement other: sb.Append(Clean(other)); break;
            }
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// 文中の改行・インデントは XML 上の折り返しなので 1 行に詰める。
    /// 日本語同士をつなぐときは空白を入れない。
    /// </summary>
    private static void AppendProse(StringBuilder sb, string text)
    {
        var lines = text.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (i > 0) line = line.TrimStart();
            if (i < lines.Length - 1) line = line.TrimEnd();
            line = string.Join(' ', line.Split([' ', '\t']).Where((w, j) => w.Length > 0 || j == 0)).Replace("  ", " ");
            if (line.Trim().Length == 0)
            {
                if (lines.Length == 1 && line.Length > 0 && sb.Length > 0 && !char.IsWhiteSpace(sb[^1])) sb.Append(' ');
                continue;
            }
            if (i > 0 && sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && !(IsWide(sb[^1]) && IsWide(line[0]))) sb.Append(' ');
            sb.Append(line);
        }
    }

    private static bool IsWide(char c) => c >= 0x2E80;

    // M:CsWSC.Window.Click(System.Int32,...) → Window.Click
    private static string ShortName(string id)
    {
        var name = id.Split('(')[0].Split('.');
        return name.Length >= 2 ? $"{name[^2]}.{name[^1]}" : name[^1];
    }

    private static string Dedent(string code)
    {
        var lines = code.Replace("\r", "").Split('\n')
            .SkipWhile(string.IsNullOrWhiteSpace).Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse().ToArray();
        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
    }

    private static string KindOf(CompletionItem item)
    {
        foreach (var tag in item.Tags)
        {
            switch (tag)
            {
                case WellKnownTags.Method or WellKnownTags.ExtensionMethod: return "method";
                case WellKnownTags.Property: return "property";
                case WellKnownTags.Field: return "field";
                case WellKnownTags.Event: return "event";
                case WellKnownTags.Class: return "class";
                case WellKnownTags.Structure: return "struct";
                case WellKnownTags.Interface: return "interface";
                case WellKnownTags.Enum: return "enum";
                case WellKnownTags.EnumMember: return "enumMember";
                case WellKnownTags.Delegate: return "class";
                case WellKnownTags.Namespace: return "module";
                case WellKnownTags.Keyword: return "keyword";
                case WellKnownTags.Local or WellKnownTags.Parameter or WellKnownTags.RangeVariable: return "variable";
                case WellKnownTags.Constant: return "constant";
                case WellKnownTags.TypeParameter: return "typeParameter";
                case WellKnownTags.Snippet: return "snippet";
            }
        }
        return "text";
    }
}
