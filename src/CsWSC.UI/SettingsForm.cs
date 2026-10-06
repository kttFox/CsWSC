namespace CsWSC.UI;

/// <summary>UWSC と同じ構成の設定ダイアログ (記録方法 / ホットキー)。</summary>
public partial class SettingsForm : Form {
	private readonly AppSettings? _settings;

	public SettingsForm() : this( null ) { }

	public SettingsForm( AppSettings? settings ) {
		InitializeComponent();
		_settings = settings;

		foreach( var (mod, key) in HotkeyRows ) {
			mod.Items.AddRange( Hotkey.Modifiers );
			key.Items.AddRange( Hotkey.Keys );
		}
		if( settings != null ) LoadFrom( settings );
		UpdateLowLevelOptions();
	}

	private (ComboBox Modifier, ComboBox Key)[] HotkeyRows => [
		(playModifierComboBox, playKeyComboBox),
		(stopModifierComboBox, stopKeyComboBox),
		(recordModifierComboBox, recordKeyComboBox),
		(trayModifierComboBox, trayKeyComboBox),
	];

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

		Select( playModifierComboBox, playKeyComboBox, s.PlayHotkey );
		Select( stopModifierComboBox, stopKeyComboBox, s.StopHotkey );
		Select( recordModifierComboBox, recordKeyComboBox, s.RecordHotkey );
		Select( trayModifierComboBox, trayKeyComboBox, s.TrayHotkey );
	}

	private static void Select( ComboBox mod, ComboBox key, Hotkey hotkey ) {
		mod.SelectedItem = hotkey.Modifier;
		if( mod.SelectedIndex < 0 ) mod.SelectedIndex = 0;
		key.SelectedItem = hotkey.Key;
		if( key.SelectedIndex < 0 ) key.SelectedIndex = 0;
	}

	private static Hotkey Read( ComboBox mod, ComboBox key ) =>
		new( (string)mod.SelectedItem!, (string)key.SelectedItem! );

	private void LowLevelRadioButton_CheckedChanged( object sender, EventArgs e ) => UpdateLowLevelOptions();

	// 低レベル記録のオプションは低レベル選択時のみ有効
	private void UpdateLowLevelOptions() {
		skipIdleCheckBox.Enabled = relativeMouseCheckBox.Enabled = backgroundCheckBox.Enabled = lowLevelRadioButton.Checked;
	}

	private void OkButton_Click( object sender, EventArgs e ) {
		var hotkeys = HotkeyRows.Select( r => Read( r.Modifier, r.Key ) ).ToArray();
		var dup = hotkeys.Where( h => h.Enabled ).GroupBy( h => h ).FirstOrDefault( g => g.Count() > 1 );
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
			(_settings.PlayHotkey, _settings.StopHotkey, _settings.RecordHotkey, _settings.TrayHotkey) =
				(hotkeys[0], hotkeys[1], hotkeys[2], hotkeys[3]);
			_settings.Save();
		}
		DialogResult = DialogResult.OK;
	}
}
