using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class DefinitionTests : IDisposable {
	// #load 先を置くための一時フォルダ (スクリプトはその中にあるものとして解析する)
	private readonly string _dir = Directory.CreateTempSubdirectory( "cswsc-definition-" ).FullName;
	private readonly LanguageService _service = new( new ScriptWorkspace() );

	public void Dispose() => Directory.Delete( _dir, true );

	private string Uri => new Uri( Path.Combine( _dir, "test.csws" ) ).AbsoluteUri;

	// | の位置で定義を探す
	private Task<DefinitionEntry[]> Definition( string text ) {
		var offset = text.IndexOf( '|' );
		return _service.DefinitionAsync( Uri, text.Remove( offset, 1 ), offset );
	}

	[Fact]
	public async Task LocalVariable() {
		var r = Assert.Single( await Definition( "var count = 1;\r\nPrint( co|unt );" ) );
		Assert.Equal( new DefinitionEntry( null, 0, 4, 0, 9 ), r );
	}

	[Fact]
	public async Task LocalFunctionAndClass() {
		var text = "void Hello() { }\r\nclass Foo { }\r\n";
		Assert.Equal( new DefinitionEntry( null, 0, 5, 0, 10 ), Assert.Single( await Definition( text + "Hel|lo();" ) ) );
		Assert.Equal( new DefinitionEntry( null, 1, 6, 1, 9 ), Assert.Single( await Definition( text + "new F|oo();" ) ) );
	}

	[Fact]
	public async Task LoadedFile() {
		var lib = Path.Combine( _dir, "lib.csws" );
		File.WriteAllText( lib, "// ライブラリ\r\nint Twice( int x ) => x * 2;\r\n" );
		var r = Assert.Single( await Definition( "#load \"lib.csws\"\r\nPrint( Tw|ice( 1 ) );" ) );
		Assert.Equal( lib, r.Path, ignoreCase: true );
		Assert.Equal( (1, 4, 1, 9), (r.StartLine, r.StartCharacter, r.EndLine, r.EndCharacter) );
	}

	[Fact]
	public async Task Metadata_ReturnsNothing() {
		Assert.Empty( await Definition( "Pr|int( 1 );" ) );
		Assert.Empty( await Definition( "var s = new Str|ingBuilder();" ) );
	}
}

/// <summary>Source Link でソースの無い定義を開く。.NET の型は msdl.microsoft.com と GitHub からダウンロードする。</summary>
public class SourceLinkTests : IDisposable {
	private readonly string _cache = Directory.CreateTempSubdirectory( "cswsc-sourcelink-" ).FullName;
	private readonly LanguageService _service = new( new ScriptWorkspace() );
	private readonly SourceLinkResolver _resolver;

	public SourceLinkTests() => _resolver = new SourceLinkResolver( _cache );

	public void Dispose() {
		// キャッシュは読み取り専用にしてあるので外してから消す
		foreach( var f in Directory.GetFiles( _cache, "*", SearchOption.AllDirectories ) ) File.SetAttributes( f, FileAttributes.Normal );
		Directory.Delete( _cache, true );
	}

	private async Task<(DefinitionEntry Entry, string Line)> Resolve( string text, bool download = true ) {
		var offset = text.IndexOf( '|' );
		var def = await _service.MetadataDefinitionAsync( "untitled:test", text.Remove( offset, 1 ), offset );
		Assert.NotNull( def );
		var r = Assert.Single( await _resolver.ResolveAsync( def, download ) );
		return (r, File.ReadAllLines( r.Path! )[r.StartLine]);
	}

	[Fact]
	public async Task CsWSCCommand_OpensLocalSource() {
		// 開発中はビルドしたフォルダのソースをそのまま開く
		var (r, line) = await Resolve( "Pr|int( 1 );" );
		Assert.StartsWith( Path.GetFullPath( Path.Combine( AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src" ) ), r.Path!, StringComparison.OrdinalIgnoreCase );
		Assert.Contains( "Print", line );
	}

	[Fact]
	public async Task DotNetType_DownloadsFromGitHub() {
		var (r, line) = await Resolve( "System.Text.Str|ingBuilder sb = null;" );
		Assert.Equal( "StringBuilder.cs", Path.GetFileName( r.Path ) );
		Assert.Contains( "class StringBuilder", line );
		Assert.Equal( "StringBuilder", line.Substring( r.StartCharacter, r.EndCharacter - r.StartCharacter ) );
	}

	[Fact]
	public async Task DotNetMethodAndProperty() {
		var (_, method) = await Resolve( "var sb = new System.Text.StringBuilder(); sb.Append|Line();" );
		Assert.Contains( "AppendLine(", method );
		var (_, property) = await Resolve( "var sb = new System.Text.StringBuilder(); var n = sb.Len|gth;" );
		Assert.Contains( "Length", property );
	}

	[Fact]
	public async Task DotNetConstructor() {
		// new の型名の上はコンストラクター (C# と同じ)
		var (_, line) = await Resolve( "var sb = new System.Text.Str|ingBuilder();" );
		Assert.Contains( "public StringBuilder()", line );
	}

	[Fact]
	public async Task NoDownload_UsesCacheOnly() {
		var offset = "new System.Text.Str".Length;
		var def = await _service.MetadataDefinitionAsync( "untitled:test", "new System.Text.StringBuilder();", offset );
		Assert.Empty( await _resolver.ResolveAsync( def!, download: false ) );
		Assert.Single( await _resolver.ResolveAsync( def!, download: true ) );
		Assert.Single( await new SourceLinkResolver( _cache ).ResolveAsync( def!, download: false ) );
	}

	[Fact]
	public async Task SourceDefinition_IsNotMetadata() {
		Assert.Null( await _service.MetadataDefinitionAsync( "untitled:test", "var x = 1; Print( x );", "var x = 1; Print( ".Length ) );
	}
}
