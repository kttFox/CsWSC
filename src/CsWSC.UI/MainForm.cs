using System.Runtime.InteropServices;
using System.Xml.Linq;
using CsWSC.Recording;

namespace CsWSC.UI;

/// <summary>UWSC と同じツールバー型のメイン画面 (読込み / 保存 / 再生 / 記録 / 設定) です。</summary>
public partial class MainForm : Form {
	private const string AppName = "CsWSC";
	private const int WM_HOTKEY = 0x0312;
	private const uint VK_PAUSE = 0x13;
	private const int WM_SYSCOMMAND = 0x0112, WM_INITMENUPOPUP = 0x0117;
	private const int SC_MINIMIZE = 0xF020;
	private const uint MF_BYCOMMAND = 0x0, MF_ENABLED = 0x0;

	// ホットキーの ID (グローバル / フック共通)
	private const int HotkeyPause = 1, HotkeyPlay = 2, HotkeyStop = 3, HotkeyRecord = 4, HotkeyTray = 5;
	/// <summary>ランチャー (メニューを開くキーと各枠のキー) のホットキー ID は HotkeyLauncherBase + _launcherHotkeyGroups の添字です。</summary>
	private const int HotkeyLauncherBase = 100;

	/// <summary>設定メニューの「ランチャー」サブメニュー (開くたびに作り直す) です。</summary>
	private readonly ToolStripMenuItem _launcherMenuItem = new( "ランチャー(&L)" );
	/// <summary>ランチャーメニューのホットキーで出すポップアップメニューです。</summary>
	private readonly ContextMenuStrip _launcherPopup = new() { ShowImageMargin = false };
	/// <summary>ポップアップを出す前に前面にあった窓 (閉じたら戻す) です。</summary>
	private IntPtr _launcherPrevForeground;
	/// <summary>ランチャーのホットキー (同じキー・同じ登録方式の設定をまとめたもの。メニューを開くキーを含む) です。</summary>
	private List<LauncherHotkeyGroup> _launcherHotkeyGroups = [];
	/// <summary>対象アプリが前面にあるときだけ RegisterHotKey しているランチャーのホットキー ID です。</summary>
	private readonly HashSet<int> _conditionalHotkeyIds = [];
	/// <summary>前面の窓の切り替わり (とタイトル変更) を受けるフックです。</summary>
	private IntPtr _foregroundHook, _nameChangeHook;
	private WinEventProc? _winEventProc; // GC に回収されないよう保持する
	/// <summary>RegisterHotKey で常時登録したホットキー ID (設定のホットキーと対象アプリなしのランチャー。解除用。Pause は別に解除する。対象アプリ限定の分は _conditionalHotkeyIds) です。</summary>
	private readonly List<int> _registeredHotkeyIds = [];
	/// <summary>PassThrough のホットキー (キーを横取りしないフック方式) です。</summary>
	private readonly KeyboardHookHotkeys _hotkeys = new();

	private readonly AppSettings _settings = AppSettings.Load();
	private readonly PrintForm _printForm = new();
	private readonly List<EditorForm> _editors = [];
	private CancellationTokenSource? _cts;
	private string? _scriptPath;
	private readonly Recorder _recorder = new();
	/// <summary>記録した未保存のスクリプト (保存すると _scriptPath に移る) です。</summary>
	private string? _recordedSource;
	/// <summary>メニューの「終了」による終了 (閉じるボタンでトレイ収納する設定でも終了させる) です。</summary>
	private bool _exiting;
	/// <summary>再生・記録のために自動でトレイに収納したかどうかです (終わったら元に戻す)。</summary>
	private bool _autoTrayed;

	public MainForm() {
		InitializeComponent();
		ApplyButtonImages();
		FileDrop.Enable( this, path => SetScript( path ) );
		foreach( System.Windows.Forms.Control c in Controls ) FileDrop.Enable( c, path => SetScript( path ) );
		// ウインドウ全体 (ボタン上も含む) で右クリックすると設定メニューを出す
		ContextMenuStrip = settingsMenu;
		foreach( System.Windows.Forms.Control c in Controls ) c.ContextMenuStrip = settingsMenu;
		settingsMenu.Items.Insert( settingsMenu.Items.IndexOf( recentMenuItem ) + 1, _launcherMenuItem );
		_launcherPopup.Closed += ( _, _ ) => {
			// メニューのために奪った前面を、スクリプトの操作対象になる元の窓へ戻す
			if( _launcherPrevForeground != IntPtr.Zero ) SetForegroundWindow( _launcherPrevForeground );
			_launcherPrevForeground = IntPtr.Zero;
		};
	}

