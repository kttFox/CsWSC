using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class DocCommentSnippetTests {
	private readonly LanguageService _service = new( new ScriptWorkspace() );

	// | の位置 (/// の直後) で展開するタグを返す
	private Task<string[]?> Snippet( string text ) {
		var offset = text.IndexOf( '|' );
		return _service.DocCommentSnippetAsync( "untitled:test", text.Remove( offset, 1 ), offset );
	}

	// 展開するときのタグを 1 行ずつつないだもの (展開しなければ null)
	private async Task<string?> Joined( string text ) => await Snippet( text ) is { } tags ? string.Join( "\n", tags ) : null;

	[Fact]
	public async Task Method_ParamsAndReturns() {
		Assert.Equal( "<typeparam name=\"T\"></typeparam>\n<param name=\"a\"></param>\n<param name=\"b\"></param>\n<returns></returns>",
			await Joined( "///|\r\nint Add<T>( int a, int b ) => a + b;" ) );
	}

	[Fact]
	public async Task VoidMethodInClass_NoReturns() {
		Assert.Equal( "<param name=\"s\"></param>", await Joined( "class C {\r\n\t///|\r\n\tvoid Run( string s ) { }\r\n}" ) );
	}

	[Fact]
	public async Task Class_Empty() {
		Assert.Equal( "", await Joined( "///|\r\nclass C { }" ) );
	}

	[Fact]
	public async Task LocalFunction() {
		Assert.Equal( "<param name=\"x\"></param>", await Joined( "{\r\n\t///|\r\n\tvoid F( int x ) { }\r\n}" ) );
	}

	[Fact]
	public async Task Statement_Null() {
		Assert.Null( await Snippet( "///|\r\nPrint( 1 );" ) );
	}

	[Fact]
	public async Task ExistingComment_Null() {
		Assert.Null( await Snippet( "///|\r\n/// <summary>x</summary>\r\nvoid F() { }" ) );
		Assert.Null( await Snippet( "/// <summary>x</summary>\r\n///|\r\nvoid F() { }" ) );
	}
}
