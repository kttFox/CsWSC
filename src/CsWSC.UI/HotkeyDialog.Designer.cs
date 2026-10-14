namespace CsWSC.UI;

partial class HotkeyDialog {
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
		this.globalRadioButton = new RadioButton();
		this.globalNoteLabel = new Label();
		this.ctrlCheckBox = new CheckBox();
		this.altCheckBox = new CheckBox();
		this.shiftCheckBox = new CheckBox();
		this.winCheckBox = new CheckBox();
		this.plusLabel = new Label();
		this.keyComboBox = new ComboBox();
		this.hookRadioButton = new RadioButton();
		this.hookNoteLabel = new Label();
		this.hotkeyBox = new HotkeyBox();
		this.noneRadioButton = new RadioButton();
		this.statusLabel = new Label();
		this.okButton = new Button();
		this.cancelButton = new Button();
		this.SuspendLayout();
		// 
		// globalRadioButton
		// 
		this.globalRadioButton.AutoSize = true;
		this.globalRadioButton.Checked = true;
		this.globalRadioButton.Location = new Point( 12, 12 );
		this.globalRadioButton.Name = "globalRadioButton";
		this.globalRadioButton.Size = new Size( 206, 19 );
		this.globalRadioButton.TabIndex = 0;
		this.globalRadioButton.TabStop = true;
		this.globalRadioButton.Text = "グローバルホットキー (RegisterHotKey)";
		this.globalRadioButton.UseVisualStyleBackColor = true;
		this.globalRadioButton.CheckedChanged += this.RadioButton_CheckedChanged;
		// 
		// globalNoteLabel
		// 
		this.globalNoteLabel.AutoSize = true;
		this.globalNoteLabel.ForeColor = SystemColors.GrayText;
		this.globalNoteLabel.Location = new Point( 30, 34 );
		this.globalNoteLabel.Name = "globalNoteLabel";
		this.globalNoteLabel.Size = new Size( 321, 30 );
		this.globalNoteLabel.TabIndex = 1;
		this.globalNoteLabel.Text = "キーを CsWSC だけが受け取ります。前面のアプリには届きません。\r\n修飾キーが必要です。他のアプリが使用中のキーは登録できません。";
		// 
		// ctrlCheckBox
		// 
		this.ctrlCheckBox.AutoSize = true;
		this.ctrlCheckBox.Location = new Point( 32, 72 );
		this.ctrlCheckBox.Name = "ctrlCheckBox";
		this.ctrlCheckBox.Size = new Size( 52, 19 );
		this.ctrlCheckBox.TabIndex = 2;
		this.ctrlCheckBox.Text = "CTRL";
		this.ctrlCheckBox.UseVisualStyleBackColor = true;
		this.ctrlCheckBox.CheckedChanged += this.Input_Changed;
		// 
		// altCheckBox
		// 
		this.altCheckBox.AutoSize = true;
		this.altCheckBox.Location = new Point( 90, 72 );
		this.altCheckBox.Name = "altCheckBox";
		this.altCheckBox.Size = new Size( 46, 19 );
		this.altCheckBox.TabIndex = 3;
		this.altCheckBox.Text = "ALT";
		this.altCheckBox.UseVisualStyleBackColor = true;
		this.altCheckBox.CheckedChanged += this.Input_Changed;
		// 
		// shiftCheckBox
		// 
		this.shiftCheckBox.AutoSize = true;
		this.shiftCheckBox.Location = new Point( 142, 72 );
		this.shiftCheckBox.Name = "shiftCheckBox";
		this.shiftCheckBox.Size = new Size( 56, 19 );
		this.shiftCheckBox.TabIndex = 4;
		this.shiftCheckBox.Text = "SHIFT";
		this.shiftCheckBox.UseVisualStyleBackColor = true;
		this.shiftCheckBox.CheckedChanged += this.Input_Changed;
		// 
		// winCheckBox
		// 
		this.winCheckBox.AutoSize = true;
		this.winCheckBox.Location = new Point( 203, 72 );
		this.winCheckBox.Name = "winCheckBox";
		this.winCheckBox.Size = new Size( 49, 19 );
		this.winCheckBox.TabIndex = 5;
		this.winCheckBox.Text = "WIN";
		this.winCheckBox.UseVisualStyleBackColor = true;
		this.winCheckBox.CheckedChanged += this.Input_Changed;
		// 
		// plusLabel
		// 
		this.plusLabel.AutoSize = true;
		this.plusLabel.Location = new Point( 262, 74 );
		this.plusLabel.Name = "plusLabel";
		this.plusLabel.Size = new Size( 15, 15 );
		this.plusLabel.TabIndex = 6;
		this.plusLabel.Text = "+";
		// 
		// keyComboBox
		// 
		this.keyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.keyComboBox.Location = new Point( 284, 70 );
		this.keyComboBox.Name = "keyComboBox";
		this.keyComboBox.Size = new Size( 114, 23 );
		this.keyComboBox.TabIndex = 7;
		this.keyComboBox.SelectedIndexChanged += this.Input_Changed;
		// 
		// hookRadioButton
		// 
		this.hookRadioButton.AutoSize = true;
		this.hookRadioButton.Location = new Point( 12, 126 );
		this.hookRadioButton.Name = "hookRadioButton";
		this.hookRadioButton.Size = new Size( 246, 19 );
		this.hookRadioButton.TabIndex = 8;
		this.hookRadioButton.Text = "低レベルキーボードフック (WH_KEYBOARD_LL)";
		this.hookRadioButton.UseVisualStyleBackColor = true;
		this.hookRadioButton.CheckedChanged += this.RadioButton_CheckedChanged;
		// 
		// hookNoteLabel
		// 
		this.hookNoteLabel.AutoSize = true;
		this.hookNoteLabel.ForeColor = SystemColors.GrayText;
		this.hookNoteLabel.Location = new Point( 30, 148 );
		this.hookNoteLabel.Name = "hookNoteLabel";
		this.hookNoteLabel.Size = new Size( 340, 45 );
		this.hookNoteLabel.TabIndex = 9;
		this.hookNoteLabel.Text = "ホットキーとして動作したうえで、キーを前面のアプリにもそのまま送ります。\r\n管理者権限で動いているアプリが前面にあるときは反応しません。\r\n下の欄で設定したいキーかマウスのサイドボタンを押してください (Esc でなし)。";
		// 
		// hotkeyBox
		// 
		this.hotkeyBox.BackColor = SystemColors.Window;
		this.hotkeyBox.Location = new Point( 32, 200 );
		this.hotkeyBox.Name = "hotkeyBox";
		this.hotkeyBox.ReadOnly = true;
		this.hotkeyBox.ShortcutsEnabled = false;
		this.hotkeyBox.Size = new Size( 366, 23 );
		this.hotkeyBox.TabIndex = 10;
		this.hotkeyBox.Text = "なし";
		this.hotkeyBox.TextChanged += this.Input_Changed;
		// 
		// noneRadioButton
		// 
		this.noneRadioButton.AutoSize = true;
		this.noneRadioButton.Location = new Point( 12, 236 );
		this.noneRadioButton.Name = "noneRadioButton";
		this.noneRadioButton.Size = new Size( 148, 19 );
		this.noneRadioButton.TabIndex = 11;
		this.noneRadioButton.Text = "なし (ホットキーを使わない)";
		this.noneRadioButton.UseVisualStyleBackColor = true;
		this.noneRadioButton.CheckedChanged += this.RadioButton_CheckedChanged;
		// 
		// statusLabel
		// 
		this.statusLabel.AutoSize = true;
		this.statusLabel.Location = new Point( 32, 98 );
		this.statusLabel.Name = "statusLabel";
		this.statusLabel.Size = new Size( 0, 15 );
		this.statusLabel.TabIndex = 12;
		// 
		// okButton
		// 
		this.okButton.DialogResult = DialogResult.OK;
		this.okButton.Location = new Point( 210, 268 );
		this.okButton.Name = "okButton";
		this.okButton.Size = new Size( 92, 28 );
		this.okButton.TabIndex = 13;
		this.okButton.Text = "OK";
		this.okButton.UseVisualStyleBackColor = true;
		// 
		// cancelButton
		// 
		this.cancelButton.DialogResult = DialogResult.Cancel;
		this.cancelButton.Location = new Point( 308, 268 );
		this.cancelButton.Name = "cancelButton";
		this.cancelButton.Size = new Size( 92, 28 );
		this.cancelButton.TabIndex = 14;
		this.cancelButton.Text = "キャンセル";
		this.cancelButton.UseVisualStyleBackColor = true;
		// 
		// HotkeyDialog
		// 
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.CancelButton = this.cancelButton;
		this.ClientSize = new Size( 420, 308 );
		this.Controls.Add( this.globalRadioButton );
		this.Controls.Add( this.globalNoteLabel );
		this.Controls.Add( this.ctrlCheckBox );
		this.Controls.Add( this.altCheckBox );
		this.Controls.Add( this.shiftCheckBox );
		this.Controls.Add( this.winCheckBox );
		this.Controls.Add( this.plusLabel );
		this.Controls.Add( this.keyComboBox );
		this.Controls.Add( this.hookRadioButton );
		this.Controls.Add( this.hookNoteLabel );
		this.Controls.Add( this.hotkeyBox );
		this.Controls.Add( this.noneRadioButton );
		this.Controls.Add( this.statusLabel );
		this.Controls.Add( this.okButton );
		this.Controls.Add( this.cancelButton );
		this.Font = new Font( "Yu Gothic UI", 9F );
		this.FormBorderStyle = FormBorderStyle.FixedDialog;
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.Name = "HotkeyDialog";
		this.ShowIcon = false;
		this.ShowInTaskbar = false;
		this.StartPosition = FormStartPosition.CenterParent;
		this.Text = "ホットキーの設定";
		this.ResumeLayout( false );
		this.PerformLayout();
	}

	#endregion

	private RadioButton globalRadioButton;
	private Label globalNoteLabel;
	private CheckBox ctrlCheckBox;
	private CheckBox altCheckBox;
	private CheckBox shiftCheckBox;
	private CheckBox winCheckBox;
	private Label plusLabel;
	private ComboBox keyComboBox;
	private RadioButton hookRadioButton;
	private Label hookNoteLabel;
	private HotkeyBox hotkeyBox;
	private RadioButton noneRadioButton;
	private Label statusLabel;
	private Button okButton;
	private Button cancelButton;
}