	protected override void OnLoad( EventArgs e ) {
		base.OnLoad( e );
		if( DesignMode ) return;

		_ = _printForm.Handle; // スクリプトのスレッドから Invoke できるよう先に作っておく
		TopMost = topMostMenuItem.Checked = _settings.TopMost;
		if( _settings.Location is { } loc && Screen.AllScreens.Any( s => s.WorkingArea.Contains( loc ) ) ) {
			StartPosition = FormStartPosition.Manual;
			Location = loc;
		}
		if( _settings.PrintBounds is { } pb && Screen.AllScreens.Any( s => s.WorkingArea.IntersectsWith( pb ) ) ) {
			_printForm.StartPosition = FormStartPosition.Manual;
			_printForm.Bounds = pb;
		}
	}

	protected override void OnShown( EventArgs e ) {
		base.OnShown( e );
		if( _printForm.StartPosition != FormStartPosition.Manual ) {
			// 初回はメインウインドウのすぐ上に表示する
			_printForm.StartPosition = FormStartPosition.Manual;
			_printForm.Location = new Point( Left, Math.Max( Screen.FromControl( this ).WorkingArea.Top, Top - _printForm.Height ) );
		}
		if( _settings.TrayOnStart ) MinimizeToTray();
		ScriptProcess.Prepare();
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
		if( _cts != null || _recorder.IsRecording ) return;
		ShowSettingsDialog( () => new SettingsForm( _settings ) );
	}

	/// <summary>設定画面を開きます。保存後に登録できないホットキーがあり、キャンセルが選ばれたら設定画面を開き直します。</summary>
	private void ShowSettingsDialog( Func<Form> create ) {
		while( true ) {
			// 設定中にホットキーが反応しないよう、いったん解除してから開く
			UnregisterHotkeys();
			bool saved;
			using( var dlg = create() ) saved = dlg.ShowDialog( this ) == DialogResult.OK;
			var failed = RegisterHotkeys();
			if( !saved || ShowHotkeyFailures( failed ) ) return;
		}
	}

	private void EditMenuItem_Click( object sender, EventArgs e ) {
		if( _recordedSource != null ) OpenEditor( null, _recordedSource );
		else OpenEditor( _scriptPath );
	}

	private void NewScriptMenuItem_Click( object sender, EventArgs e ) => OpenEditor( null );

	private void SettingsMenu_Opening( object? sender, System.ComponentModel.CancelEventArgs e ) {
		// 最近使用したファイルのサブメニューを作り直す (存在しないファイルは除く)
		recentMenuItem.DropDownItems.Clear();
		bool idle = _cts == null && !_recorder.IsRecording;
		int n = 0;
		foreach( var path in _settings.RecentFiles.Where( File.Exists ) ) {
			var item = new ToolStripMenuItem( $"&{++n % 10} {Path.GetFileName( path )}" ) {
				ToolTipText = path,
				Checked = string.Equals( path, _scriptPath, StringComparison.OrdinalIgnoreCase ),
			};
			item.Click += ( _, _ ) => SetScript( path );
			recentMenuItem.DropDownItems.Add( item );
		}
		recentMenuItem.Enabled = idle && n > 0;
		BuildLauncherMenu( idle );
		// 設定中はホットキー (Pause 含む) を解除するので、再生中・記録中は開かせない
		optionsMenuItem.Enabled = idle;
	}

	private void BuildLauncherMenu( bool idle ) {
		_launcherMenuItem.DropDownItems.Clear();
		AddLauncherItems( _launcherMenuItem.DropDownItems, _settings.Launchers, idle );
		if( _launcherMenuItem.DropDownItems.Count > 0 ) _launcherMenuItem.DropDownItems.Add( new ToolStripSeparator() );
		_launcherMenuItem.DropDownItems.Add( LauncherSettingsItem( idle ) );
	}

