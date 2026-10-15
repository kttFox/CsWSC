using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace CsWSC;

/// <summary>スクリプトのコンパイルエラー・実行時エラーです。Line は 1 始まりです (不明なら 0)。</summary>
public sealed class ScriptException( string message, int line, Exception? inner = null )
	: Exception( message, inner ) {
	public int Line { get; } = line;
}

/// <summary>C# スクリプト (Roslyn Scripting) の実行器です。</summary>
public static class ScriptRunner {
	private const string ScriptPath = "script.csws";

	/// <summary>
	/// スクリプトが参照するアセンブリです (言語サーバーと共通)。
	/// 共有フレームワークの dll は、ここにあるアセンブリのフォルダからすべて参照します。
	/// </summary>
	public static IReadOnlyList<Assembly> References { get; } =
	[
		typeof(object).Assembly, // Microsoft.NETCore.App
		typeof(Form).Assembly,   // Microsoft.WindowsDesktop.App
		typeof(ScriptGlobals).Assembly,
	];

	/// <summary>スクリプトで最初から using される名前空間です (言語サーバーと共通)。</summary>
	public static IReadOnlyList<string> Imports { get; } =
	[
		"System", "System.IO", "System.Linq", "System.Text", "System.Collections.Generic",
		"System.Threading", "System.Threading.Tasks", "System.Drawing", "System.Windows.Forms",
		"CsWSC",
	];

	/// <summary>
	/// References のメタデータ参照です。同じインスタンスを使い回すと、読み込んだメタデータが
	/// コンパイル間で共有され、2 回目以降のコンパイルが速くなります。
	/// ScriptRunner の静的フィールドにすると、キャッシュから実行するときも Roslyn が読み込まれるため、
	/// 入れ子のクラスに分けて初めてコンパイルするときに初期化します。
	/// References が含まれる共有フレームワーク (.NET + WindowsDesktop) の dll はすべて参照し、
	/// 言語サーバーと同じ型 (Stack・Regex・Process など) を使えるようにします。
	/// </summary>
	private static class MetadataReferences {
		public static readonly ImmutableArray<MetadataReference> Value = Load();

		private static ImmutableArray<MetadataReference> Load() {
			var frameworkDirs = References
				.Select( a => System.IO.Path.GetDirectoryName( a.Location )! )
				.Where( d => d.Contains( "shared", StringComparison.OrdinalIgnoreCase ) )
				.ToHashSet( StringComparer.OrdinalIgnoreCase );
			var tpa = ( (string?)AppContext.GetData( "TRUSTED_PLATFORM_ASSEMBLIES" ) ?? "" ).Split( System.IO.Path.PathSeparator );
			var files = tpa.Where( f => f.Length > 0 && frameworkDirs.Contains( System.IO.Path.GetDirectoryName( f )! ) )
				.Concat( References.Select( a => a.Location ) )
				.Distinct( StringComparer.OrdinalIgnoreCase );
			return [.. files.Select( f => (MetadataReference)MetadataReference.CreateFromFile( f ) )];
		}
	}

	/// <summary>
	/// スクリプトのコンパイルオプションです。#r / #load の相対パスは path のフォルダ
	/// (未保存なら実行ファイルのフォルダ) を基準に解決します。
	/// </summary>
	private static ScriptOptions CreateOptions( string? path, bool insertStopChecks = false ) {
		var filePath = System.IO.Path.GetFullPath( path ?? System.IO.Path.Combine( AppContext.BaseDirectory, ScriptPath ) );
		var baseDir = System.IO.Path.GetDirectoryName( filePath )!;
		SourceReferenceResolver sourceResolver = ScriptSourceResolver.Default.WithBaseDirectory( baseDir );
		// #load で読み込むファイルにも停止チェックを入れる
		if( insertStopChecks ) {
			sourceResolver = new StopCheckSourceResolver( sourceResolver );
		}

		return ScriptOptions.Default
			.WithReferences( MetadataReferences.Value )
			.WithImports( Imports )
			.WithEmitDebugInformation( true ) // 実行時エラーの行番号取得用
			.WithFilePath( filePath )
			.WithFileEncoding( System.Text.Encoding.UTF8 )
			.WithMetadataResolver( ScriptMetadataResolver.Default.WithBaseDirectory( baseDir ) )
			.WithSourceResolver( sourceResolver );
	}

	/// <summary>読み込んだソースに停止チェックを挿入する SourceReferenceResolver です。</summary>
	private sealed class StopCheckSourceResolver( SourceReferenceResolver inner ) : SourceReferenceResolver {
		private readonly SourceReferenceResolver _inner = inner;

		public override string? NormalizePath( string path, string? baseFilePath ) => _inner.NormalizePath( path, baseFilePath );
		public override string? ResolveReference( string path, string? baseFilePath ) => _inner.ResolveReference( path, baseFilePath );

