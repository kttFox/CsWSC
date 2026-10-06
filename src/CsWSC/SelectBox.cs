using System.Runtime.InteropServices;

namespace CsWSC;

/// <summary><see cref="ScriptGlobals.SlctBox(SlctKind, string, string[], double)"/> の表示形式。</summary>
public enum SlctKind
{
    /// <summary>項目ごとのボタン (押したボタンが選択になる)。</summary>
    Button,
    /// <summary>ラジオボタン + OK。</summary>
    Radio,
    /// <summary>ドロップダウン (コンボボックス) + OK。</summary>
    Combo,
    /// <summary>リストボックス + OK。</summary>
    List,
}

/// <summary>SLCTBOX のダイアログ。スクリプトのスレッド (STA) でモーダル表示する。</summary>
internal sealed class SelectBoxForm : Form
{
    private readonly Func<int[]> _getResult;
    private int[] _result = [];

    public SelectBoxForm(string message, IReadOnlyList<string> items, SlctKind? single, bool multiAsList)
    {
        Text = "CsWSC";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Font = SystemFonts.MessageBoxFont ?? Font;

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        Controls.Add(layout);
        if (message.Length > 0)
            layout.Controls.Add(new Label { Text = message.ReplaceLineEndings("\r\n"), AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 0, 0, 8) });

        const int width = 260;
        Func<int[]> get = () => [];
        switch (single)
        {
            case SlctKind.Button:
                for (var i = 0; i < items.Count; i++)
                {
                    var index = i;
                    var b = new Button { Text = items[i], Width = width, AutoSize = true };
                    b.Click += (_, _) => Finish([index]);
                    layout.Controls.Add(b);
                }
                get = () => [];
                break;
            case SlctKind.Radio:
            {
                var radios = items.Select((t, i) => new RadioButton { Text = t, AutoSize = true, Checked = i == 0 }).ToArray();
                layout.Controls.AddRange(radios);
                get = () => radios.Select((r, i) => (r, i)).Where(x => x.r.Checked).Select(x => x.i).ToArray();
                break;
            }
            case SlctKind.Combo:
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
                combo.Items.AddRange([.. items]);
                if (items.Count > 0) combo.SelectedIndex = 0;
                layout.Controls.Add(combo);
                get = () => combo.SelectedIndex >= 0 ? [combo.SelectedIndex] : [];
                break;
            }
            case SlctKind.List:
            case null when multiAsList:
            {
                var list = new ListBox
                {
                    Width = width,
                    Height = Math.Clamp(items.Count, 4, 15) * 18,
                    SelectionMode = single == null ? SelectionMode.MultiExtended : SelectionMode.One,
                    IntegralHeight = false,
                };
                list.Items.AddRange([.. items]);
                if (single != null && items.Count > 0) list.SelectedIndex = 0;
                list.DoubleClick += (_, _) => { if (single != null) Finish(get()); };
                layout.Controls.Add(list);
                get = () => [.. list.SelectedIndices.Cast<int>()];
                break;
            }
            default: // チェックボックス (複数選択)
            {
                var checks = items.Select(t => new CheckBox { Text = t, AutoSize = true }).ToArray();
                layout.Controls.AddRange(checks);
                get = () => checks.Select((c, i) => (c, i)).Where(x => x.c.Checked).Select(x => x.i).ToArray();
                break;
            }
        }
        _getResult = get;

        if (single != SlctKind.Button)
        {
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Width = width, Margin = new Padding(0, 8, 0, 0) };
            var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, AutoSize = true };
            var ok = new Button { Text = "OK", AutoSize = true };
            ok.Click += (_, _) => Finish(_getResult());
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    /// <summary>選ばれた項目の番号。キャンセル・タイムアウト時は空。</summary>
    public int[] Result => _result;

    private void Finish(int[] result)
    {
        _result = result;
        DialogResult = DialogResult.OK;
    }

    /// <summary>モーダル表示して結果を返す。timeout 秒 (0 以下で無制限) で閉じる。停止要求でも閉じる。</summary>
    public int[] Run(double timeout, CancellationToken cancellation)
    {
        using var timer = new System.Windows.Forms.Timer();
        if (timeout > 0)
        {
            timer.Interval = Math.Max(1, (int)(timeout * 1000));
            timer.Tick += (_, _) => { timer.Stop(); Close(); };
            Shown += (_, _) => timer.Start();
        }
        using var reg = cancellation.Register(() =>
        {
            try { if (IsHandleCreated) BeginInvoke(Close); }
            catch (InvalidOperationException) { }
        });
        Shown += (_, _) => { if (cancellation.IsCancellationRequested) Close(); Activate(); };
        ShowDialog();
        cancellation.ThrowIfCancellationRequested();
        return _result;
    }
}

