namespace CsWSC.UI;

/// <summary>スクリプト編集窓。再生はメイン窓 (MainForm) に依頼する。</summary>
public partial class EditorForm : Form
{
    public const string FileFilter = "CsWSC スクリプト (*.csws)|*.csws|すべてのファイル (*.*)|*.*";

    private readonly MainForm? _host;
    private bool _dirty;

    public string? FilePath { get; private set; }

    public EditorForm() : this(null, null) { }

    /// <param name="text">未保存の内容 (記録したスクリプトなど) を開く場合に指定。</param>
    public EditorForm(MainForm? host, string? path, string? text = null)
    {
        InitializeComponent();
        _host = host;

        // Fill の TextBox より後ろ (ドッキングでは先に配置される側) に行番号を置く
        var gutter = new LineNumberGutter(editorTextBox);
        Controls.Add(gutter);
        Controls.SetChildIndex(gutter, Controls.GetChildIndex(editorTextBox) + 1);

        if (text != null)
        {
            editorTextBox.Text = text.ReplaceLineEndings("\r\n");
            _dirty = true;
            UpdateTitle();
            return;
        }
        if (path != null && File.Exists(path)) LoadFile(path);
        else
        {
            FilePath = path;
            // TextBox は CRLF でないと改行表示されないため揃える
            editorTextBox.Text = path == null ? SampleScript.ReplaceLineEndings("\r\n") : "";
        }
        _dirty = false;
        UpdateTitle();
    }

    public void GoToLine(int line)
    {
        int index = editorTextBox.GetFirstCharIndexFromLine(Math.Max(0, line - 1));
        if (index < 0) return;
        Activate();
        editorTextBox.Focus();
        editorTextBox.Select(index, editorTextBox.Lines.ElementAtOrDefault(line - 1)?.Length ?? 0);
        editorTextBox.ScrollToCaret();
    }

    public void SetStatus(string text) => statusLabel.Text = text;

    // ---- イベントハンドラー ----

    private void NewMenuItem_Click(object sender, EventArgs e)
    {
        if (!ConfirmDiscard()) return;
        editorTextBox.Clear();
        FilePath = null;
        _dirty = false;
        UpdateTitle();
    }

    private void OpenMenuItem_Click(object sender, EventArgs e)
    {
        if (!ConfirmDiscard()) return;
        using var dlg = new OpenFileDialog { Filter = FileFilter };
        if (dlg.ShowDialog(this) == DialogResult.OK) LoadFile(dlg.FileName);
    }

    private void SaveMenuItem_Click(object sender, EventArgs e) => Save();
    private void SaveAsMenuItem_Click(object sender, EventArgs e) => SaveAs();
    private void CloseMenuItem_Click(object sender, EventArgs e) => Close();

    private async void RunMenuItem_Click(object sender, EventArgs e)
    {
        if (_host == null) return;
        // UWSC と同様、保存してから再生する
        if (_dirty && FilePath != null) Save();
        await _host.RunAsync(editorTextBox.Text, this);
    }

    private void EditorTextBox_TextChanged(object sender, EventArgs e)
    {
        _dirty = true;
        UpdateTitle();
    }

    // ---- ファイル操作 ----

    private void UpdateTitle()
    {
        var name = FilePath != null ? Path.GetFileName(FilePath) : "無題";
        Text = $"{(_dirty ? "*" : "")}{name} - CsWSC エディタ";
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        var r = MessageBox.Show(this, "変更を保存しますか？", "CsWSC", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return r switch
        {
            DialogResult.Yes => Save(),
            DialogResult.No => true,
            _ => false,
        };
    }

    private bool LoadFile(string path)
    {
        try
        {
            editorTextBox.Text = File.ReadAllText(path).ReplaceLineEndings("\r\n");
            FilePath = path;
            _dirty = false;
            UpdateTitle();
            _host?.SetScript(path);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CsWSC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool Save() => FilePath == null ? SaveAs() : WriteFile(FilePath);

    private bool SaveAs()
    {
        using var dlg = new SaveFileDialog { Filter = FileFilter, FileName = FilePath ?? "script.csws" };
        return dlg.ShowDialog(this) == DialogResult.OK && WriteFile(dlg.FileName);
    }

    private bool WriteFile(string path)
    {
        try
        {
            File.WriteAllText(path, editorTextBox.Text);
            FilePath = path;
            _dirty = false;
            UpdateTitle();
            _host?.SetScript(path);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "CsWSC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmDiscard()) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }

    private const string SampleScript = """
        // CsWSC サンプル (C# スクリプト): メモ帳を起動して文字を入力
        // F5 で再生 / Pause キーで停止

        var notepad = Exec("notepad.exe") ?? GetWindow("メモ帳", timeout: 5);
        if (notepad == null)
        {
            Print("メモ帳が見つかりませんでした");
            return;
        }

        notepad.Activate().Move(100, 100, 600, 400);
        Sleep(0.5);

        for (var i = 1; i <= 3; i++)
        {
            notepad.SendText($"こんにちは CsWSC! {i} 回目");
            Kbd(Keys.Enter);
            Sleep(0.2);
        }

        Print($"完了: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
        """;
}
