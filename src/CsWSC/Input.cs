using static CsWSC.NativeMethods;

namespace CsWSC;

public enum MouseButton { Left, Right, Middle }

public enum KeyAction { Click, Down, Up }

/// <summary>MouseOrg で指定する座標の基準。</summary>
public enum MouseOrigin { Screen, Window, Client }

/// <summary>マウスカーソルの種類 (MUSCUR)。値は Win32 の IDC_* 。</summary>
public enum CursorKind
{
    Other = 0, Hidden = -1,
    Arrow = 32512, IBeam = 32513, Wait = 32514, Cross = 32515, UpArrow = 32516,
    SizeNWSE = 32642, SizeNESW = 32643, SizeWE = 32644, SizeNS = 32645, SizeAll = 32646,
    No = 32648, Hand = 32649, AppStarting = 32650, Help = 32651,
}

/// <summary>SendInput によるマウス・キーボード入力。</summary>
internal static class Input
{
    public static void MoveMouse(int x, int y) => SetCursorPos(x, y);

    public static void Button(MouseButton button, KeyAction action)
    {
        var (down, up) = button switch
        {
            MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
        };
        switch (action)
        {
            case KeyAction.Down: Send(Mouse(down)); break;
            case KeyAction.Up: Send(Mouse(up)); break;
            default: Send(Mouse(down), Mouse(up)); break;
        }
    }

    public static void Click(int x, int y, MouseButton button)
    {
        SetCursorPos(x, y);
        Button(button, KeyAction.Click);
    }

    public static void Wheel(int notches) => Send(Mouse(MOUSEEVENTF_WHEEL, -notches * 120)); // 正で下方向

    public static void Key(Keys key, KeyAction action)
    {
        var vk = ToVirtualKey(key);
        switch (action)
        {
            case KeyAction.Down: Send(NativeMethods.Key(vk, 0, 0)); break;
            case KeyAction.Up: Send(NativeMethods.Key(vk, 0, KEYEVENTF_KEYUP)); break;
            default: Send(NativeMethods.Key(vk, 0, 0), NativeMethods.Key(vk, 0, KEYEVENTF_KEYUP)); break;
        }
    }

    /// <summary>同時押し。押した順の逆順で離す。</summary>
    public static void Hotkey(Keys[] keys)
    {
        var vks = keys.Select(ToVirtualKey).ToArray();
        foreach (var vk in vks) Send(NativeMethods.Key(vk, 0, 0));
        foreach (var vk in vks.Reverse()) Send(NativeMethods.Key(vk, 0, KEYEVENTF_KEYUP));
    }

    /// <summary>Unicode 入力で IME に依存せず文字列を送る。</summary>
    public static void SendText(string text)
    {
        foreach (var ch in text.ReplaceLineEndings("\r"))
            Send(NativeMethods.Key(0, ch, KEYEVENTF_UNICODE), NativeMethods.Key(0, ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
    }

    // Keys.Control などの修飾フラグを実キーに読み替える
    private static ushort ToVirtualKey(Keys key) => key switch
    {
        Keys.Control => (ushort)Keys.ControlKey,
        Keys.Shift => (ushort)Keys.ShiftKey,
        Keys.Alt => (ushort)Keys.Menu,
        _ => (ushort)(key & Keys.KeyCode),
    };
}
