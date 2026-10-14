using System.Text.Json;

namespace CsWSC.UI;

/// <summary>%AppData%\CsWSC\settings.json に保存する設定です。</summary>
public sealed class AppSettings {
	private const int MaxRecent = 20;

	private static readonly string SettingsPath = Path.Combine(
		Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "CsWSC", "settings.json" );

	/// <summary>最近使ったスクリプト (先頭が最新) です。</summary>
	public List<string> RecentFiles { get; set; } = [];

	public bool TopMost { get; set; }

	public Point? Location { get; set; }

	/// <summary>Print 出力窓の位置とサイズです。</summary>
	public Rectangle? PrintBounds { get; set; }

	// ---- 記録方法 ----

	/// <summary>true: 低レベル記録 (座標ベース) / false: 高レベル記録 (ウィンドウ・コントロールベース) です。</summary>
	public bool LowLevelRecord { get; set; } = true;

	/// <summary>余分な時間、マウス移動は記録しません。</summary>
	public bool SkipIdle { get; set; } = true;

	/// <summary>マウス座標を相対座標で記録します。</summary>
	public bool RelativeMouse { get; set; } = true;

	/// <summary>バックグラウンドで実行できる形で記録します。</summary>
	public bool BackgroundRecord { get; set; }

	/// <summary>記録後クリップボードへコピーします。</summary>
	public bool CopyAfterRecord { get; set; } = true;

	// ---- タスクトレイ ----

	/// <summary>起動時にタスクトレイに収納します。</summary>
	public bool TrayOnStart { get; set; }

	/// <summary>閉じるボタンで終了せずタスクトレイに収納します (終了は設定メニューから)。</summary>
	public bool TrayOnClose { get; set; }

	/// <summary>再生・記録中はタスクトレイに収納します。</summary>
	public bool TrayWhileRunning { get; set; }

	// ---- ホットキー ----

	public Hotkey PlayHotkey { get; set; } = new( "ALT", "F1" );
	public Hotkey StopHotkey { get; set; } = new( "ALT", "F2" );
	public Hotkey RecordHotkey { get; set; } = new( "ALT", "F3" );
	public Hotkey TrayHotkey { get; set; } = new( "WIN", "F1" );

	// ---- ランチャー ----

	/// <summary>
	/// 対象アプリなしのランチャーのメニューを開くホットキー (どのアプリでも有効。
	/// 同じキーを対象アプリのランチャーにも設定していれば、前面の窓に合えば対象アプリのランチャーの方を使う) です。
	/// </summary>
	public Hotkey LauncherMenuHotkey { get; set; } = new( "", "なし" );

	/// <summary>対象アプリなしのランチャーの枠 (0〜9, A〜Z の 36 枠。Load で必ず 36 枠にそろえる) です。右クリックメニューにも出します。</summary>
	public List<LauncherSlot> Launchers { get; set; } = LauncherSlot.Normalize( null );

	/// <summary>
	/// 対象アプリごとのランチャーです。TargetApps / TargetTitle が対象アプリの条件、Children が 36 枠、Hotkey がメニューを開くホットキー
	/// (そのアプリが前面のときだけ有効) です。同じキーの設定が複数あれば、前面の窓に合うもののうち、対象アプリのランチャーを対象アプリなしより優先し、対象アプリどうしでは一覧で先のものを使います
	/// (<see cref="LauncherHotkeyEntry.Pick"/>)。
	/// </summary>
	public List<LauncherSlot> LauncherProfiles { get; set; } = [];

	/// <summary>ランチャーのホットキー (メニューを開くキーと各枠のキー) です。</summary>
	public IEnumerable<LauncherHotkeyEntry> LauncherHotkeyEntries() =>
		LauncherSlot.HotkeyEntries( LauncherMenuHotkey, Launchers, LauncherProfiles );

	/// <summary>ランチャーのホットキー (HotkeyDialog の「使用中」判定や、再生などのキーとの重複チェック用) です。</summary>
	public IEnumerable<Hotkey> LauncherHotkeys() => LauncherHotkeyEntries().Select( e => e.Hotkey );

	public static AppSettings Load() {
		try {
			if( File.Exists( SettingsPath ) )
				if( JsonSerializer.Deserialize<AppSettings>( File.ReadAllText( SettingsPath ) ) is { } settings ) {
					settings.Launchers = LauncherSlot.Normalize( settings.Launchers );
					foreach( var profile in settings.LauncherProfiles ) profile.Children = LauncherSlot.Normalize( profile.Children );
					return settings;
				}
		} catch( Exception ) { }
		return new();
	}

	public void Save() {
		try {
			Directory.CreateDirectory( Path.GetDirectoryName( SettingsPath )! );
			File.WriteAllText( SettingsPath, JsonSerializer.Serialize( this, new JsonSerializerOptions { WriteIndented = true } ) );
		} catch( Exception ) { }
	}

	public void AddRecent( string path ) {
		path = Path.GetFullPath( path );
		RecentFiles.RemoveAll( p => string.Equals( p, path, StringComparison.OrdinalIgnoreCase ) );
		RecentFiles.Insert( 0, path );
		if( RecentFiles.Count > MaxRecent ) RecentFiles.RemoveRange( MaxRecent, RecentFiles.Count - MaxRecent );
	}
}
