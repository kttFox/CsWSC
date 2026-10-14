using System.Runtime.InteropServices;

namespace CsWSC.UI;

/// <summary>
/// 低レベルキーボードフックによるホットキー (フック方式。マウスのサイドボタンは低レベルマウスフックで受ける) です。
/// RegisterHotKey と違ってキーを横取りしないので、押したキーは前面のアプリにもそのまま届きます。
/// </summary>
/// <remarks>フックを設定したスレッド (UI スレッド) でメッセージループが回っている必要があります。</remarks>
internal sealed class KeyboardHookHotkeys : IDisposable {
	private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
	private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
	private const int WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C;
	private const uint LLKHF_INJECTED = 0x10, LLMHF_INJECTED = 0x1;
	private const uint VK_XBUTTON1 = 0x05, VK_XBUTTON2 = 0x06;
	private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

	[StructLayout( LayoutKind.Sequential )]
	private struct KBDLLHOOKSTRUCT {
		public uint vkCode, scanCode, flags, time;
		public IntPtr dwExtraInfo;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct MSLLHOOKSTRUCT {
		public int x, y;
		public uint mouseData, flags, time;
		public IntPtr dwExtraInfo;
	}

	private delegate IntPtr HookProc( int nCode, IntPtr wParam, IntPtr lParam );

	[DllImport( "user32.dll", SetLastError = true )] private static extern IntPtr SetWindowsHookEx( int idHook, HookProc fn, IntPtr hMod, uint threadId );
	[DllImport( "user32.dll" )] private static extern bool UnhookWindowsHookEx( IntPtr hhk );
	[DllImport( "user32.dll" )] private static extern IntPtr CallNextHookEx( IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam );
	[DllImport( "user32.dll" )] private static extern short GetAsyncKeyState( int vKey );
	[DllImport( "kernel32.dll" )] private static extern IntPtr GetModuleHandle( string? name );

	private readonly HookProc _proc, _mouseProc; // GC に回収されないよう保持する
	private readonly SynchronizationContext _context;
	private readonly List<(uint Vk, uint Modifiers, Action Action)> _entries = [];
	/// <summary>押しっぱなしのキー (キーリピートで何度も発火させないため) です。</summary>
	private readonly HashSet<uint> _down = [];
	private IntPtr _hook, _mouseHook;

	public KeyboardHookHotkeys() {
		_proc = HookCallback;
		_mouseProc = MouseHookCallback;
		_context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
	}

	/// <summary>ホットキーを追加します。無効なホットキー、またはフックを設定できなければ false です。</summary>
	public bool Add( Hotkey hotkey, Action action ) {
		if( !hotkey.Enabled || !hotkey.TryGetVirtualKey( out var vk ) ) return false;
		if( vk is VK_XBUTTON1 or VK_XBUTTON2 ) {
			if( _mouseHook == IntPtr.Zero ) {
				_mouseHook = SetWindowsHookEx( WH_MOUSE_LL, _mouseProc, GetModuleHandle( null ), 0 );
				if( _mouseHook == IntPtr.Zero ) return false;
			}
		} else if( _hook == IntPtr.Zero ) {
			_hook = SetWindowsHookEx( WH_KEYBOARD_LL, _proc, GetModuleHandle( null ), 0 );
			if( _hook == IntPtr.Zero ) return false;
		}
		_entries.Add( (vk, hotkey.ModifierFlags, action) );
		return true;
	}

	/// <summary>すべてのホットキーを解除します。</summary>
	public void Clear() {
		_entries.Clear();
		_down.Clear();
		if( _hook != IntPtr.Zero ) {
			UnhookWindowsHookEx( _hook );
			_hook = IntPtr.Zero;
		}
		if( _mouseHook != IntPtr.Zero ) {
			UnhookWindowsHookEx( _mouseHook );
			_mouseHook = IntPtr.Zero;
		}
	}

	public void Dispose() => Clear();

	private static bool IsDown( int vk ) => ( GetAsyncKeyState( vk ) & 0x8000 ) != 0;

	private static uint CurrentModifiers() {
		uint mods = 0;
		if( IsDown( VK_MENU ) ) mods |= Hotkey.MOD_ALT;
		if( IsDown( VK_CONTROL ) ) mods |= Hotkey.MOD_CONTROL;
		if( IsDown( VK_SHIFT ) ) mods |= Hotkey.MOD_SHIFT;
		if( IsDown( VK_LWIN ) || IsDown( VK_RWIN ) ) mods |= Hotkey.MOD_WIN;
		return mods;
	}

	private IntPtr HookCallback( int nCode, IntPtr wParam, IntPtr lParam ) {
		if( nCode >= 0 ) {
			var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>( lParam );
			int msg = (int)wParam;
			if( msg is WM_KEYUP or WM_SYSKEYUP ) {
				_down.Remove( data.vkCode );
			} else if( msg is WM_KEYDOWN or WM_SYSKEYDOWN
				&& ( data.flags & LLKHF_INJECTED ) == 0 // SendInput などで送られたキー (スクリプトが送ったキーを含む) には反応しない
				&& _down.Add( data.vkCode ) ) {
				Fire( data.vkCode );
			}
		}
		// 横取りせず次へ渡す (前面のアプリにもキーが届く)
		return CallNextHookEx( _hook, nCode, wParam, lParam );
	}

	private IntPtr MouseHookCallback( int nCode, IntPtr wParam, IntPtr lParam ) {
		int msg = (int)wParam;
		if( nCode >= 0 && msg is WM_XBUTTONDOWN or WM_XBUTTONUP ) {
			var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>( lParam );
			// mouseData の上位ワードが 1 なら XBUTTON1 (戻る)、2 なら XBUTTON2 (進む)
			uint vk = ( data.mouseData >> 16 ) == 1 ? VK_XBUTTON1 : VK_XBUTTON2;
			if( msg == WM_XBUTTONUP ) _down.Remove( vk );
			else if( ( data.flags & LLMHF_INJECTED ) == 0 && _down.Add( vk ) ) Fire( vk );
		}
		// 横取りせず次へ渡す (前面のアプリにもクリックが届く)
		return CallNextHookEx( _mouseHook, nCode, wParam, lParam );
	}

	private void Fire( uint pressed ) {
		var mods = CurrentModifiers();
		foreach( var (vk, modifiers, action) in _entries ) {
			// フック内で時間をかけると入力が遅れるので、処理は後回しにする
			if( vk == pressed && modifiers == mods ) _context.Post( _ => action(), null );
		}
	}
}
