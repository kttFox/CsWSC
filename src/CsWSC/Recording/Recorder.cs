using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static CsWSC.NativeMethods;

namespace CsWSC.Recording;

/// <summary>
/// 低レベルフック (WH_MOUSE_LL / WH_KEYBOARD_LL) でマウス・キーボード操作を記録する。
/// Start はメッセージループのあるスレッド (UI スレッド) から呼ぶこと。
/// </summary>
public sealed class Recorder : IDisposable
{
    private const int MoveThrottleMs = 30;

    private readonly List<RecordedEvent> _events = [];
    private readonly object _lock = new();
    private readonly Stopwatch _clock = new();
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    // フックのデリゲートは GC されないようフィールドで保持する
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyboardProc;
    private IntPtr _mouseHook, _keyboardHook;

    // 高レベル記録: クリック位置のコントロールを別スレッドで UI Automation により取得
    private BlockingCollection<MouseButtonEvent>? _uiaQueue;
    private Thread? _uiaThread;

    private RecordOptions _options = new(false, true, true, false);
    private long _lastMoveTime = -MoveThrottleMs;
    private readonly HashSet<MouseButton> _ignoredButtons = [];
    private readonly HashSet<MouseButton> _heldButtons = [];

    public Recorder()
    {
        _mouseProc = MouseHook;
        _keyboardProc = KeyboardHook;
    }

    public bool IsRecording => _mouseHook != IntPtr.Zero;

    public void Start(RecordOptions options)
    {
        if (IsRecording) throw new InvalidOperationException("すでに記録中です");
        _options = options;
        lock (_lock) _events.Clear();
        _ignoredButtons.Clear();
        _heldButtons.Clear();
        _lastMoveTime = -MoveThrottleMs;

        if (options.HighLevel)
        {
            _uiaQueue = [];
            _uiaThread = new Thread(UiaWorker) { IsBackground = true, Name = "CsWSC UIA" };
            _uiaThread.SetApartmentState(ApartmentState.MTA);
            _uiaThread.Start();
        }

        _clock.Restart();
        var hMod = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
        if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
        {
            var err = Marshal.GetLastWin32Error();
            Unhook();
            throw new InvalidOperationException($"フックを設定できませんでした (エラー {err})");
        }
    }

    /// <summary>
    /// 記録を終了してスクリプトを返す。操作が無ければ null。
    /// trailingKeys: 末尾から取り除くキー (記録終了に使ったホットキーなど)。
    /// </summary>
    public string? Stop(IEnumerable<int>? trailingKeys = null)
    {
        Unhook();
        _clock.Stop();

        if (_uiaQueue != null)
        {
            _uiaQueue.CompleteAdding();
            _uiaThread?.Join(TimeSpan.FromSeconds(5));
            _uiaQueue.Dispose();
            _uiaQueue = null;
            _uiaThread = null;
        }

        List<RecordedEvent> events;
        lock (_lock) events = [.. _events];

        var trim = trailingKeys?.ToHashSet() ?? [];
        while (events.Count > 0 && events[^1] is KeyEvent k && trim.Contains(k.VirtualKey)) events.RemoveAt(events.Count - 1);
        // 末尾のマウス移動だけの操作は不要
        while (events.Count > 0 && events[^1] is MouseMoveEvent) events.RemoveAt(events.Count - 1);

        return events.Any(e => e is not MouseMoveEvent) ? ScriptGenerator.Generate(events, _options) : null;
    }

    public void Dispose() => Unhook();

    private void Unhook()
    {
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        _mouseHook = _keyboardHook = IntPtr.Zero;
    }

    private void Add(RecordedEvent e)
    {
        lock (_lock) _events.Add(e);
    }

    // ---- フック (処理は最小限にすること。遅いと Windows にフックを外される) ----

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try { OnMouse((int)wParam, Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam)); }
            catch (Exception) { /* 記録の失敗で入力を止めない */ }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try { OnKey((int)wParam, Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam)); }
            catch (Exception) { }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void OnMouse(int msg, MSLLHOOKSTRUCT info)
    {
        if ((info.flags & LLMHF_INJECTED) != 0) return; // 再生中の入力などは記録しない
        long t = _clock.ElapsedMilliseconds;
        int x = info.pt.X, y = info.pt.Y;

        switch (msg)
        {
            case WM_MOUSEMOVE:
                // 余分なマウス移動は記録しない (ドラッグ中は除く)
                if (_options.SkipIdle && _heldButtons.Count == 0) return;
                if (t - _lastMoveTime < MoveThrottleMs) return;
                _lastMoveTime = t;
                Add(new MouseMoveEvent(t, x, y));
                return;

            case WM_MOUSEWHEEL:
            {
                var win = RootWindowAt(info.pt);
                if (win == null) return;
                int delta = (short)(info.mouseData >> 16);
                Add(new WheelEvent(t, -delta / 120, x, y, win));
                return;
            }
        }

        (MouseButton button, bool down)? b = msg switch
        {
            WM_LBUTTONDOWN => (MouseButton.Left, true),
            WM_LBUTTONUP => (MouseButton.Left, false),
            WM_RBUTTONDOWN => (MouseButton.Right, true),
            WM_RBUTTONUP => (MouseButton.Right, false),
            WM_MBUTTONDOWN => (MouseButton.Middle, true),
            WM_MBUTTONUP => (MouseButton.Middle, false),
            _ => null,
        };
        if (b is not var (button, down)) return;

        if (down)
        {
            var win = RootWindowAt(info.pt);
            // CsWSC 自身の窓 (記録ボタンなど) への操作は記録しない
            if (win == null) { _ignoredButtons.Add(button); return; }
            _heldButtons.Add(button);
            var ev = new MouseButtonEvent(t, button, true, x, y, win);
            Add(ev);
            _uiaQueue?.TryAdd(ev);
        }
        else
        {
            _heldButtons.Remove(button);
            if (_ignoredButtons.Remove(button)) return;
            Add(new MouseButtonEvent(t, button, false, x, y, RootWindowAt(info.pt, includeOwn: true)));
        }
    }

    private void OnKey(int msg, KBDLLHOOKSTRUCT info)
    {
        if ((info.flags & LLKHF_INJECTED) != 0) return;
        bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        if (!down && msg is not (WM_KEYUP or WM_SYSKEYUP)) return;

        var fg = GetForegroundWindow();
        GetWindowThreadProcessId(fg, out var pid);
        if (pid == _ownProcessId) return; // CsWSC 自身へのキー入力は記録しない
        Add(new KeyEvent(_clock.ElapsedMilliseconds, (int)info.vkCode, down, Describe(fg)));
    }

    private WindowInfo? RootWindowAt(POINT pt, bool includeOwn = false)
    {
        var root = GetAncestor(WindowFromPoint(pt), GA_ROOT);
        if (root == IntPtr.Zero) return null;
        GetWindowThreadProcessId(root, out var pid);
        if (!includeOwn && pid == _ownProcessId) return null;
        return Describe(root);
    }

    private static WindowInfo Describe(IntPtr h)
    {
        GetWindowRect(h, out var r);
        return new WindowInfo(h, GetTitle(h), GetClass(h), r.Left, r.Top);
    }

    private void UiaWorker()
    {
        foreach (var ev in _uiaQueue!.GetConsumingEnumerable())
            ev.Element = UiAutomation.ElementAt(ev.X, ev.Y);
    }
}