/// <summary>POPUPMENU。Win32 のポップアップメニューを同期表示する。</summary>
internal static class PopupMenu
{
    private const uint MF_STRING = 0, MF_POPUP = 0x10, MF_SEPARATOR = 0x800;
    private const uint TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 0x2;
    private const int WM_CANCELMODE = 0x1F, WM_NULL = 0;

    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, IntPtr id, string? text);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

    /// <summary>
    /// items: "-" は区切り線、先頭に "&lt;&lt;" を付けると直前の項目のサブメニュー ("&lt;&lt;&lt;&lt;" で 2 段下)。
    /// 戻り値: 選ばれた項目 (先頭の "&lt;&lt;" を除いた文字列)。キャンセル時は null。
    /// </summary>
    public static string? Show(IReadOnlyList<string> items, Point screenPoint, CancellationToken cancellation)
    {
        var menus = new List<IntPtr>();
        var texts = new List<string>(); // コマンド ID - 1 → 文字列
        try
        {
            var root = CreatePopupMenu();
            menus.Add(root);
            Build(root, items, 0, 0, menus, texts);

            // メニューの所有ウィンドウ (前面に出さないとメニュー外クリックで閉じない)
            using var owner = new Form { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None, Size = new Size(1, 1), StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Opacity = 0 };
            owner.Show();
            SetForegroundWindow(owner.Handle);
            var hwnd = owner.Handle;
            using var reg = cancellation.Register(() => PostMessage(hwnd, WM_CANCELMODE, 0, 0));
            var id = cancellation.IsCancellationRequested ? 0
                : TrackPopupMenuEx(root, TPM_RETURNCMD | TPM_RIGHTBUTTON, screenPoint.X, screenPoint.Y, hwnd, IntPtr.Zero);
            PostMessage(hwnd, WM_NULL, 0, 0);
            cancellation.ThrowIfCancellationRequested();
            return id > 0 ? texts[id - 1] : null;
        }
        finally
        {
            // サブメニューは親と一緒に破棄されるので、ルートだけ破棄する
            if (menus.Count > 0) DestroyMenu(menus[0]);
        }
    }

    private static int Level(string item)
    {
        var level = 0;
        while (item.AsSpan(level * 2).StartsWith("<<")) level++;
        return level;
    }

    // items[start..] のうち level の項目を menu に追加し、次に処理すべき位置を返す
    private static int Build(IntPtr menu, IReadOnlyList<string> items, int start, int level, List<IntPtr> menus, List<string> texts)
    {
        var i = start;
        while (i < items.Count)
        {
            var itemLevel = Level(items[i]);
            if (itemLevel < level) break;
            var text = items[i][(itemLevel * 2)..];
            if (itemLevel > level)
                throw new ArgumentException($"POPUPMENU: \"{items[i]}\" の親項目がありません");

            if (text == "-")
            {
                AppendMenu(menu, MF_SEPARATOR, 0, null);
                i++;
                continue;
            }
            if (i + 1 < items.Count && Level(items[i + 1]) > level)
            {
                var sub = CreatePopupMenu();
                menus.Add(sub);
                i = Build(sub, items, i + 1, level + 1, menus, texts);
                AppendMenu(menu, MF_POPUP, sub, text);
                continue;
            }
            texts.Add(text);
            AppendMenu(menu, MF_STRING, texts.Count, text);
            i++;
        }
        return i;
    }
}
