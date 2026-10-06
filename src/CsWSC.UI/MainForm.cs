using System.Runtime.InteropServices;
using System.Xml.Linq;
using CsWSC.Recording;

namespace CsWSC.UI;

/// <summary>UWSC と同じツールバー型のメイン窓 (読込み / 保存 / 再生 / 記録 / 設定)。</summary>
public partial class MainForm : Form {
	private const string AppName = "CsWSC";
	private const int WM_HOTKEY = 0x0312;
	private const uint VK_PAUSE = 0x13;

	// グローバルホットキーの ID
	private const int HotkeyPause = 1, HotkeyPlay = 2, HotkeyStop = 3, HotkeyRecord = 4, HotkeyTray = 5;

	private readonly string? _startupScript;
	private readonly AppSettings _settings = AppSettings.Load();
	private readonly PrintForm _printForm = new();
	private readonly List<EditorForm> _editors = [];
	private CancellationTokenSource? _cts;
	private string? _scriptPath;
	private readonly Recorder _recorder = new();
	/// <summary>記録した未保存のスクリプト (保存すると _scriptPath に移る)。</summary>
	private string? _recordedSource;
	/// <summary>メニューの「終了」による終了 (閉じるボタンでトレイ収納する設定でも終了させる)。</summary>
	private bool _exiting;
	/// <summary>再生・記録のために自動でトレイに収納した (終わったら元に戻す)。</summary>
	private bool _autoTrayed;

	public MainForm() : this( null ) { }

	public MainForm( string? startupScript ) {
		InitializeComponent();
		_startupScript = startupScript;
	}

	protected override void OnLoad( EventArgs e ) {
		base.OnLoad( e );
		if( DesignMode ) return;

		_printForm.Owner = this;
		_ = _printForm.Handle; // スクリプトのスレッドから Invoke できるよう先に作っておく
		TopMost = topMostMenuItem.Checked = _settings.TopMost;
		if( _settings.Location is { } loc && Screen.AllScreens.Any( s => s.WorkingArea.Contains( loc ) ) ) {
			StartPosition = FormStartPosition.Manual;
			Location = loc;
		}
		// 前回読み込んだスクリプトを復元
		SetScript( _startupScript ?? _settings.RecentFiles.FirstOrDefault( File.Exists ) );
	}

	protected override async void OnShown( EventArgs e ) {
		base.OnShown( e );
		if( _settings.TrayOnStart ) MinimizeToTray();
		if( _startupScript != null && _scriptPath != null ) await PlayAsync();
	}

	// ---- ツールバー ----

	private void LoadButton_Click( object sender, EventArgs e ) {
		using var dlg = new OpenFileDialog { Filter = EditorForm.FileFilter };
		if( _scriptPath != null ) dlg.InitialDirectory = Path.GetDirectoryName( _scriptPath );
		if( dlg.ShowDialog( this ) == DialogResult.OK ) SetScript( dlg.FileName );
	}

	private void SaveButton_Click( object sender, EventArgs e ) {
		if( _recordedSource != null ) {
			SaveRecorded();
			return;
		}
		// 読み込み中のスクリプトを別名で保存
		if( _scriptPath == null ) return;
		using var dlg = new SaveFileDialog {
			Filter = EditorForm.FileFilter,
			InitialDirectory = Path.GetDirectoryName( _scriptPath ),
			FileName = Path.GetFileName( _scriptPath ),
		};
		if( dlg.ShowDialog( this ) != DialogResult.OK ) return;
		try {
			if( !string.Equals( Path.GetFullPath( dlg.FileName ), Path.GetFullPath( _scriptPath ), StringComparison.OrdinalIgnoreCase ) )
				File.Copy( _scriptPath, dlg.FileName, overwrite: true );
			SetScript( dlg.FileName );
		} catch( Exception ex ) {
			MessageBox.Show( this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error );
		}
	}

	private async void PlayButton_Click( object sender, EventArgs e ) {
		if( _recorder.IsRecording ) StopRecording();
		else if( _cts != null ) _cts.Cancel();
		else await PlayAsync();
	}

	private void RecordButton_Click( object sender, EventArgs e ) {
		if( _recorder.IsRecording ) StopRecording();
		else StartRecording();
	}

	private void SettingsButton_Click( object sender, EventArgs e ) =>
		settingsMenu.Show( settingsButton, 0, settingsButton.Height );

	// ---- ショートカットキー ----

	protected override bool ProcessCmdKey( ref Message msg, Keys keyData ) {
		Button button = keyData switch {
			Keys.L => loadButton,
			Keys.S => saveButton,
			Keys.P => playButton,
			Keys.R => recordButton,
			Keys.O => settingsButton,
			_ => null,
		};
		if( button != null ) {
			if( button.Enabled ) button.PerformClick();
			return true;
		}
		return base.ProcessCmdKey( ref msg, keyData );
	}

