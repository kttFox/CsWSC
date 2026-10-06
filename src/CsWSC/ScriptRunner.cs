using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace CsWSC;

/// <summary>スクリプトのコンパイルエラー・実行時エラー。Line は 1 始まり (不明なら 0)。</summary>
public sealed class ScriptException(string message, int line, Exception? inner = null)
    : Exception(message, inner)
{
    public int Line { get; } = line;
}

/// <summary>C# スクリプト (Roslyn Scripting) の実行器。</summary>
public static class ScriptRunner
{
    private const string ScriptPath = "script.csws";

    /// <summary>スクリプトが参照するアセンブリ (言語サーバーと共通)。</summary>
    public static IReadOnlyList<Assembly> References { get; } =
    [
        typeof(object).Assembly,
        typeof(Enumerable).Assembly,
        typeof(Form).Assembly,
        typeof(Point).Assembly,
        typeof(ScriptGlobals).Assembly,
        typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly, // dynamic (CreateOleObj など) 用
    ];

    /// <summary>スクリプトで最初から using される名前空間 (言語サーバーと共通)。</summary>
    public static IReadOnlyList<string> Imports { get; } =
    [
        "System", "System.IO", "System.Linq", "System.Text", "System.Collections.Generic",
        "System.Threading", "System.Threading.Tasks", "System.Drawing", "System.Windows.Forms",
        "CsWSC",
    ];

    private static readonly ScriptOptions Options = ScriptOptions.Default
        .WithReferences(References)
        .WithImports(Imports)
        .WithEmitDebugInformation(true) // 実行時エラーの行番号取得用
        .WithFilePath(ScriptPath)
        .WithFileEncoding(System.Text.Encoding.UTF8);

    /// <summary>コンパイルのみ行い、エラーがあれば ScriptException を投げる。</summary>
    public static Script<object> Compile(string code)
    {
        var script = CSharpScript.Create(code, Options, typeof(ScriptGlobals));
        var errors = script.Compile().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        if (errors.Length > 0)
        {
            var first = errors[0];
            var line = first.Location.GetLineSpan().StartLinePosition.Line + 1;
            var all = string.Join(Environment.NewLine, errors.Select(e =>
                $"{e.Location.GetLineSpan().StartLinePosition.Line + 1}行目: {e.GetMessage()} ({e.Id})"));
            throw new ScriptException(all, line);
        }
        return script;
    }

    /// <summary>スクリプトを実行する。呼び出したスレッドで同期的に実行される。</summary>
    public static void Run(string code, Action<string> output, CancellationToken cancellation, ILogWindow? logWindow = null)
    {
        var script = Compile(code);
        var globals = new ScriptGlobals(output, cancellation, logWindow);
        try
        {
            script.RunAsync(globals, cancellationToken: cancellation).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (globals.ThreadError == null) { throw; }
        catch (Exception ex)
        {
            // THREAD のエラーで止まった場合はそちらを報告する
            throw ToScriptException(globals.ThreadError ?? ex);
        }
        finally
        {
            globals.EndThreads(TimeSpan.FromSeconds(1));
        }
        if (globals.ThreadError is { } threadError) throw ToScriptException(threadError);
    }

    private static ScriptException ToScriptException(Exception ex)
    {
        var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
        var line = FindScriptLine(inner);
        var prefix = line > 0 ? $"{line}行目: " : "";
        return new ScriptException($"{prefix}{inner.GetType().Name}: {inner.Message}", line, inner);
    }

    /// <summary>API 一覧 (ヘルプ表示用)。</summary>
    public static IEnumerable<string> ApiSignatures =>
        typeof(ScriptGlobals).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m is PropertyInfo || m is MethodInfo { IsSpecialName: false })
            .Where(m => m.GetCustomAttribute<System.ComponentModel.EditorBrowsableAttribute>()?.State != System.ComponentModel.EditorBrowsableState.Never)
            .Select(m => m switch
            {
                MethodInfo mi => $"{mi.ReturnType.Name} {mi.Name}({string.Join(", ", mi.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})",
                PropertyInfo pi => $"{pi.PropertyType.Name} {pi.Name}",
                _ => m.Name,
            })
            .Distinct();

    private static int FindScriptLine(Exception ex) =>
        new StackTrace(ex, true).GetFrames()
            .FirstOrDefault(f => f.GetFileName() == ScriptPath)?.GetFileLineNumber() ?? 0;
}
