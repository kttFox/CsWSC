using static CsWSC.NativeMethods;

namespace CsWSC;

public enum MouseButton { Left, Right, Middle }

public enum KeyAction { Click, Down, Up }

/// <summary>マウスカーソルの種類です (MUSCUR)。値は Win32 の IDC_* です。</summary>
public enum CursorKind {
	Other = 0, Hidden = -1,
	Arrow = 32512, IBeam = 32513, Wait = 32514, Cross = 32515, UpArrow = 32516,
	SizeNWSE = 32642, SizeNESW = 32643, SizeWE = 32644, SizeNS = 32645, SizeAll = 32646,
	No = 32648, Hand = 32649, AppStarting = 32650, Help = 32651,
}

/// <summary>SendInput によるマウス・キーボード入力です。</summary>
internal static class Input {
	public static void MoveMouse( int x, int y ) => SetCursorPos( x, y );

	public static void Button( MouseButton button, KeyAction action ) {
		var (down, up) = button switch {
			MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
			MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
			_ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
		};
		switch( action ) {
			case KeyAction.Down: Send( Mouse( down ) ); break;
			case KeyAction.Up: Send( Mouse( up ) ); break;
			default: Send( Mouse( down ), Mouse( up ) ); break;
		}
	}

	public static void Click( int x, int y, MouseButton button ) {
		SetCursorPos( x, y );
		Button( button, KeyAction.Click );
	}

	public static void Wheel( int notches ) => Send( Mouse( MOUSEEVENTF_WHEEL, -notches * 120 ) ); // 正で下方向

	public static void Key( Keys key, KeyAction action ) {
		var vks = ExpandModifiers( key ).ToArray();
		switch( action ) {
			case KeyAction.Down: foreach( var vk in vks ) { Send( KeyInput( vk, false ) ); } break;
			case KeyAction.Up: foreach( var vk in vks.Reverse() ) { Send( KeyInput( vk, true ) ); } break;
			default: ShortcutKey( [key] ); break;
		}
	}

	/// <summary>同時押しです。押した順の逆順で離します。</summary>
	public static void ShortcutKey( IEnumerable<Keys> keys ) {
		var vks = keys.SelectMany( ExpandModifiers ).ToArray();
		foreach( var vk in vks ) {
			Send( KeyInput( vk, false ) );
		}

		foreach( var vk in vks.Reverse() ) {
			Send( KeyInput( vk, true ) );
		}
	}

	/// <summary>Keys.Control | Keys.S のような組み合わせを、修飾キー → 本体キーの順に展開します。</summary>
	private static IEnumerable<ushort> ExpandModifiers( Keys key ) {
		var code = key & Keys.KeyCode;
		if( key.HasFlag( Keys.Control ) ) {
			yield return (ushort)Keys.ControlKey;
		}

		if( key.HasFlag( Keys.Shift ) ) {
			yield return (ushort)Keys.ShiftKey;
		}

		if( key.HasFlag( Keys.Alt ) ) {
			yield return (ushort)Keys.Menu;
		}

		// Keys.Control 単体や Keys.Control | Keys.Alt は修飾キーだけを押す
		if( code != Keys.None ) {
			yield return (ushort)code;
		}
	}

	// スキャンコードと拡張キーフラグも付ける
	// (付けないと矢印などがテンキー扱いになり、NumLock オン時の Shift+矢印で選択できない)
	private static INPUT KeyInput( ushort vk, bool up ) =>
		NativeMethods.Key( vk, ScanCode( vk ), ( up ? KEYEVENTF_KEYUP : KEYEVENTF_KEYDOWN ) | ( IsExtendedKey( vk ) ? KEYEVENTF_EXTENDEDKEY : 0 ) );

	internal static ushort ScanCode( ushort vk ) => (ushort)MapVirtualKey( vk, MAPVK_VK_TO_VSC );

	/// <summary>
	/// 拡張キー (テンキー側にも同じ働きのキーがあるもの・右側の修飾キーなど) かどうかです。
	/// テンキーの Enter は VK が Enter と同じで区別できないので、メインの Enter として扱います。
	/// </summary>
	internal static bool IsExtendedKey( ushort vk ) => (Keys)vk is
		Keys.Left or Keys.Right or Keys.Up or Keys.Down
		or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Insert or Keys.Delete
		or Keys.RControlKey or Keys.RMenu or Keys.LWin or Keys.RWin or Keys.Apps
		or Keys.Divide or Keys.NumLock or Keys.PrintScreen or Keys.Cancel;

	/// <summary>Unicode 入力で IME に依存せず文字列を送ります。</summary>
	public static void SendText( string text ) {
		foreach( var ch in text.ReplaceLineEndings( "\r" ) ) {
			Send( NativeMethods.Key( 0, ch, KEYEVENTF_UNICODE ), NativeMethods.Key( 0, ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP ) );
		}
	}

	// Keys.Control などの修飾フラグを実キーに読み替える
	internal static ushort ToVirtualKey( Keys key ) => key switch {
		Keys.Control => (ushort)Keys.ControlKey,
		Keys.Shift => (ushort)Keys.ShiftKey,
		Keys.Alt => (ushort)Keys.Menu,
		_ => (ushort)( key & Keys.KeyCode ),
	};
}
