namespace CsWSC.UI;

/// <summary>スクリプト編集窓です。再生はメイン画面 (MainForm) に依頼します。</summary>
public partial class EditorForm : Form {
	public const string FileFilter = "CsWSC スクリプト (*.csws)|*.csws|すべてのファイル (*.*)|*.*";

	private readonly MainForm? _host;
	private readonly SyntaxHighlighter _highlighter;
	private bool _dirty;

	public string? FilePath { get; private set; }

	public EditorForm() : this( null, null ) { }

	/// <param name="host">スクリプトを実行するメイン画面 (null なら実行できない) です。</param>
	/// <param name="path">開くファイル (null なら新規) です。</param>
	/// <param name="text">未保存の内容 (記録したスクリプトなど) を開く場合に指定します。</param>
	public EditorForm( MainForm? host, string? path, string? text = null ) {
		InitializeComponent();
		_host = host;

		SetupEditor();
		_highlighter = new SyntaxHighlighter( editorTextBox );
		FormClosed += ( _, _ ) => _highlighter.Dispose();
		FileDrop.Enable( this, OpenDropped, allowLibrary: true );
		FileDrop.Enable( editorTextBox, OpenDropped, allowLibrary: true );

		if( text != null ) {
			editorTextBox.Text = text.ReplaceLineEndings( "\r\n" );
			_dirty = true;
			UpdateTitle();
			_highlighter.Refresh();
			return;
		}
		if( path != null && File.Exists( path ) ) LoadFile( path );
		else {
			FilePath = path;
			// 改行は CRLF に揃える (保存時もこのまま書き出す)
			editorTextBox.Text = path == null ? SampleScript.ReplaceLineEndings( "\r\n" ) : "";
		}
		_dirty = false;
		UpdateTitle();
		_highlighter.Refresh();
	}

	private void SetupEditor() {
		var e = editorTextBox;
		var style = e.Styles[ScintillaNET.Style.Default];
		style.Font = "ＭＳ ゴシック";
		style.Size = 11;
		e.StyleClearAll(); // 既定スタイルのフォントを全スタイルへ反映する (色の設定より先に呼ぶ)
		e.Styles[ScintillaNET.Style.LineNumber].ForeColor = SystemColors.GrayText;
		e.EolMode = ScintillaNET.Eol.CrLf;
		e.TabWidth = 4;
		e.WrapMode = ScintillaNET.WrapMode.None;
		e.CaretLineBackColor = Color.FromArgb( 0xF0, 0xF4, 0xFF );

		// 行番号 (桁数に合わせて幅を変える)
		var margin = e.Margins[0];
		margin.Type = ScintillaNET.MarginType.Number;
		void UpdateMarginWidth() {
			var digits = Math.Max( 3, e.Lines.Count.ToString().Length );
			margin.Width = e.TextWidth( ScintillaNET.Style.LineNumber, new string( '9', digits ) ) + 12;
		}
		e.Margins[1].Width = 6; // 行番号と本文の間の余白
		e.TextChanged += ( _, _ ) => UpdateMarginWidth();
		UpdateMarginWidth();
	}

	public void GoToLine( int line ) {
		if( line < 1 || line > editorTextBox.Lines.Count ) return;
		var target = editorTextBox.Lines[line - 1];
		Activate();
		editorTextBox.Focus();
		target.Goto();
		// 改行を含めずに行を選択する
		editorTextBox.SetSelection( target.Position + target.Text.TrimEnd( '\r', '\n' ).Length, target.Position );
		editorTextBox.ScrollCaret();
	}

	public void SetStatus( string text ) => statusLabel.Text = text;

	// ---- イベントハンドラー ----

	private void NewMenuItem_Click( object sender, EventArgs e ) {
		if( !ConfirmDiscard() ) return;
		editorTextBox.ClearAll();
		FilePath = null;
		_dirty = false;
		UpdateTitle();
	}

