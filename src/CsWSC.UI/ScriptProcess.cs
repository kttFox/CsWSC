using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CsWSC.UI;

/// <summary>
/// スクリプトを別プロセス (CsWSC.exe --run-script) で実行します。
/// 停止要求から一定時間で終わらなければプロセスごと強制終了します。
/// 起動待ちを省くため、次に使う子プロセスを 1 つ待機させておきます (待機中に Roslyn と実行経路を準備する)。
/// </summary>
/// <remarks>
/// やり取りは標準入出力のテキスト (UTF-8) です。JSON は初回の処理に約 30 ms かかるので使いません。
/// 親 → 子: 要求 = 種類 ("run" / "compile") の行、パス (未保存なら空) の行、ソースの文字数の行、ソース本体です。
///          以降は "stop" の行で停止要求です。"compile" ではコンパイル結果をキャッシュに保存して終了します。
/// 子 → 親: 1 行 1 メッセージで、項目はタブ区切りです (文字列は <see cref="Escape"/> する)。
///          compiling (1 = コンパイル開始, 0 = 終了) / print / visible / bounds / clear と、最後に ok / stopped / error のどれかです。
/// </remarks>
internal static class ScriptProcess {
	public const string ChildArgument = "--run-script";

	/// <summary>子プロセスに付けると、要求を待つ間に Roslyn と実行経路を準備します (待機用のプロセスだけ)。</summary>
	public const string WarmupArgument = "--warmup";

	/// <summary>停止要求から強制終了までの猶予です。</summary>
	private static readonly TimeSpan KillDelay = TimeSpan.FromSeconds( 1 );

	private static readonly UTF8Encoding Utf8 = new( false );

	// ---- 親 (UI) 側 (UI スレッドから呼ぶ) ----

	/// <summary>待機中の子プロセスです。</summary>
	private static Process? _standby;

	/// <summary>スクリプトを実行中の子プロセスです。</summary>
	private static Process? _running;

	/// <summary>次の再生用に子プロセスを待機させるかどうかです (起動引数のスクリプトを実行して終わるときは不要)。</summary>
	public static bool KeepStandby { get; set; } = true;

	/// <summary>窓の準備より先に実行を始めた子プロセス (起動引数のスクリプト) です。</summary>
	private static (string Source, string? Path, Process Process)? _early;

	/// <summary>
	/// 起動引数のスクリプトを、窓の準備と並行して実行し始めます。
	/// 出力はパイプに溜まり、同じスクリプトの RunAsync で受け取ります。
	/// </summary>
	public static void StartEarly( string source, string? path ) {
		try {
			var process = StartProcess( warmup: false );
			WriteRequest( process, "run", source, path );
			_early = (source, path, process);
		} catch( Exception ) { _early = null; }
	}

	/// <summary>先に始めた実行を使わないことになったので止めます。</summary>
	public static void DiscardEarly() {
		if( _early is not { Process: var process } ) return;
		_early = null;
		try { process.Kill( entireProcessTree: true ); } catch( Exception ) { }
		process.Dispose();
	}

	/// <summary>実行中の事前コンパイルです。</summary>
	private static (string Source, string? Path, Task Task)? _precompile;

	private static Process StartProcess( bool warmup ) {
		var psi = new ProcessStartInfo( Environment.ProcessPath!, warmup ? $"{ChildArgument} {WarmupArgument}" : ChildArgument ) {
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			StandardInputEncoding = Utf8,
			StandardOutputEncoding = Utf8,
			CreateNoWindow = true,
		};
		return Process.Start( psi ) ?? throw new InvalidOperationException( "スクリプトのプロセスを起動できません。" );
	}

	private static void WriteRequest( Process process, string kind, string source, string? path ) {
		var stdin = process.StandardInput;
		stdin.Write( $"{kind}\n{path}\n{source.Length.ToString( CultureInfo.InvariantCulture )}\n" );
		stdin.Write( source );
		stdin.Flush();
	}

	/// <summary>待機中の子プロセスを取り出します。無ければ起動します。</summary>
	private static Process TakeProcess() {
		var process = _standby;
		_standby = null;
		if( process is { HasExited: false } ) return process;
		process?.Dispose();
		// すぐ使うので温めない (温めるとスクリプトの実行と CPU を取り合う)
		return StartProcess( warmup: false );
	}

	/// <summary>次の再生用に子プロセスを待機させておきます (起動済みなら何もしない)。</summary>
	public static void Prepare() {
		if( !KeepStandby || _standby is { HasExited: false } ) return;
		_standby?.Dispose();
		try { _standby = StartProcess( warmup: true ); } catch( Exception ) { _standby = null; }
	}

