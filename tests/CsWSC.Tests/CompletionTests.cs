using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class CompletionTests {
	private readonly LanguageService _service = new( new ScriptWorkspace() );

	private const string Uri = "file:///c:/test/test.csws";

	// text の末尾で補完し、名前空間 ns の label の候補を選んだときの全文 (追加の変更と挿入を適用したもの) を返す
	private async Task<string> Complete( string text, string label, string ns ) {
		var list = await _service.CompleteAsync( Uri, text, text.Length );
		var item = list!.Items.First( i => i.Label == label && i.Detail == ns );
		var detail = await _service.ResolveCompletionAsync( item.Index );
		var result = text[..list.Start] + item.InsertText + text[list.End..];
		foreach( var e in ( detail!.Edits ?? [] ).OrderByDescending( e => e.Start ) ) result = result[..e.Start] + e.NewText + result[e.End..];
		return result;
	}

	[Fact]
	public async Task UnimportedType_AddsUsing() {
		Assert.Equal( "using System.Numerics;\r\n\r\nPrint( 1 );\r\nvar v = Vector2",
			await Complete( "Print( 1 );\r\nvar v = Vector2", "Vector2", "System.Numerics" ) );
	}

	[Fact]
	public async Task UnimportedType_AddsUsingNextToExisting() {
		Assert.Equal( "using System.Diagnostics;\r\nusing System.Numerics;\r\n\r\nvar v = Vector2",
			await Complete( "using System.Diagnostics;\r\n\r\nvar v = Vector2", "Vector2", "System.Numerics" ) );
	}

	[Fact]
	public async Task ImportedType_NoEdits() {
		var text = "var s = StringBuild";
		var list = await _service.CompleteAsync( Uri, text, text.Length );
		var item = list!.Items.First( i => i.Label == "StringBuilder" );
		var detail = await _service.ResolveCompletionAsync( item.Index );
		Assert.Null( detail!.Edits );
		Assert.NotNull( detail.Description );
	}
}
