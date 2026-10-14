namespace CsWSC.UI;

/// <summary>
/// ランチャーの設定ダイアログです。UWSC と同じく 0〜9, A〜Z の 36 枠にスクリプトや階層を割り当てます。
/// 基本は「対象アプリなし」のランチャーで、対象アプリを追加するとそのアプリ用のランチャー (36 枠とメニューを開くホットキー) を持てます。
/// </summary>
public partial class LauncherForm : Form {
	private readonly AppSettings _settings;
	/// <summary>
	/// 編集中のランチャー (OK を押すまで設定には反映しない) です。[0] が対象アプリなし、以降が対象アプリごとです。
	/// どれも Children が 36 枠、Hotkey がメニューを開くホットキー、TargetApps / TargetTitle が対象アプリの条件です。
	/// </summary>
	private readonly List<LauncherSlot> _pages;
	private int _page;
	/// <summary>開いている階層 (空ならルート) です。</summary>
	private readonly List<LauncherSlot> _levels = [];
	/// <summary>コードから入力欄を書き換えている間は、イベントで編集内容に反映しません。</summary>
	private bool _updating;

	public LauncherForm() : this( new AppSettings() ) { }

	/// <param name="settings">編集するランチャーを持つアプリ設定です。</param>
	/// <param name="profile">最初に開く対象アプリのランチャー (null なら対象アプリなし) です。</param>
	public LauncherForm( AppSettings settings, LauncherSlot? profile = null ) {
		InitializeComponent();
		_settings = settings;
		// 列の幅を一覧の幅に合わせる (横スクロールバーを出さない)
		listView.ClientSizeChanged += ( _, _ ) => FitColumns();
		FitColumns();
		_pages = [
			new() { Children = LauncherSlot.Normalize( settings.Launchers.Select( s => s.Clone() ).ToList() ), Hotkey = settings.LauncherMenuHotkey },
			.. settings.LauncherProfiles.Select( p => {
				var page = p.Clone();
				page.Children = LauncherSlot.Normalize( page.Children );
				return page;
			} ),
		];
		RefreshProfiles( profile == null ? 0 : settings.LauncherProfiles.IndexOf( profile ) + 1 );
	}

	private LauncherSlot Page => _pages[_page];

	/// <summary>キーの列は固定幅、残りをタイトル : 内容 : ホットキー = 4 : 3 : 3 で分けます。</summary>
	private void FitColumns() {
		int rest = Math.Max( 0, listView.ClientSize.Width - keyColumnHeader.Width );
		titleColumnHeader.Width = rest * 4 / 10;
		contentColumnHeader.Width = rest * 3 / 10;
		hotkeyColumnHeader.Width = rest - titleColumnHeader.Width - contentColumnHeader.Width;
	}

	private List<LauncherSlot> Current => _levels.Count == 0 ? Page.Children! : _levels[^1].Children!;

	private int SelectedIndex => listView.SelectedIndices.Count > 0 ? listView.SelectedIndices[0] : -1;

	private static string PageName( int index, LauncherSlot page ) => index == 0 ? "(対象アプリなし)" : page.ProfileName;

	/// <summary>保存済みの設定で CsWSC が登録しているホットキー (HotkeyDialog の「使用中」判定用) です。</summary>
	private IEnumerable<Hotkey> OwnHotkeys() =>
		[_settings.PlayHotkey, _settings.StopHotkey, _settings.RecordHotkey, _settings.TrayHotkey, .. _settings.LauncherHotkeys()];

	// ---- 対象アプリ ----

	private void RefreshProfiles( int select ) {
		_updating = true;
		profileComboBox.BeginUpdate();
		profileComboBox.Items.Clear();
		for( int i = 0; i < _pages.Count; i++ ) profileComboBox.Items.Add( PageName( i, _pages[i] ) );
		profileComboBox.EndUpdate();
		profileComboBox.SelectedIndex = Math.Clamp( select, 0, _pages.Count - 1 );
		_updating = false;
		ShowPage();
	}

	private void ProfileComboBox_SelectedIndexChanged( object? sender, EventArgs e ) {
		if( _updating || profileComboBox.SelectedIndex < 0 ) return;
		ShowPage();
	}

	/// <summary>コンボボックスで選んだランチャーを表示します。</summary>
	private void ShowPage() {
		_page = Math.Max( 0, profileComboBox.SelectedIndex );
		_levels.Clear();
		_updating = true;
		targetAppsTextBox.Text = Page.TargetApps;
		targetTitleTextBox.Text = Page.TargetTitle;
		_updating = false;
		// 対象アプリなしには条件が無い
		targetAppsLabel.Enabled = targetAppsTextBox.Enabled = targetListButton.Enabled
			= targetTitleLabel.Enabled = targetTitleTextBox.Enabled
			= removeProfileButton.Enabled = _page > 0;
		// 前面の窓に合う最初の対象アプリを使うので、並び順が優先順位になる (対象アプリなしは先頭に固定)
		profileUpButton.Enabled = _page > 1;
		profileDownButton.Enabled = _page > 0 && _page < _pages.Count - 1;
		menuHotkeyTextBox.Text = Page.Hotkey.ToDisplayString();
		RefreshList( 0 );
	}