	// ---- 設定メニュー ----

	private void OptionsMenuItem_Click( object sender, EventArgs e ) {
		// 設定中にホットキーが反応しないよう、いったん解除してから開く
		UnregisterHotkeys();
		using var dlg = new SettingsForm( _settings );
		dlg.ShowDialog( this );
		RegisterHotkeys();
	}

	private void EditMenuItem_Click( object sender, EventArgs e ) {
		if( _recordedSource != null ) OpenEditor( null, _recordedSource );
		else OpenEditor( _scriptPath );
	}

	private void NewScriptMenuItem_Click( object sender, EventArgs e ) => OpenEditor( null );

	private void TopMostMenuItem_Click( object sender, EventArgs e ) {
		TopMost = _settings.TopMost = topMostMenuItem.Checked;
		_settings.Save();
	}

	private void ShowPrintMenuItem_Click( object sender, EventArgs e ) => _printForm.Show( this );

	private void ApiListMenuItem_Click( object sender, EventArgs e ) =>
		MessageBox.Show( this, string.Join( Environment.NewLine, ScriptRunner.ApiSignatures ), "組み込み API 一覧" );

	private void AboutMenuItem_Click( object sender, EventArgs e ) =>
		MessageBox.Show( this, $"{AppName} - C# Window Script\nVersion {Application.ProductVersion}", "バージョン情報" );

	private void ExitMenuItem_Click( object sender, EventArgs e ) {
		_exiting = true;
		Close();
	}

	private void TrayMenuItem_Click( object sender, EventArgs e ) => MinimizeToTray();

	// ---- スクリプト ----

	/// <summary>再生対象のスクリプトを設定する (エディタで保存したときにも呼ばれる)。</summary>
	public void SetScript( string? path ) {
		if( path != null && !File.Exists( path ) ) path = null;
		_scriptPath = path;
		if( path != null ) {
			_recordedSource = null;
			_settings.AddRecent( path );
			_settings.Save();
		}
		UpdateButtons();
		UpdateTitle();
	}

	private void UpdateTitle() {
		if( _recorder.IsRecording ) {
			this.Text = "記録中…";
			return;
		}
		if( _cts != null ) {
			this.Text = $"{Path.GetFileName( _scriptPath )} - 再生中";
			return;
		}

		if( _scriptPath != null ) {
			this.Text = Path.GetFileName( _scriptPath );
			return;
		}

		if( _recordedSource != null ) {
			this.Text = "(記録)";
			return;
		}

		this.Text = AppName;
	}

	private EditorForm OpenEditor( string? path, string? text = null ) {
		var existing = path == null ? null
			: _editors.FirstOrDefault( f => string.Equals( f.FilePath, path, StringComparison.OrdinalIgnoreCase ) );
		if( existing != null ) {
			existing.Activate();
			return existing;
		}
		var editor = new EditorForm( this, path, text );
		editor.FormClosed += ( _, _ ) => _editors.Remove( editor );
		_editors.Add( editor );
		editor.Show();
		return editor;
	}

	// ---- 再生 ----

	private async Task PlayAsync() {
		if( _recordedSource != null ) {
			await RunAsync( _recordedSource, null );
			return;
		}
		if( _scriptPath is not { } path ) return;
		string source;
		try { source = File.ReadAllText( path ); } catch( Exception ex ) {
			MessageBox.Show( this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error );
			return;
		}
		await RunAsync( source, null, path );
	}

	/// <summary>スクリプトを再生する。エラー時は editor (無ければ path のエディタを開いて) 該当行を示す。</summary>
	public async Task RunAsync( string source, EditorForm? editor, string? path = null ) {
		if( _cts != null ) return;

		_cts = new CancellationTokenSource();
		var token = _cts.Token;
		SetRunning( true );
		_printForm.Reset();
		editor?.SetStatus( $"再生中… ({_settings.StopHotkey} または Pause キーで停止)" );

		string? status = null;
		try {
			// UI スレッドを塞がないよう専用の STA スレッドで実行 (MsgBox/クリップボード用)
			await RunOnStaThreadAsync( () =>
				ScriptRunner.Run( source, text => BeginInvoke( () => _printForm.AppendLine( text ) ), token, _printForm ) );
		} catch( OperationCanceledException ) {
			status = "停止しました";
		} catch( ScriptException ex ) {
			status = "エラー";
			MessageBox.Show( this, ex.Message, $"{AppName} - エラー", MessageBoxButtons.OK, MessageBoxIcon.Error );
			if( ex.Line > 0 ) {
				editor ??= path != null ? OpenEditor( path )
					: source == _recordedSource ? OpenEditor( null, source ) : null;
				editor?.GoToLine( ex.Line );
			}
		} catch( Exception ex ) {
			status = "エラー";
			MessageBox.Show( this, ex.Message, $"{AppName} - エラー", MessageBoxButtons.OK, MessageBoxIcon.Error );
		} finally {
			_cts.Dispose();
			_cts = null;
			SetRunning( false );
		}
		editor?.SetStatus( status ?? "正常終了" );
	}

