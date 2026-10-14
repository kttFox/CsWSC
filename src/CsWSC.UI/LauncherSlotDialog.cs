namespace CsWSC.UI;

/// <summary>ランチャーの 1 枠を設定するダイアログ (設定なし / スクリプトファイル / 階層化) です。</summary>
public partial class LauncherSlotDialog : Form {
	private readonly LauncherSlot _slot;
	private readonly IEnumerable<Hotkey> _ownHotkeys;
	private Hotkey _hotkey;

	public LauncherSlotDialog() : this( 0, new(), [] ) { }

	/// <param name="index">枠の番号 (タイトルにキーを出す) です。</param>
	/// <param name="slot">設定する枠 (このダイアログでは書き換えない。結果は <see cref="Slot"/>) です。</param>
	/// <param name="ownHotkeys">CsWSC の設定にあるホットキー (HotkeyDialog に渡す) です。</param>
	public LauncherSlotDialog( int index, LauncherSlot slot, IEnumerable<Hotkey> ownHotkeys ) {
		InitializeComponent();
		_slot = slot;
		_ownHotkeys = ownHotkeys;
		_hotkey = slot.Hotkey;
		Text = $"スクリプト設定 - {LauncherSlot.KeyOf( index )}";

		titleTextBox.Text = slot.Title;
		pathTextBox.Text = slot.Path;
		separateCheckBox.Checked = slot.SeparateProcess;
		hotkeyTextBox.Text = _hotkey.ToDisplayString();
		if( slot.IsFolder ) folderRadioButton.Checked = true;
		else if( slot.IsScript ) scriptRadioButton.Checked = true;
		else noneRadioButton.Checked = true;
		FileDrop.Enable( pathTextBox, path => {
			scriptRadioButton.Checked = true;
			pathTextBox.Text = path;
		} );
		UpdateMode();
	}

	/// <summary>設定した枠です。階層化した枠の中身は元の枠から引き継ぎます。</summary>
	public LauncherSlot Slot { get; private set; } = new();

	private void RadioButton_CheckedChanged( object? sender, EventArgs e ) => UpdateMode();

	private void UpdateMode() {
		bool script = scriptRadioButton.Checked;
		pathTextBox.Enabled = browseButton.Enabled = script;
		// 別プロセス・ホットキーは実行できる枠だけ
		separateCheckBox.Enabled = hotkeyLabel.Enabled = hotkeyTextBox.Enabled = hotkeyButton.Enabled = script;
	}

	private void BrowseButton_Click( object? sender, EventArgs e ) {
		using var dlg = new OpenFileDialog { Filter = EditorForm.FileFilter, FileName = Path.GetFileName( pathTextBox.Text ) };
		if( Path.GetDirectoryName( pathTextBox.Text ) is { Length: > 0 } dir && Directory.Exists( dir ) ) dlg.InitialDirectory = dir;
		if( dlg.ShowDialog( this ) == DialogResult.OK ) pathTextBox.Text = dlg.FileName;
	}

	private void HotkeyButton_Click( object? sender, EventArgs e ) {
		using var dlg = new HotkeyDialog( titleTextBox.Text, _hotkey, _ownHotkeys );
		if( dlg.ShowDialog( this ) != DialogResult.OK ) return;
		_hotkey = dlg.Hotkey;
		hotkeyTextBox.Text = _hotkey.ToDisplayString();
	}

	private void OkButton_Click( object? sender, EventArgs e ) {
		var title = titleTextBox.Text.Trim();
		if( scriptRadioButton.Checked ) {
			var path = pathTextBox.Text.Trim().Trim( '"' );
			if( path == "" ) {
				MessageBox.Show( this, "スクリプトファイルを指定してください。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning );
				return;
			}
			Slot = new() { Title = title, Path = Path.GetFullPath( path ), SeparateProcess = separateCheckBox.Checked, Hotkey = _hotkey };
		} else if( folderRadioButton.Checked ) {
			if( title == "" ) {
				MessageBox.Show( this, "階層名をタイトルに入力してください。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning );
				return;
			}
			Slot = new() { Title = title, Children = LauncherSlot.Normalize( _slot.Children?.Select( c => c.Clone() ).ToList() ) };
		} else {
			Slot = new() { Title = title };
		}
		DialogResult = DialogResult.OK;
	}
}
