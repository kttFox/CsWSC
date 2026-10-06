using System.Text.Json;

namespace CsWSC.UI;

/// <summary>%AppData%\CsWSC\settings.json に保存する設定。</summary>
public sealed class AppSettings
{
    private const int MaxRecent = 20;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CsWSC", "settings.json");

    /// <summary>最近使ったスクリプト (先頭が最新)。</summary>
    public List<string> RecentFiles { get; set; } = [];

    public bool TopMost { get; set; }

    public Point? Location { get; set; }

    // ---- 記録方法 ----

    /// <summary>true: 低レベル記録 (座標ベース) / false: 高レベル記録 (ウィンドウ・コントロールベース)。</summary>
    public bool LowLevelRecord { get; set; } = true;

    /// <summary>余分な時間、マウス移動は記録しない。</summary>
    public bool SkipIdle { get; set; } = true;

    /// <summary>マウス座標を相対座標で記録する。</summary>
    public bool RelativeMouse { get; set; } = true;

    /// <summary>バックグラウンドで実行できる形で記録。</summary>
    public bool BackgroundRecord { get; set; }

    /// <summary>記録後クリップボードへコピーする。</summary>
    public bool CopyAfterRecord { get; set; } = true;

    // ---- タスクトレイ ----

    /// <summary>起動時にタスクトレイに収納する。</summary>
    public bool TrayOnStart { get; set; }

    /// <summary>閉じるボタンで終了せずタスクトレイに収納する (終了は設定メニューから)。</summary>
    public bool TrayOnClose { get; set; }

    /// <summary>再生・記録中はタスクトレイに収納する。</summary>
    public bool TrayWhileRunning { get; set; }

    // ---- ホットキー ----

    public Hotkey PlayHotkey { get; set; } = new("ALT", "F1");
    public Hotkey StopHotkey { get; set; } = new("ALT", "F2");
    public Hotkey RecordHotkey { get; set; } = new("ALT", "F3");
    public Hotkey TrayHotkey { get; set; } = new("WIN", "F1");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch (Exception) { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception) { }
    }

    public void AddRecent(string path)
    {
        path = Path.GetFullPath(path);
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecent) RecentFiles.RemoveRange(MaxRecent, RecentFiles.Count - MaxRecent);
    }
}
