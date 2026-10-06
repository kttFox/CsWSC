using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsWSC.LanguageServer;

// VS Code 拡張 (editors/vscode) から起動される解析サーバー。
// 標準入出力で 1 行 1 JSON のリクエスト / レスポンスをやりとりする。
//   → {"id":1,"method":"completion","uri":"file:///...","text":"...","offset":10}
//   ← {"id":1,"result":{...}}  または  {"id":1,"error":"..."}

var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
};
var service = new LanguageService(new ScriptWorkspace());

// 起動直後の初回解析は遅いので、空のスクリプトで先に温めておく
// (ワークスペースはスレッドセーフでないため、最初のリクエストの前に完了を待つ)
var warmup = Task.Run(async () => { await service.CompleteAsync("warmup", "Print", 5); service.Close("warmup"); });

while (await input.ReadLineAsync() is { } line)
{
    await warmup;
    if (string.IsNullOrWhiteSpace(line)) continue;
    JsonNode? id = null;
    object? result;
    string? error = null;
    try
    {
        var req = JsonNode.Parse(line)!.AsObject();
        id = req["id"]?.DeepClone();
        var uri = (string?)req["uri"] ?? "untitled";
        var text = (string?)req["text"] ?? "";
        var offset = (int?)req["offset"] ?? 0;
        result = (string?)req["method"] switch
        {
            "completion" => await service.CompleteAsync(uri, text, offset),
            "resolve" => await service.ResolveCompletionAsync((int?)req["index"] ?? -1),
            "hover" => await service.HoverAsync(uri, text, offset),
            "signature" => await service.SignatureHelpAsync(uri, text, offset),
            "diagnostics" => await service.DiagnoseAsync(uri, text),
            "close" => Close(uri),
            "shutdown" => null,
            var m => throw new InvalidOperationException($"unknown method: {m}"),
        };
        if ((string?)req["method"] == "shutdown") break;
    }
    catch (Exception e)
    {
        result = null;
        error = e.Message;
    }
    var res = new JsonObject { ["id"] = id };
    if (error != null) res["error"] = error;
    else res["result"] = JsonSerializer.SerializeToNode(result, json);
    await output.WriteLineAsync(res.ToJsonString());
}

object? Close(string uri)
{
    service.Close(uri);
    return null;
}
