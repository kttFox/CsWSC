using System.Collections.Immutable;
using CsWSC;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;

namespace CsWSC.LanguageServer;

/// <summary>
/// .csws を CsWSC の実行時と同じ条件 (スクリプト形式・ScriptGlobals がホスト・同じ using) で解析する Roslyn ワークスペースです。
/// スクリプトは互いに独立なので、ファイルごとに 1 プロジェクトを作ります。
/// </summary>
internal sealed partial class ScriptWorkspace {
	private readonly AdhocWorkspace _workspace = new( MefHostServices.DefaultHost );
	private readonly Dictionary<string, DocumentId> _documents = [];
	private readonly ImmutableArray<MetadataReference> _references = LoadReferences();

	/// <summary>スクリプトの #r でライブラリ (.cswsl) を解決するリゾルバーと、プロジェクトを作った時刻です。</summary>
	private readonly Dictionary<string, (ScriptLibraryResolver Resolver, DateTime Created)> _libraryResolvers = [];

	/// <summary>uri のドキュメントを最新の内容で返します。</summary>
	public Document GetDocument( string uri, string text ) {
		// #r したライブラリが保存されたら、スクリプトのプロジェクトを作り直して参照し直す
		// (スクリプトのテキストが同じだとコンパイル結果が使い回され、ライブラリを読み直さないため)
		if( _libraryResolvers.TryGetValue( uri, out var library )
			&& library.Resolver.Sources.Any( f => File.GetLastWriteTimeUtc( f ) > library.Created ) ) {
			Close( uri );
		}

		if( !_documents.TryGetValue( uri, out var id ) ) {
			var projectId = ProjectId.CreateNewId();
			id = DocumentId.CreateNewId( projectId );
			// #r / #load の相対パスは実行時と同じくスクリプトのフォルダを基準にする
			var filePath = ToFilePath( uri );
			var baseDir = filePath != null ? Path.GetDirectoryName( filePath )! : AppContext.BaseDirectory;
			if( ScriptLibraryResolver.IsLibrary( uri ) ) {
				AddLibrary( projectId, id, uri, filePath );
				_documents[uri] = id;
				return GetDocument( uri, text );
			}

			var resolver = new ScriptLibraryResolver( baseDir, _references );
			_libraryResolvers[uri] = (resolver, DateTime.UtcNow);
			var project = ProjectInfo.Create( projectId, VersionStamp.Create(), uri, "cswsc", LanguageNames.CSharp,
				filePath: filePath,
				compilationOptions: new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary, usings: ScriptRunner.Imports, nullableContextOptions: ScriptRunner.NullableContext )
					.WithMetadataReferenceResolver( resolver )
					.WithSourceReferenceResolver( ScriptSourceResolver.Default.WithBaseDirectory( baseDir ) ),
				parseOptions: new CSharpParseOptions( LanguageVersion.Latest, kind: SourceCodeKind.Script ),
				metadataReferences: _references,
				isSubmission: true,
				hostObjectType: typeof( ScriptGlobals ) );
			_workspace.AddProject( project );
			_workspace.AddDocument( DocumentInfo.Create( id, Path.GetFileName( uri ), sourceCodeKind: SourceCodeKind.Script, filePath: filePath,
				loader: TextLoader.From( TextAndVersion.Create( SourceText.From( "" ), VersionStamp.Create() ) ) ) );
			_documents[uri] = id;
		}
		var solution = _workspace.CurrentSolution.WithDocumentText( id, SourceText.From( text ) );
		_workspace.TryApplyChanges( solution );
		return _workspace.CurrentSolution.GetDocument( id )!;
	}

	/// <summary>
	/// #r で参照するライブラリ (.cswsl) を、実行時と同じ条件 (通常の C#・同じ参照と global using) のプロジェクトとして追加します。
	/// </summary>
	private void AddLibrary( ProjectId projectId, DocumentId id, string uri, string? filePath ) {
		var project = ProjectInfo.Create( projectId, VersionStamp.Create(), uri, "cswsl", LanguageNames.CSharp,
			filePath: filePath,
			compilationOptions: ScriptLibraryResolver.CompilationOptions,
			parseOptions: ScriptLibraryResolver.ParseOptions,
			metadataReferences: _references );
		_workspace.AddProject( project );
		_workspace.AddDocument( DocumentInfo.Create( DocumentId.CreateNewId( projectId ), "GlobalUsings.g.cs",
			loader: TextLoader.From( TextAndVersion.Create( SourceText.From( ScriptLibraryResolver.GlobalUsings ), VersionStamp.Create() ) ) ) );
		_workspace.AddDocument( DocumentInfo.Create( id, Path.GetFileName( uri ), filePath: filePath,
			loader: TextLoader.From( TextAndVersion.Create( SourceText.From( "" ), VersionStamp.Create() ) ) ) );
	}

	/// <summary>
	/// file URI をファイルパスにします。file URI でなければ null です。
	/// VS Code はドライブのコロンを file:///c%3A/... とエンコードし、そのままだと Uri.LocalPath が絶対パスにならないので戻します。
	/// </summary>
	internal static string? ToFilePath( string uri ) {
		uri = DriveColon().Replace( uri, "$1:" );
		return Uri.TryCreate( uri, UriKind.Absolute, out var u ) && u.IsFile ? u.LocalPath : null;
	}

	[System.Text.RegularExpressions.GeneratedRegex( @"^(file:///[A-Za-z])%3[Aa]" )]
	private static partial System.Text.RegularExpressions.Regex DriveColon();

	/// <summary>
	/// uri のドキュメントを、スクリプトのフォルダから上にある .editorconfig を読み直したうえで返します。
	/// .editorconfig は編集されることがあるので、書式設定のたびに読み直します。
	/// </summary>
	public Document GetDocumentWithEditorConfig( string uri, string text ) {
		var doc = GetDocument( uri, text );
		var project = doc.Project;
		var solution = project.Solution;
		foreach( var id in project.AnalyzerConfigDocuments.Select( d => d.Id ).ToArray() ) solution = solution.RemoveAnalyzerConfigDocument( id );
		if( doc.FilePath != null ) {
			foreach( var path in FindEditorConfigs( Path.GetDirectoryName( doc.FilePath )! ) ) {
				string content;
				try { content = File.ReadAllText( path ); } catch( IOException ) { continue; }
				solution = solution.AddAnalyzerConfigDocument( DocumentId.CreateNewId( project.Id ), ".editorconfig",
					SourceText.From( content ), filePath: path );
			}
		}
		_workspace.TryApplyChanges( solution );
		return _workspace.CurrentSolution.GetDocument( doc.Id )!;
	}

	/// <summary>dir から上へ .editorconfig を探します。root = true のものがあればそこで止めます。</summary>
	private static List<string> FindEditorConfigs( string dir ) {
		var found = new List<string>();
		for( var d = new DirectoryInfo( dir ); d != null; d = d.Parent ) {
			var path = Path.Combine( d.FullName, ".editorconfig" );
			if( !File.Exists( path ) ) continue;
			found.Add( path );
			if( IsRoot( path ) ) break;
		}
		return found;
	}

	private static bool IsRoot( string path ) {
		try {
			foreach( var line in File.ReadLines( path ) ) {
				var t = line.Trim();
				if( t.StartsWith( '[' ) ) return false; // root はセクションより前にしか書けない
				var eq = t.IndexOf( '=' );
				if( eq > 0 && t[..eq].Trim().Equals( "root", StringComparison.OrdinalIgnoreCase ) )
					return t[( eq + 1 )..].Trim().Equals( "true", StringComparison.OrdinalIgnoreCase );
			}
		} catch( IOException ) { }
		return false;
	}

	public void Close( string uri ) {
		_libraryResolvers.Remove( uri );
		if( _documents.Remove( uri, out var id ) ) _workspace.TryApplyChanges( _workspace.CurrentSolution.RemoveProject( id.ProjectId ) );
	}

	// 実行時と同じ共有フレームワーク (.NET + WindowsDesktop) と CsWSC を参照する
	private static ImmutableArray<MetadataReference> LoadReferences() {
		var frameworkDirs = ScriptRunner.References
			.Select( a => Path.GetDirectoryName( a.Location )! )
			.Where( d => d.Contains( "shared", StringComparison.OrdinalIgnoreCase ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToArray();
		var tpa = ( (string?)AppContext.GetData( "TRUSTED_PLATFORM_ASSEMBLIES" ) ?? "" ).Split( Path.PathSeparator );
		var files = tpa.Where( f => frameworkDirs.Contains( Path.GetDirectoryName( f ), StringComparer.OrdinalIgnoreCase ) )
			.Append( typeof( ScriptGlobals ).Assembly.Location );

		var builder = ImmutableArray.CreateBuilder<MetadataReference>();
		foreach( var file in files.Distinct( StringComparer.OrdinalIgnoreCase ) ) {
			try {
				// CsWSC 自身の dll はビルドで上書きされるため、メモリに読み込んでファイルを開いたままにしない
				builder.Add( string.Equals( file, typeof( ScriptGlobals ).Assembly.Location, StringComparison.OrdinalIgnoreCase )
					? MetadataReference.CreateFromImage( File.ReadAllBytes( file ), documentation: FindDocumentation( file ), filePath: file )
					: MetadataReference.CreateFromFile( file, documentation: FindDocumentation( file ) ) );
			} catch( Exception e ) when( e is BadImageFormatException or IOException ) { }
		}
		return builder.ToImmutable();
	}

	/// <summary>
	/// XML ドキュメント (説明文) を探します。CsWSC は dll の隣にあります。
	/// .NET の実装 dll (System.Private.CoreLib など) は説明文の XML と名前が違う (System.Runtime.xml など) ため、
	/// SDK の参照パック (packs/*.Ref) の XML をまとめて検索します。
	/// </summary>
	private static DocumentationProvider? FindDocumentation( string dll ) {
		var xml = Path.ChangeExtension( dll, ".xml" );
		// ファイルを開いたままにしないよう、内容をメモリに読み込む
		if( File.Exists( xml ) ) return XmlDocumentationProvider.CreateFromBytes( File.ReadAllBytes( xml ) );
		return FrameworkDocumentation.Value;
	}

	private static readonly Lazy<string[]> RefPackDirs = new( () => {
		// ...\dotnet\shared\Microsoft.NETCore.App\10.0.x → ...\dotnet\packs
		var shared = Path.GetDirectoryName( typeof( object ).Assembly.Location )!;
		var packs = Path.GetFullPath( Path.Combine( shared, "..", "..", "..", "packs" ) );
		if( !Directory.Exists( packs ) ) return [];
		var major = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
		return new[] { "Microsoft.NETCore.App.Ref", "Microsoft.WindowsDesktop.App.Ref" }
			.Select( p => Path.Combine( packs, p ) )
			.Where( Directory.Exists )
			.Select( p => Directory.GetDirectories( p )
				.OrderByDescending( v => Version.TryParse( Path.GetFileName( v ), out var ver ) ? ver : new Version() )
				.FirstOrDefault() )
			.Where( v => v != null )
			.Select( v => Path.Combine( v!, "ref", major ) )
			.Where( Directory.Exists )
			.ToArray();
	} );

	private static readonly Lazy<DocumentationProvider?> FrameworkDocumentation = new( () => {
		var files = RefPackDirs.Value.SelectMany( d => Directory.GetFiles( d, "*.xml" ) )
			// よく使うものを先に探す
			.OrderBy( f => Path.GetFileName( f ) switch {
				"System.Runtime.xml" => 0,
				"System.Linq.xml" => 1,
				"System.Collections.xml" => 2,
				"System.Windows.Forms.xml" => 3,
				"System.Drawing.Primitives.xml" => 4,
				_ => 9,
			} )
			.ToArray();
		return files.Length == 0 ? null : new CompositeDocumentationProvider( files );
	} );

	private sealed class CompositeDocumentationProvider( string[] files ) : DocumentationProvider {
		// ファイルは必要になった時点で読み、メンバー ID → <member> 要素の XML を覚える
		private readonly Lazy<Dictionary<string, string>>[] _files =
			files.Select( f => new Lazy<Dictionary<string, string>>( () => Load( f ) ) ).ToArray();

		protected override string? GetDocumentationForSymbol( string documentationMemberID, System.Globalization.CultureInfo preferredCulture, CancellationToken cancellationToken = default ) {
			foreach( var f in _files )
				if( f.Value.TryGetValue( documentationMemberID, out var xml ) ) return xml;
			return null;
		}

		private static Dictionary<string, string> Load( string file ) {
			var map = new Dictionary<string, string>();
			try {
				using var reader = System.Xml.XmlReader.Create( file );
				while( !reader.EOF ) {
					if( reader.NodeType == System.Xml.XmlNodeType.Element && reader.Name == "member" && reader.GetAttribute( "name" ) is { } name )
						map[name] = reader.ReadOuterXml();
					else reader.Read();
				}
			} catch( System.Xml.XmlException ) { }
			return map;
		}

		public override bool Equals( object? obj ) => ReferenceEquals( this, obj );
		public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode( this );
	}
}
