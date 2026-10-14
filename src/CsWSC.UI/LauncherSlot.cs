using System.Text.Json.Serialization;

namespace CsWSC.UI;

/// <summary>
/// ランチャーの 1 枠 (UWSC と同じく 0〜9, A〜Z の 36 枠) です。
/// Path があればスクリプト、Children があれば階層 (サブメニュー)、どちらも無ければ未設定です
/// (タイトルだけなら選べない見出し行として表示する)。
/// </summary>
public sealed class LauncherSlot {
	/// <summary>1 階層の枠の数です。</summary>
	public const int Count = 36;

	/// <summary>枠のキー (0〜9, A〜Z) です。メニューのアクセスキーにもなります。</summary>
	public static char KeyOf( int index ) => index < 10 ? (char)( '0' + index ) : (char)( 'A' + index - 10 );

	/// <summary>メニューに出す名前です。空ならスクリプトのファイル名です。</summary>
	public string Title { get; set; } = "";

	public string Path { get; set; } = "";

	/// <summary>階層化した枠の中身 (Count 枠) です。</summary>
	public List<LauncherSlot>? Children { get; set; }

	/// <summary>CsWSC の再生とは別に起動します (再生中でも実行でき、STOP では止まらない)。</summary>
	public bool SeparateProcess { get; set; }

	/// <summary>メニューを開かずに直接実行するホットキー (任意) です。対象アプリのランチャー (AppSettings.LauncherProfiles の要素) ではメニューを開くホットキーです。</summary>
	public Hotkey Hotkey { get; set; } = new( "", "なし" );

	/// <summary>
	/// 対象アプリのランチャー (AppSettings.LauncherProfiles) の条件: 前面の窓のプロセス名です。
	/// ; 区切り、* ? のワイルドカード可です。.exe を含めた全体と比べます (notepad.exe)。空ならプロセス名は問いません。
	/// </summary>
	public string TargetApps { get; set; } = "";

	/// <summary>対象アプリのランチャーの条件: 前面の窓のタイトル (部分一致。* ? を含めばワイルドカードで全体一致) です。空なら問いません。</summary>
	public string TargetTitle { get; set; } = "";

	[JsonIgnore] public bool HasTarget => TargetApps.Trim() != "" || TargetTitle.Trim() != "";

	/// <summary>前面の窓 (プロセス名・タイトル) がこの枠の対象かどうかです。</summary>
	public bool MatchesTarget( string processName, string title ) {
		var apps = TargetApps.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
		var exe = StripExe( processName ) + ".exe";
		if( apps.Length > 0 && !apps.Any( a => Wildcard( a, exe ) ) ) return false;
		var t = TargetTitle.Trim();
		if( t == "" ) return true;
		return t.IndexOfAny( ['*', '?'] ) >= 0 ? Wildcard( t, title ) : title.Contains( t, StringComparison.OrdinalIgnoreCase );
	}

