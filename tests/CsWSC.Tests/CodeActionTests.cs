using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class CodeActionTests {
	private readonly LanguageService _service = new( new ScriptWorkspace() );
	private const string Uri = "file:///c:/temp/test.csws";

	// | の位置のコードアクションから title のものを適用し、変更後のテキストを返す
	private async Task<string> Apply( string text, string title ) {
		var offset = text.IndexOf( '|' );
		text = text.Remove( offset, 1 );
		var actions = await _service.CodeActionsAsync( Uri, text, offset, offset );
		var action = Assert.Single( actions, a => a.Title == title );
		var r = await _service.ResolveCodeActionAsync( action.Index );
		Assert.Null( r.Error );
		foreach( var e in r.Edits!.OrderByDescending( e => e.Start ) ) text = text[..e.Start] + e.NewText + text[e.End..];
		return text;
	}

	[Fact]
	public async Task QuickFix_AddsUsing() {
		Assert.Equal( "using System.Text.RegularExpressions;\r\n\r\nvar r = new Regex( \"a\" );",
			await Apply( "var r = new Re|gex( \"a\" );", "using System.Text.RegularExpressions;" ) );
	}

	[Fact]
	public async Task QuickFix_IsLinkedToDiagnostic() {
		var actions = await _service.CodeActionsAsync( Uri, "var r = new Regex( \"a\" );", 13, 13 );
		Assert.All( actions.Where( a => a.Kind == "quickfix" ), a => Assert.Equal( [12], a.Diagnostics ) );
	}

	[Fact]
	public async Task Refactor_InvertIf() {
		Assert.Equal( "void M( int x ) { if (x != 1) return; Print(x); }",
			await Apply( "void M( int x ) { i|f( x == 1 ) Print( x ); }", "if を反転する" ) );
	}

	[Fact]
	public async Task QuickFix_IdeAnalyzer_AddBraces() {
		var text = await Apply( "void M( int x ) { i|f( x == 1 ) Print( x ); }", "波かっこを追加します" );
		Assert.Matches( @"if\( x == 1 \)\s*\{\s*Print\( x \);\s*\}", text );
	}

	[Fact]
	public async Task Refactor_TopLevel_UseImplicitType() {
		Assert.Equal( "var x = 1;\r\nPrint( x );", await Apply( "i|nt x = 1;\r\nPrint( x );", "暗黙的な型の使用" ) );
	}

	[Fact]
	public async Task Refactor_TopLevel_ExcludesProgramMainConversion() {
		var actions = await _service.CodeActionsAsync( Uri, "var x = 1;\r\nPrint( x );", 1, 1 );
		Assert.DoesNotContain( actions, a => a.Title.Contains( "Program.Main" ) );
	}

	[Fact]
	public async Task Resolve_StaleIndex_ReturnsError() {
		Assert.NotNull( ( await _service.ResolveCodeActionAsync( 99 ) ).Error );
	}
}