	/// <summary>待機中・先に始めた・実行中の子プロセスを終了します (アプリ終了時)。</summary>
	public static void Shutdown() {
		DiscardEarly();
		// 実行中のスクリプトは停止を待たずに終了させる
		try { _running?.Kill( entireProcessTree: true ); } catch( Exception ) { }
		_running = null;
		try { _standby?.Kill(); } catch( InvalidOperationException ) { }
		_standby?.Dispose();
		_standby = null;
	}

	/// <summary>
	/// 再生前にスクリプトをコンパイルしてキャッシュに保存しておきます。終わったら次の子プロセスを待機させます。
	/// 失敗 (コンパイルエラーなど) は無視します (再生時に改めて報告される)。
	/// </summary>
	public static async Task PrecompileAsync( string source, string? path ) {
		if( _precompile is { } p && p.Source == source && p.Path == path ) { await p.Task; return; }
		var task = PrecompileCoreAsync( source, path );
		_precompile = (source, path, task);
		try { await task; } finally {
			if( _precompile is { } q && q.Task == task ) _precompile = null;
			Prepare();
		}
	}

	private static async Task PrecompileCoreAsync( string source, string? path ) {
		try {
			using var process = TakeProcess();
			WriteRequest( process, "compile", source, path );
			process.StandardInput.Close();
			await process.StandardOutput.ReadToEndAsync();
			await process.WaitForExitAsync();
		} catch( Exception ) { }
	}

	/// <summary>
	/// スクリプトを子プロセスで実行し、終わるまで待ちます。
	/// 停止は OperationCanceledException、スクリプトのエラーは ScriptException で通知します。
	/// output / logWindow は呼び出し元の同期コンテキスト (通常は UI スレッド) で呼ばれます。同期コンテキストが無い場合はスレッドプールから呼ばれるので、UI 以外のスレッドから呼ばれても安全にしておいてください。
	/// </summary>
	public static async Task RunAsync( string source, string? path, Action<string> output, ILogWindow logWindow, CancellationToken cancellation ) {
		// 同じスクリプトを事前コンパイル中なら、二重にコンパイルせず終わるのを待つ
		if( _precompile is { } p && p.Source == source && p.Path == path ) {
			using var notice = CompilingNotice.Begin();
			await p.Task;
		}
		try {
			await RunCoreAsync( source, path, output, logWindow, cancellation );
		} finally {
			Prepare();
		}
	}

	private static async Task RunCoreAsync( string source, string? path, Action<string> output, ILogWindow logWindow, CancellationToken cancellation ) {
		Process process;
		if( _early is { } early && early.Source == source && early.Path == path ) {
			// 先に始めた実行を引き継ぐ (要求は送信済み)
			_early = null;
			process = early.Process;
		} else {
			process = TakeProcess();
			WriteRequest( process, "run", source, path );
		}
		using var _process = process;
		_running = process;
		try {
			await ReadOutputAsync( process, output, logWindow, cancellation );
		} finally {
			if( _running == process ) _running = null;
		}
	}

	private static async Task ReadOutputAsync( Process process, Action<string> output, ILogWindow logWindow, CancellationToken cancellation ) {
		var stdin = process.StandardInput;

		using var _ = cancellation.Register( () => {
			try { stdin.Write( "stop\n" ); stdin.Flush(); } catch( IOException ) { } catch( ObjectDisposedException ) { }
			// 猶予内に終わらなければ強制終了する
			Task.Delay( KillDelay ).ContinueWith( _ => {
				try { if( !process.HasExited ) process.Kill( entireProcessTree: true ); } catch( InvalidOperationException ) { }
			} );
		} );

		string[]? end = null;
		CompilingNotice? notice = null;
		try {
			while( await process.StandardOutput.ReadLineAsync() is { } line ) {
				var f = line.Split( '\t' );
				switch( f[0] ) {
					case "compiling":
						notice?.Dispose();
						notice = f[1] == "1" ? CompilingNotice.Begin() : null;
						break;
					case "print": output( Unescape( f[1] ) ); break;
					case "visible": logWindow.SetVisible( f[1] == "1" ); break;
					case "bounds": logWindow.SetBounds( ParseInt( f[1] ), ParseInt( f[2] ), ParseInt( f[3] ), ParseInt( f[4] ) ); break;
					case "clear": logWindow.Clear(); break;
					case "ok" or "stopped" or "error": end = f; break;
				}
			}
		} finally {
			notice?.Dispose();
		}
		await process.WaitForExitAsync( CancellationToken.None );

		switch( end?[0] ) {
			case "ok": return;
			case "error": throw new ScriptException( Unescape( end[2] ), ParseInt( end[1] ) ?? 0 );
			case "stopped": throw new OperationCanceledException( cancellation );
		}
		if( cancellation.IsCancellationRequested ) throw new OperationCanceledException( cancellation );
		throw new InvalidOperationException( $"スクリプトのプロセスが異常終了しました (終了コード {process.ExitCode})。" );
	}

