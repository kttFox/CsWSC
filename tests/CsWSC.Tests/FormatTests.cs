using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class FormatTests : IDisposable {
	// .editorconfig を置くための一時フォルダ (スクリプトはその中にあるものとして解析する)
	private readonly string _dir = Directory.CreateTempSubdirectory( "cswsc-format-" ).FullName;
	private readonly LanguageService _service = new( new ScriptWorkspace() );

	public void Dispose() => Directory.Delete( _dir, true );

	private string Uri => new Uri( Path.Combine( _dir, "test.csws" ) ).AbsoluteUri;

	private async Task<string> Format( string text, bool useTabs = true, int tabSize = 4, int start = 0, int end = -1 ) {
		var edits = await _service.FormatAsync( Uri, text, start, end, useTabs, tabSize );
		// 後ろから当てる
		foreach( var e in edits.OrderByDescending( e => e.Start ) ) text = text[..e.Start] + e.NewText + text[e.End..];
		return text;
	}

	private void EditorConfig( string content ) => File.WriteAllText( Path.Combine( _dir, ".editorconfig" ), "root = true\r\n" + content );

	[Fact]
	public async Task NoEditorConfig_UsesRoslynDefaultsAndEditorIndent() {
		var result = await Format( "if(true){\r\nPrint(1);\r\n}\r\n", useTabs: false, tabSize: 2 );
		Assert.Equal( "if (true)\r\n{\r\n  Print(1);\r\n}\r\n", result );
	}

	[Fact]
	public async Task EditorConfig_CswsSection_Applied() {
		EditorConfig( """
			[*.{cs,csws}]
			indent_style = tab
			csharp_new_line_before_open_brace = none
			csharp_space_between_method_call_parameter_list_parentheses = true
			""" );
		// エディターの設定 (スペース 2) より .editorconfig を優先する
		var result = await Format( "if(true){\r\nPrint(1);\r\n}\r\n", useTabs: false, tabSize: 2 );
		Assert.Equal( "if (true) {\r\n\tPrint( 1 );\r\n}\r\n", result );
	}

	[Fact]
	public async Task EditorConfig_CsOnlySection_NotApplied() {
		EditorConfig( """
			[*.cs]
			csharp_new_line_before_open_brace = none
			""" );
		var result = await Format( "if(true){\r\nPrint(1);\r\n}\r\n" );
		Assert.Equal( "if (true)\r\n{\r\n\tPrint(1);\r\n}\r\n", result );
	}

	[Fact]
	public async Task EditorConfig_ChangeIsReloaded() {
		EditorConfig( "[*.csws]\r\ncsharp_space_between_method_call_parameter_list_parentheses = true\r\n" );
		Assert.Equal( "Print( 1 );", await Format( "Print(1);" ) );
		EditorConfig( "[*.csws]\r\ncsharp_space_between_method_call_parameter_list_parentheses = false\r\n" );
		Assert.Equal( "Print(1);", await Format( "Print( 1 );" ) );
	}

	[Fact]
	public async Task Range_OnlyFormatsSelection() {
		var text = "var a=1;\r\nvar b=2;\r\n";
		var second = text.IndexOf( "var b" );
		Assert.Equal( "var a=1;\r\nvar b = 2;\r\n", await Format( text, start: second, end: text.Length ) );
	}

	[Fact]
	public async Task LfText_KeepsLf() {
		Assert.Equal( "if (true)\n{\n\tPrint(1);\n}\n", await Format( "if(true){\nPrint(1);\n}\n" ) );
	}
}
