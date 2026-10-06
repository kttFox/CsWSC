using System.Runtime.InteropServices;
using static CsWSC.NativeMethods;

namespace CsWSC;

/// <summary><see cref="ScriptGlobals.LockHardEx"/> でロックする入力の種類。</summary>
[Flags]
public enum LockMode
{
    /// <summary>ロックを解除する。</summary>
    None = 0,
    /// <summary>キーボード入力をロックする。</summary>
    Keyboard = 1,
    /// <summary>マウス入力をロックする。</summary>
    Mouse = 2,
    /// <summary>キーボードとマウスの両方をロックする。</summary>
    All = Keyboard | Mouse,
}

/// <summary>
/// LOCKHARD / LOCKHARDEX。低レベルフックでユーザーの入力を捨てる。
/// スクリプトが SendInput で送る入力 (INJECTED) と、非常停止用の Pause キーは通す。
/// </summary>
internal sealed class InputLock : IDisposable
{
    private const int VK_PAUSE = 0x13;
    private const int WM_QUIT = 0x12;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, int msg, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private readonly object _lock = new();
    private readonly HookProc _mouseProc, _keyboardProc;
    private System.Threading.Thread? _thread;
    private uint _threadId;
    private volatile LockMode _mode;
    private volatile IntPtr _target; // 0 なら全ウィンドウ

    public InputLock()
    {
        _mouseProc = MouseHook;
        _keyboardProc = KeyboardHook;
    }

    /// <summary>ロック状態を設定する。mode が None ならフックを外す。</summary>
    public void Set(LockMode mode, IntPtr target)
    {
        lock (_lock)
        {
            _target = target;
            _mode = mode;
            if (mode == LockMode.None) Stop();
            else Start();
        }
    }

    public void Dispose() => Set(LockMode.None, IntPtr.Zero);

    private void Start()
    {
        if (_thread != null) return;
        using var ready = new ManualResetEventSlim();
        string? error = null;
        // 低レベルフックはメッセージループのあるスレッドで設定する必要がある
        _thread = new System.Threading.Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            var hMod = GetModuleHandle(null);
            var mouse = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
            var keyboard = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            if (mouse == IntPtr.Zero || keyboard == IntPtr.Zero) error = $"フックを設定できませんでした (エラー {Marshal.GetLastWin32Error()})";
            ready.Set();
            if (error == null)
                while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0) { }
            if (mouse != IntPtr.Zero) UnhookWindowsHookEx(mouse);
            if (keyboard != IntPtr.Zero) UnhookWindowsHookEx(keyboard);
        }) { IsBackground = true, Name = "CsWSC LOCKHARD" };
        _thread.Start();
        ready.Wait();
        if (error != null)
        {
            _thread.Join();
            _thread = null;
            throw new InvalidOperationException(error);
        }
    }

    private void Stop()
    {
        if (_thread == null) return;
        PostThreadMessage(_threadId, WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private bool IsTarget(IntPtr hwnd) =>
        _target == IntPtr.Zero || (hwnd != IntPtr.Zero && GetAncestor(hwnd, GA_ROOT) == _target);

    // ---- フック (処理は最小限に) ----

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (_mode & LockMode.Mouse) != 0)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((info.flags & LLMHF_INJECTED) == 0 && IsTarget(WindowFromPoint(info.pt))) return 1;
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (_mode & LockMode.Keyboard) != 0)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if ((info.flags & LLKHF_INJECTED) == 0 && info.vkCode != VK_PAUSE && IsTarget(GetForegroundWindow())) return 1;
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
