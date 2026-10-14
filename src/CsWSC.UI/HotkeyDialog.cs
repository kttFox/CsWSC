using System.Runtime.InteropServices;

namespace CsWSC.UI;

/// <summary>
/// ホットキーを 1 つ設定するダイアログです。
/// グローバルホットキーは修飾キー (必須) とキーを選び、低レベルキーボードフックは実際にキーを押して設定します。
/// </summary>
public partial class HotkeyDialog : Form {
	[DllImport( "user32.dll" )] private static extern bool RegisterHotKey( IntPtr hWnd, int id, uint mod, uint vk );
	[DllImport( "user32.dll" )] private static extern bool UnregisterHotKey( IntPtr hWnd, int id );

	/// <summary>CsWSC 自身が登録しているグローバルホットキー (別プロセスの CsWSC が登録中でも「使用中」にしない) です。</summary>
	private readonly HashSet<Hotkey> _ownHotkeys = [];

	public HotkeyDialog() : this( "", new( "", "なし" ), [] ) { }

	/// <param name="title">設定するホットキーの名前 (タイトルに出す) です。</param>
	/// <param name="hotkey">初期値です。</param>
	/// <param name="ownHotkeys">CsWSC の設定にあるホットキーです。</param>
	public HotkeyDialog( string title, Hotkey hotkey, IEnumerable<Hotkey> ownHotkeys ) {
		InitializeComponent();
		_ownHotkeys = ownHotkeys.Where( h => h.Enabled && !h.PassThrough ).ToHashSet();
		if( title != "" ) Text = $"ホットキーの設定 - {title}";

		keyComboBox.Items.AddRange( Hotkey.Keys );
		hotkeyBox.Hotkey = hotkey;
		SelectGlobal( hotkey );
		if( !hotkey.Enabled ) noneRadioButton.Checked = true;
		else if( hotkey.PassThrough ) hookRadioButton.Checked = true;
		else globalRadioButton.Checked = true;
		UpdateMode();
	}

	/// <summary>修飾キーのチェックボックス (Hotkey.Modifier の並び順 CTRL+ALT+SHIFT+WIN) です。</summary>
	private (string Name, CheckBox Check)[] ModifierChecks => [
		("CTRL", ctrlCheckBox),
		("ALT", altCheckBox),
		("SHIFT", shiftCheckBox),
		("WIN", winCheckBox),
	];

	/// <summary>設定されたホットキーです。</summary>
	public Hotkey Hotkey => noneRadioButton.Checked ? new Hotkey( "", "なし" )
		: globalRadioButton.Checked ? new Hotkey( string.Join( "+", ModifierChecks.Where( m => m.Check.Checked ).Select( m => m.Name ) ), (string?)keyComboBox.SelectedItem ?? "なし" )
		: hotkeyBox.Hotkey with { PassThrough = true };

	/// <summary>グローバルホットキーの入力欄にホットキーを反映します。キーが一覧に無ければ「なし」です。</summary>
	private void SelectGlobal( Hotkey hotkey ) {
		var mods = hotkey.Modifier.Split( '+' );
		foreach( var (name, check) in ModifierChecks ) check.Checked = mods.Contains( name );
		keyComboBox.SelectedItem = hotkey.Key;
		if( keyComboBox.SelectedIndex < 0 ) keyComboBox.SelectedIndex = 0;
	}

	private void RadioButton_CheckedChanged( object? sender, EventArgs e ) => UpdateMode();

	private void Input_Changed( object? sender, EventArgs e ) => UpdateStatus();

	/// <summary>選ばれていない登録方式の入力欄はグレーにします。</summary>
	private void UpdateMode() {
		bool global = globalRadioButton.Checked, hook = hookRadioButton.Checked;
		foreach( var (_, check) in ModifierChecks ) check.Enabled = global;
		plusLabel.Enabled = keyComboBox.Enabled = global;
		hotkeyBox.Enabled = hook;
		hotkeyBox.BackColor = hook ? SystemColors.Window : SystemColors.Control;
		UpdateStatus();
	}

	protected override void OnShown( EventArgs e ) {
		base.OnShown( e );
		if( hookRadioButton.Checked ) hotkeyBox.Focus();
		else if( globalRadioButton.Checked ) ctrlCheckBox.Focus();
		else noneRadioButton.Focus();
		UpdateStatus();
	}

	/// <summary>グローバルホットキーとして登録できるかを試し、結果を表示します。</summary>
	private void UpdateStatus() {
		var hotkey = Hotkey;
		bool ok = true;
		string text = "";
		// 状態はグローバルホットキーの欄の下に出す (フック・なしは常に OK)
		if( !globalRadioButton.Checked || !hotkey.Enabled ) {
		} else if( hotkey.Modifier == "" ) {
			ok = false;
			text = "✖ 修飾キーを 1 つ以上選んでください。";
		} else if( IsHandleCreated && hotkey.TryGetVirtualKey( out var vk ) ) {
			// 実際に登録してみてすぐ解除する (CsWSC 自身のホットキーは設定中は解除されている)
			const int TestId = 0xBFFF;
			ok = RegisterHotKey( Handle, TestId, hotkey.ModifierFlags | Hotkey.MOD_NOREPEAT, vk );
			if( ok ) UnregisterHotKey( Handle, TestId );
			// 登録元は調べられないので、CsWSC の設定にあるキーなら自分 (スクリプト実行中の別プロセスなど) とみなす
			else ok = _ownHotkeys.Contains( hotkey );
			text = ok ? "✔ 登録できます。" : "✖ 他のアプリ (または Windows) が使用中のため登録できません。";
		}
		statusLabel.Text = text;
		statusLabel.ForeColor = ok ? SystemColors.ControlText : Color.Firebrick;
		okButton.Enabled = ok;
	}
}
