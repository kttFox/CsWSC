using static CsWSC.NativeMethods;

namespace CsWSC;

/// <summary>スクリプトから操作するウィンドウ。UWSC のウィンドウ ID に相当。</summary>
public sealed class Window
{
    public IntPtr Handle { get; }

    internal Window(IntPtr handle) => Handle = handle;

    /// <summary>ウィンドウハンドルから Window を作る。無効なハンドルなら null。</summary>
    public static Window? FromHandle(IntPtr handle) => handle != IntPtr.Zero && IsWindow(handle) ? new(handle) : null;

    /// <summary>現在アクティブなウィンドウ。</summary>
    public static Window Active => new(GetForegroundWindow());

    public bool Exists => IsWindow(Handle);
    public string Title => GetTitle(Handle);
    public string ClassName => GetClass(Handle);
    public bool Visible => IsWindowVisible(Handle);

    public Rectangle Bounds
    {
        get
        {
            GetWindowRect(Handle, out var r);
            return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        }
    }

    public int X => Bounds.X;
    public int Y => Bounds.Y;
    public int Width => Bounds.Width;
    public int Height => Bounds.Height;

    /// <summary>前面に出してアクティブにする (最小化されていれば元に戻す)。</summary>
    public Window Activate()
    {
        if (IsIconic(Handle)) ShowWindow(Handle, SW_RESTORE);
        SetForegroundWindow(Handle);
        return this;
    }

    /// <summary>位置を変更する。サイズを省略すると現在のサイズのまま。</summary>
    public Window Move(int x, int y, int? width = null, int? height = null)
    {
        var b = Bounds;
        MoveWindow(Handle, x, y, width ?? b.Width, height ?? b.Height, true);
        return this;
    }

    // ---- 状態 (UWSC の STATUS に相当) ----

    /// <summary>アクティブか (ST_ACTIVE)。</summary>
    public bool IsActive => GetForegroundWindow() == Handle;
    /// <summary>最小化されているか (ST_ICONIC)。</summary>
    public bool Minimized => IsIconic(Handle);
    /// <summary>最大化されているか (ST_MAXIMIZED)。</summary>
    public bool Maximized => IsZoomed(Handle);
    /// <summary>入力を受け付けるか (ST_ENABLED)。</summary>
    public bool Enabled => IsWindowEnabled(Handle);
    /// <summary>応答なしか (ST_BUSY)。</summary>
    public bool Busy => IsHungAppWindow(Handle);
    /// <summary>常に手前か (ST_TOPMOST)。</summary>
    public bool Topmost => (GetWindowLongPtr(Handle, GWL_EXSTYLE).ToInt64() & WS_EX_TOPMOST) != 0;

    /// <summary>クライアント領域 (スクリーン座標)。ST_CLX / ST_CLY / ST_CLWIDTH / ST_CLHEIGHT に相当。</summary>
    public Rectangle ClientBounds
    {
        get
        {
            GetClientRect(Handle, out var r);
            var p = new POINT();
            ClientToScreen(Handle, ref p);
            return new Rectangle(p.X, p.Y, r.Right - r.Left, r.Bottom - r.Top);
        }
    }

    /// <summary>プロセス ID (ST_PROCESS)。</summary>
    public int ProcessId { get { GetWindowThreadProcessId(Handle, out var pid); return (int)pid; } }

    /// <summary>プロセス名 (拡張子なし)。取得できなければ空文字。</summary>
    public string ProcessName
    {
        get
        {
            try { using var p = System.Diagnostics.Process.GetProcessById(ProcessId); return p.ProcessName; }
            catch (Exception) { return ""; }
        }
    }

    /// <summary>実行ファイルのパス (ST_PATH)。権限不足などで取得できなければ空文字。</summary>
    public string ProcessPath
    {
        get
        {
            try { using var p = System.Diagnostics.Process.GetProcessById(ProcessId); return p.MainModule?.FileName ?? ""; }
            catch (Exception) { return ""; }
        }
    }

    /// <summary>親ウィンドウ。トップレベルなら null。</summary>
    public Window? Parent
    {
        get { var p = GetParent(Handle); return p == IntPtr.Zero ? null : new Window(p); }
    }

