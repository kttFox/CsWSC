using System.Runtime.InteropServices;

namespace CsWSC.UI;

/// <summary>
/// 起動引数 (関連付けのダブルクリックなど) で渡されたスクリプトを実行します。
/// メイン画面は出さず、Print の出力があれば PRINT 窓を出します。スクリプトが終わったらアプリも終了します。
/// </summary>
/// <remarks>停止は Pause キーと、設定の停止ホットキーで行えます。</remarks>
internal sealed class ScriptHost : ApplicationContext {
	private const string AppName = "CsWSC";

	private readonly AppSettings _settings = AppSettings.Load();
	private readonly PrintForm _printForm = new();
	private readonly CancellationTokenSource _cts = new();
	private readonly StopHotkeys _hotkeys;

	public ScriptHost( string source, string path ) {
		if( _settings.PrintBounds is { } pb && Screen.AllScreens.Any( s => s.WorkingArea.IntersectsWith( pb ) ) ) {
			_printForm.StartPosition = FormStartPosition.Manual;
			_printForm.Bounds = pb;
		}
		_ = _printForm.Handle; // スクリプトの出力を受けるスレッドから Invoke できるよう先に作っておく
		_hotkeys = new StopHotkeys( _settings.StopHotkey, _cts.Cancel );
		// コントロールを作った時点で WinForms の同期コンテキストが入るので、await 後は UI スレッドに戻る
		_ = RunAsync( source, path );
	}

	private async Task RunAsync( string source, string path ) {
		try {
			await ScriptProcess.RunAsync( source, path,
				_printForm.AppendLine, _printForm, _cts.Token );
		} catch( OperationCanceledException ) {
		} catch( Exception ex ) {
			// メイン画面が無いので、エラーはメッセージだけ出す (ScriptException の文言には行番号が入っている)
			MessageBox.Show( ex.Message, $"{AppName} - エラー", MessageBoxButtons.OK, MessageBoxIcon.Error );
		}
		// メイン画面で開いたときと同じく「最近使用したファイル」に載せる (起動を遅らせないよう終了時に保存する)
		_settings.AddRecent( path );
		if( _printForm.Visible )
			_settings.PrintBounds = _printForm.WindowState == FormWindowState.Normal ? _printForm.Bounds : _printForm.RestoreBounds;
		_settings.Save();
		ExitThread();
	}

	protected override void Dispose( bool disposing ) {
		if( disposing ) {
			_hotkeys.Dispose();
			_printForm.Dispose();
			_cts.Dispose();
		}
		base.Dispose( disposing );
	}

	/// <summary>停止用のグローバルホットキー (Pause キーと設定の停止キー) を受けるメッセージ専用窓です。</summary>
	/// <remarks>設定の停止キーが PassThrough ならキーを横取りしないフック方式で受けます。</remarks>
	private sealed class StopHotkeys : NativeWindow, IDisposable {
		private const int WM_HOTKEY = 0x0312;
		private const uint VK_PAUSE = 0x13;
		private const int HotkeyPause = 1, HotkeyStop = 2;
		private static readonly IntPtr HWND_MESSAGE = new( -3 );

		[DllImport( "user32.dll" )] private static extern bool RegisterHotKey( IntPtr hWnd, int id, uint mod, uint vk );
		[DllImport( "user32.dll" )] private static extern bool UnregisterHotKey( IntPtr hWnd, int id );

		private readonly Action _stop;
		private readonly KeyboardHookHotkeys _hotkeys = new();

		public StopHotkeys( Hotkey stopHotkey, Action stop ) {
			_stop = stop;
			CreateHandle( new CreateParams { Parent = HWND_MESSAGE } );
			RegisterHotKey( Handle, HotkeyPause, 0, VK_PAUSE );
			if( stopHotkey.PassThrough ) _hotkeys.Add( stopHotkey, stop );
			else if( stopHotkey.Enabled && stopHotkey.TryGetVirtualKey( out var vk ) )
				RegisterHotKey( Handle, HotkeyStop, stopHotkey.ModifierFlags | Hotkey.MOD_NOREPEAT, vk );
		}

		protected override void WndProc( ref Message m ) {
			if( m.Msg == WM_HOTKEY ) _stop();
			base.WndProc( ref m );
		}

		public void Dispose() {
			if( Handle == IntPtr.Zero ) return;
			UnregisterHotKey( Handle, HotkeyPause );
			UnregisterHotKey( Handle, HotkeyStop );
			_hotkeys.Dispose();
			DestroyHandle();
		}
	}
}
