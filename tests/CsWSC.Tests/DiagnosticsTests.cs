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
	public async Task Nullable_IsEnabledInScript() {
		var r = await _service.DiagnoseAsync( "file:///c%3A/test.csws", "string? a = null;\nstring b = null;\n" );
		Assert.DoesNotContain( r, d => d.Code == "CS8632" ); // nullable が無効なら '?' に警告が出る
		Assert.Contains( r, d => d.Code is "CS8600" or "CS8625" ); // トップレベルの変数はフィールドなので CS8625
	}

	[Fact]
	public async Task Library_IsAnalyzedAsRegularCSharp() {
		// .cswsl は通常の C# なので extension ブロックが書け、既定の using (Point) も使える。スクリプトのコマンドは使えない
		var text = "public static class PointExt { extension(Point) { public static Point operator *(Point p, int v) => new(p.X * v, p.Y * v); } }\n"
			+ "static class Other { static void F() => Print(1); }\n";
		var r = await _service.DiagnoseAsync( "file:///c%3A/test/Ext.cswsl", text );
		Assert.DoesNotContain( r, d => d.Code == "CS9283" );
		Assert.Single( r, d => d.Severity == "error" );
		Assert.Contains( r, d => d.Code == "CS0103" );
	}

	[Fact]
	public async Task ReferenceCswsl_FromLoadedScript() {
		// #load 先の #r で参照したライブラリの型が、エラーにならず使える
		var directory = Path.Combine( Path.GetTempPath(), "CsWSC.Tests." + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( Path.Combine( directory, "common" ) );
		try {
			File.WriteAllText( Path.Combine( directory, "common", "lib.cswsl" ), "public static class Lib { public static string Greet() => \"a\"; }" );
			File.WriteAllText( Path.Combine( directory, "common", "共通.csws" ), "#r \"lib.cswsl\"\nstring Hello() => Lib.Greet();" );
			var uri = new Uri( Path.Combine( directory, "main.csws" ) ).AbsoluteUri;
			var r = await _service.DiagnoseAsync( uri, "#load \"common/共通.csws\"\nPrint(Hello());\nPrint(Lib.Greet());\n" );
			Assert.DoesNotContain( r, d => d.Severity == "error" );
		} finally { try { Directory.Delete( directory, true ); } catch( UnauthorizedAccessException ) { } catch( IOException ) { } }
	}

	[Fact]
	public void ToFilePath_DecodesDriveColon() {
		Assert.Equal( @"c:\Users\test\my script.csws", ScriptWorkspace.ToFilePath( "file:///c%3A/Users/test/my%20script.csws" ) );
	}
}
