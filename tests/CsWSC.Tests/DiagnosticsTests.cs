using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class DiagnosticsTests {
	private readonly LanguageService _service = new( new ScriptWorkspace() );

	// VS Code はドライブのコロンを %3A とエンコードした URI を送ってくる
	[Theory]
	[InlineData( "file:///c%3A/Users/test/my%20script.csws" )]
	[InlineData( "file:///c:/Users/test/test.csws" )]
	public async Task UnknownName_IsReported( string uri ) {
		var r = await _service.DiagnoseAsync( uri, "MouseMove( 1, 2 );\n" );
		Assert.Contains( r, d => d.Code == "CS0103" );
	}

	[Fact]
	public async Task SemanticTokens_ClassifiesTypes() {
		var text = "var p = new Point( 1, 2 );\nvar c = Color.Red;\n";
		var r = await _service.SemanticTokensAsync( "file:///c%3A/test.csws", text );
		string TypeOf( string word ) {
			var start = text.IndexOf( word );
			for( var i = 0; i < r.Length; i += 3 ) if( r[i] == start && r[i + 1] == word.Length ) return LanguageService.SemanticTokenTypes[r[i + 2]];
			return "(なし)";
		}
		Assert.Equal( "struct", TypeOf( "Point" ) );
		Assert.Equal( "struct", TypeOf( "Color" ) );
		Assert.Equal( "property", TypeOf( "Red" ) );
	}

	[Fact]
	public void ToFilePath_DecodesDriveColon() {
		Assert.Equal( @"c:\Users\test\my script.csws", ScriptWorkspace.ToFilePath( "file:///c%3A/Users/test/my%20script.csws" ) );
	}
}
