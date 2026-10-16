using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsWSC.LanguageServer;

// VS Code 拡張 (editors/vscode) から起動される解析サーバー。
// 標準入出力で 1 行 1 JSON のリクエスト / レスポンスをやりとりする。
//   → {"id":1,"method":"completion","uri":"file:///...","text":"...","offset":10}
//   ← {"id":1,"result":{...}}  または  {"id":1,"error":"..."}

var input = new StreamReader( Console.OpenStandardInput(), new UTF8Encoding( false ) );
var output = new StreamWriter( Console.OpenStandardOutput(), new UTF8Encoding( false ) ) { AutoFlush = true };
var json = new JsonSerializerOptions( JsonSerializerDefaults.Web ) {
	DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
};
var service = new LanguageService( new ScriptWorkspace() );
var sourceLink = new SourceLinkResolver( Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "CsWSC", "SourceLink" ) );
// 時間のかかる処理 (ダウンロード) の応答は後から別スレッドで書くので、書き込みを 1 つずつにする
var outputLock = new SemaphoreSlim( 1, 1 );

// 起動直後の初回解析は遅いので、空のスクリプトで先に温めておく
// (ワークスペースはスレッドセーフでないため、最初のリクエストの前に完了を待つ)
// (温めは最適化にすぎないので、失敗してもログに出して続ける)
var warmup = Task.Run( async () => {
	try { await service.CompleteAsync( "warmup", "Print", 5 ); service.Close( "warmup" ); } catch( Exception e ) { Console.Error.WriteLine( $"warmup failed: {e}" ); }
} );

while( await input.ReadLineAsync() is { } line ) {
	await warmup;
	if( string.IsNullOrWhiteSpace( line ) ) continue;
	JsonNode? id = null;
	object? result;
	string? error = null;
	try {
		var req = JsonNode.Parse( line )!.AsObject();
		id = req["id"]?.DeepClone();
		var uri = (string?)req["uri"] ?? "untitled";
		var text = (string?)req["text"] ?? "";
		var offset = (int?)req["offset"] ?? 0;
		result = (string?)req["method"] switch {
			"completion" => await service.CompleteAsync( uri, text, offset ),
			"resolve" => await service.ResolveCompletionAsync( (int?)req["index"] ?? -1 ),
			"hover" => await service.HoverAsync( uri, text, offset ),
			"signature" => await service.SignatureHelpAsync( uri, text, offset ),
			"definition" => await service.DefinitionAsync( uri, text, offset ) is { Length: > 0 } found ? (object)found
				: await service.MetadataDefinitionAsync( uri, text, offset ) is { } def
					? new Deferred( async () => await sourceLink.ResolveAsync( def, (bool?)req["sourceLink"] ?? true ) )
					: Array.Empty<DefinitionEntry>(),
			"prepareRename" => await service.PrepareRenameAsync( uri, text, offset ),
			"rename" => await service.RenameAsync( uri, text, offset, (string?)req["newName"] ?? "" ),
			"codeActions" => await service.CodeActionsAsync( uri, text, offset, (int?)req["end"] ?? offset ),
			"resolveCodeAction" => await service.ResolveCodeActionAsync( (int?)req["index"] ?? -1 ),
			"diagnostics" => await service.DiagnoseAsync( uri, text ),
			"semanticTokens" => await service.SemanticTokensAsync( uri, text ),
			"format" => await service.FormatAsync( uri, text, offset, (int?)req["end"] ?? -1,
				(bool?)req["useTabs"] ?? true, (int?)req["tabSize"] ?? 4 ),
			"referenceLenses" => await service.ReferenceLensesAsync( uri, text,
				req["kinds"]?.AsArray().Select( k => (string?)k ?? "" ).ToArray() ),
			"docComment" =>await service.DocCommentSnippetAsync( uri, text, offset ),
			"close" => Close( uri ),
			"shutdown" => null,
			var m => throw new InvalidOperationException( $"unknown method: {m}" ),
		};
		if( (string?)req["method"] == "shutdown" ) break;
	} catch( Exception e ) {
		result = null;
		error = e.Message;
	}
	// ワークスペースを使わない後半 (ダウンロードなど) は、ほかの要求を止めないよう裏で実行して後から応答する
	if( result is Deferred deferred ) {
		var deferredId = id;
		_ = Task.Run( async () => {
			try { await Send( deferredId, await deferred.Run(), null ); } catch( Exception e ) { await Send( deferredId, null, e.Message ); }
		} );
		continue;
	}
	await Send( id, result, error );
}

async Task Send( JsonNode? id, object? result, string? error ) {
	var res = new JsonObject { ["id"] = id };
	if( error != null ) res["error"] = error;
	else res["result"] = JsonSerializer.SerializeToNode( result, json );
	await outputLock.WaitAsync();
	try { await output.WriteLineAsync( res.ToJsonString() ); } finally { outputLock.Release(); }
}

object? Close( string uri ) {
	service.Close( uri );
	return null;
}

/// <summary>応答を後から返す処理です。</summary>
internal sealed record Deferred( Func<Task<object?>> Run );