		public override System.IO.Stream OpenRead( string resolvedPath ) {
			string code;
			using( var reader = new System.IO.StreamReader( _inner.OpenRead( resolvedPath ), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true ) ) {
				code = reader.ReadToEnd();
			}

			return new System.IO.MemoryStream( System.Text.Encoding.UTF8.GetBytes( InsertStopChecks( code ) ) );
		}

		public override bool Equals( object? other ) => other is StopCheckSourceResolver o && _inner.Equals( o._inner );
		public override int GetHashCode() => _inner.GetHashCode();
	}

	/// <summary>コンパイルのみ行い、エラーがあれば ScriptException を投げます。path は #r / #load の相対パスの基準です。</summary>
	public static Script<object> Compile( string code, string? path = null ) {
		var script = CSharpScript.Create( code, CreateOptions( path ), typeof( ScriptGlobals ) );
		var errors = script.Compile().Where( d => d.Severity == DiagnosticSeverity.Error ).ToImmutableArray();
		if( errors.Length > 0 ) {
			var first = errors[0];
			var line = first.Location.GetLineSpan().StartLinePosition.Line + 1;
			var all = string.Join( Environment.NewLine, errors.Select( e =>
				$"{e.Location.GetLineSpan().StartLinePosition.Line + 1}行目: {e.GetMessage()} ({e.Id})" ) );
			throw new ScriptException( all, line );
		}
		return script;
	}

	/// <summary>実行時と同じ条件のコンパイル結果を返します (構文ハイライトなど解析用。エラーがあっても投げない)。</summary>
	public static Compilation GetCompilation( string code, string? path = null ) =>
		CSharpScript.Create( code, CreateOptions( path ), typeof( ScriptGlobals ) ).GetCompilation();

