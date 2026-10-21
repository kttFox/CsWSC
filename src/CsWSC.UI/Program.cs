using System.Diagnostics;

namespace CsWSC.UI;

static class Program {
	/// <summary>
	///  CsWSC.exe [スクリプト.csws]  … 引数を渡すと、メイン画面を停止ボタンの状態で出して実行し、終わったら終了します。
	///  ファイルを読めなければ通常どおり起動します。
	///  CsWSC.exe --run-script [--warmup]  … スクリプトを実行する子プロセスとして動きます (内部用)。
	///  CsWSC.exe --debug スクリプト.csws  … デバッグ実行します (VS Code 拡張から使う)。
	///  CsWSC.exe --recompile  … 依存ファイル (#load / #r) が変わったキャッシュをコンパイルし直して終了します (VS Code 拡張・実行時に裏で使う)。
	/// </summary>
	[STAThread]
	static void Main( string[] args ) {
		if( args is [ScriptProcess.ChildArgument, ..] ) {
			ApplicationConfiguration.Initialize();
			Environment.Exit( ScriptProcess.ChildMain( args.Contains( ScriptProcess.WarmupArgument ) ) );
			return;
		}

		if( args is [DebugArgument, var debugPath] ) {
			ApplicationConfiguration.Initialize();
			Environment.Exit( RunForDebug( debugPath ) );
			return;
		}

		if( args is [RecompileArgument] ) {
			Environment.Exit( Recompile() );
			return;
		}

		LaunchTrace.Inherit();

		// 起動引数のスクリプトは、窓の準備 (ApplicationConfiguration.Initialize を含む) を待たずに実行を始める
		// (StartEarly は窓を作らないので Initialize より前に呼んでよい)
		string? source = null;
		if( args.FirstOrDefault() is { } path ) {
			// ライブラリは読み込まない (メイン画面も出さずに終わる)
			if( ScriptLibraryResolver.IsLibrary( path ) ) {
				ApplicationConfiguration.Initialize();
				MessageBox.Show( ScriptRunner.LibraryNotRunnableMessage( path ), "CsWSC", MessageBoxButtons.OK, MessageBoxIcon.Warning );
				return;
			}
			try { source = File.ReadAllText( path ); } catch( Exception ) { }
			if( source != null ) {
				ScriptProcess.KeepStandby = false; // 終わったら終了するので、次の再生用の待機プロセスは要らない
				// 別プロセスとして起動されたのでなければ (コマンドラインなど)、このプロセスが作られた時刻から記録する
				if( !LaunchTrace.Active ) LaunchTrace.BeginAtProcessStart();
				ScriptProcess.StartEarly( source, path );
			}
		}

		ApplicationConfiguration.Initialize();
		if( source != null ) {
			Application.Run( new MainForm( (args[0], source) ) );
			ScriptProcess.Shutdown();
			return;
		}
		// 常駐する起動のときだけ、共通のスクリプトなどの変更を見張ってキャッシュを作り直す
		using var watcher = DependencyWatcher.Start();
		Application.Run( new MainForm() );
	}

	/// <summary>CsWSC.exe --debug スクリプト.csws … デバッガーから起動する用です。</summary>
	public const string DebugArgument = "--debug";

	/// <summary>CsWSC.exe --recompile … 依存ファイルが変わったキャッシュをコンパイルし直す用です。</summary>
	public const string RecompileArgument = "--recompile";

	/// <summary>
	/// 依存ファイルが変わったキャッシュをコンパイルし直します。スクリプトの実行を邪魔しないよう優先度を下げ、同時には 1 つだけ動かします。
	/// 動いている間に別の変更があった場合に取りこぼさないよう、待っている側は終わるのを待ってからもう一度行います。
	/// </summary>
	private static int Recompile() {
		try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch( Exception ) { }
		using var mutex = new Mutex( false, @"Local\CsWSC.Recompile" );
		try {
			try { mutex.WaitOne(); } catch( AbandonedMutexException ) { }
			ScriptRunner.RecompileStale( ScriptRunner.DefaultCacheDirectory );
			return 0;
		} catch( Exception ) {
			return 1;
		} finally {
			mutex.ReleaseMutex();
		}
	}

	/// <summary>--recompile を別プロセスで裏で起動します (終わるのを待たない)。</summary>
	public static void StartRecompile() {
		try {
			using var _ = Process.Start( new ProcessStartInfo( Environment.ProcessPath!, RecompileArgument ) { UseShellExecute = false, CreateNoWindow = true } );
		} catch( Exception ) { }
	}

	/// <summary>
	/// デバッガーのブレークポイントで止まれるよう、子プロセスを使わずこのプロセスで実行します。
	/// 出力は標準出力 (VS Code のデバッグコンソール) に出します。停止はデバッガーの停止で行います。
	/// </summary>
	private static int RunForDebug( string path ) {
		// VS Code のデバッグコンソールは UTF-8 で読むので、既定のコードページ (CP932) だと日本語が化ける
		Console.OutputEncoding = System.Text.Encoding.UTF8;
		try {
			ScriptRunner.RunForDebug( File.ReadAllText( path ), path, Console.WriteLine, CancellationToken.None );
			return 0;
		} catch( ScriptException ex ) {
			Console.Error.WriteLine( ex.Line > 0 ? $"{path}({ex.Line}): {ex.Message}" : ex.Message );
		} catch( Exception ex ) {
			Console.Error.WriteLine( ex.Message );
		}
		return 1;
	}
}

public class Form : System.Windows.Forms.Form {
	public Form() {
		this.Icon = System.Drawing.SystemIcons.Application;
	}
}
