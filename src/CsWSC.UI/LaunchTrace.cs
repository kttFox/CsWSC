using System.Runtime.InteropServices;
using System.Globalization;

namespace CsWSC.UI;

/// <summary>
/// 再生の操作 (ボタン・ホットキー・ランチャー・コマンドライン) からスクリプトの実行が始まるまでの各段階の時刻を記録し、ログに書き出します (遅いマシンでの原因調べ用)。
/// </summary>
/// <remarks>
/// 記録は「名前=時刻 (UTC の Ticks)」を ; で区切った文字列で、プロセスをまたいで引き継ぎます。
/// 別プロセスで起動する CsWSC.exe へは環境変数 <see cref="EnvironmentVariable"/>、スクリプトを実行する子プロセスへは実行の要求に付けて渡します。
/// 子プロセスはスクリプトの実行を始めるときに、まとめて 1 行で Print とは別のログ (launch.log、その日の分だけ残す) に書きます。
/// </remarks>
internal static class LaunchTrace {
	/// <summary>別プロセスで起動する CsWSC.exe に記録を渡す環境変数です。</summary>
	public const string EnvironmentVariable = "CSWSC_LAUNCH_TRACE";

	/// <summary>このプロセスで記録中の内容です。記録していなければ null です。</summary>
	private static string? _marks;

	/// <summary>記録中かどうかです。</summary>
	public static bool Active => _marks != null;

	/// <summary>このプロセスが作られた時刻から記録を始めます (コマンドラインから起動されたとき)。</summary>
	public static void BeginAtProcessStart() {
		_marks = Append( "", "プロセス作成", ProcessStartTime() );
		Mark( "Main 開始" );
	}

	/// <summary>記録を始めます。</summary>
	/// <param name="name">最初の段階の名前です。</param>
	public static void Begin( string name ) => _marks = Append( "", name, DateTime.UtcNow );

	/// <summary>記録中なら、今の時刻で段階を追加します。</summary>
	/// <param name="name">段階の名前です。</param>
	public static void Mark( string name ) {
		if( _marks != null ) _marks = Append( _marks, name, DateTime.UtcNow );
	}

	/// <summary>環境変数から記録を引き継ぎます (別プロセスで起動された CsWSC.exe の Main で呼びます)。</summary>
	public static void Inherit() {
		if( Environment.GetEnvironmentVariable( EnvironmentVariable ) is not { } marks ) return;
		// スクリプトを実行する子プロセスには要求で渡すので、環境変数は引き継がせない
		Environment.SetEnvironmentVariable( EnvironmentVariable, null );
		_marks = Append( marks, "プロセス作成", ProcessStartTime() );
		Mark( "Main 開始" );
	}

	/// <summary>記録を取り出して終えます。記録していなければ null です。</summary>
	public static string? Take() {
		var marks = _marks;
		_marks = null;
		return marks;
	}

	/// <summary>記録に段階を追加した文字列を返します。</summary>
	public static string Append( string marks, string name, DateTime utc ) =>
		$"{marks};{name}={utc.Ticks.ToString( CultureInfo.InvariantCulture )}";

	/// <summary>このプロセスが作られた時刻 (UTC) です。</summary>
	/// <remarks>Process.StartTime は初回の読み込みが遅く、遅いマシンでは 1 秒以上かかったので GetProcessTimes を直接呼びます。</remarks>
	public static DateTime ProcessStartTime() =>
		GetProcessTimes( GetCurrentProcess(), out var creation, out _, out _, out _ ) ? DateTime.FromFileTimeUtc( creation ) : DateTime.UtcNow;

	[DllImport( "kernel32.dll" )] private static extern IntPtr GetCurrentProcess();
	[DllImport( "kernel32.dll" )] private static extern bool GetProcessTimes( IntPtr process, out long creation, out long exit, out long kernel, out long user );

	/// <summary>ログに書く 1 行にします。各段階を、最初の段階からの経過ミリ秒で表します。</summary>
	public static string Describe( string marks ) {
		var items = marks.Split( ';', StringSplitOptions.RemoveEmptyEntries ).Select( s => {
			var i = s.LastIndexOf( '=' );
			return (Name: s[..i], Ticks: long.Parse( s[( i + 1 )..], CultureInfo.InvariantCulture ));
		} ).ToList();
		if( items.Count == 0 ) return "起動時間: (記録なし)";
		var origin = items[0].Ticks;
		// 待機していた子プロセスは押す前に作られているので、マイナスになる
		return "起動時間 (ms): " + string.Join( " / ", items.Select( x =>
			$"{x.Name} {( ( x.Ticks - origin ) / TimeSpan.TicksPerMillisecond ).ToString( "+0;-0;0", CultureInfo.InvariantCulture )}" ) );
	}
}