	private static Task RunOnStaThreadAsync( Action action ) {
		var tcs = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
		var thread = new Thread( () => {
			try { action(); tcs.SetResult(); } catch( Exception ex ) { tcs.SetException( ex ); }
		} ) { IsBackground = true, Name = "CsWSC" };
		thread.SetApartmentState( ApartmentState.STA );
		thread.Start();
		return tcs.Task;
	}

	private void SetRunning( bool running ) {
		SetStopLayout( running, $"停止 ({_settings.StopHotkey})" );
		UpdateButtons();
		AutoTray( running );
	}

	/// <summary>再生・記録中は再生ボタンを横幅いっぱいの STOP ボタンにする。</summary>
	private void SetStopLayout( bool running, string stopTip ) {
		// 再生中は再生ボタンが停止ボタンになる
		playButton.Image = running ? Properties.Resources.Stop : Properties.Resources.Play;
		// 再生中は他のボタンを隠し、横幅いっぱいの STOP ボタンだけにする
		loadButton.Visible = saveButton.Visible = recordButton.Visible = settingsButton.Visible = !running;
		playButton.Text = running ? "STOP" : "";
		playButton.TextImageRelation = running ? TextImageRelation.ImageBeforeText : TextImageRelation.Overlay;
		// アイコンを右寄せ・文字を左寄せにすると、両者が中央で隣り合う
		playButton.ImageAlign = running ? ContentAlignment.MiddleRight : ContentAlignment.MiddleCenter;
		playButton.TextAlign = running ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
		playButton.Width = running ? ClientSize.Width : 62;
		toolTip.SetToolTip( playButton, running ? stopTip : "再生(P)" );
	}

	private void UpdateButtons() {
		bool running = _cts != null, recording = _recorder.IsRecording;
		bool hasScript = _scriptPath != null || _recordedSource != null;
		loadButton.Enabled = !running && !recording;
		saveButton.Enabled = !running && !recording && hasScript;
		playButton.Enabled = running || recording || hasScript;
		recordButton.Enabled = !running;
		recordButton.Image = recording ? Properties.Resources.Stop : Properties.Resources.Record;
		editMenuItem.Enabled = hasScript;
		toolTip.SetToolTip( recordButton, recording ? $"記録終了 ({_settings.RecordHotkey})" : "記録" );
		UpdateTitle();
	}

	// ---- 記録 ----

	private void StartRecording() {
		if( _cts != null ) return;
		var options = new RecordOptions(
			HighLevel: !_settings.LowLevelRecord,
			SkipIdle: _settings.SkipIdle,
			RelativeMouse: _settings.RelativeMouse,
			Background: _settings.BackgroundRecord );
		try {
			_recorder.Start( options );
		} catch( Exception ex ) {
			MessageBox.Show( this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error );
			return;
		}
		SetStopLayout( true, $"記録終了 ({_settings.RecordHotkey})" );
		UpdateButtons();
		AutoTray( true );
	}

