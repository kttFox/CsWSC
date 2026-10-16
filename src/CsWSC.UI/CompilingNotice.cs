namespace CsWSC.UI;

/// <summary>
/// スクリプトのコンパイルを待つ間に出す「コンパイル中」の表示です (初回はコンパイルに時間がかかるため)。
/// すぐ終わるときにちらつかないよう、少し待ってから出します (待ち時間は showTimer の Interval。温めた後のコンパイルは約 0.1 秒なので、それより長くする)。
/// 操作中の窓からフォーカスは奪わず、クリックは下の窓へ素通りさせます。
/// </summary>
internal sealed partial class CompilingNotice : Form {
	private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
	private const uint LWA_ALPHA = 0x2;

	[System.Runtime.InteropServices.DllImport( "user32.dll" )]
	private static extern bool SetLayeredWindowAttributes( IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags );

	public CompilingNotice() {
		InitializeComponent();
	}

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams {
		get {
			var cp = base.CreateParams;
			// レイヤードウィンドウに WS_EX_TRANSPARENT を付けるとマウス入力が下の窓へ届く
			cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
			return cp;
		}
	}

	protected override void OnHandleCreated( EventArgs e ) {
		base.OnHandleCreated( e );
		// レイヤードウィンドウは属性を設定しないと描画されないので、不透明で設定する
		SetLayeredWindowAttributes( Handle, 0, 255, LWA_ALPHA );
	}

	protected override void OnPaint( PaintEventArgs e ) {
		base.OnPaint( e );
		// ラベルは Padding の 1 ピクセル内側にあるので、外周の枠は隠れない
		ControlPaint.DrawBorder( e.Graphics, ClientRectangle, SystemColors.WindowFrame, ButtonBorderStyle.Solid );
	}

	/// <summary>表示を始めます (UI スレッドから呼ぶ)。Dispose で消えます。</summary>
	public static CompilingNotice Begin() {
		var notice = new CompilingNotice();
		notice.showTimer.Start();
		return notice;
	}

	private void showTimer_Tick( object? sender, EventArgs e ) {
		showTimer.Stop();
		var area = Screen.FromPoint( Cursor.Position ).WorkingArea;
		Location = new Point( area.Left + ( area.Width - PreferredSize.Width ) / 2, area.Top + ( area.Height - PreferredSize.Height ) / 2 );
		Show();
	}
}