    // ---- 制御 (UWSC の CTRLWIN / ACW に相当) ----

    public void Close() => PostMessage(Handle, WM_CLOSE, 0, 0);

    /// <summary>プロセスごと強制終了する (CTRLWIN の CLOSE2)。</summary>
    public void Kill()
    {
        using var p = System.Diagnostics.Process.GetProcessById(ProcessId);
        p.Kill();
    }

    /// <summary>サイズだけ変更する。</summary>
    public Window Resize(int width, int height)
    {
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER);
        return this;
    }

    /// <summary>常に手前に表示するかを設定する (CTRLWIN の TOPMOST / NOTOPMOST)。</summary>
    public Window SetTopmost(bool topmost = true)
    {
        SetWindowPos(Handle, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        return this;
    }

    /// <summary>入力の有効・無効を切り替える。</summary>
    public Window SetEnabled(bool enabled = true) { EnableWindow(Handle, enabled); return this; }

    /// <summary>ウィンドウが閉じるまで待つ。timeout 秒 (負なら無制限) 内に閉じれば true。</summary>
    public bool WaitClose(double timeout = -1) => WaitUntil(() => !Exists, timeout);

    /// <summary>アクティブになるまで待つ。</summary>
    public bool WaitActive(double timeout = 5) => WaitUntil(() => IsActive, timeout);

    private static bool WaitUntil(Func<bool> condition, double timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (timeout >= 0 && sw.Elapsed.TotalSeconds >= timeout) return false;
            Thread.Sleep(100);
        }
        return true;
    }

    // ---- 子ウィンドウ・文字列 (UWSC の GETCTLHND / GETSTR / SENDSTR に相当) ----

    /// <summary>すべての子孫ウィンドウ (コントロール)。className を指定するとクラス名で絞り込む (部分一致)。</summary>
    public Window[] Children(string? className = null)
    {
        var list = new List<Window>();
        EnumChildWindows(Handle, (h, _) =>
        {
            if (string.IsNullOrEmpty(className) || GetClass(h).Contains(className, StringComparison.OrdinalIgnoreCase))
                list.Add(new Window(h));
            return true;
        }, IntPtr.Zero);
        return [.. list];
    }

    /// <summary>テキスト (ボタン名など, 部分一致) で子ウィンドウを探す (GETCTLHND)。無ければ null。</summary>
    public Window? FindChild(string text, string? className = null) =>
        Children(className).FirstOrDefault(c => GetControlText(c.Handle).Contains(text, StringComparison.OrdinalIgnoreCase));

    /// <summary>WM_GETTEXT で得られるテキスト (エディットなら内容、ボタンなら表示名)。</summary>
    public string Text => GetControlText(Handle);

    /// <summary>
    /// 指定クラス (既定: Edit 系) の n 番目 (1 から) の子コントロールのテキストを取得する (GETSTR)。
    /// 無ければ空文字。
    /// </summary>
    public string GetStr(int index = 1, string className = "Edit")
    {
        var c = Children(className);
        return index >= 1 && index <= c.Length ? GetControlText(c[index - 1].Handle) : "";
    }

    /// <summary>
    /// 指定クラス (既定: Edit 系) の n 番目 (1 から) の子コントロールへ文字列を設定する (SENDSTR)。
    /// append = true なら末尾に追記。index = 0 ならフォーカスのあるコントロールへ。成功すれば true。
    /// </summary>
    public bool SendStr(string text, int index = 1, bool append = false, string className = "Edit")
    {
        IntPtr target;
        if (index == 0) target = FocusedChild();
        else
        {
            var c = Children(className);
            if (index < 1 || index > c.Length) return false;
            target = c[index - 1].Handle;
        }
        if (append)
        {
            SendMessageTimeout(target, EM_SETSEL, -1, -1, SMTO_ABORTIFHUNG, 1000, out _);
            return SendMessageTimeout(target, EM_REPLACESEL, 0, text, SMTO_ABORTIFHUNG, 1000, out _) != 0;
        }
        return SendMessageTimeout(target, WM_SETTEXT, 0, text, SMTO_ABORTIFHUNG, 1000, out _) != 0;
    }
    public Window Minimize() { ShowWindow(Handle, SW_MINIMIZE); return this; }
    public Window Maximize() { ShowWindow(Handle, SW_MAXIMIZE); return this; }
    public Window Restore() { ShowWindow(Handle, SW_RESTORE); return this; }
    public Window Hide() { ShowWindow(Handle, SW_HIDE); return this; }
    public Window Show() { ShowWindow(Handle, SW_SHOW); return this; }

    /// <summary>ウィンドウ内の相対座標 (左上基準) をクリックする。</summary>
    public Window Click(Point point, MouseButton button = MouseButton.Left)
    {
        var b = Bounds;
        Input.Click(b.X + point.X, b.Y + point.Y, button);
        return this;
    }

    /// <inheritdoc cref="Click(Point, MouseButton)"/>
    public Window Click(int x, int y, MouseButton button = MouseButton.Left) => Click(new Point(x, y), button);

    /// <summary>アクティブにしてから文字列を入力する。</summary>
    public Window SendText(string text)
    {
        EnsureActive();
        Input.SendText(text);
        return this;
    }

    /// <summary>アクティブにしてからキーを押す。複数指定で同時押し (例: Keys.ControlKey, Keys.S)。</summary>
    public Window SendKeys(params Keys[] keys)
    {
        EnsureActive();
        Input.Hotkey(keys);
        return this;
    }

    // ---- バックグラウンド操作 (アクティブにせずメッセージを送る) ----

    /// <summary>ウィンドウ内の相対座標 (左上基準) にある子ウィンドウへクリックを送る。前面に出さない。</summary>
    public Window PostClick(Point point, MouseButton button = MouseButton.Left)
    {
        var screen = new POINT { X = X + point.X, Y = Y + point.Y };
        var target = DeepestChildAt(screen);
        var client = screen;
        ScreenToClient(target, ref client);
        var lp = MakeLParam(client.X, client.Y);
        var (down, up, mk) = button switch
        {
            MouseButton.Right => (WM_RBUTTONDOWN, WM_RBUTTONUP, MK_RBUTTON),
            MouseButton.Middle => (WM_MBUTTONDOWN, WM_MBUTTONUP, MK_MBUTTON),
            _ => (WM_LBUTTONDOWN, WM_LBUTTONUP, MK_LBUTTON),
        };
        PostMessage(target, WM_MOUSEMOVE, 0, lp);
        PostMessage(target, (uint)down, mk, lp);
        PostMessage(target, (uint)up, 0, lp);
        return this;
    }

    /// <inheritdoc cref="PostClick(Point, MouseButton)"/>
    public Window PostClick(int x, int y, MouseButton button = MouseButton.Left) => PostClick(new Point(x, y), button);

    /// <summary>フォーカスのある子ウィンドウへキーを送る。前面に出さない。</summary>
    public Window PostKey(Keys key)
    {
        var target = FocusedChild();
        PostMessage(target, WM_KEYDOWN, (IntPtr)(int)key, 1);
        PostMessage(target, WM_KEYUP, (IntPtr)(int)key, unchecked((IntPtr)0xC0000001));
        return this;
    }

    /// <summary>フォーカスのある子ウィンドウへ文字列を送る (WM_CHAR)。前面に出さない。</summary>
    public Window PostText(string text)
    {
        var target = FocusedChild();
        foreach (var ch in text.ReplaceLineEndings("\r")) PostMessage(target, WM_CHAR, ch, 1);
        return this;
    }

    private IntPtr DeepestChildAt(POINT screen)
    {
        var current = Handle;
        while (true)
        {
            var p = screen;
            ScreenToClient(current, ref p);
            var child = ChildWindowFromPointEx(current, p, CWP_SKIPINVISIBLE | CWP_SKIPTRANSPARENT);
            if (child == IntPtr.Zero || child == current) return current;
            current = child;
        }
    }

    private IntPtr FocusedChild()
    {
        var info = new GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
        var tid = GetWindowThreadProcessId(Handle, out _);
        return GetGUIThreadInfo(tid, ref info) && info.hwndFocus != IntPtr.Zero ? info.hwndFocus : Handle;
    }

    // ---- UI Automation (高レベル操作) ----

    /// <summary>
    /// 名前 (ボタンの表示文字列など) でコントロールを探して操作する。UWSC の CLKITEM に相当。
    /// 見つからなければ timeout 秒まで待ち、それでも無ければ false。
    /// </summary>
    public bool ClickItem(string name, double timeout = 3) => UiAutomation.ClickItem(Handle, name, timeout);

    /// <summary>ボタン類 (チェックボックス・ラジオ・トグル) の状態 (CHKBTN)。1: オン, 0: オフ, 2: 不定, -1: 見つからない。</summary>
    public int ChkBtn(string name) => UiAutomation.CheckState(Handle, name);

    /// <summary>キャプション文字やリスト項目などを列挙する (GETITEM)。</summary>
    public string[] GetItem(ItemKind kinds = ItemKind.All) => UiAutomation.GetItems(Handle, kinds);

    /// <summary>n 番目 (1 から) のリスト / コンボ / リストビュー / ツリーで選択中の項目 (GETSLCTLST)。</summary>
    public string[] GetSlctLst(int index = 1) => UiAutomation.GetSelected(Handle, index);

    /// <summary>n 番目 (1 から) のスライダー・スクロールバー・スピンの値 (GETSLIDER)。見つからなければ null。</summary>
    public SliderInfo? GetSlider(int index = 1) => UiAutomation.GetSlider(Handle, index);

    /// <summary>n 番目 (1 から) のスライダー類に値を設定する (SETSLIDER)。範囲外は丸める。成功すれば true。</summary>
    public bool SetSlider(double value, int index = 1) => UiAutomation.SetSlider(Handle, index, value);

    /// <summary>ショートカットキーを実行する (SCKEY)。SendKeys と同じ。</summary>
    public Window ScKey(params Keys[] keys) => SendKeys(keys);

    /// <summary>ウィンドウ内の相対座標にある文字 (コントロール名・値) を取得する (POSACC)。</summary>
    public string PosAcc(Point point) => UiAutomation.TextAt(X + point.X, Y + point.Y);

    /// <inheritdoc cref="PosAcc(Point)"/>
    public string PosAcc(int x, int y) => PosAcc(new Point(x, y));

    /// <summary>ウィンドウ部分の画面を画像として保存する (SAVEIMG)。形式は拡張子で決まる。</summary>
    public void SaveImg(string path) => ScreenImage.Save(path, Bounds);

    /// <summary>ウィンドウ内で画像を探す (CHKIMG)。見つかればウィンドウ相対の左上座標、無ければ null。</summary>
    public Point? ChkImg(string imagePath, int tolerance = 0)
    {
        var b = Bounds;
        return ScreenImage.Find(imagePath, b, tolerance) is { } p ? new Point(p.X - b.X, p.Y - b.Y) : null;
    }

    private void EnsureActive()
    {
        if (GetForegroundWindow() == Handle) return;
        Activate();
        Thread.Sleep(50);
    }

    public override string ToString() => $"Window(0x{Handle:X}, \"{Title}\")";
    public override bool Equals(object? obj) => obj is Window w && w.Handle == Handle;
    public override int GetHashCode() => Handle.GetHashCode();

    /// <summary>タイトル (部分一致) とクラス名 (部分一致) で可視ウィンドウを探す。</summary>
    internal static Window? Find(string title, string? className) => FindAll(title, className, firstOnly: true).FirstOrDefault();

    /// <summary>条件に合う可視トップレベルウィンドウを Z オーダー順 (手前から) に列挙する。</summary>
    internal static List<Window> FindAll(string? title, string? className, bool firstOnly = false)
    {
        var list = new List<Window>();
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            var t = GetTitle(h);
            if (string.IsNullOrEmpty(title) ? t.Length == 0 : !t.Contains(title, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(className) && !GetClass(h).Contains(className, StringComparison.OrdinalIgnoreCase)) return true;
            list.Add(new Window(h));
            return !firstOnly;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>スクリーン座標にあるウィンドウ。child = false ならトップレベル、true なら最も深い子ウィンドウ。</summary>
    internal static Window? FromPoint(int x, int y, bool child)
    {
        var h = WindowFromPoint(new POINT { X = x, Y = y });
        if (h == IntPtr.Zero) return null;
        return new Window(child ? h : GetAncestor(h, GA_ROOT));
    }
}
