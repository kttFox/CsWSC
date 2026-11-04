namespace CsWSC.UI;

/// <summary>
/// 1 つの機能に割り当てるホットキーを複数設定するダイアログです。1 つずつの設定は HotkeyDialog で行います。
/// </summary>
public partial class HotkeyListDialog : Form {
	private readonly string _name;
	private readonly IEnumerable<Hotkey> _ownHotkeys;
	private readonly List<Hotkey> _hotkeys;

	public HotkeyListDialog() : this( "", [], [] ) { }

	/// <param name="name">設定するホットキーの名前 (タイトルに出す) です。</param>
	/// <param name="hotkeys">初期値です。</param>
	/// <param name="ownHotkeys">CsWSC の設定にあるホットキー (HotkeyDialog に渡す) です。</param>
	public HotkeyListDialog( string name, IEnumerable<Hotkey> hotkeys, IEnumerable<Hotkey> ownHotkeys ) {
		InitializeComponent();
		_name = name;
		_ownHotkeys = ownHotkeys;
		_hotkeys = [.. hotkeys.Where( h => h.Enabled )];
		if( name != "" ) Text = $"ホットキーの一覧 - {name}";
		UpdateList( _hotkeys.Count > 0 ? 0 : -1 );
	}

	/// <summary>設定されたホットキーです。</summary>
	public List<Hotkey> Hotkeys => [.. _hotkeys];

	private void UpdateList( int selected ) {
		hotkeyListBox.BeginUpdate();
		hotkeyListBox.Items.Clear();
		foreach( var h in _hotkeys ) hotkeyListBox.Items.Add( h.ToDisplayString() );
		hotkeyListBox.EndUpdate();
		hotkeyListBox.SelectedIndex = Math.Min( selected, _hotkeys.Count - 1 );
		UpdateButtons();
	}

	private void UpdateButtons() => editButton.Enabled = removeButton.Enabled = hotkeyListBox.SelectedIndex >= 0;

	private void HotkeyListBox_SelectedIndexChanged( object? sender, EventArgs e ) => UpdateButtons();

	private void AddButton_Click( object? sender, EventArgs e ) {
		if( Edit( -1 ) is { } hotkey ) {
			_hotkeys.Add( hotkey );
			UpdateList( _hotkeys.Count - 1 );
		}
	}

	private void EditButton_Click( object? sender, EventArgs e ) {
		int i = hotkeyListBox.SelectedIndex;
		if( i < 0 || Edit( i ) is not { } hotkey ) return;
		// 「なし」にしたら一覧から外す
		if( hotkey.Enabled ) _hotkeys[i] = hotkey;
		else _hotkeys.RemoveAt( i );
		UpdateList( i );
	}

	private void RemoveButton_Click( object? sender, EventArgs e ) {
		int i = hotkeyListBox.SelectedIndex;
		if( i < 0 ) return;
		_hotkeys.RemoveAt( i );
		UpdateList( i );
	}

	/// <summary>index 番目 (-1 なら新規) のホットキーを HotkeyDialog で設定します。キャンセルされたか、一覧の他のキーと重複していれば null です。</summary>
	private Hotkey? Edit( int index ) {
		using var dlg = new HotkeyDialog( _name, index < 0 ? new( "", "なし" ) : _hotkeys[index], _ownHotkeys );
		if( dlg.ShowDialog( this ) != DialogResult.OK ) return null;
		var hotkey = dlg.Hotkey;
		if( index < 0 && !hotkey.Enabled ) return null;
		if( hotkey.Enabled && _hotkeys.Where( ( _, i ) => i != index ).Contains( hotkey ) ) {
			MessageBox.Show( this, $"ホットキー {hotkey} はすでに一覧にあります。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return null;
		}
		return hotkey;
	}
}
