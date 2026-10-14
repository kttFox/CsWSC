using System.Runtime.InteropServices;

namespace CsWSC.UI;

/// <summary>キー (またはマウスのサイドボタン) を実際に押してホットキーを設定する入力欄です。Esc (修飾キーなし) で「なし」に戻します。</summary>
public sealed class HotkeyBox : TextBox {
	private const int WM_KEYUP = 0x0101, WM_CHAR = 0x0102, WM_SYSKEYUP = 0x0105, WM_SYSCHAR = 0x0106;
	private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;

	[DllImport( "user32.dll" )]
	private static extern short GetKeyState( int nVirtKey );

	private Hotkey _hotkey = new( "", "なし" );

	public HotkeyBox() {
		ReadOnly = true;
		BackColor = SystemColors.Window;
		Cursor = Cursors.Default;
		ShortcutsEnabled = false;
		Text = _hotkey.ToString();
	}

	[System.ComponentModel.DesignerSerializationVisibility( System.ComponentModel.DesignerSerializationVisibility.Hidden )]
	[System.ComponentModel.Browsable( false )]
	public Hotkey Hotkey {
		get => _hotkey;
		set {
			_hotkey = value;
			Text = value.ToString();
		}
	}

	private static bool IsWinDown => ( GetKeyState( VK_LWIN ) & 0x8000 ) != 0 || ( GetKeyState( VK_RWIN ) & 0x8000 ) != 0;

	/// <summary>押されている修飾キーを Hotkey.Modifier の形式 (CTRL+ALT+SHIFT+WIN の順) にします。</summary>
	private static string CurrentModifier() {
		var mods = ModifierKeys;
		var parts = new List<string>();
		if( mods.HasFlag( Keys.Control ) ) parts.Add( "CTRL" );
		if( mods.HasFlag( Keys.Alt ) ) parts.Add( "ALT" );
		if( mods.HasFlag( Keys.Shift ) ) parts.Add( "SHIFT" );
		if( IsWinDown ) parts.Add( "WIN" );
		return string.Join( "+", parts );
	}

	private static string? KeyName( Keys key ) => key switch {
		Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin
			or Keys.LShiftKey or Keys.RShiftKey or Keys.LControlKey or Keys.RControlKey or Keys.LMenu or Keys.RMenu
			or Keys.ProcessKey or Keys.None => null,
		>= Keys.D0 and <= Keys.D9 => ( (char)( '0' + ( key - Keys.D0 ) ) ).ToString(),
		// PageUp/PageDown は Prior/Next と同じ値で ToString() の結果が定まらないので、名前を固定する
		Keys.PageUp => "PAGEUP",
		Keys.PageDown => "PAGEDOWN",
		_ => key.ToString().ToUpperInvariant(),
	};

	// ProcessCmdKey で横取りし、Enter/Esc/Alt+F4 などでダイアログが反応しないようにする
	protected override bool ProcessCmdKey( ref Message msg, Keys keyData ) {
		var key = keyData & Keys.KeyCode;
		var modifier = CurrentModifier();

		if( key == Keys.Escape && modifier == "" ) {
			Hotkey = new( "", "なし" );
			return true;
		}

		var name = KeyName( key );
		if( name == null ) {
			// 修飾キーだけ押されている間は途中経過を表示
			Text = modifier == "" ? _hotkey.ToString() : $"{modifier} + …";
			return true;
		}
		// 文字・数字・スペースを修飾キーなしで登録すると入力のたびに反応してしまうので受け付けない
		if( modifier == "" && ( key is >= Keys.A and <= Keys.Z or >= Keys.D0 and <= Keys.D9 or Keys.Space ) ) return true;

		var hotkey = new Hotkey( modifier, name );
		if( hotkey.Enabled ) Hotkey = hotkey;
		return true;
	}

	// マウスのサイドボタン (戻る / 進む) も、修飾キーと組み合わせて設定できる
	protected override void OnMouseDown( MouseEventArgs e ) {
		base.OnMouseDown( e );
		var name = e.Button switch {
			MouseButtons.XButton1 => "XBUTTON1",
			MouseButtons.XButton2 => "XBUTTON2",
			_ => null,
		};
		if( name != null ) Hotkey = new( CurrentModifier(), name );
	}

	protected override void WndProc( ref Message m ) {
		switch( m.Msg ) {
			case WM_CHAR or WM_SYSCHAR:
				return; // ビープ音・ニーモニック処理を抑止
			case WM_KEYUP or WM_SYSKEYUP:
				// 修飾キーだけ押して離したら確定済みの表示に戻す
				if( CurrentModifier() == "" ) Text = _hotkey.ToString();
				return; // Alt を離したときのシステムメニュー起動を抑止
		}
		base.WndProc( ref m );
	}

	protected override void OnLostFocus( EventArgs e ) {
		Text = _hotkey.ToString();
		base.OnLostFocus( e );
	}
}
