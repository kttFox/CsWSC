namespace CsWSC.UI;

/// <summary>UWSC と同じ構成の設定ダイアログ (記録方法 / ホットキー / タスクトレイ) です。</summary>
public partial class SettingsForm : Form {
	private readonly AppSettings? _settings;

	public SettingsForm() : this( null ) { }

	public SettingsForm( AppSettings? settings ) {
		InitializeComponent();
		_settings = settings;

		foreach( var row in HotkeyRows ) {
			row.Button.Click += ( _, _ ) => {
				using var dlg = new HotkeyListDialog( row.Name, row.Hotkeys, OwnHotkeys() );
				if( dlg.ShowDialog( this ) == DialogResult.OK ) SetHotkeys( row, dlg.Hotkeys );
			};
		}

		if( settings != null ) LoadFrom( settings );
		UpdateLowLevelOptions();
	}

	/// <summary>ホットキー 1 行分 (表示欄と変更ボタン) です。設定中の値は Hotkeys に持ちます。</summary>
	private sealed class HotkeyRow( string name, TextBox textBox, Button button ) {
		public string Name => name;
		public TextBox TextBox => textBox;
		public Button Button => button;
		public List<Hotkey> Hotkeys { get; set; } = [];
	}

	private HotkeyRow[]? _hotkeyRows;
	private HotkeyRow[] HotkeyRows => _hotkeyRows ??= [
		new( "再生", playHotkeyTextBox, playHotkeyButton ),
		new( "停止", stopHotkeyTextBox, stopHotkeyButton ),
		new( "記録", recordHotkeyTextBox, recordHotkeyButton ),
		new( "トレイ", trayHotkeyTextBox, trayHotkeyButton ),
	];

	/// <summary>保存済みの設定で CsWSC が登録しているホットキーです。</summary>
	private IEnumerable<Hotkey> OwnHotkeys() => _settings?.AllHotkeys() ?? [];

	private static void SetHotkeys( HotkeyRow row, List<Hotkey> hotkeys ) {
		row.Hotkeys = hotkeys;
		row.TextBox.Text = Hotkey.ToDisplayString( hotkeys );
	}

	private void LoadFrom( AppSettings s ) {
		lowLevelRadioButton.Checked = s.LowLevelRecord;
		highLevelRadioButton.Checked = !s.LowLevelRecord;
		skipIdleCheckBox.Checked = s.SkipIdle;
		relativeMouseCheckBox.Checked = s.RelativeMouse;
		backgroundCheckBox.Checked = s.BackgroundRecord;
		copyAfterRecordCheckBox.Checked = s.CopyAfterRecord;
		trayOnStartCheckBox.Checked = s.TrayOnStart;
		trayOnCloseCheckBox.Checked = s.TrayOnClose;
		trayWhileRunningCheckBox.Checked = s.TrayWhileRunning;
		launchNoticeCheckBox.Checked = s.HotkeyLaunchNotice;
		openEditorOnErrorCheckBox.Checked = s.OpenEditorOnError;

		var hotkeys = new[] { s.PlayHotkeys, s.StopHotkeys, s.RecordHotkeys, s.TrayHotkeys };
		foreach( var (row, list) in HotkeyRows.Zip( hotkeys ) ) SetHotkeys( row, [.. list] );
	}

	private void LowLevelRadioButton_CheckedChanged( object sender, EventArgs e ) => UpdateLowLevelOptions();

	// 相対座標・バックグラウンドは低レベル選択時のみ有効 (余分な時間の省略は高レベルでも効く)
	private void UpdateLowLevelOptions() {
		relativeMouseCheckBox.Enabled = backgroundCheckBox.Enabled = lowLevelRadioButton.Checked;
	}

	private void OkButton_Click( object sender, EventArgs e ) {
		var hotkeys = HotkeyRows.Select( r => r.Hotkeys ).ToArray();
		// ランチャーのホットキーとも重複させない
		// (ランチャーの枠どうしは対象アプリが違えば同じキーを使えるので、ランチャー側は重複を除いて比べる)
		var dup = hotkeys.SelectMany( h => h ).Concat( _settings?.LauncherHotkeys().Where( h => h.Enabled ).Distinct() ?? [] )
			.Where( h => h.Enabled ).GroupBy( h => h ).FirstOrDefault( g => g.Count() > 1 );
		if( dup != null ) {
			MessageBox.Show( this, $"ホットキー {dup.Key} が重複しています。", "設定", MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}

		if( _settings != null ) {
			_settings.LowLevelRecord = lowLevelRadioButton.Checked;
			_settings.SkipIdle = skipIdleCheckBox.Checked;
			_settings.RelativeMouse = relativeMouseCheckBox.Checked;
			_settings.BackgroundRecord = backgroundCheckBox.Checked;
			_settings.CopyAfterRecord = copyAfterRecordCheckBox.Checked;
			_settings.TrayOnStart = trayOnStartCheckBox.Checked;
			_settings.TrayOnClose = trayOnCloseCheckBox.Checked;
			_settings.TrayWhileRunning = trayWhileRunningCheckBox.Checked;
			_settings.HotkeyLaunchNotice = launchNoticeCheckBox.Checked;
			_settings.OpenEditorOnError = openEditorOnErrorCheckBox.Checked;
			(_settings.PlayHotkeys, _settings.StopHotkeys, _settings.RecordHotkeys, _settings.TrayHotkeys) =
				(hotkeys[0], hotkeys[1], hotkeys[2], hotkeys[3]);
			_settings.Save();
		}
		DialogResult = DialogResult.OK;
	}
}