	private void StopRecording() {
		if( !_recorder.IsRecording ) return;
		// 記録終了に使ったホットキー (記録・停止) のキーは末尾から取り除く
		var trim = new[] { _settings.RecordHotkey, _settings.StopHotkey }
			.SelectMany( HotkeyKeys )
			.ToList();
		var script = _recorder.Stop( trim );
		SetStopLayout( false, "" );
		UpdateButtons();
		AutoTray( false );

		if( script == null ) {
			MessageBox.Show( this, "記録された操作がありません。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information );
			return;
		}
		_recordedSource = script;
		if( _settings.CopyAfterRecord ) {
			try { Clipboard.SetText( script ); } catch( Exception ) { }
		}
		UpdateButtons();
	}

	private static IEnumerable<int> HotkeyKeys( Hotkey hotkey ) {
		if( hotkey.TryGetVirtualKey( out var vk ) ) yield return (int)vk;
		foreach( var m in hotkey.Modifier.Split( '+' ) ) {
			switch( m ) {
				case "ALT": yield return 0x12; yield return 0xA4; yield return 0xA5; break;
				case "CTRL": yield return 0x11; yield return 0xA2; yield return 0xA3; break;
				case "SHIFT": yield return 0x10; yield return 0xA0; yield return 0xA1; break;
				case "WIN": yield return 0x5B; yield return 0x5C; break;
			}
		}
	}

	private void SaveRecorded() {
		using var dlg = new SaveFileDialog {
			Filter = EditorForm.FileFilter,
			FileName = $"記録_{DateTime.Now:yyyyMMdd_HHmmss}.csws",
		};
		if( _scriptPath != null ) dlg.InitialDirectory = Path.GetDirectoryName( _scriptPath );
		if( dlg.ShowDialog( this ) != DialogResult.OK ) return;
		try {
			File.WriteAllText( dlg.FileName, _recordedSource );
			SetScript( dlg.FileName );
		} catch( Exception ex ) {
			MessageBox.Show( this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error );
		}
	}

	// ---- グローバルホットキー ----

	[DllImport( "user32.dll" )] private static extern bool RegisterHotKey( IntPtr hWnd, int id, uint mod, uint vk );
	[DllImport( "user32.dll" )] private static extern bool UnregisterHotKey( IntPtr hWnd, int id );

	private void RegisterHotkeys() {
		if( DesignMode || !IsHandleCreated ) return;
		RegisterHotKey( Handle, HotkeyPause, 0, VK_PAUSE ); // 非常停止用 (常に有効)
		var failed = new List<string>();
		foreach( var (id, name, hotkey) in new[]
		{
			(HotkeyPlay, "再生", _settings.PlayHotkey),
			(HotkeyStop, "停止", _settings.StopHotkey),
			(HotkeyRecord, "記録", _settings.RecordHotkey),
			(HotkeyTray, "トレイ", _settings.TrayHotkey),
		} ) {
			if( !hotkey.Enabled || !hotkey.TryGetVirtualKey( out var vk ) ) continue;
			if( !RegisterHotKey( Handle, id, hotkey.ModifierFlags | Hotkey.MOD_NOREPEAT, vk ) ) failed.Add( $"{name}: {hotkey}" );
		}
		//if( failed.Count > 0 )
		//	MessageBox.Show( this, "次のホットキーは他のアプリで使用中のため登録できませんでした。\n\n" + string.Join( "\n", failed ),
		//		AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning );
	}

	private void UnregisterHotkeys() {
		foreach( var id in new[] { HotkeyPause, HotkeyPlay, HotkeyStop, HotkeyRecord, HotkeyTray } ) UnregisterHotKey( Handle, id );
	}

	protected override void OnHandleCreated( EventArgs e ) {
		base.OnHandleCreated( e );
		RegisterHotkeys();
	}

	protected override void OnHandleDestroyed( EventArgs e ) {
		UnregisterHotkeys();
		base.OnHandleDestroyed( e );
	}

	protected override void WndProc( ref Message m ) {
		if( m.Msg == WM_HOTKEY ) {
			switch( (int)m.WParam ) {
				case HotkeyPause or HotkeyStop:
					if( _recorder.IsRecording ) StopRecording();
					else _cts?.Cancel();
					break;
				case HotkeyPlay:
					if( _cts == null && !_recorder.IsRecording ) _ = PlayAsync();
					break;
				case HotkeyRecord:
					if( _cts == null ) RecordButton_Click( this, EventArgs.Empty );
					break;
				case HotkeyTray:
					ToggleTray();
					break;
			}
		}
		base.WndProc( ref m );
	}

	// ---- タスクトレイ ----

	private void ToggleTray() {
		if( Visible ) MinimizeToTray();
		else RestoreFromTray();
	}

	private void MinimizeToTray() {
		trayIcon.Visible = true;
		Hide();
	}

	private void RestoreFromTray() {
		_autoTrayed = false;
		Show();
		Activate();
		trayIcon.Visible = false;
	}

	/// <summary>再生・記録の開始/終了時に呼ぶ。設定に応じてトレイへ出し入れする。</summary>
	private void AutoTray( bool active ) {
		if( !_settings.TrayWhileRunning ) return;
		if( active && Visible ) {
			_autoTrayed = true;
			MinimizeToTray();
		} else if( !active && _autoTrayed ) RestoreFromTray();
	}

	private void TrayIcon_MouseClick( object? sender, MouseEventArgs e ) {
		if( e.Button == MouseButtons.Left ) RestoreFromTray();
		else if( e.Button == MouseButtons.Right ) settingsMenu.Show( Cursor.Position );
	}

	protected override void OnFormClosing( FormClosingEventArgs e ) {
		// 閉じるボタンではトレイに収納する設定
		if( e.CloseReason == CloseReason.UserClosing && _settings.TrayOnClose && !_exiting ) {
			e.Cancel = true;
			MinimizeToTray();
			return;
		}
		_exiting = false;
		// 未保存のエディタがあれば確認 (キャンセルされたら終了しない)
		foreach( var editor in _editors.ToList() ) {
			editor.Close();
			if( !editor.IsDisposed ) { e.Cancel = true; return; }
		}
		_cts?.Cancel();
		_recorder.Dispose();
		trayIcon.Visible = false;
		_settings.Location = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
		_settings.Save();
		base.OnFormClosing( e );
	}
}