	private static string StripExe( string name ) =>
		name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) ? name[..^4] : name;

	private static bool Wildcard( string pattern, string text ) =>
		System.Text.RegularExpressions.Regex.IsMatch( text,
			"^" + System.Text.RegularExpressions.Regex.Escape( pattern ).Replace( @"\*", ".*" ).Replace( @"\?", "." ) + "$",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline );

	[JsonIgnore] public bool IsScript => Children == null && Path != "";
	[JsonIgnore] public bool IsFolder => Children != null;
	[JsonIgnore] public bool IsEmpty => !IsScript && !IsFolder && Title == "";

	[JsonIgnore]
	public string DisplayName => Title != "" ? Title : IsScript ? System.IO.Path.GetFileName( Path ) : "";

	public LauncherSlot Clone() => new() {
		Title = Title,
		Path = Path,
		Children = Children?.Select( c => c.Clone() ).ToList(),
		SeparateProcess = SeparateProcess,
		Hotkey = Hotkey,
		TargetApps = TargetApps,
		TargetTitle = TargetTitle,
	};

	/// <summary>ちょうど Count 枠になるよう、足りなければ空き枠を足し、多ければ切り捨てます (階層の中も)。</summary>
	public static List<LauncherSlot> Normalize( List<LauncherSlot>? slots ) {
		var list = ( slots ?? [] ).Take( Count ).ToList();
		while( list.Count < Count ) list.Add( new() );
		foreach( var s in list.Where( s => s.IsFolder ) ) s.Children = Normalize( s.Children );
		return list;
	}

	/// <summary>階層の中も含めたすべての枠です。</summary>
	public static IEnumerable<LauncherSlot> All( IEnumerable<LauncherSlot> slots ) =>
		slots.SelectMany( s => s.IsFolder ? All( s.Children! ).Prepend( s ) : [s] );

	/// <summary>対象アプリのランチャー (36 枠を Children に持つ) の表示名です。</summary>
	public string ProfileName => TargetApps.Trim() != "" ? TargetApps.Trim() : TargetTitle.Trim() != "" ? $"タイトル: {TargetTitle.Trim()}" : "(条件未設定)";

	/// <summary>
	/// ホットキーを設定したスクリプトの枠と、その枠に効く対象アプリの条件 (対象アプリのランチャーなら root、それ以外は空) です。
	/// </summary>
	public static IEnumerable<(LauncherSlot Slot, LauncherSlot[] Conditions)> HotkeyScripts( IEnumerable<LauncherSlot> slots, LauncherSlot? root = null ) =>
		HotkeyScripts( slots, root is { HasTarget: true } ? [root] : [] );

	private static IEnumerable<(LauncherSlot, LauncherSlot[])> HotkeyScripts( IEnumerable<LauncherSlot> slots, LauncherSlot[] parents ) =>
		slots.SelectMany( s => s.IsFolder ? HotkeyScripts( s.Children!, parents )
			: s.IsScript && s.Hotkey.Enabled ? [(s, parents)] : (IEnumerable<(LauncherSlot, LauncherSlot[])>)[] );

	/// <summary>
	/// ランチャーのホットキー (メニューを開くキーと各枠のキー) をすべて並べます。
	/// defaults は対象アプリなしの 36 枠、profiles は対象アプリごとのランチャー (Hotkey がメニューを開くキー) です。
	/// </summary>
	public static IEnumerable<LauncherHotkeyEntry> HotkeyEntries( Hotkey defaultMenuHotkey, List<LauncherSlot> defaults, IEnumerable<LauncherSlot> profiles ) {
		var defaultRoot = new LauncherSlot { Children = defaults };
		if( defaultMenuHotkey.Enabled ) yield return new( defaultMenuHotkey, [], null, defaultRoot );
		foreach( var (slot, conditions) in HotkeyScripts( defaults ) ) yield return new( slot.Hotkey, conditions, slot, null );
		foreach( var profile in profiles ) {
			if( profile.Hotkey.Enabled ) yield return new( profile.Hotkey, [profile], null, profile );
			foreach( var (slot, conditions) in HotkeyScripts( profile.Children ?? [], profile ) ) yield return new( slot.Hotkey, conditions, slot, null );
		}
	}

	/// <summary>
	/// ランチャーのホットキーの重複を調べ、問題があればメッセージを返します。
	/// 対象アプリのランチャーが違えば同じキーを使えます (前面のアプリで振り分ける)。
	/// </summary>
	public static string? CheckHotkeys( IEnumerable<LauncherHotkeyEntry> entries, IEnumerable<Hotkey> reserved ) {
		var used = reserved.Where( h => h.Enabled ).ToHashSet();
		foreach( var group in entries.GroupBy( x => x.Hotkey ) ) {
			var list = group.ToList();
			var name = list[0].Name;
			if( used.Contains( group.Key ) ) return $"ホットキー {group.Key} が他のホットキーと重複しています。({name})";
			for( int i = 0; i < list.Count; i++ ) {
				for( int j = i + 1; j < list.Count; j++ ) {
					if( list[i].Conditions.SequenceEqual( list[j].Conditions ) )
						return $"ホットキー {group.Key} が重複しています。({list[i].Name} / {list[j].Name})\n同じキーを使えるのは対象アプリが違う場合だけです。";
				}
			}
			// 登録方式ごとに別々に登録するので、方式の違う設定が同じ窓で同時に動かないことを確かめる
			for( int i = 0; i < list.Count; i++ ) {
				for( int j = i + 1; j < list.Count; j++ ) {
					if( list[i].Hotkey.PassThrough != list[j].Hotkey.PassThrough && !Disjoint( list[i], list[j] ) )
						return $"ホットキー {group.Key} を使う設定で、登録方式 (グローバル / フック) をそろえてください。({list[i].Name} / {list[j].Name})\n" +
							"方式を混ぜられるのは、どちらも対象アプリのプロセス名で限定し、プロセス名が重ならない場合だけです。";
				}
			}
		}
		return null;
	}

	/// <summary>
	/// 2 つの設定が同じ前面の窓で同時に有効にならないことが確かかどうかです。
	/// どちらの条件にもプロセス名があり、互いのプロセス名が重ならない場合だけ true です (タイトルの条件は重なりを判定できないので見ない)。
	/// </summary>
	private static bool Disjoint( LauncherHotkeyEntry a, LauncherHotkeyEntry b ) =>
		a.Conditions.Any( x => b.Conditions.Any( y => AppsDisjoint( x, y ) ) );

	private static bool AppsDisjoint( LauncherSlot a, LauncherSlot b ) {
		string[] Apps( LauncherSlot s ) =>
			[.. s.TargetApps.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )];
		var (x, y) = (Apps( a ), Apps( b ));
		if( x.Length == 0 || y.Length == 0 ) return false;
		static bool IsPattern( string s ) => s.IndexOfAny( ['*', '?'] ) >= 0;
		return !x.Any( p => y.Any( q => (IsPattern( p ), IsPattern( q )) switch {
			(true, true ) => true, // パターンどうしは重なりを判定できないので、重なるとみなす
			(true, false ) => Wildcard( p, q ),
			(false, true ) => Wildcard( q, p ),
			_ => string.Equals( p, q, StringComparison.OrdinalIgnoreCase ),
		} ) );
	}

	/// <summary>対象アプリなしの枠だけでの重複チェック (root を渡すとその対象アプリのランチャーとして調べる) です。テスト用です (アプリ本体は上の版を使う)。</summary>
	public static string? CheckHotkeys( IEnumerable<LauncherSlot> slots, IEnumerable<Hotkey> reserved, LauncherSlot? root = null ) =>
		CheckHotkeys( HotkeyScripts( slots, root ).Select( x => new LauncherHotkeyEntry( x.Slot.Hotkey, x.Conditions, x.Slot, null ) ), reserved );
}

/// <summary>
/// ランチャーのホットキー 1 つ分です。Script があればその枠を実行し、Menu があればそのランチャー (Children の 36 枠) のメニューを開きます。
/// Conditions は前面の窓がすべてに合うときだけ有効です (対象アプリ付きのものが、対象アプリなしより優先される)。
/// </summary>
public sealed record LauncherHotkeyEntry( Hotkey Hotkey, LauncherSlot[] Conditions, LauncherSlot? Script, LauncherSlot? Menu ) {
	public string Name => Script?.DisplayName ?? ( Menu is { HasTarget: true } m ? $"{m.ProfileName} のメニュー" : "メニュー" );

	public bool Matches( string processName, string title ) => Conditions.All( c => c.MatchesTarget( processName, title ) );

	/// <summary>同じホットキーの候補から、前面の窓に合うもののうち条件の多い (より限定した) ものを選びます。同数なら先のものです。合うものが無ければ null です。</summary>
	public static LauncherHotkeyEntry? Pick( IEnumerable<LauncherHotkeyEntry> candidates, string processName, string title ) =>
		candidates.Where( c => c.Matches( processName, title ) ).MaxBy( c => c.Conditions.Length );
}
