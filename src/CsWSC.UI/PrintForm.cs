
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

	// スクリプト実行中か
	private bool _running;

	/// <summary>
	/// スクリプトの実行状態を設定します。UWSC と同様、実行中は他のアプリがアクティブでも最前面に表示します。
	/// </summary>
	public void SetRunning( bool running ) {
		_running = running;
		UpdateZOrder();
	}

	// 実行中は常に最前面、それ以外はこのアプリがアクティブな間だけ最前面にする
	// (TopMost プロパティはアクティブ化を伴うため使わない)
	private void UpdateZOrder() {
		if( !IsHandleCreated ) return;
		SetZOrder( _running || ActiveForm != null ? HWND_TOPMOST : HWND_NOTOPMOST );
	}

	protected override void WndProc( ref Message m ) {
		const int WM_ACTIVATEAPP = 0x001C;
		if( m.Msg == WM_ACTIVATEAPP && IsHandleCreated ) {
			SetZOrder( _running || m.WParam != IntPtr.Zero ? HWND_TOPMOST : HWND_NOTOPMOST );
		}
		base.WndProc( ref m );
	}

	// 表示した時点で既にこのアプリがアクティブなら WM_ACTIVATEAPP は来ないので、ここで合わせる
	protected override void OnVisibleChanged( EventArgs e ) {
		base.OnVisibleChanged( e );
		if( Visible ) UpdateZOrder();
	}

	public void Clear() => OnUi( ClearText );

	/// <summary>スクリプト開始時の初期化です。内容を消し、LogPrint の非表示指定も解除します。</summary>
	public void Reset() {
		_suppressed = false;
		ClearText();
	}

	public void AppendLine( string text ) {
		if( !Visible && !_suppressed ) Show();
		foreach( var line in text.Split( '\n' ) ) {
			_maxLineWidth = Math.Max( _maxLineWidth, MeasureLine( line.TrimEnd( '\r' ) ) );
		}
		outputTextBox.AppendText( text + Environment.NewLine );
		UpdateHorizontalScrollBar();
	}

	// これまでに出力した行のうち最も長い行の幅 (ピクセル)
	private int _maxLineWidth;

	private void ClearText() {
		outputTextBox.Clear();
		_maxLineWidth = 0;
		UpdateHorizontalScrollBar();
	}

	private int MeasureLine( string line ) {
		const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.ExpandTabs | TextFormatFlags.NoPadding;
		return line.Length == 0 ? 0 : TextRenderer.MeasureText( line, outputTextBox.Font, Size.Empty, flags ).Width;
	}

	protected override void OnResize( EventArgs e ) {
		base.OnResize( e );
		if( outputTextBox != null ) UpdateHorizontalScrollBar();
	}

	// TextBox は横スクロールバーを自動で出し入れしないので、最長行が収まらないときだけ表示する
	// (縦は常に表示しておくことで、横の有無でクライアント幅が変わらず判定が振動しない)
	private void UpdateHorizontalScrollBar() {
		const int margin = 8; // エディットコントロールの左右余白とキャレットの分
		var scrollBars = _maxLineWidth + margin > outputTextBox.ClientSize.Width ? ScrollBars.Both : ScrollBars.Vertical;
		if( outputTextBox.ScrollBars == scrollBars ) return;
		// ScrollBars を変えるとハンドルが作り直されスクロール位置が先頭に戻るので、キャレット位置へ戻す
		int caret = outputTextBox.SelectionStart;
		outputTextBox.ScrollBars = scrollBars;
		outputTextBox.SelectionStart = caret;
		outputTextBox.ScrollToCaret();
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
