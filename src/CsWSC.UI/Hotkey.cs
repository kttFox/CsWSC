namespace CsWSC.UI;

/// <summary>グローバルホットキー (修飾キー + キー)。設定画面のコンボボックスの文字列そのままで保持する。</summary>
public sealed record Hotkey(string Modifier, string Key)
{
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

    /// <summary>コンボボックスの選択肢 (修飾キー)。</summary>
    public static readonly string[] Modifiers = ["ALT", "CTRL", "SHIFT", "WIN", "CTRL+ALT", "CTRL+SHIFT", "ALT+SHIFT"];

    /// <summary>コンボボックスの選択肢 (キー)。「なし」で無効。</summary>
    public static readonly string[] Keys =
    [
        "なし",
        .. Enumerable.Range(1, 12).Select(i => $"F{i}"),
        .. Enumerable.Range('A', 26).Select(c => ((char)c).ToString()),
        .. Enumerable.Range('0', 10).Select(c => ((char)c).ToString()),
        "PAUSE", "SCROLL", "INSERT", "DELETE", "HOME", "END",
    ];

    public bool Enabled => Key != "なし" && TryGetVirtualKey(out _);

    public uint ModifierFlags => Modifier.Split('+').Aggregate(0u, (acc, m) => acc | m switch
    {
        "ALT" => MOD_ALT,
        "CTRL" => MOD_CONTROL,
        "SHIFT" => MOD_SHIFT,
        "WIN" => MOD_WIN,
        _ => 0u,
    });

    public bool TryGetVirtualKey(out uint vk)
    {
        System.Windows.Forms.Keys key = Key switch
        {
            "PAUSE" => System.Windows.Forms.Keys.Pause,
            "SCROLL" => System.Windows.Forms.Keys.Scroll,
            "INSERT" => System.Windows.Forms.Keys.Insert,
            "DELETE" => System.Windows.Forms.Keys.Delete,
            "HOME" => System.Windows.Forms.Keys.Home,
            "END" => System.Windows.Forms.Keys.End,
            { Length: 1 } k when char.IsDigit(k[0]) => System.Windows.Forms.Keys.D0 + (k[0] - '0'),
            _ => Enum.TryParse<System.Windows.Forms.Keys>(Key, true, out var parsed) ? parsed : System.Windows.Forms.Keys.None,
        };
        vk = (uint)key;
        return key != System.Windows.Forms.Keys.None;
    }

    public override string ToString() => Enabled ? $"{Modifier} + {Key}" : "なし";
}
