using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class RenameTests : IDisposable {
	// #load 先を置くための一時フォルダ (スクリプトはその中にあるものとして解析する)
	private readonly string _dir = Directory.CreateTempSubdirectory( "cswsc-rename-" ).FullName;
	private readonly LanguageService _service = new( new ScriptWorkspace() );

	public void Dispose() => Directory.Delete( _dir, true );

	private string Uri => new Uri( Path.Combine( _dir, "test.csws" ) ).AbsoluteUri;

	// | の位置で名前を変更し、変更後のテキストを返す
	private async Task<string> Rename( string text, string newName ) {
		var offset = text.IndexOf( '|' );
		text = text.Remove( offset, 1 );
		var r = await _service.RenameAsync( Uri, text, offset, newName );
		Assert.Null( r.Error );
		foreach( var e in r.Edits!.OrderByDescending( e => e.Start ) ) text = text[..e.Start] + e.NewText + text[e.End..];
		return text;
	}

	private async Task<string?> PrepareError( string text ) {
		var offset = text.IndexOf( '|' );
		return ( await _service.PrepareRenameAsync( Uri, text.Remove( offset, 1 ), offset ) ).Error;
	}

	[Fact]
	public async Task Prepare_ReturnsRangeAndName() {
		var r = await _service.PrepareRenameAsync( Uri, "var count = 1;\r\nPrint( count );", 24 );
		Assert.Null( r.Error );
		Assert.Equal( (23, 28, "count"), (r.Start, r.End, r.Placeholder) );
	}

	[Fact]
	public async Task LocalVariable() {
		Assert.Equal( "var total = 1;\r\nPrint( total + total );",
			await Rename( "var count = 1;\r\nPrint( co|unt + count );", "total" ) );
	}

	[Fact]
	public async Task AtEndOfName() {
		Assert.Equal( "var total = 1;", await Rename( "var count| = 1;", "total" ) );
	}

	[Fact]
	public async Task FunctionAndParameter() {
		Assert.Equal( "int Twice( int value ) => value * 2;\r\nPrint( Twice( 3 ) );",
			await Rename( "int Twice( int |x ) => x * 2;\r\nPrint( Twice( 3 ) );", "value" ) );
		Assert.Equal( "int Double( int x ) => x * 2;\r\nPrint( Double( 3 ) );",
			await Rename( "int Twice( int x ) => x * 2;\r\nPrint( Tw|ice( 3 ) );", "Double" ) );
	}

	[Fact]
	public async Task ClassFromConstructorCall() {
		// new Foo() の上でもクラスの名前を変える
		Assert.Equal( "class Bar { public Bar() { } }\r\nvar b = new Bar();",
			await Rename( "class Foo { public Foo() { } }\r\nvar b = new F|oo();", "Bar" ) );
	}

	[Fact]
	public async Task Keyword_AddsAt() {
		Assert.Equal( "var @class = 1;\r\nPrint( @class );", await Rename( "var |x = 1;\r\nPrint( x );", "class" ) );
	}

	[Fact]
	public async Task InvalidName_Rejected() {
		var r = await _service.RenameAsync( Uri, "var x = 1;", 4, "1abc" );
		Assert.Null( r.Edits );
		Assert.Contains( "1abc", r.Error );
	}

	[Fact]
	public async Task Conflict_Rejected() {
		var r = await _service.RenameAsync( Uri, "var x = 1;\r\nvar y = 2;\r\nPrint( x + y );", 4, "y" );
		Assert.Null( r.Edits );
		Assert.NotNull( r.Error );
	}

	[Fact]
	public async Task Conflict_InFunction_Rejected() {
		// 関数の中のローカル変数どうし (スクリプトのトップレベルとは別の経路)
		var text = "void F() { var x = 1; var y = 2; Print( x + y ); }";
		var r = await _service.RenameAsync( Uri, text, text.IndexOf( 'x' ), "y" );
		Assert.Null( r.Edits );
		Assert.NotNull( r.Error );
	}

	[Fact]
	public async Task MetadataAndLoaded_Rejected() {
		Assert.Contains( "CsWSC や .NET", await PrepareError( "Pr|int( 1 );" ) );
		Assert.Contains( "CsWSC や .NET", await PrepareError( "var sb = new System.Text.String|Builder();" ) );
		File.WriteAllText( Path.Combine( _dir, "lib.csws" ), "int Twice( int x ) => x * 2;\r\n" );
		Assert.Contains( "#load", await PrepareError( "#load \"lib.csws\"\r\nPrint( Tw|ice( 1 ) );" ) );
	}

	[Fact]
	public async Task Override_Rejected() {
		Assert.Contains( "継承元", await PrepareError( "class Foo { public override string To|String() => \"\"; }" ) );
	}

	[Fact]
	public async Task NotOnName_Rejected() {
		Assert.NotNull( await PrepareError( "var x = 1 |+ 2;" ) );
	}
}