	private static int? ParseInt( string s ) => int.TryParse( s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v ) ? v : null;

	/// <summary>1 行・タブ区切りに収まるよう \ 改行 タブを \\ \n \r \t にします。</summary>
	private static string Escape( string s ) {
		if( s.AsSpan().IndexOfAny( "\\\n\r\t" ) < 0 ) return s;
		var sb = new StringBuilder( s.Length + 8 );
		foreach( var c in s ) {
			switch( c ) {
				case '\\': sb.Append( "\\\\" ); break;
				case '\n': sb.Append( "\\n" ); break;
				case '\r': sb.Append( "\\r" ); break;
				case '\t': sb.Append( "\\t" ); break;
				default: sb.Append( c ); break;
			}
		}
		return sb.ToString();
	}

	private static string Unescape( string s ) {
		if( !s.Contains( '\\' ) ) return s;
		var sb = new StringBuilder( s.Length );
		for( var i = 0; i < s.Length; i++ ) {
			if( s[i] != '\\' || i + 1 == s.Length ) { sb.Append( s[i] ); continue; }
			sb.Append( s[++i] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', var c => c } );
		}
		return sb.ToString();
	}

	// ---- 子 (スクリプト実行) 側 ----

	/// <summary>子プロセスのエントリポイントです。</summary>
	public static int ChildMain( bool warmup ) {
		var stdout = new StreamWriter( Console.OpenStandardOutput(), Utf8 ) { AutoFlush = true, NewLine = "\n" };
		var stdin = new StreamReader( Console.OpenStandardInput(), Utf8 );
		// 待機用なら要求を待つ間に準備しておく
		// (キャッシュが無いときのコンパイルが約 1 秒 → 約 0.1 秒、キャッシュがあるときの実行開始も数十 ms 短くなる)
		if( warmup ) new Thread( () => { try { ScriptRunner.Warmup( ScriptRunner.DefaultCacheDirectory ); } catch( Exception ) { } } ) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "CsWSC warmup" }.Start();
		var gate = new object();
		void Send( string message ) {
			lock( gate ) {
				try { stdout.WriteLine( message ); } catch( IOException ) { }
			}
		}

		// 要求を読む
		if( stdin.ReadLine() is not { } kind || stdin.ReadLine() is not { } pathLine
			|| !int.TryParse( stdin.ReadLine(), NumberStyles.None, CultureInfo.InvariantCulture, out var length ) ) return 1;
		var buffer = new char[length];
		if( stdin.ReadBlock( buffer, 0, length ) != length ) return 1;
		var source = new string( buffer );
		var path = pathLine.Length == 0 ? null : pathLine;

		if( kind == "compile" ) {
			try { ScriptRunner.Precompile( source, path, ScriptRunner.DefaultCacheDirectory ); } catch( Exception ) { return 1; }
			return 0;
		}

		using var cts = new CancellationTokenSource();
		new Thread( () => {
			try {
				// 停止要求の後も親の終了 (EOF) を見張り続ける
				while( stdin.ReadLine() is { } line )
					if( line == "stop" ) cts.Cancel();
			} catch( Exception e ) when( e is IOException or ObjectDisposedException ) { }
			try { cts.Cancel(); } catch( ObjectDisposedException ) { }
			// 親が終了した場合は、猶予内に終わらなければ自分で終了する
			Thread.Sleep( KillDelay );
			Environment.Exit( 1 );
		} ) { IsBackground = true, Name = "CsWSC stop" }.Start();

		try {
			ScriptRunner.Run( source, text => { PrintLog.Write( path, text ); Send( "print\t" + Escape( text ) ); }, cts.Token, new LogWindowProxy( Send ),
				path, ScriptRunner.DefaultCacheDirectory, compiling => Send( compiling ? "compiling\t1" : "compiling\t0" ) );
			Send( "ok" );
		} catch( OperationCanceledException ) {
			Send( "stopped" );
		} catch( ScriptException ex ) {
			Send( $"error\t{ex.Line.ToString( CultureInfo.InvariantCulture )}\t{Escape( ex.Message )}" );
		} catch( Exception ex ) {
			Send( "error\t0\t" + Escape( ex.Message ) );
		}
		return 0;
	}

	/// <summary>LogPrint などの PRINT 窓操作を親に転送します。</summary>
	private sealed class LogWindowProxy( Action<string> send ) : ILogWindow {
		public void SetVisible( bool visible ) => send( visible ? "visible\t1" : "visible\t0" );
		public void SetBounds( int? x, int? y, int? width, int? height ) =>
			send( string.Join( '\t', "bounds", Format( x ), Format( y ), Format( width ), Format( height ) ) );
		public void Clear() => send( "clear" );

		private static string Format( int? v ) => v?.ToString( CultureInfo.InvariantCulture ) ?? "";
	}
}
