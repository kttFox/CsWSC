namespace CsWSC.UI;

partial class EditorForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        menuStrip = new MenuStrip();
        fileMenu = new ToolStripMenuItem();
        newMenuItem = new ToolStripMenuItem();
        openMenuItem = new ToolStripMenuItem();
        saveMenuItem = new ToolStripMenuItem();
        saveAsMenuItem = new ToolStripMenuItem();
        fileSeparator = new ToolStripSeparator();
        closeMenuItem = new ToolStripMenuItem();
        runMenu = new ToolStripMenuItem();
        runMenuItem = new ToolStripMenuItem();
        editorTextBox = new TextBox();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        menuStrip.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        //
        // menuStrip
        //
        menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, runMenu });
        menuStrip.Location = new Point(0, 0);
        menuStrip.Name = "menuStrip";
        menuStrip.Size = new Size(784, 24);
        menuStrip.TabIndex = 0;
        //
        // fileMenu
        //
        fileMenu.DropDownItems.AddRange(new ToolStripItem[] { newMenuItem, openMenuItem, saveMenuItem, saveAsMenuItem, fileSeparator, closeMenuItem });
        fileMenu.Name = "fileMenu";
        fileMenu.Size = new Size(67, 20);
        fileMenu.Text = "ファイル(&F)";
        //
        // newMenuItem
        //
        newMenuItem.Name = "newMenuItem";
        newMenuItem.ShortcutKeys = Keys.Control | Keys.N;
        newMenuItem.Size = new Size(240, 22);
        newMenuItem.Text = "新規(&N)";
        newMenuItem.Click += NewMenuItem_Click;
        //
        // openMenuItem
        //
        openMenuItem.Name = "openMenuItem";
        openMenuItem.ShortcutKeys = Keys.Control | Keys.O;
        openMenuItem.Size = new Size(240, 22);
        openMenuItem.Text = "開く(&O)...";
        openMenuItem.Click += OpenMenuItem_Click;
        //
        // saveMenuItem
        //
        saveMenuItem.Name = "saveMenuItem";
        saveMenuItem.ShortcutKeys = Keys.Control | Keys.S;
        saveMenuItem.Size = new Size(240, 22);
        saveMenuItem.Text = "保存(&S)";
        saveMenuItem.Click += SaveMenuItem_Click;
        //
        // saveAsMenuItem
        //
        saveAsMenuItem.Name = "saveAsMenuItem";
        saveAsMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
        saveAsMenuItem.Size = new Size(240, 22);
        saveAsMenuItem.Text = "名前を付けて保存(&A)...";
        saveAsMenuItem.Click += SaveAsMenuItem_Click;
        //
        // fileSeparator
        //
        fileSeparator.Name = "fileSeparator";
        fileSeparator.Size = new Size(237, 6);
        //
        // closeMenuItem
        //
        closeMenuItem.Name = "closeMenuItem";
        closeMenuItem.Size = new Size(240, 22);
        closeMenuItem.Text = "閉じる(&C)";
        closeMenuItem.Click += CloseMenuItem_Click;
        //
        // runMenu
        //
        runMenu.DropDownItems.AddRange(new ToolStripItem[] { runMenuItem });
        runMenu.Name = "runMenu";
        runMenu.Size = new Size(58, 20);
        runMenu.Text = "実行(&R)";
        //
        // runMenuItem
        //
        runMenuItem.Name = "runMenuItem";
        runMenuItem.ShortcutKeys = Keys.F5;
        runMenuItem.Size = new Size(200, 22);
        runMenuItem.Text = "再生(&P)";
        runMenuItem.Click += RunMenuItem_Click;
        //
        // editorTextBox
        //
        editorTextBox.AcceptsTab = true;
        editorTextBox.Dock = DockStyle.Fill;
        editorTextBox.Font = new Font("ＭＳ ゴシック", 11F);
        editorTextBox.Location = new Point(0, 24);
        editorTextBox.Multiline = true;
        editorTextBox.Name = "editorTextBox";
        editorTextBox.ScrollBars = ScrollBars.Both;
        editorTextBox.Size = new Size(784, 515);
        editorTextBox.TabIndex = 1;
        editorTextBox.WordWrap = false;
        editorTextBox.TextChanged += EditorTextBox_TextChanged;
        //
        // statusStrip
        //
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
        statusStrip.Location = new Point(0, 539);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(784, 22);
        statusStrip.TabIndex = 2;
        //
        // statusLabel
        //
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(769, 17);
        statusLabel.Spring = true;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // EditorForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(784, 561);
        Controls.Add(editorTextBox);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);
        Font = new Font("Yu Gothic UI", 9F);
        MainMenuStrip = menuStrip;
        Icon = Properties.Resources.AppIcon;
        Name = "EditorForm";
        StartPosition = FormStartPosition.WindowsDefaultLocation;
        Text = "CsWSC エディタ";
        menuStrip.ResumeLayout(false);
        menuStrip.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private MenuStrip menuStrip;
    private ToolStripMenuItem fileMenu;
    private ToolStripMenuItem newMenuItem;
    private ToolStripMenuItem openMenuItem;
    private ToolStripMenuItem saveMenuItem;
    private ToolStripMenuItem saveAsMenuItem;
    private ToolStripSeparator fileSeparator;
    private ToolStripMenuItem closeMenuItem;
    private ToolStripMenuItem runMenu;
    private ToolStripMenuItem runMenuItem;
    private TextBox editorTextBox;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel statusLabel;
}
