namespace CsWSC.UI;

/// <summary>
/// ホットキーでスクリプトを実行したときに、マウスカーソルの近くへ出す表示です (押したのが効いたか分かるように)。
/// 実行を始めた表示はスクリプトの実行が始まるまで、実行しなかった表示は短い間だけ出します。
/// 操作中の窓からフォーカスは奪わず、クリックは下の窓へ素通りさせます。新しく出すと前の表示は消えます。
/// </summary>
internal sealed class LaunchNotice : Form {
	private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
	private const uint LWA_ALPHA = 0x2;
	private const int DisplayMilliseconds = 1200;
	/// <summary>実行の開始を知らせてこないまま (異常終了など) でも、この時間で消します。</summary>
	private const int StartTimeoutMilliseconds = 60_000;

	/// <summary>別プロセスで起動する CsWSC.exe に、実行の開始を知らせる名前付きイベントの名前を渡す環境変数です。</summary>
	public const string EnvironmentVariable = "CSWSC_STARTED_EVENT";

	[System.Runtime.InteropServices.DllImport( "user32.dll" )]
	private static extern bool SetLayeredWindowAttributes( IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags );

	private static LaunchNotice? _current;
	private readonly System.Windows.Forms.Timer _closeTimer = new();
	private readonly bool _waitingStart;
	private RegisteredWaitHandle? _startedWait;

	private LaunchNotice( string message, bool started ) {
		SuspendLayout();
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.Manual;
		AutoSize = true;
		AutoSizeMode = AutoSizeMode.GrowAndShrink;
		Padding = new Padding( 1 );
		Font = new Font( "Yu Gothic UI", 9F );
		_waitingStart = started;
		_closeTimer.Interval = started ? StartTimeoutMilliseconds : DisplayMilliseconds;
		BackColor = started ? Color.FromArgb( 0xE6, 0xF4, 0xEA ) : Color.FromArgb( 0xFD, 0xEC, 0xEA );
		Controls.Add( new Label {
			AutoSize = true,
			Padding = new Padding( 8, 5, 8, 5 ),
			Text = message,
			UseMnemonic = false,
		} );
		_closeTimer.Tick += ( _, _ ) => Close();

		// 余白は DPI 100% のときの値。DPI に合わせて拡大する
		AutoScaleDimensions = new SizeF( 96F, 96F );
		AutoScaleMode = AutoScaleMode.Dpi;
		ResumeLayout( false );
		PerformLayout();
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
		_startedWait?.Unregister( null );
		if( _current == this ) _current = null;
		base.OnFormClosed( e );
	}

	/// <summary>実行を始めたことを表示します (UI スレッドから呼ぶ)。<see cref="ScriptStarted"/> で消えます。</summary>
	public static void Started( string? path ) => Show( $"▶ {Name( path )}", true );

	/// <summary>
	/// 別プロセスで実行を始めたことを表示します (UI スレッドから呼ぶ)。
	/// 返す名前の名前付きイベントを <see cref="EnvironmentVariable"/> でそのプロセスへ渡すと、実行が始まったときに消えます。
	/// </summary>
	public static string StartedInOtherProcess( string? path ) {
		var notice = Show( $"▶ {Name( path )}", true );
		var name = $"CsWSC.Started.{Guid.NewGuid():N}";
		var started = new EventWaitHandle( false, EventResetMode.ManualReset, name );
		notice._startedWait = ThreadPool.RegisterWaitForSingleObject( started, ( _, _ ) => {
			started.Dispose();
			try { notice.BeginInvoke( notice.CloseIfWaiting ); } catch( InvalidOperationException ) { } // もう閉じている
		}, null, StartTimeoutMilliseconds, executeOnlyOnce: true );
		return name;
	}

	/// <summary>
	/// スクリプトの実行が始まった (または実行できずに終わった) ときに呼びます。どのスレッドから呼んでもかまいません。
	/// 実行を始めた表示を消し、ランチャーから別プロセスで起動されたときは起動元へ知らせます。
	/// </summary>
	public static void ScriptStarted() {
		if( _current is { } notice ) {
			try { notice.BeginInvoke( notice.CloseIfWaiting ); } catch( InvalidOperationException ) { }
		}
		if( Environment.GetEnvironmentVariable( EnvironmentVariable ) is { Length: > 0 } name ) {
			// 知らせるのは最初の 1 回だけ (この後の子プロセスにも引き継がない)
			Environment.SetEnvironmentVariable( EnvironmentVariable, null );
			try {
				using var started = EventWaitHandle.OpenExisting( name );
				started.Set();
			} catch( Exception e ) when( e is WaitHandleCannotBeOpenedException or UnauthorizedAccessException or IOException ) { }
		}
	}

	private void CloseIfWaiting() {
		if( _waitingStart && !IsDisposed ) Close();
	}

	/// <summary>実行しなかったことを理由とともに表示します (UI スレッドから呼ぶ)。</summary>
	public static void Skipped( string? path, string reason ) => Show( $"× {Name( path )} ({reason})", false );

	private static string Name( string? path ) => path == null ? "(記録)" : Path.GetFileName( path );

	private static LaunchNotice Show( string message, bool started ) {
		_current?.Close();
		var notice = _current = new LaunchNotice( message, started );
		// カーソルの右下に出し、画面からはみ出すなら内側へ寄せる
		var cursor = Cursor.Position;
		var area = Screen.FromPoint( cursor ).WorkingArea;
		var size = notice.PreferredSize;
		notice.Location = new Point(
			Math.Max( area.Left, Math.Min( cursor.X + notice.LogicalToDeviceUnits( 16 ), area.Right - size.Width ) ),
			Math.Max( area.Top, Math.Min( cursor.Y + notice.LogicalToDeviceUnits( 20 ), area.Bottom - size.Height ) ) );
		notice.Show();
		notice._closeTimer.Start();
		return notice;
	}
}
