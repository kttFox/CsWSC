using System.Text;

namespace CsWSC.UI;

/// <summary>
/// Print で出した文字を %LocalAppData%\CsWSC\Logs\yyyy-MM-dd.log に追記します。
/// </summary>
/// <remarks>
/// 1 行は「時刻 [スクリプト名:プロセス ID] 文字」です。スクリプトは 1 回の実行ごとに別プロセスなので、
/// 同時に実行しているスクリプトはプロセス ID で区別できます。
/// 実行の開始と終了は「==== 開始 ====」「==== 終了 (結果) ====」の行で記録します。
/// 書き込みに失敗しても実行は止めません。
/// </remarks>
internal static class PrintLog {
	/// <summary>ログを置くフォルダーです。</summary>
	public static readonly string Directory = Path.Combine(
		Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "CsWSC", "Logs" );

	private static readonly UTF8Encoding Utf8 = new( false );
	private static readonly object Gate = new();

	/// <summary>1 件追記します。</summary>
	/// <param name="scriptPath">スクリプトのパスです。未保存なら null です。</param>
	/// <param name="text">Print した文字です。</param>
	public static void Write( string? scriptPath, string text ) => Write( scriptPath, text, null );

	/// <summary>起動時間 (<see cref="LaunchTrace"/>) を、Print とは別のファイル (launch.log) に追記します。日が変わって最初に書くときに中身を消します。</summary>
	/// <param name="scriptPath">スクリプトのパスです。未保存なら null です。</param>
	/// <param name="text">書く内容です。</param>
	public static void WriteLaunch( string? scriptPath, string text ) => Write( scriptPath, text, "launch.log" );

	/// <param name="dailyFile">その日の分だけ残すファイルの名前です。null なら日付のファイルに追記します。</param>
	private static void Write( string? scriptPath, string text, string? dailyFile ) {
		var now = DateTime.Now;
		var name = scriptPath is null ? "(未保存)" : Path.GetFileName( scriptPath );
		var prefix = $"{now:HH:mm:ss.fff} [{name}:{Environment.ProcessId}] ";
		// 複数行は各行に見出しを付ける
		var sb = new StringBuilder();
		foreach( var line in text.ReplaceLineEndings( "\n" ).Split( '\n' ) )
			sb.Append( prefix ).Append( line ).Append( "\r\n" );
		lock( Gate ) {
			try {
				System.IO.Directory.CreateDirectory( Directory );
				if( dailyFile != null ) {
					// 最後に書いたのが今日でなければ、前の日の分を消して書き直す
					var file = Path.Combine( Directory, dailyFile );
					if( File.Exists( file ) && File.GetLastWriteTime( file ).Date == now.Date ) File.AppendAllText( file, sb.ToString(), Utf8 );
					else File.WriteAllText( file, sb.ToString(), Utf8 );
				} else File.AppendAllText( Path.Combine( Directory, $"{now:yyyy-MM-dd}.log" ), sb.ToString(), Utf8 );
			} catch( Exception e ) when( e is IOException or UnauthorizedAccessException ) { }
		}
	}

	/// <summary>実行の開始を記録します。</summary>
	/// <param name="scriptPath">スクリプトのパスです。未保存なら null です。</param>
	public static void Start( string? scriptPath ) =>
		Write( scriptPath, $"==== 開始 ==== {scriptPath ?? "(未保存)"}" );

	/// <summary>実行の終了を記録します。</summary>
	/// <param name="scriptPath">スクリプトのパスです。未保存なら null です。</param>
	/// <param name="result">終わり方です (正常終了 / 停止 / エラーの内容)。</param>
	public static void End( string? scriptPath, string result ) =>
		Write( scriptPath, $"==== 終了 ({result}) ====" );
}
