namespace CsWSC.UI;

/// <summary>
/// スクリプトのコンパイルを待つ間に出す「コンパイル中」の表示です (初回はコンパイルに時間がかかるため)。
/// すぐ終わるときにちらつかないよう、少し待ってから出します (待ち時間は showTimer の Interval。温めた後のコンパイルは約 0.1 秒なので、それより長くする)。
/// 操作中の窓からフォーカスは奪いません。
/// </summary>
internal sealed partial class CompilingNotice : Form {
	private const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

	public CompilingNotice() {
		InitializeComponent();
	}

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams {
		get {
			var cp = base.CreateParams;
			cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
			return cp;
		}
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