	private void AddProfileButton_Click( object? sender, EventArgs e ) {
		_pages.Add( new() { Children = LauncherSlot.Normalize( null ), Hotkey = new( "", "なし" ) } );
		RefreshProfiles( _pages.Count - 1 );
		targetAppsTextBox.Focus();
	}

	private void RemoveProfileButton_Click( object? sender, EventArgs e ) {
		if( _page == 0 ) return;
		if( LauncherSlot.All( Page.Children! ).Any( s => !s.IsEmpty )
			&& MessageBox.Show( this, $"対象アプリ「{Page.ProfileName}」のランチャーを削除します。よろしいですか?", Text,
				MessageBoxButtons.OKCancel, MessageBoxIcon.Question ) != DialogResult.OK ) return;
		int page = _page;
		_pages.RemoveAt( page );
		RefreshProfiles( page - 1 );
	}

	private void ProfileUpButton_Click( object? sender, EventArgs e ) => MoveProfile( -1 );
	private void ProfileDownButton_Click( object? sender, EventArgs e ) => MoveProfile( +1 );

	private void MoveProfile( int delta ) {
		int i = _page, j = i + delta;
		if( i < 1 || j < 1 || j >= _pages.Count ) return;
		(_pages[i], _pages[j]) = (_pages[j], _pages[i]);
		RefreshProfiles( j );
	}

	private void TargetTextBox_TextChanged( object? sender, EventArgs e ) {
		if( _updating || _page == 0 ) return;
		Page.TargetApps = targetAppsTextBox.Text.Trim();
		Page.TargetTitle = targetTitleTextBox.Text.Trim();
		// コンボボックスの表示名も合わせる (項目の差し替えで選択が変わらないよう抑止する)
		_updating = true;
		profileComboBox.Items[_page] = PageName( _page, Page );
		profileComboBox.SelectedIndex = _page;
		_updating = false;
	}

	private void TargetListButton_Click( object? sender, EventArgs e ) => ShowAppList( targetListButton, targetAppsTextBox );

