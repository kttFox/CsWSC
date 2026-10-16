namespace CsWSC.UI;

/// <summary>
/// ホットキーでスクリプトを実行したときに、マウスカーソルの近くへ短い間だけ出す表示です (押したのが効いたか分かるように)。
/// 操作中の窓からフォーカスは奪わず、クリックは下の窓へ素通りさせます。新しく出すと前の表示は消えます。
/// </summary>
internal sealed class LaunchNotice : Form {
	private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
	private const uint LWA_ALPHA = 0x2;
	private const int DisplayMilliseconds = 1200;

	[System.Runtime.InteropServices.DllImport( "user32.dll" )]
	private static extern bool SetLayeredWindowAttributes( IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags );

	private static LaunchNotice? _current;
	private readonly System.Windows.Forms.Timer _closeTimer = new() { Interval = DisplayMilliseconds };

	private LaunchNotice( string message, bool started ) {
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.Manual;
		AutoSize = true;
		AutoSizeMode = AutoSizeMode.GrowAndShrink;
		Padding = new Padding( 1 );
		Font = new Font( "Yu Gothic UI", 9F );
		BackColor = started ? Color.FromArgb( 0xE6, 0xF4, 0xEA ) : Color.FromArgb( 0xFD, 0xEC, 0xEA );
		Controls.Add( new Label {
			AutoSize = true,
			Padding = new Padding( 8, 5, 8, 5 ),
			Text = message,
			UseMnemonic = false,
		} );
		_closeTimer.Tick += ( _, _ ) => Close();
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
		ControlPaint.DrawBorder( e.Graphics, ClientRectangle, SystemColors.WindowFrame, ButtonBorderStyle.Solid );
	}

	protected override void OnFormClosed( FormClosedEventArgs e ) {
		_closeTimer.Dispose();
		if( _current == this ) _current = null;
		base.OnFormClosed( e );
	}

	/// <summary>実行を始めたことを表示します (UI スレッドから呼ぶ)。</summary>
	public static void Started( string? path ) => Show( $"▶ {Name( path )}", true );

	/// <summary>実行しなかったことを理由とともに表示します (UI スレッドから呼ぶ)。</summary>
	public static void Skipped( string? path, string reason ) => Show( $"× {Name( path )} ({reason})", false );

	private static string Name( string? path ) => path == null ? "(記録)" : Path.GetFileName( path );

	private static void Show( string message, bool started ) {
		_current?.Close();
		var notice = _current = new LaunchNotice( message, started );
		// カーソルの右下に出し、画面からはみ出すなら内側へ寄せる
		var cursor = Cursor.Position;
		var area = Screen.FromPoint( cursor ).WorkingArea;
		var size = notice.PreferredSize;
		notice.Location = new Point(
			Math.Max( area.Left, Math.Min( cursor.X + 16, area.Right - size.Width ) ),
			Math.Max( area.Top, Math.Min( cursor.Y + 20, area.Bottom - size.Height ) ) );
		notice.Show();
		notice._closeTimer.Start();
	}
}