	private void OpenMenuItem_Click( object sender, EventArgs e ) {
		if( !ConfirmDiscard() ) return;
		using var dlg = new OpenFileDialog { Filter = FileFilter };
		if( dlg.ShowDialog( this ) == DialogResult.OK ) LoadFile( dlg.FileName );
	}

	private void OpenDropped( string path ) {
		if( ConfirmDiscard() ) LoadFile( path );
	}

	private void SaveMenuItem_Click( object sender, EventArgs e ) => Save();
	private void SaveAsMenuItem_Click( object sender, EventArgs e ) => SaveAs();
	private void CloseMenuItem_Click( object sender, EventArgs e ) => Close();

	private async void RunMenuItem_Click( object sender, EventArgs e ) {
		if( _host == null ) return;
		// UWSC と同様、保存してから再生する
		if( _dirty && FilePath != null ) Save();
		await _host.RunAsync( editorTextBox.Text, this );
	}

	private void EditorTextBox_TextChanged( object sender, EventArgs e ) {
		_dirty = true;
		UpdateTitle();
	}

	// ---- ファイル操作 ----

	private void UpdateTitle() {
		var name = FilePath != null ? Path.GetFileName( FilePath ) : "無題";
		Text = $"{( _dirty ? "*" : "" )}{name} - CsWSC エディタ";
		// ライブラリ (.cswsl) は編集だけで、実行はできない
		runMenuItem.Enabled = _host != null && !( FilePath != null && ScriptLibraryResolver.IsLibrary( FilePath ) );
	}

	private bool ConfirmDiscard() {
		if( !_dirty ) return true;
		var r = MessageBox.Show( this, "変更を保存しますか？", "CsWSC", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question );
		return r switch {
			DialogResult.Yes => Save(),
			DialogResult.No => true,
			_ => false,
		};
	}

	private bool LoadFile( string path ) {
		try {
			editorTextBox.Text = File.ReadAllText( path ).ReplaceLineEndings( "\r\n" );
			FilePath = path;
			_dirty = false;
			UpdateTitle();
			if( !ScriptLibraryResolver.IsLibrary( path ) ) _host?.SetScript( path ); // ライブラリは再生対象にしない
			return true;
		} catch( Exception ex ) {
			MessageBox.Show( this, ex.Message, "CsWSC", MessageBoxButtons.OK, MessageBoxIcon.Error );
			return false;
		}
	}

	private bool Save() => FilePath == null ? SaveAs() : WriteFile( FilePath );

	private bool SaveAs() {
		using var dlg = new SaveFileDialog { Filter = FileFilter, FileName = FilePath ?? "script.csws" };
		return dlg.ShowDialog( this ) == DialogResult.OK && WriteFile( dlg.FileName );
	}

	private bool WriteFile( string path ) {
		try {
			File.WriteAllText( path, editorTextBox.Text );
			FilePath = path;
			_dirty = false;
			UpdateTitle();
			if( !ScriptLibraryResolver.IsLibrary( path ) ) _host?.SetScript( path ); // ライブラリは再生対象にしない
			return true;
		} catch( Exception ex ) {
			MessageBox.Show( this, ex.Message, "CsWSC", MessageBoxButtons.OK, MessageBoxIcon.Error );
			return false;
		}
	}

	protected override void OnFormClosing( FormClosingEventArgs e ) {
		if( !ConfirmDiscard() ) { e.Cancel = true; return; }
		base.OnFormClosing( e );
	}

	private const string SampleScript = """
        // CsWSC サンプル (C# スクリプト): メモ帳を起動して文字を入力
        // F5 で再生 / Pause キーで停止

        var notepad = Exec("notepad.exe") ?? GetWindow("メモ帳");
        if (notepad == null)
        {
            Print("メモ帳が見つかりませんでした");
            return;
        }

        notepad.Activate().MoveWindow(100, 100, 600, 400);
        Sleep(500);

        for (var i = 1; i <= 3; i++)
        {
            notepad.SendText($"こんにちは CsWSC! {i} 回目");
            Keyboard(Keys.Enter);
            Sleep(200);
        }

        Print($"完了: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
        """;
}