	/// <summary>いま窓を出しているアプリのプロセス名を button の下に一覧し、選んだものを targetAppsTextBox に追加します。</summary>
	private static void ShowAppList( Button targetListButton, TextBox targetAppsTextBox ) {
		var menu = new ContextMenuStrip();
		menu.Closed += ( _, _ ) => targetListButton.BeginInvoke( menu.Dispose );
		var names = System.Diagnostics.Process.GetProcesses()
			.Where( p => { try { return p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle != "" && p.Id != Environment.ProcessId; } catch( Exception ) { return false; } } )
			.GroupBy( p => p.ProcessName, StringComparer.OrdinalIgnoreCase )
			.OrderBy( g => g.Key, StringComparer.OrdinalIgnoreCase );
		foreach( var g in names ) {
			var name = g.Key + ".exe";
			var item = new ToolStripMenuItem( $"{name}  -  {g.First().MainWindowTitle}" );
			item.Click += ( _, _ ) => {
				var apps = targetAppsTextBox.Text.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries ).ToList();
				// .exe なしで書かれた同じ名前も重複とみなす
				if( !apps.Any( a => a.Equals( name, StringComparison.OrdinalIgnoreCase ) || a.Equals( g.Key, StringComparison.OrdinalIgnoreCase ) ) ) apps.Add( name );
				targetAppsTextBox.Text = string.Join( "; ", apps );
			};
			menu.Items.Add( item );
		}
		if( menu.Items.Count == 0 ) menu.Items.Add( new ToolStripMenuItem( "(窓を出しているアプリがありません)" ) { Enabled = false } );
		menu.Show( targetListButton, 0, targetListButton.Height );
	}

	private void MenuHotkeyButton_Click( object? sender, EventArgs e ) {
		using var dlg = new HotkeyDialog( $"メニュー - {PageName( _page, Page )}", Page.Hotkey, OwnHotkeys() );
		if( dlg.ShowDialog( this ) != DialogResult.OK ) return;
		Page.Hotkey = dlg.Hotkey;
		menuHotkeyTextBox.Text = Page.Hotkey.ToDisplayString();
	}

	// ---- 枠 ----

	private void ListView_SelectedIndexChanged( object? sender, EventArgs e ) => UpdateButtons();
	private void ListView_DoubleClick( object? sender, EventArgs e ) => OpenOrEdit();
	private void EditButton_Click( object? sender, EventArgs e ) => EditSelected();
	private void OpenButton_Click( object? sender, EventArgs e ) => OpenSelected();
	private void ParentButton_Click( object? sender, EventArgs e ) => GoParent();
	private void ClearButton_Click( object? sender, EventArgs e ) => ClearSelected();
	private void UpButton_Click( object? sender, EventArgs e ) => MoveSelected( -1 );
	private void DownButton_Click( object? sender, EventArgs e ) => MoveSelected( +1 );

	private void ListView_KeyDown( object? sender, KeyEventArgs e ) {
		switch( e.KeyCode ) {
			case Keys.Enter: OpenOrEdit(); break;
			case Keys.Back: GoParent(); break;
			case Keys.Delete: ClearSelected(); break;
			default: return;
		}
		e.Handled = e.SuppressKeyPress = true;
	}

	private void RefreshList( int select ) {
		var slots = Current;
		listView.BeginUpdate();
		listView.Items.Clear();
		for( int i = 0; i < slots.Count; i++ ) {
			var s = slots[i];
			string content = s.IsFolder ? "▶ 階層" : s.IsScript ? Path.GetFileName( s.Path ) : s.Title != "" ? "(見出し)" : "";
			if( s.IsScript && s.SeparateProcess ) content += " [別プロセス]";
			listView.Items.Add( new ListViewItem( [$"{LauncherSlot.KeyOf( i )}.", s.DisplayName, content, s.IsScript ? s.Hotkey.ToDisplayString() : ""] ) {
				ToolTipText = s.IsScript ? s.Path : "",
				ForeColor = s.IsScript && !File.Exists( s.Path ) ? Color.Firebrick : SystemColors.WindowText,
			} );
		}
		listView.EndUpdate();
		levelLabel.Text = string.Join( " > ", _levels.Select( l => l.Title ).Prepend( "ルート" ) );
		select = Math.Clamp( select, 0, slots.Count - 1 );
		listView.Items[select].Selected = listView.Items[select].Focused = true;
		listView.Items[select].EnsureVisible();
		UpdateButtons();
	}

	private void UpdateButtons() {
		int i = SelectedIndex;
		bool has = i >= 0;
		editButton.Enabled = has;
		openButton.Enabled = has && Current[i].IsFolder;
		clearButton.Enabled = has && !Current[i].IsEmpty;
		upButton.Enabled = i > 0;
		downButton.Enabled = has && i < Current.Count - 1;
		parentButton.Enabled = _levels.Count > 0;
	}

	private void OpenOrEdit() {
		int i = SelectedIndex;
		if( i < 0 ) return;
		if( Current[i].IsFolder ) OpenSelected();
		else EditSelected();
	}

	private void EditSelected() {
		int i = SelectedIndex;
		if( i < 0 ) return;
		using var dlg = new LauncherSlotDialog( i, Current[i], OwnHotkeys() );
		if( dlg.ShowDialog( this ) != DialogResult.OK ) return;
		Current[i] = dlg.Slot;
		RefreshList( i );
	}

	private void OpenSelected() {
		int i = SelectedIndex;
		if( i < 0 || !Current[i].IsFolder ) return;
		_levels.Add( Current[i] );
		RefreshList( 0 );
	}

	private void GoParent() {
		if( _levels.Count == 0 ) return;
		var folder = _levels[^1];
		_levels.RemoveAt( _levels.Count - 1 );
		RefreshList( Current.IndexOf( folder ) );
	}

	private void ClearSelected() {
		int i = SelectedIndex;
		if( i < 0 || Current[i].IsEmpty ) return;
		if( Current[i].IsFolder && LauncherSlot.All( Current[i].Children! ).Any( s => !s.IsEmpty )
			&& MessageBox.Show( this, $"階層「{Current[i].Title}」の中身もすべて消えます。よろしいですか?", Text,
				MessageBoxButtons.OKCancel, MessageBoxIcon.Question ) != DialogResult.OK ) return;
		Current[i] = new();
		RefreshList( i );
	}

	private void MoveSelected( int delta ) {
		int i = SelectedIndex, j = i + delta;
		var slots = Current;
		if( i < 0 || j < 0 || j >= slots.Count ) return;
		(slots[i], slots[j]) = (slots[j], slots[i]);
		RefreshList( j );
	}

	// ---- OK ----

	private void OkButton_Click( object? sender, EventArgs e ) {
		// 対象アプリは条件が必要
		int noTarget = _pages.FindIndex( 1, p => !p.HasTarget );
		if( noTarget > 0 ) {
			MessageBox.Show( this, "対象アプリのプロセス名か窓タイトルを入力してください。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning );
			profileComboBox.SelectedIndex = noTarget;
			targetAppsTextBox.Focus();
			return;
		}
		// 再生・停止・記録・トレイとは重複させない。ランチャーどうしは対象アプリが違えば同じキーを使える
		var reserved = new[] { _settings.PlayHotkey, _settings.StopHotkey, _settings.RecordHotkey, _settings.TrayHotkey };
		var entries = LauncherSlot.HotkeyEntries( _pages[0].Hotkey, _pages[0].Children!, _pages.Skip( 1 ) );
		if( LauncherSlot.CheckHotkeys( entries, reserved ) is { } error ) {
			MessageBox.Show( this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning );
			return;
		}
		_settings.Launchers = _pages[0].Children!;
		_settings.LauncherMenuHotkey = _pages[0].Hotkey;
		_settings.LauncherProfiles = [.. _pages.Skip( 1 )];
		_settings.Save();
		DialogResult = DialogResult.OK;
	}
}
