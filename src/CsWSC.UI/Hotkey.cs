namespace CsWSC.UI;

/// <summary>ホットキー (グローバル / フック、修飾キー + キー) です。修飾キーは "CTRL+ALT" のような + 区切り (空ならなし) です。</summary>
public sealed record Hotkey( string Modifier, string Key ) {
	public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

	/// <summary>コンボボックスの選択肢 (キー) です。「なし」で無効です。</summary>
	public static readonly string[] Keys =
	[
		"なし",
		.. Enumerable.Range(1, 12).Select(i => $"F{i}"),
		.. Enumerable.Range('A', 26).Select(c => ((char)c).ToString()),
		.. Enumerable.Range('0', 10).Select(c => ((char)c).ToString()),
		"HOME", "END", "PAGEUP", "PAGEDOWN", "SPACE", "TAB",
	];

	/// <summary>押したキーを横取りせず、前面のアプリにもそのまま送ります (低レベルフックで受ける)。</summary>
	public bool PassThrough { get; init; }

	// 重複チェック用に、等価性はキーの組み合わせだけで判定する (PassThrough は含めない)
	public bool Equals( Hotkey? other ) => other is not null && Modifier == other.Modifier && Key == other.Key;
	public override int GetHashCode() => HashCode.Combine( Modifier, Key );

	public bool Enabled => Key != "なし" && TryGetVirtualKey( out _ );

	public uint ModifierFlags => Modifier.Split( '+' ).Aggregate( 0u, ( acc, m ) => acc | m switch {
		"ALT" => MOD_ALT,
		"CTRL" => MOD_CONTROL,
		"SHIFT" => MOD_SHIFT,
		"WIN" => MOD_WIN,
		_ => 0u,
	} );

	public bool TryGetVirtualKey( out uint vk ) {
		System.Windows.Forms.Keys key = Key switch {
			"PAUSE" => System.Windows.Forms.Keys.Pause,
			"SCROLL" => System.Windows.Forms.Keys.Scroll,
			"INSERT" => System.Windows.Forms.Keys.Insert,
			"DELETE" => System.Windows.Forms.Keys.Delete,
			"HOME" => System.Windows.Forms.Keys.Home,
			"END" => System.Windows.Forms.Keys.End,
			"PAGEUP" => System.Windows.Forms.Keys.PageUp,
			"PAGEDOWN" => System.Windows.Forms.Keys.PageDown,
			{ Length: 1 } k when char.IsDigit( k[0] ) => System.Windows.Forms.Keys.D0 + ( k[0] - '0' ),
			_ => Enum.TryParse<System.Windows.Forms.Keys>( Key, true, out var parsed ) ? parsed : System.Windows.Forms.Keys.None,
		};
		vk = (uint)key;
		return key != System.Windows.Forms.Keys.None;
	}

	/// <summary>設定画面に出す文字列 (登録方式付き) です。</summary>
	public string ToDisplayString() => !Enabled ? "なし" : $"{this} ({( PassThrough ? "フック" : "グローバル" )})";

	public override string ToString() => !Enabled ? "なし" : Modifier == "" ? Key : $"{Modifier} + {Key}";
}
