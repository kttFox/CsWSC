using System.Diagnostics;
using System.IO;

namespace CsWSC.UI;

/// <summary>
/// --recompile の進み具合を、別プロセスのメイン画面へ伝えるためのファイルです。
/// キャッシュフォルダの recompile.progress に「PID・済んだ件数・全件数・コンパイル中のパス」をタブ区切りで書きます。
/// 書いたプロセスが終わっていれば (強制終了で消し忘れた場合も) 無いものとして扱います。
/// </summary>
internal static class RecompileProgress {
	/// <summary>読み取った進み具合です。</summary>
	public sealed record State( int Done, int Total, string Path );

	private static string FilePath => Path.Combine( ScriptRunner.DefaultCacheDirectory, "recompile.progress" );

	/// <summary>進み具合を書きます。失敗しても無視します (表示されないだけ)。</summary>
	public static void Write( int done, int total, string path ) {
		try {
			var temp = FilePath + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";
			File.WriteAllText( temp, $"{Environment.ProcessId}\t{done}\t{total}\t{path}" );
			File.Move( temp, FilePath, overwrite: true );
		} catch( Exception e ) when( e is IOException or UnauthorizedAccessException ) { }
	}

	/// <summary>進み具合のファイルを消します。</summary>
	public static void Clear() {
		try { File.Delete( FilePath ); } catch( Exception e ) when( e is IOException or UnauthorizedAccessException ) { }
	}

	/// <summary>進み具合を読みます。コンパイルし直していなければ null です。</summary>
	public static State? Read() {
		try {
			if( File.ReadAllText( FilePath ).Split( '\t', 4 ) is not [var pid, var done, var total, var path]
				|| !int.TryParse( pid, out var id ) || !int.TryParse( done, out var d ) || !int.TryParse( total, out var t ) || t <= 0 ) {
				return null;
			}

			using( var process = Process.GetProcessById( id ) ) {
				if( process.HasExited ) return null;
			}
			return new State( d, t, path );
		} catch( Exception e ) when( e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException ) {
			return null;
		}
	}
}