	/// <summary>UWSC と同じく「キー. タイトル」の形で、最後に使っている枠までをメニューに並べます。</summary>
	private void AddLauncherItems( ToolStripItemCollection items, List<LauncherSlot> slots, bool idle ) {
		int last = slots.FindLastIndex( s => !s.IsEmpty );
		for( int i = 0; i <= last; i++ ) {
			var slot = slots[i];
			var item = new ToolStripMenuItem( $"&{LauncherSlot.KeyOf( i )}. {slot.DisplayName.Replace( "&", "&&" )}" );
			if( slot.IsFolder ) {
				AddLauncherItems( item.DropDownItems, slot.Children!, idle );
				item.Enabled = item.DropDownItems.Count > 0;
			} else if( slot.IsScript ) {
				item.ToolTipText = slot.Path;
				item.ShortcutKeyDisplayString = slot.Hotkey.Enabled ? slot.Hotkey.ToString() : null;
				// 別プロセスで実行する枠は再生中・記録中でも起動できる
				item.Enabled = ( idle || slot.SeparateProcess ) && File.Exists( slot.Path );
				item.Click += ( _, _ ) => _ = RunLauncherAsync( slot );
			} else {
				item.Enabled = false; // 未設定・見出し行
			}
			items.Add( item );
		}
	}

	/// <param name="idle">再生中・記録中でないかどうかです (false なら項目を無効にする)。</param>
	/// <param name="profile">設定画面で最初に開く対象アプリのランチャー (null なら対象アプリなし) です。</param>
	private ToolStripMenuItem LauncherSettingsItem( bool idle, LauncherSlot? profile = null ) {
		var item = new ToolStripMenuItem( "ランチャーの設定..." ) { Enabled = idle };
		item.Click += ( _, _ ) => {
			if( _cts != null || _recorder.IsRecording ) return;
			ShowSettingsDialog( () => new LauncherForm( _settings, profile ) );
		};
		return item;
	}

	/// <summary>ランチャーメニューをマウス位置に出します (ホットキーから)。menu は開くランチャー (Children が 36 枠) です。</summary>
	private void ShowLauncherPopup( LauncherSlot menu ) {
		if( _launcherPopup.Visible ) {
			_launcherPopup.Close();
			return;
		}
		bool idle = _cts == null && !_recorder.IsRecording;
		_launcherPrevForeground = GetForegroundWindow();
		_launcherPopup.Items.Clear();
		var profile = menu.HasTarget ? menu : null;
		AddLauncherItems( _launcherPopup.Items, menu.Children ?? [], idle );
		if( _launcherPopup.Items.Count > 0 ) _launcherPopup.Items.Add( new ToolStripSeparator() );
		_launcherPopup.Items.Add( LauncherSettingsItem( idle, profile ) );
		_launcherPopup.Show( Cursor.Position );
		// 前面にしないとキー (0〜9, A〜Z) で選べず、外をクリックしても閉じない
		SetForegroundWindow( _launcherPopup.Handle );
	}

