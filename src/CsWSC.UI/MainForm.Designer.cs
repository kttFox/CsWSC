namespace CsWSC.UI;

partial class MainForm
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
	private void InitializeComponent() {
		this.components = new System.ComponentModel.Container();
		this.loadButton = new Button();
		this.saveButton = new Button();
		this.playButton = new Button();
		this.recordButton = new Button();
		this.settingsButton = new Button();
		this.settingsMenu = new ContextMenuStrip( this.components );
		this.optionsMenuItem = new ToolStripMenuItem();
		this.optionsSeparator = new ToolStripSeparator();
		this.editMenuItem = new ToolStripMenuItem();
		this.newScriptMenuItem = new ToolStripMenuItem();
		this.settingsSeparator1 = new ToolStripSeparator();
		this.topMostMenuItem = new ToolStripMenuItem();
		this.showPrintMenuItem = new ToolStripMenuItem();
		this.trayMenuItem = new ToolStripMenuItem();
		this.settingsSeparator2 = new ToolStripSeparator();
		this.apiListMenuItem = new ToolStripMenuItem();
		this.aboutMenuItem = new ToolStripMenuItem();
		this.settingsSeparator3 = new ToolStripSeparator();
		this.exitMenuItem = new ToolStripMenuItem();
		this.trayIcon = new NotifyIcon( this.components );
		this.toolTip = new ToolTip( this.components );
		this.flowLayoutPanel1 = new FlowLayoutPanel();
		this.settingsMenu.SuspendLayout();
		this.flowLayoutPanel1.SuspendLayout();
		this.SuspendLayout();
		// 
		// loadButton
		// 
		this.loadButton.Image = Properties.Resources.Load;
		this.loadButton.Location = new Point( 0, 0 );
		this.loadButton.Margin = new Padding( 0 );
		this.loadButton.Name = "loadButton";
		this.loadButton.Size = new Size( 38, 32 );
		this.loadButton.TabIndex = 0;
		this.toolTip.SetToolTip( this.loadButton, "読込み(L)" );
		this.loadButton.UseVisualStyleBackColor = true;
		this.loadButton.Click +=  this.LoadButton_Click ;
		// 
		// saveButton
		// 
		this.saveButton.Enabled = false;
		this.saveButton.Image = Properties.Resources.Save;
		this.saveButton.Location = new Point( 38, 0 );
		this.saveButton.Margin = new Padding( 0 );
		this.saveButton.Name = "saveButton";
		this.saveButton.Size = new Size( 38, 32 );
		this.saveButton.TabIndex = 1;
		this.toolTip.SetToolTip( this.saveButton, "保存(S)" );
		this.saveButton.UseVisualStyleBackColor = true;
		this.saveButton.Click +=  this.SaveButton_Click ;
		// 
		// playButton
		// 
		this.playButton.Enabled = false;
		this.playButton.Image = Properties.Resources.Play;
		this.playButton.Location = new Point( 76, 0 );
		this.playButton.Margin = new Padding( 0 );
		this.playButton.Name = "playButton";
		this.playButton.Size = new Size( 62, 32 );
		this.playButton.TabIndex = 2;
		this.toolTip.SetToolTip( this.playButton, "再生(P)" );
		this.playButton.UseVisualStyleBackColor = true;
		this.playButton.Click +=  this.PlayButton_Click ;
		// 
		// recordButton
		// 
		this.recordButton.Image = Properties.Resources.Record;
		this.recordButton.Location = new Point( 138, 0 );
		this.recordButton.Margin = new Padding( 0 );
		this.recordButton.Name = "recordButton";
		this.recordButton.Size = new Size( 38, 32 );
		this.recordButton.TabIndex = 3;
		this.toolTip.SetToolTip( this.recordButton, "記録(R)" );
		this.recordButton.UseVisualStyleBackColor = true;
		this.recordButton.Click +=  this.RecordButton_Click ;
		// 
		// settingsButton
		// 
		this.settingsButton.ContextMenuStrip = this.settingsMenu;
		this.settingsButton.Image = Properties.Resources.Settings;
		this.settingsButton.Location = new Point( 176, 0 );
		this.settingsButton.Margin = new Padding( 0 );
		this.settingsButton.Name = "settingsButton";
		this.settingsButton.Size = new Size( 38, 32 );
		this.settingsButton.TabIndex = 4;
		this.toolTip.SetToolTip( this.settingsButton, "設定(O)" );
		this.settingsButton.UseVisualStyleBackColor = true;
		this.settingsButton.Click +=  this.SettingsButton_Click ;
		// 
		// settingsMenu
		// 
		this.settingsMenu.Items.AddRange( new ToolStripItem[] { this.optionsMenuItem, this.optionsSeparator, this.editMenuItem, this.newScriptMenuItem, this.settingsSeparator1, this.topMostMenuItem, this.showPrintMenuItem, this.trayMenuItem, this.settingsSeparator2, this.apiListMenuItem, this.aboutMenuItem, this.settingsSeparator3, this.exitMenuItem } );
		this.settingsMenu.Name = "settingsMenu";
		this.settingsMenu.Size = new Size( 183, 226 );
		// 
		// optionsMenuItem
		// 
		this.optionsMenuItem.Name = "optionsMenuItem";
		this.optionsMenuItem.Size = new Size( 182, 22 );
		this.optionsMenuItem.Text = "設定(&O)...";
		this.optionsMenuItem.Click +=  this.OptionsMenuItem_Click ;
		// 
		// optionsSeparator
		// 
		this.optionsSeparator.Name = "optionsSeparator";
		this.optionsSeparator.Size = new Size( 179, 6 );
		// 
		// editMenuItem
		// 
		this.editMenuItem.Name = "editMenuItem";
		this.editMenuItem.Size = new Size( 182, 22 );
		this.editMenuItem.Text = "スクリプトを編集(&E)";
		this.editMenuItem.Click +=  this.EditMenuItem_Click ;
		// 
		// newScriptMenuItem
		// 
		this.newScriptMenuItem.Name = "newScriptMenuItem";
		this.newScriptMenuItem.Size = new Size( 182, 22 );
		this.newScriptMenuItem.Text = "新規作成(&N)";
		this.newScriptMenuItem.Click +=  this.NewScriptMenuItem_Click ;
		// 
		// settingsSeparator1
		// 
		this.settingsSeparator1.Name = "settingsSeparator1";
		this.settingsSeparator1.Size = new Size( 179, 6 );
		// 
		// topMostMenuItem
		// 
		this.topMostMenuItem.CheckOnClick = true;
		this.topMostMenuItem.Name = "topMostMenuItem";
		this.topMostMenuItem.Size = new Size( 182, 22 );
		this.topMostMenuItem.Text = "常に手前に表示(&T)";
		this.topMostMenuItem.Click +=  this.TopMostMenuItem_Click ;
		// 
		// showPrintMenuItem
		// 
		this.showPrintMenuItem.Name = "showPrintMenuItem";
		this.showPrintMenuItem.Size = new Size( 182, 22 );
		this.showPrintMenuItem.Text = "PRINT 窓を表示(&P)";
		this.showPrintMenuItem.Click +=  this.ShowPrintMenuItem_Click ;
		// 
		// trayMenuItem
		// 
		this.trayMenuItem.Name = "trayMenuItem";
		this.trayMenuItem.Size = new Size( 182, 22 );
		this.trayMenuItem.Text = "タスクトレイに収納(&I)";
		this.trayMenuItem.Click +=  this.TrayMenuItem_Click ;
		// 
		// settingsSeparator2
		// 
		this.settingsSeparator2.Name = "settingsSeparator2";
		this.settingsSeparator2.Size = new Size( 179, 6 );
		// 
		// apiListMenuItem
		// 
		this.apiListMenuItem.Name = "apiListMenuItem";
		this.apiListMenuItem.Size = new Size( 182, 22 );
		this.apiListMenuItem.Text = "組み込み API 一覧(&L)";
		this.apiListMenuItem.Click +=  this.ApiListMenuItem_Click ;
		// 
		// aboutMenuItem
		// 
		this.aboutMenuItem.Name = "aboutMenuItem";
		this.aboutMenuItem.Size = new Size( 182, 22 );
		this.aboutMenuItem.Text = "バージョン情報(&A)";
		this.aboutMenuItem.Click +=  this.AboutMenuItem_Click ;
		// 
		// settingsSeparator3
		// 
		this.settingsSeparator3.Name = "settingsSeparator3";
		this.settingsSeparator3.Size = new Size( 179, 6 );
		// 
		// exitMenuItem
		// 
		this.exitMenuItem.Name = "exitMenuItem";
		this.exitMenuItem.Size = new Size( 182, 22 );
		this.exitMenuItem.Text = "終了(&X)";
		this.exitMenuItem.Click +=  this.ExitMenuItem_Click ;
		// 
		// trayIcon
		// 
		this.trayIcon.Icon = Properties.Resources.AppIcon;
		this.trayIcon.Text = "CsWSC";
		this.trayIcon.MouseClick +=  this.TrayIcon_MouseClick ;
		// 
		// flowLayoutPanel1
		// 
		this.flowLayoutPanel1.AutoSize = true;
		this.flowLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		this.flowLayoutPanel1.Controls.Add( this.loadButton );
		this.flowLayoutPanel1.Controls.Add( this.saveButton );
		this.flowLayoutPanel1.Controls.Add( this.playButton );
		this.flowLayoutPanel1.Controls.Add( this.recordButton );
		this.flowLayoutPanel1.Controls.Add( this.settingsButton );
		this.flowLayoutPanel1.Dock = DockStyle.Fill;
		this.flowLayoutPanel1.Location = new Point( 0, 0 );
		this.flowLayoutPanel1.Name = "flowLayoutPanel1";
		this.flowLayoutPanel1.Size = new Size( 214, 32 );
		this.flowLayoutPanel1.TabIndex = 5;
		// 
		// MainForm
		// 
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.AutoSize = true;
		this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		this.ClientSize = new Size( 214, 32 );
		this.Controls.Add( this.flowLayoutPanel1 );
		this.Font = new Font( "Yu Gothic UI", 9F );
		this.FormBorderStyle = FormBorderStyle.FixedSingle;
		this.Icon = Properties.Resources.AppIcon;
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.Name = "MainForm";
		this.StartPosition = FormStartPosition.CenterScreen;
		this.Text = "CsWSC";
		this.settingsMenu.ResumeLayout( false );
		this.flowLayoutPanel1.ResumeLayout( false );
		this.ResumeLayout( false );
		this.PerformLayout();
	}

	#endregion

	private Button loadButton;
    private Button saveButton;
    private Button playButton;
    private Button recordButton;
    private Button settingsButton;
    private ContextMenuStrip settingsMenu;
    private ToolStripMenuItem editMenuItem;
    private ToolStripMenuItem newScriptMenuItem;
    private ToolStripSeparator settingsSeparator1;
    private ToolStripMenuItem topMostMenuItem;
    private ToolStripMenuItem showPrintMenuItem;
    private ToolStripSeparator settingsSeparator2;
    private ToolStripMenuItem apiListMenuItem;
    private ToolStripMenuItem aboutMenuItem;
    private ToolStripSeparator settingsSeparator3;
    private ToolStripMenuItem exitMenuItem;
    private ToolTip toolTip;
	private FlowLayoutPanel flowLayoutPanel1;
	private ToolStripMenuItem optionsMenuItem;
	private ToolStripSeparator optionsSeparator;
	private NotifyIcon trayIcon;
	private ToolStripMenuItem trayMenuItem;
}