	/// <summary>コンパイル済みスクリプトの既定のキャッシュフォルダです。</summary>
	public static string DefaultCacheDirectory { get; } =
		System.IO.Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "CsWSC", "ScriptCache" );

	/// <summary>
	/// スクリプトを実行します。呼び出したスレッドで同期的に実行されます。
	/// cacheDirectory を指定するとコンパイル結果を保存し、次回からは Roslyn を読み込まずに実行します。
	/// </summary>
	public static void Run( string code, Action<string> output, CancellationToken cancellation, ILogWindow? logWindow = null, string? path = null, string? cacheDirectory = null ) {
		var filePath = System.IO.Path.GetFullPath( path ?? System.IO.Path.Combine( AppContext.BaseDirectory, ScriptPath ) );
		var cachePath = ScriptCache.GetPath( cacheDirectory, code, filePath );
		// キャッシュがあれば Roslyn を使わない (コンパイル処理は別メソッドにして Roslyn の読み込みを避ける)
		var compiled = ( cachePath != null ? ScriptCache.TryRead( cachePath ) : null ) ?? CompileToImage( code, path, cachePath );
		Execute( compiled, filePath, output, cancellation, logWindow, setStopToken: true );
	}

	/// <summary>
	/// デバッガー (VS Code の coreclr など) から実行するとき用です。呼び出したスレッドで同期的に実行されます。
	/// ブレークポイントがファイルと一致するよう停止チェックを挿入せず、キャッシュも使いません。
	/// </summary>
	public static void RunForDebug( string code, string path, Action<string> output, CancellationToken cancellation, ILogWindow? logWindow = null ) {
		var filePath = System.IO.Path.GetFullPath( path );
		var compiled = CompileToImage( code, path, null, insertStopChecks: false );
		Execute( compiled, filePath, output, cancellation, logWindow, setStopToken: true );
	}

	/// <summary>コンパイル済みのスクリプトを実行します。setStopToken が false なら自動挿入の停止チェックの対象を変えません (温め用)。</summary>
	private static void Execute( ScriptCache.Entry compiled, string filePath, Action<string> output, CancellationToken cancellation, ILogWindow? logWindow, bool setStopToken ) {
		// #r で参照した DLL は既定の探索場所に無いので、名前で探して読み込む
		Func<AssemblyLoadContext, AssemblyName, Assembly?> resolve = ( context, name ) => ResolveReference( context, name, compiled.References );
		AssemblyLoadContext.Default.Resolving += resolve;
		var factory = Assembly.Load( compiled.Image ).GetType( "Submission#0" )!.GetMethod( "<Factory>", BindingFlags.Public | BindingFlags.Static )!;

		var globals = new ScriptGlobals( output, cancellation, logWindow );
		var previousToken = ScriptStop.Token;
		if( setStopToken ) {
			ScriptStop.Token = globals.Cancellation;
		}

		try {
			cancellation.ThrowIfCancellationRequested();
			// Roslyn のスクリプト実行と同じく、[0] にグローバル、[1] に自身の Submission が入る配列を渡す
			( (Task<object>)factory.Invoke( null, [new object?[] { globals, null }] )! ).GetAwaiter().GetResult();
		} catch( OperationCanceledException ) when( globals.ThreadError == null ) { throw; } catch( TargetInvocationException ex ) when( ex.InnerException is OperationCanceledException && globals.ThreadError == null ) {
			throw ex.InnerException;
		} catch( Exception ex ) {
			// THREAD のエラーで止まった場合はそちらを報告する
			throw ToScriptException( globals.ThreadError ?? ex, filePath );
		} finally {
			globals.EndThreads( TimeSpan.FromSeconds( 1 ) );
			// EndThreads で停止済みになるので、残すと終了後に Window などのメソッドが例外になる
			if( setStopToken ) {
				ScriptStop.Token = previousToken;
			}
			AssemblyLoadContext.Default.Resolving -= resolve;
		}
		if( globals.ThreadError is { } threadError ) {
			throw ToScriptException( threadError, filePath );
		}
	}

	/// <summary>#r で参照した DLL (と同じフォルダの依存 DLL) から、名前の一致するものを読み込みます。</summary>
	private static Assembly? ResolveReference( AssemblyLoadContext context, AssemblyName name, IReadOnlyList<string> references ) {
		foreach( var file in references.Concat( references.Select( r => System.IO.Path.Combine( System.IO.Path.GetDirectoryName( r )!, name.Name + ".dll" ) ) ) ) {
			if( !System.IO.File.Exists( file ) ) {
				continue;
			}

			try {
				if( AssemblyName.ReferenceMatchesDefinition( name, AssemblyName.GetAssemblyName( file ) ) ) {
					return context.LoadFromAssemblyPath( file );
				}
			} catch( BadImageFormatException ) { }
		}
		return null;
	}

	/// <summary>
	/// 実行せずにコンパイルだけ行い、キャッシュに保存します (再生前の準備用)。
	/// キャッシュ (.dll) が既にあれば何もしません (#load / #r の依存ファイルが変わっていても再コンパイルせず、実行時に判定する)。
	/// コンパイルエラーは ScriptException です。
	/// </summary>
	public static void Precompile( string code, string? path, string cacheDirectory ) {
		var filePath = System.IO.Path.GetFullPath( path ?? System.IO.Path.Combine( AppContext.BaseDirectory, ScriptPath ) );
		if( ScriptCache.GetPath( cacheDirectory, code, filePath ) is not { } cachePath || System.IO.File.Exists( cachePath ) ) {
			return;
		}

		CompileToImage( code, path, cachePath );
	}

	/// <summary>
	/// 小さなスクリプトをコンパイル・実行して、Roslyn と実行経路の JIT・読み込みを済ませます (待機用プロセス向け)。
	/// 初回コンパイルは約 1 秒かかりますが、これを済ませておくと 2 回目以降は約 0.1 秒になります。
	/// 実行経路 (キャッシュのハッシュ・アセンブリ読み込み・ScriptGlobals 生成) も初回は数十 ms かかります。
	/// 本物の実行と並行しても影響しないよう、キャッシュには保存せず停止チェックの対象も変えません。
	/// </summary>
	public static void Warmup( string? cacheDirectory ) {
		const string Code = "for (var i = 0; i < 1; i++) { }";
		var filePath = System.IO.Path.Combine( AppContext.BaseDirectory, ScriptPath );
		var compiled = CompileToImage( Code, null, null );
		_ = ScriptCache.GetPath( cacheDirectory, Code, filePath );
		Execute( compiled, filePath, _ => { }, CancellationToken.None, null, setStopToken: false );
	}

	/// <summary>停止チェックを入れてコンパイルし、アセンブリのバイト列を返します。cachePath があれば保存します。</summary>
	[System.Runtime.CompilerServices.MethodImpl( System.Runtime.CompilerServices.MethodImplOptions.NoInlining )]
	private static ScriptCache.Entry CompileToImage( string code, string? path, string? cachePath, bool insertStopChecks = true ) {
		var script = CSharpScript.Create( insertStopChecks ? InsertStopChecks( code ) : code,
			CreateOptions( path, insertStopChecks ).WithOptimizationLevel( OptimizationLevel.Debug ), typeof( ScriptGlobals ) );
		using var stream = new System.IO.MemoryStream();
		// 診断だけのコンパイルは省き Emit 1 回で済ませる。失敗したときだけ元のコードでコンパイルし直して位置を報告する
		// (停止チェックは改行を入れないが、列や挿入箇所のエラーを元のコードで示すため)
		// 実行時エラーの行番号取得用に PDB を埋め込む
		var compilation = script.GetCompilation();
		var result = compilation.Emit( stream,
			options: new Microsoft.CodeAnalysis.Emit.EmitOptions( debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.Embedded ) );
		if( !result.Success ) {
			Compile( code, path );
			// 元のコードでは通る (停止チェックの挿入で壊れた) 場合も、壊れたアセンブリを実行・保存しない
			var errors = result.Diagnostics.Where( d => d.Severity == DiagnosticSeverity.Error );
			throw new ScriptException( string.Join( Environment.NewLine, errors.Select( e => e.GetMessage() ) ), 0 );
		}

		// 依存ファイル: #load したスクリプトと #r した DLL (既定の参照以外)
		var mainPath = script.Options.FilePath;
		var loads = compilation.SyntaxTrees.Select( t => t.FilePath )
			.Where( f => !string.IsNullOrEmpty( f ) && !string.Equals( f, mainPath, StringComparison.OrdinalIgnoreCase ) );
		var defaults = References.Select( a => a.Location ).ToHashSet( StringComparer.OrdinalIgnoreCase );
		var references = compilation.References.OfType<PortableExecutableReference>()
			.Select( r => r.FilePath ).OfType<string>().Where( f => !defaults.Contains( f ) );
		var entry = new ScriptCache.Entry( stream.ToArray(), [.. references.Distinct( StringComparer.OrdinalIgnoreCase )] );
		if( cachePath != null ) {
			ScriptCache.Write( cachePath, entry, [.. loads.Distinct( StringComparer.OrdinalIgnoreCase )] );
		}

		return entry;
	}

	/// <summary>
	/// 組み込みコマンドを呼ばないループでも停止できるよう、ループ本体の先頭に停止チェックを挿入します。
	/// 行番号がずれないよう改行は入れません。
	/// </summary>
	internal static string InsertStopChecks( string code ) {
		const string Check = "global::CsWSC.ScriptStop.Check();";
		var options = CSharpParseOptions.Default.WithKind( SourceCodeKind.Script ).WithLanguageVersion( LanguageVersion.Latest );
		var root = CSharpSyntaxTree.ParseText( code, options ).GetRoot();
		var inserts = new List<(int Position, string Text)>();
		foreach( var loop in root.DescendantNodes() ) {
			var body = loop switch {
				WhileStatementSyntax w => w.Statement,
				DoStatementSyntax d => d.Statement,
				ForStatementSyntax f => f.Statement,
				CommonForEachStatementSyntax f => f.Statement,
				_ => null,
			};
			if( body == null || body.IsMissing ) {
				continue;
			}

			if( body is BlockSyntax block ) {
				inserts.Add( (block.OpenBraceToken.Span.End, " " + Check) );
			} else {
				inserts.Add( (body.SpanStart, "{ " + Check + " ") );
				inserts.Add( (body.Span.End, " }") );
			}
		}
		var sb = new System.Text.StringBuilder( code );
		foreach( var (position, text) in inserts.OrderByDescending( i => i.Position ) ) {
			sb.Insert( position, text );
		}

		return sb.ToString();
	}

	private static ScriptException ToScriptException( Exception ex, string scriptPath ) {
		var inner = ex is TargetInvocationException { InnerException: { } ie } ? ie : ex;
		var line = FindScriptLine( inner, scriptPath );
		var prefix = line > 0 ? $"{line}行目: " : "";
		return new ScriptException( $"{prefix}{inner.GetType().Name}: {inner.Message}", line, inner );
	}

	private static int FindScriptLine( Exception ex, string scriptPath ) =>
		new StackTrace( ex, true ).GetFrames()
			.FirstOrDefault( f => string.Equals( f.GetFileName(), scriptPath, StringComparison.OrdinalIgnoreCase ) )?.GetFileLineNumber() ?? 0;
}

/// <summary>スクリプトに自動挿入される停止チェックです。</summary>
public static class ScriptStop {
	internal static CancellationToken Token { get; set; }

	/// <summary>停止要求があれば OperationCanceledException を投げます。</summary>
	public static void Check() => Token.ThrowIfCancellationRequested();

	/// <summary>停止要求を確認しながら ms ミリ秒待ちます。</summary>
	internal static void Sleep( int ms ) => Sleep( ms, Token );

	/// <summary>停止要求を確認しながら ms ミリ秒待ちます。0 以下なら待たずに停止要求だけ確認します。</summary>
	internal static void Sleep( int ms, CancellationToken token ) {
		// 負の値は待たない (WaitOne は -1 以外の負の値で例外になる)
		token.WaitHandle.WaitOne( Math.Max( ms, 0 ) );
		token.ThrowIfCancellationRequested();
	}
}
