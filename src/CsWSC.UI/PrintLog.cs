using System.Text;

namespace CsWSC.UI;

/// <summary>
/// Print で出した文字を %LocalAppData%\CsWSC\Logs\yyyy-MM-dd.log に追記します。
/// </summary>
/// <remarks>1 行は「時刻 [スクリプト名] 文字」です。書き込みに失敗しても実行は止めません。</remarks>
internal static class PrintLog {
	/// <summary>ログを置くフォルダーです。</summary>
	public static readonly string Directory = Path.Combine(
		Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "CsWSC", "Logs" );

	private static readonly UTF8Encoding Utf8 = new( false );
	private static readonly object Gate = new();

	/// <summary>1 件追記します。</summary>
	/// <param name="scriptPath">スクリプトのパスです。未保存なら null です。</param>
	/// <param name="text">Print した文字です。</param>
	public static void Write( string? scriptPath, string text ) {
		var now = DateTime.Now;
		var name = scriptPath is null ? "(未保存)" : Path.GetFileName( scriptPath );
		var prefix = $"{now:HH:mm:ss.fff} [{name}] ";
		// 複数行は各行に見出しを付ける
		var sb = new StringBuilder();
		foreach( var line in text.ReplaceLineEndings( "\n" ).Split( '\n' ) )
			sb.Append( prefix ).Append( line ).Append( "\r\n" );
		lock( Gate ) {
			try {
				System.IO.Directory.CreateDirectory( Directory );
				File.AppendAllText( Path.Combine( Directory, $"{now:yyyy-MM-dd}.log" ), sb.ToString(), Utf8 );
			} catch( Exception e ) when( e is IOException or UnauthorizedAccessException ) { }
		}
	}
}