	/// <summary>ランチャーの枠のスクリプトを実行します。</summary>
	private async Task RunLauncherAsync( LauncherSlot slot ) {
		var path = slot.Path;
		if( !File.Exists( path ) ) {
			MessageBox.Show( this, $"スクリプトが見つかりません。\n{path}", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		if( slot.SeparateProcess ) {
			// CsWSC.exe スクリプト で起動する (メイン画面の再生とは独立して動き、終わったら終了する)
			try {
				System.Diagnostics.Process.Start( new System.Diagnostics.ProcessStartInfo( Environment.ProcessPath!, [path] ) {
					WorkingDirectory = Path.GetDirectoryName( path )!,
				} );
			} catch( Exception ex ) {
				MessageBox.Show( this, ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error );
			}
			return;
		}
		if( _cts != null || _recorder.IsRecording ) return;
		SetScript( path );
		await PlayAsync();
	}

	private void TopMostMenuItem_Click( object sender, EventArgs e ) {
		TopMost = _settings.TopMost = topMostMenuItem.Checked;
		_settings.Save();
	}

	private void ShowPrintMenuItem_Click( object sender, EventArgs e ) => _printForm.Show();

	private void AboutMenuItem_Click( object sender, EventArgs e ) =>
		MessageBox.Show( this, $"{AppName} - C# Window Script\nVersion {Application.ProductVersion}", "バージョン情報" );

	private void ExitMenuItem_Click( object sender, EventArgs e ) {
		_exiting = true;
		Close();
	}

	private void TrayMenuItem_Click( object sender, EventArgs e ) => MinimizeToTray();

	// ---- スクリプト ----

	/// <summary>再生対象のスクリプトを設定します (エディタで保存したときにも呼ばれる)。</summary>
	public void SetScript( string? path ) {
		if( path != null && !File.Exists( path ) ) path = null;
		_scriptPath = path;
		if( path != null ) {
			_recordedSource = null;
			_settings.AddRecent( path );
			_settings.Save();
			// 再生ボタンを押す前にコンパイルしてキャッシュを作っておく (再生中は邪魔しないよう行わない)
			if( _cts == null ) {
				try { _ = ScriptProcess.PrecompileAsync( File.ReadAllText( path ), path ); } catch( Exception ) { }
			}
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

	/// <summary>スクリプトを再生します。エラー時は editor (無ければ path のエディタを開いて) 該当行を示します。</summary>
	public async Task RunAsync( string source, EditorForm? editor, string? path = null ) {
		if( _cts != null ) return;

		_cts = new CancellationTokenSource();
		var token = _cts.Token;
		SetRunning( true );
		_printForm.Reset();
		editor?.SetStatus( $"再生中… ({_settings.StopHotkey} または Pause キーで停止)" );

		string? status = null;
		try {
			// 別プロセスで実行する (停止要求後 1 秒たっても終わらなければ強制終了される)
			await ScriptProcess.RunAsync( source, path ?? editor?.FilePath,
				text => BeginInvoke( () => _printForm.AppendLine( text ) ), _printForm, token );
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

	private void SetRunning( bool running ) {
		SetStopLayout( running, $"停止 ({_settings.StopHotkey})" );
		UpdateButtons();
		AutoTray( running );
		// 実行中は停止ボタンと Print 窓を最前面に出しておく (操作中の窓からフォーカスは奪わない)
		_printForm.Running = running;
		SetTopMostNoActivate( running || _settings.TopMost );
	}

	[DllImport( "user32.dll" )] private static extern bool SetWindowPos( IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags );

	/// <summary>アクティブにせずに最前面表示を切り替えます。</summary>
	private void SetTopMostNoActivate( bool topMost ) {
		const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
		SetWindowPos( Handle, new IntPtr( topMost ? -1 : -2 ), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE );
	}

	/// <summary>再生・記録中は再生ボタンを横幅いっぱいの STOP ボタンにします。</summary>
	private void SetStopLayout( bool running, string stopTip ) {
		// 再生中は再生ボタンが停止ボタンになる
		playButton.Image = ButtonImage( running ? nameof( Properties.Resources.Stop ) : nameof( Properties.Resources.Play ) );
		// 再生中は他のボタンを隠し、横幅いっぱいの STOP ボタンだけにする
		loadButton.Visible = saveButton.Visible = recordButton.Visible = settingsButton.Visible = !running;
		playButton.Text = running ? "STOP" : "";
		playButton.TextImageRelation = running ? TextImageRelation.ImageBeforeText : TextImageRelation.Overlay;
		// アイコンを右寄せ・文字を左寄せにすると、両者が中央で隣り合う
		playButton.ImageAlign = running ? ContentAlignment.MiddleRight : ContentAlignment.MiddleCenter;
		playButton.TextAlign = running ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleCenter;
		playButton.Width = running ? ClientSize.Width : LogicalToDeviceUnits( 62 );
		toolTip.SetToolTip( playButton, running ? stopTip : "再生(P)" );
	}

	/// <summary>DPI に合わせて拡大したボタン画像のキャッシュです (キーはリソース名)。</summary>
	private readonly Dictionary<string, Image> _buttonImages = [];
	private int _buttonImagesDpi;

	/// <summary>ボタン画像のリソースに用意されうるサイズです。<c>Load_40</c> のように名前の後ろにサイズを付けます (付けないものは 20)。</summary>
	private static readonly int[] ButtonImageSizes = [20, 25, 30, 35, 40, 50];

	/// <summary>リソースのボタン画像から現在の DPI に合うサイズのものを返します。ちょうどのサイズがなければ、それより大きいものを縮小します。</summary>
	private Image ButtonImage( string name ) {
		if( _buttonImagesDpi != DeviceDpi ) {
			foreach( var img in _buttonImages.Values ) img.Dispose();
			_buttonImages.Clear();
			_buttonImagesDpi = DeviceDpi;
		}
		if( _buttonImages.TryGetValue( name, out var cached ) ) return cached;

		int target = LogicalToDeviceUnits( 20 );
		Bitmap? src = null;
		foreach( int size in ButtonImageSizes ) {
			var key = size == 20 ? name : $"{name}_{size}";
			if( Properties.Resources.ResourceManager.GetObject( key ) is not Bitmap bmp ) continue;
			src?.Dispose();
			src = bmp;
			if( size >= target ) break; // 最小の「目標以上」のサイズ (なければ最大のもの)
		}
		if( src == null ) throw new InvalidOperationException( $"ボタン画像 {name} がありません。" );
		if( src.Width == target ) return _buttonImages[name] = src;

		var scaled = new Bitmap( target, target );
		using( var g = Graphics.FromImage( scaled ) ) {
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
			g.DrawImage( src, new Rectangle( 0, 0, target, target ) );
		}
		src.Dispose();
		return _buttonImages[name] = scaled;
	}

	/// <summary>各ボタンの画像を現在の DPI のものにします。</summary>
	private void ApplyButtonImages() {
		loadButton.Image = ButtonImage( nameof( Properties.Resources.Load ) );
		saveButton.Image = ButtonImage( nameof( Properties.Resources.Save ) );
		settingsButton.Image = ButtonImage( nameof( Properties.Resources.Settings ) );
		bool running = _cts != null, recording = _recorder.IsRecording;
		playButton.Image = ButtonImage( running || recording ? nameof( Properties.Resources.Stop ) : nameof( Properties.Resources.Play ) );
		recordButton.Image = ButtonImage( recording ? nameof( Properties.Resources.Stop ) : nameof( Properties.Resources.Record ) );
	}

	protected override void OnDpiChanged( DpiChangedEventArgs e ) {
		base.OnDpiChanged( e );
		ApplyButtonImages();
	}

	private void UpdateButtons() {
		bool running = _cts != null, recording = _recorder.IsRecording;
		bool hasScript = _scriptPath != null || _recordedSource != null;
		loadButton.Enabled = !running && !recording;
		saveButton.Enabled = !running && !recording && hasScript;
		playButton.Enabled = running || recording || hasScript;
		recordButton.Enabled = !running;
		recordButton.Image = ButtonImage( recording ? nameof( Properties.Resources.Stop ) : nameof( Properties.Resources.Record ) );
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
		// 記録終了に使ったホットキー (記録・停止・Pause) のキーは末尾から取り除く
		var trim = new[] { _settings.RecordHotkey, _settings.StopHotkey }
			.SelectMany( HotkeyKeys )
			.Append( (int)VK_PAUSE )
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

	// ---- ホットキー ----

	[DllImport( "user32.dll" )] private static extern bool RegisterHotKey( IntPtr hWnd, int id, uint mod, uint vk );
	[DllImport( "user32.dll" )] private static extern bool UnregisterHotKey( IntPtr hWnd, int id );
	[DllImport( "user32.dll" )] private static extern IntPtr GetForegroundWindow();
	[DllImport( "user32.dll" )] private static extern bool SetForegroundWindow( IntPtr hWnd );
	[DllImport( "user32.dll" )] private static extern bool EnableMenuItem( IntPtr hMenu, uint idEnableItem, uint uEnable );

	/// <summary>設定のホットキーとランチャーのホットキーを登録します。</summary>
	/// <returns>登録できなかったホットキーの説明 (対象アプリ限定のグローバルホットキーは前面の窓に合わせて後で登録するので含まない) です。</returns>
	private List<string> RegisterHotkeys() {
		var failed = new List<string>();
		if( DesignMode || !IsHandleCreated ) return failed;
		RegisterHotKey( Handle, HotkeyPause, 0, VK_PAUSE ); // 非常停止用 (常に有効)
		foreach( var (id, hotkey, name) in new[]
		{
			(HotkeyPlay, _settings.PlayHotkey, "再生"),
			(HotkeyStop, _settings.StopHotkey, "停止"),
			(HotkeyRecord, _settings.RecordHotkey, "記録"),
			(HotkeyTray, _settings.TrayHotkey, "タスクトレイに収納"),
		} ) {
			if( !AddHotkey( id, hotkey ) ) failed.Add( $"{hotkey} ({name})" );
		}
		// 同じキーでも登録方式 (グローバル / フック) が違えば別に登録する (方式が混ざるのは、対象アプリが重ならない場合だけ)
		_launcherHotkeyGroups = _settings.LauncherHotkeyEntries()
			.GroupBy( x => (x.Hotkey, x.Hotkey.PassThrough) )
			.Select( g => new LauncherHotkeyGroup( g.Key.Hotkey, [.. g] ) )
			.ToList();
		for( int i = 0; i < _launcherHotkeyGroups.Count; i++ ) {
			var group = _launcherHotkeyGroups[i];
			// 対象アプリ限定だけのグローバルホットキーは、そのアプリが前面のときだけ登録する (他のアプリではキーを横取りしない)
			if( group.Conditional && !group.Hotkey.PassThrough ) continue;
			if( !AddHotkey( HotkeyLauncherBase + i, group.Hotkey ) ) failed.Add( $"{group.Hotkey} ({group.Candidates[0].Name})" );
		}
		if( _launcherHotkeyGroups.Any( g => g.Conditional && !g.Hotkey.PassThrough ) ) {
			_winEventProc ??= OnWinEvent;
			_foregroundHook = SetWinEventHook( EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT );
			// タイトルで限定しているときは、同じ窓のタイトル変更 (ブラウザのタブ切り替えなど) も見る
			if( _launcherHotkeyGroups.Any( g => g.Candidates.Any( c => c.Conditions.Any( x => x.TargetTitle.Trim() != "" ) ) ) )
				_nameChangeHook = SetWinEventHook( EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT );
			UpdateConditionalHotkeys();
		}
		return failed;
	}

	/// <summary>登録できなかったホットキーがあれば知らせます (設定の保存時に呼ぶ)。キャンセルが選ばれたら false を返します。</summary>
	private bool ShowHotkeyFailures( List<string> failed ) {
		if( failed.Count == 0 ) return true;
		return MessageBox.Show( this, "次のホットキーを登録できませんでした。他のアプリが使っている可能性があります。\n\n" + string.Join( "\n", failed )
			+ "\n\n[キャンセル] で設定画面に戻ります。",
			AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK;
	}

	// ---- 対象アプリを限定したランチャーのホットキー ----

	private const uint EVENT_SYSTEM_FOREGROUND = 0x0003, EVENT_OBJECT_NAMECHANGE = 0x800C, WINEVENT_OUTOFCONTEXT = 0;
	private const int OBJID_WINDOW = 0;

	private delegate void WinEventProc( IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time );
	[DllImport( "user32.dll" )] private static extern IntPtr SetWinEventHook( uint min, uint max, IntPtr hmod, WinEventProc proc, uint pid, uint tid, uint flags );
	[DllImport( "user32.dll" )] private static extern bool UnhookWinEvent( IntPtr hook );

	/// <summary>前面の窓のプロセス名とタイトルです。</summary>
	private readonly record struct ForegroundInfo( string ProcessName, string Title ) {
		public static ForegroundInfo Of( IntPtr hwnd ) =>
			CsWSC.Window.FromHandle( hwnd ) is { } w ? new( w.ProcessName, w.Title ) : new( "", "" );
	}

	/// <summary>同じホットキーを使うランチャーの設定です。前面の窓に合うもののうち、対象アプリのランチャーを優先し、対象アプリどうしでは一覧で先のものを選びます。</summary>
	private sealed record LauncherHotkeyGroup( Hotkey Hotkey, List<LauncherHotkeyEntry> Candidates ) {
		public bool Conditional => Candidates.All( c => c.Conditions.Length > 0 );

		public LauncherHotkeyEntry? Pick( ForegroundInfo fg ) => LauncherHotkeyEntry.Pick( Candidates, fg.ProcessName, fg.Title );
	}

	private void OnWinEvent( IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time ) {
		if( ev == EVENT_OBJECT_NAMECHANGE && ( idObject != OBJID_WINDOW || hwnd != GetForegroundWindow() ) ) return;
		UpdateConditionalHotkeys();
	}

	/// <summary>前面の窓に合わせて、対象アプリ限定のグローバルホットキーを登録・解除します。</summary>
	private void UpdateConditionalHotkeys() {
		if( _foregroundHook == IntPtr.Zero ) return;
		var fg = ForegroundInfo.Of( GetForegroundWindow() );
		for( int i = 0; i < _launcherHotkeyGroups.Count; i++ ) {
			var group = _launcherHotkeyGroups[i];
			if( !group.Conditional || group.Hotkey.PassThrough || !group.Hotkey.TryGetVirtualKey( out var vk ) ) continue;
			int id = HotkeyLauncherBase + i;
			bool want = group.Pick( fg ) != null, has = _conditionalHotkeyIds.Contains( id );
			if( want && !has && RegisterHotKey( Handle, id, group.Hotkey.ModifierFlags | Hotkey.MOD_NOREPEAT, vk ) ) _conditionalHotkeyIds.Add( id );
			else if( !want && has && UnregisterHotKey( Handle, id ) ) _conditionalHotkeyIds.Remove( id );
		}
	}

	/// <summary>PassThrough ならキーを横取りしないフックで、そうでなければ RegisterHotKey で登録します。</summary>
	/// <returns>登録できなかったら false です (無効なホットキーは登録しないので true)。</returns>
	private bool AddHotkey( int id, Hotkey hotkey ) {
		if( !hotkey.Enabled || !hotkey.TryGetVirtualKey( out var vk ) ) return true;
		if( hotkey.PassThrough ) return _hotkeys.Add( hotkey, () => OnHotkey( id ) );
		if( !RegisterHotKey( Handle, id, hotkey.ModifierFlags | Hotkey.MOD_NOREPEAT, vk ) ) return false;
		_registeredHotkeyIds.Add( id );
		return true;
	}

	private void UnregisterHotkeys() {
		UnregisterHotKey( Handle, HotkeyPause );
		foreach( var id in _registeredHotkeyIds ) UnregisterHotKey( Handle, id );
		_registeredHotkeyIds.Clear();
		_hotkeys.Clear();
		foreach( var hook in new[] { _foregroundHook, _nameChangeHook } ) {
			if( hook != IntPtr.Zero ) UnhookWinEvent( hook );
		}
		_foregroundHook = _nameChangeHook = IntPtr.Zero;
		foreach( var id in _conditionalHotkeyIds ) UnregisterHotKey( Handle, id );
		_conditionalHotkeyIds.Clear();
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
		if( m.Msg == WM_HOTKEY ) OnHotkey( (int)m.WParam );
		// 最小化ボタンは出さないが、タイトルバーの右クリックメニューからは最小化できるようにする
		if( m.Msg == WM_SYSCOMMAND && ( (int)m.WParam & 0xFFF0 ) == SC_MINIMIZE ) {
			WindowState = FormWindowState.Minimized;
			return;
		}
		base.WndProc( ref m );
		// WS_MINIMIZEBOX が無いと Windows がメニューを開くたびに「最小化」を灰色にするので、その後で有効に戻す
		if( m.Msg == WM_INITMENUPOPUP && ( (long)m.LParam >> 16 & 0xFFFF ) != 0 )
			EnableMenuItem( m.WParam, SC_MINIMIZE, MF_BYCOMMAND | MF_ENABLED );
	}

	private void OnHotkey( int id ) {
		switch( id ) {
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
			case >= HotkeyLauncherBase when id - HotkeyLauncherBase < _launcherHotkeyGroups.Count:
				// 前面のアプリに合うものを選ぶ (フック方式はキーが前面にも届くので、合うものが無ければ何もしない)
				switch( _launcherHotkeyGroups[id - HotkeyLauncherBase].Pick( ForegroundInfo.Of( GetForegroundWindow() ) ) ) {
					case { Script: { } slot }: _ = RunLauncherAsync( slot ); break;
					case { Menu: { } menu }: ShowLauncherPopup( menu ); break;
				}
				break;
		}
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

	/// <summary>再生・記録の開始/終了時に呼びます。設定に応じてトレイへ出し入れします。</summary>
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
		ScriptProcess.Shutdown();
		_recorder.Dispose();
		trayIcon.Visible = false;
		_settings.Location = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
		_settings.PrintBounds = _printForm.WindowState == FormWindowState.Normal ? _printForm.Bounds : _printForm.RestoreBounds;
		_settings.Save();
		base.OnFormClosing( e );
	}
}
