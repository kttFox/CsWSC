namespace CsWSC.UI;

partial class LauncherSlotDialog {
	/// <summary>
	///  Required designer variable.
	/// </summary>
	private System.ComponentModel.IContainer components = null;

	/// <summary>
	///  Clean up any resources being used.
	/// </summary>
	/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
	protected override void Dispose( bool disposing ) {
		if( disposing && ( components != null ) ) {
			components.Dispose();
		}
		base.Dispose( disposing );
	}

	#region Windows Form Designer generated code

	/// <summary>
	///  Required method for Designer support - do not modify
	///  the contents of this method with the code editor.
	/// </summary>
	private void InitializeComponent() {
		this.titleLabel = new Label();
		this.titleTextBox = new TextBox();
		this.noneRadioButton = new RadioButton();
		this.scriptRadioButton = new RadioButton();
		this.pathTextBox = new TextBox();
		this.browseButton = new Button();
		this.folderRadioButton = new RadioButton();
		this.separateCheckBox = new CheckBox();
		this.hotkeyLabel = new Label();
		this.hotkeyTextBox = new TextBox();
		this.hotkeyButton = new Button();
		this.okButton = new Button();
		this.cancelButton = new Button();
		this.SuspendLayout();
		//
		// titleLabel
		//
		this.titleLabel.AutoSize = true;
		this.titleLabel.Location = new Point( 12, 15 );
		this.titleLabel.Name = "titleLabel";
		this.titleLabel.Size = new Size( 66, 15 );
		this.titleLabel.TabIndex = 0;
		this.titleLabel.Text = "タイトル(&T)";
		//
		// titleTextBox
		//
		this.titleTextBox.Location = new Point( 100, 12 );
		this.titleTextBox.Name = "titleTextBox";
		this.titleTextBox.Size = new Size( 348, 23 );
		this.titleTextBox.TabIndex = 1;
		//
		// noneRadioButton
		//
		this.noneRadioButton.AutoSize = true;
		this.noneRadioButton.Location = new Point( 12, 48 );
		this.noneRadioButton.Name = "noneRadioButton";
		this.noneRadioButton.Size = new Size( 290, 19 );
		this.noneRadioButton.TabIndex = 2;
		this.noneRadioButton.Text = "設定なし(&N)  ※タイトルだけなら見出し行になります";
		this.noneRadioButton.UseVisualStyleBackColor = true;
		this.noneRadioButton.CheckedChanged += this.RadioButton_CheckedChanged;
		//
		// scriptRadioButton
		//
		this.scriptRadioButton.AutoSize = true;
		this.scriptRadioButton.Location = new Point( 12, 76 );
		this.scriptRadioButton.Name = "scriptRadioButton";
		this.scriptRadioButton.Size = new Size( 130, 19 );
		this.scriptRadioButton.TabIndex = 3;
		this.scriptRadioButton.Text = "スクリプトファイル(&F)";
		this.scriptRadioButton.UseVisualStyleBackColor = true;
		this.scriptRadioButton.CheckedChanged += this.RadioButton_CheckedChanged;
		//
		// pathTextBox
		//
		this.pathTextBox.Location = new Point( 32, 102 );
		this.pathTextBox.Name = "pathTextBox";
		this.pathTextBox.Size = new Size( 330, 23 );
		this.pathTextBox.TabIndex = 4;
		//
		// browseButton
		//
		this.browseButton.Location = new Point( 368, 101 );
		this.browseButton.Name = "browseButton";
		this.browseButton.Size = new Size( 80, 25 );
		this.browseButton.TabIndex = 5;
		this.browseButton.Text = "参照(&B)...";
		this.browseButton.UseVisualStyleBackColor = true;
		this.browseButton.Click += this.BrowseButton_Click;
		//
		// folderRadioButton
		//
		this.folderRadioButton.AutoSize = true;
		this.folderRadioButton.Location = new Point( 12, 198 );
		this.folderRadioButton.Name = "folderRadioButton";
		this.folderRadioButton.Size = new Size( 290, 19 );
		this.folderRadioButton.TabIndex = 10;
		this.folderRadioButton.Text = "階層化(&H)  ※タイトルがサブメニューの名前になります";
		this.folderRadioButton.UseVisualStyleBackColor = true;
		this.folderRadioButton.CheckedChanged += this.RadioButton_CheckedChanged;
		//
		// separateCheckBox
		//
		this.separateCheckBox.AutoSize = true;
		this.separateCheckBox.Location = new Point( 32, 134 );
		this.separateCheckBox.Name = "separateCheckBox";
		this.separateCheckBox.Size = new Size( 330, 19 );
		this.separateCheckBox.TabIndex = 6;
		this.separateCheckBox.Text = "別プロセスで実行(&P)  ※再生中でも並行して起動します";
		this.separateCheckBox.UseVisualStyleBackColor = true;
		//
		// hotkeyLabel
		//
		this.hotkeyLabel.AutoSize = true;
		this.hotkeyLabel.Location = new Point( 32, 165 );
		this.hotkeyLabel.Name = "hotkeyLabel";
		this.hotkeyLabel.Size = new Size( 59, 15 );
		this.hotkeyLabel.TabIndex = 7;
		this.hotkeyLabel.Text = "ホットキー";
		//
		// hotkeyTextBox
		//
		this.hotkeyTextBox.Location = new Point( 100, 162 );
		this.hotkeyTextBox.Name = "hotkeyTextBox";
		this.hotkeyTextBox.ReadOnly = true;
		this.hotkeyTextBox.Size = new Size( 262, 23 );
		this.hotkeyTextBox.TabIndex = 8;
		this.hotkeyTextBox.TabStop = false;
		//
		// hotkeyButton
		//
		this.hotkeyButton.Location = new Point( 368, 161 );
		this.hotkeyButton.Name = "hotkeyButton";
		this.hotkeyButton.Size = new Size( 80, 25 );
		this.hotkeyButton.TabIndex = 9;
		this.hotkeyButton.Text = "変更(&K)...";
		this.hotkeyButton.UseVisualStyleBackColor = true;
		this.hotkeyButton.Click += this.HotkeyButton_Click;
		//
		// okButton
		//
		this.okButton.Location = new Point( 274, 236 );
		this.okButton.Name = "okButton";
		this.okButton.Size = new Size( 84, 28 );
		this.okButton.TabIndex = 11;
		this.okButton.Text = "OK";
		this.okButton.UseVisualStyleBackColor = true;
		this.okButton.Click += this.OkButton_Click;
		//
		// cancelButton
		//
		this.cancelButton.DialogResult = DialogResult.Cancel;
		this.cancelButton.Location = new Point( 364, 236 );
		this.cancelButton.Name = "cancelButton";
		this.cancelButton.Size = new Size( 84, 28 );
		this.cancelButton.TabIndex = 12;
		this.cancelButton.Text = "キャンセル";
		this.cancelButton.UseVisualStyleBackColor = true;
		//
		// LauncherSlotDialog
		//
		this.AcceptButton = this.okButton;
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.CancelButton = this.cancelButton;
		this.ClientSize = new Size( 460, 276 );
		this.Controls.Add( this.titleLabel );
		this.Controls.Add( this.titleTextBox );
		this.Controls.Add( this.noneRadioButton );
		this.Controls.Add( this.scriptRadioButton );
		this.Controls.Add( this.pathTextBox );
		this.Controls.Add( this.browseButton );
		this.Controls.Add( this.folderRadioButton );
		this.Controls.Add( this.separateCheckBox );
		this.Controls.Add( this.hotkeyLabel );
		this.Controls.Add( this.hotkeyTextBox );
		this.Controls.Add( this.hotkeyButton );
		this.Controls.Add( this.okButton );
		this.Controls.Add( this.cancelButton );
		this.FormBorderStyle = FormBorderStyle.FixedDialog;
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.Name = "LauncherSlotDialog";
		this.ShowInTaskbar = false;
		this.StartPosition = FormStartPosition.CenterParent;
		this.Text = "スクリプト設定";
		this.ResumeLayout( false );
		this.PerformLayout();
	}

	#endregion

	private Label titleLabel;
	private TextBox titleTextBox;
	private RadioButton noneRadioButton;
	private RadioButton scriptRadioButton;
	private TextBox pathTextBox;
	private Button browseButton;
	private RadioButton folderRadioButton;
	private CheckBox separateCheckBox;
	private Label hotkeyLabel;
	private TextBox hotkeyTextBox;
	private Button hotkeyButton;
	private Button okButton;
	private Button cancelButton;
}
