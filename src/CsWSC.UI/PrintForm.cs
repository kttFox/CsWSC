
namespace CsWSC.UI;

/// <summary>Print() の出力窓です。閉じても破棄せず隠すだけです。</summary>
public partial class PrintForm : Form, ILogWindow {
	// LogPrint(false) で隠されたら、Print しても自動表示しない
	private bool _suppressed;

	public PrintForm() {
		InitializeComponent();
	}

	/// <summary>表示してもアクティブにしません (スクリプトが操作中の窓からフォーカスを奪わないため)。</summary>
	protected override bool ShowWithoutActivation => true;

	[System.Runtime.InteropServices.DllImport( "user32.dll" )]
	private static extern bool SetWindowPos( IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags );

	private static readonly IntPtr HWND_TOPMOST = new( -1 ), HWND_NOTOPMOST = new( -2 );

	/// <summary>アクティブにせず Z オーダーを変えます。</summary>
	private void SetZOrder( IntPtr insertAfter ) {
		const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
		SetWindowPos( Handle, insertAfter, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE );
	}

	// UWSC と同様、このアプリがアクティブな間だけ最前面にし、他のアプリへ切り替わったら解除する
	// (TopMost プロパティはアクティブ化を伴うため使わない)
	protected override void WndProc( ref Message m ) {
		const int WM_ACTIVATEAPP = 0x001C;
		if( m.Msg == WM_ACTIVATEAPP && IsHandleCreated ) {
			SetZOrder( m.WParam != IntPtr.Zero ? HWND_TOPMOST : HWND_NOTOPMOST );
		}
		base.WndProc( ref m );
	}

	// 表示した時点で既にこのアプリがアクティブなら WM_ACTIVATEAPP は来ないので、ここで合わせる
	protected override void OnVisibleChanged( EventArgs e ) {
		base.OnVisibleChanged( e );
		if( Visible && ActiveForm != null ) SetZOrder( HWND_TOPMOST );
	}

	public void Clear() => OnUi( outputTextBox.Clear );

	/// <summary>スクリプト開始時の初期化です。内容を消し、LogPrint の非表示指定も解除します。</summary>
	public void Reset() {
		_suppressed = false;
		outputTextBox.Clear();
	}

	public void AppendLine( string text ) {
		if( !Visible && !_suppressed ) Show();
		outputTextBox.AppendText( text + Environment.NewLine );
	}

	void ILogWindow.SetVisible( bool visible ) => OnUi( () => {
		_suppressed = !visible;
		if( visible && !Visible ) Show();
		else if( !visible && Visible ) Hide();
	} );

	void ILogWindow.SetBounds( int? x, int? y, int? width, int? height ) => OnUi( () =>
		SetBounds( x ?? Left, y ?? Top, width ?? Width, height ?? Height ) );

	// 通常は UI スレッドから呼ばれるが、UI 以外のスレッドから呼ばれても安全なよう UI スレッドで同期実行する
	private void OnUi( Action action ) {
		if( InvokeRequired ) Invoke( action );
		else action();
	}

	protected override void OnFormClosing( FormClosingEventArgs e ) {
		if( e.CloseReason == CloseReason.UserClosing ) {
			e.Cancel = true;
			Hide();
		}
		base.OnFormClosing( e );
	}
}
