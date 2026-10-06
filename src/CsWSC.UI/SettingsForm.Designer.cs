namespace CsWSC.UI;

partial class SettingsForm {
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
		this.recordGroupBox = new GroupBox();
		this.lowLevelRadioButton = new RadioButton();
		this.skipIdleCheckBox = new CheckBox();
		this.relativeMouseCheckBox = new CheckBox();
		this.backgroundCheckBox = new CheckBox();
		this.highLevelRadioButton = new RadioButton();
		this.copyAfterRecordCheckBox = new CheckBox();
		this.hotkeyGroupBox = new GroupBox();
		this.playLabel = new Label();
		this.playModifierComboBox = new ComboBox();
		this.playPlusLabel = new Label();
		this.playKeyComboBox = new ComboBox();
		this.stopLabel = new Label();
		this.stopModifierComboBox = new ComboBox();
		this.stopPlusLabel = new Label();
		this.stopKeyComboBox = new ComboBox();
		this.recordLabel = new Label();
		this.recordModifierComboBox = new ComboBox();
		this.recordPlusLabel = new Label();
		this.recordKeyComboBox = new ComboBox();
		this.trayLabel = new Label();
		this.trayModifierComboBox = new ComboBox();
		this.trayPlusLabel = new Label();
		this.trayKeyComboBox = new ComboBox();
		this.okButton = new Button();
		this.cancelButton = new Button();
		this.trayGroupBox = new GroupBox();
		this.trayOnStartCheckBox = new CheckBox();
		this.trayOnCloseCheckBox = new CheckBox();
		this.trayWhileRunningCheckBox = new CheckBox();
		this.recordGroupBox.SuspendLayout();
		this.hotkeyGroupBox.SuspendLayout();
		this.trayGroupBox.SuspendLayout();
		this.SuspendLayout();
		//
		// recordGroupBox
		//
		this.recordGroupBox.Controls.Add( this.lowLevelRadioButton );
		this.recordGroupBox.Controls.Add( this.skipIdleCheckBox );
		this.recordGroupBox.Controls.Add( this.relativeMouseCheckBox );
		this.recordGroupBox.Controls.Add( this.backgroundCheckBox );
		this.recordGroupBox.Controls.Add( this.highLevelRadioButton );
		this.recordGroupBox.Controls.Add( this.copyAfterRecordCheckBox );
		this.recordGroupBox.Location = new Point( 8, 6 );
		this.recordGroupBox.Name = "recordGroupBox";
		this.recordGroupBox.Size = new Size( 294, 186 );
		this.recordGroupBox.TabIndex = 0;
		this.recordGroupBox.TabStop = false;
		this.recordGroupBox.Text = "記録方法(&R)";
		//
		// lowLevelRadioButton
		//
		this.lowLevelRadioButton.AutoSize = true;
		this.lowLevelRadioButton.Checked = true;
		this.lowLevelRadioButton.Location = new Point( 12, 22 );
		this.lowLevelRadioButton.Name = "lowLevelRadioButton";
		this.lowLevelRadioButton.Size = new Size( 85, 19 );
		this.lowLevelRadioButton.TabIndex = 0;
		this.lowLevelRadioButton.TabStop = true;
		this.lowLevelRadioButton.Text = "低レベル記録";
		this.lowLevelRadioButton.UseVisualStyleBackColor = true;
		this.lowLevelRadioButton.CheckedChanged +=  this.LowLevelRadioButton_CheckedChanged ;
		//
		// skipIdleCheckBox
		//
		this.skipIdleCheckBox.AutoSize = true;
		this.skipIdleCheckBox.Location = new Point( 32, 46 );
		this.skipIdleCheckBox.Name = "skipIdleCheckBox";
		this.skipIdleCheckBox.Size = new Size( 210, 19 );
		this.skipIdleCheckBox.TabIndex = 1;
		this.skipIdleCheckBox.Text = "余分な時間、マウス移動は記録しない";
		this.skipIdleCheckBox.UseVisualStyleBackColor = true;
		//
		// relativeMouseCheckBox
		//
		this.relativeMouseCheckBox.AutoSize = true;
		this.relativeMouseCheckBox.Location = new Point( 32, 70 );
		this.relativeMouseCheckBox.Name = "relativeMouseCheckBox";
		this.relativeMouseCheckBox.Size = new Size( 198, 19 );
		this.relativeMouseCheckBox.TabIndex = 2;
		this.relativeMouseCheckBox.Text = "マウス座標を相対座標で記録する";
		this.relativeMouseCheckBox.UseVisualStyleBackColor = true;
		//
		// backgroundCheckBox
		//
		this.backgroundCheckBox.AutoSize = true;
		this.backgroundCheckBox.Location = new Point( 32, 94 );
		this.backgroundCheckBox.Name = "backgroundCheckBox";
		this.backgroundCheckBox.Size = new Size( 222, 19 );
		this.backgroundCheckBox.TabIndex = 3;
		this.backgroundCheckBox.Text = "バックグラウンドで実行できる形で記録";
		this.backgroundCheckBox.UseVisualStyleBackColor = true;
		//
		// highLevelRadioButton
		//
		this.highLevelRadioButton.AutoSize = true;
		this.highLevelRadioButton.Location = new Point( 12, 126 );
		this.highLevelRadioButton.Name = "highLevelRadioButton";
		this.highLevelRadioButton.Size = new Size( 85, 19 );
		this.highLevelRadioButton.TabIndex = 4;
		this.highLevelRadioButton.Text = "高レベル記録";
		this.highLevelRadioButton.UseVisualStyleBackColor = true;
		//
		// copyAfterRecordCheckBox
		//
		this.copyAfterRecordCheckBox.AutoSize = true;
		this.copyAfterRecordCheckBox.Location = new Point( 12, 154 );
		this.copyAfterRecordCheckBox.Name = "copyAfterRecordCheckBox";
		this.copyAfterRecordCheckBox.Size = new Size( 186, 19 );
		this.copyAfterRecordCheckBox.TabIndex = 5;
		this.copyAfterRecordCheckBox.Text = "記録後クリップボードへコピーする";
		this.copyAfterRecordCheckBox.UseVisualStyleBackColor = true;
		//
		// hotkeyGroupBox
		//
		this.hotkeyGroupBox.Controls.Add( this.playLabel );
		this.hotkeyGroupBox.Controls.Add( this.playModifierComboBox );
		this.hotkeyGroupBox.Controls.Add( this.playPlusLabel );
		this.hotkeyGroupBox.Controls.Add( this.playKeyComboBox );
		this.hotkeyGroupBox.Controls.Add( this.stopLabel );
		this.hotkeyGroupBox.Controls.Add( this.stopModifierComboBox );
		this.hotkeyGroupBox.Controls.Add( this.stopPlusLabel );
		this.hotkeyGroupBox.Controls.Add( this.stopKeyComboBox );
		this.hotkeyGroupBox.Controls.Add( this.recordLabel );
		this.hotkeyGroupBox.Controls.Add( this.recordModifierComboBox );
		this.hotkeyGroupBox.Controls.Add( this.recordPlusLabel );
		this.hotkeyGroupBox.Controls.Add( this.recordKeyComboBox );
		this.hotkeyGroupBox.Controls.Add( this.trayLabel );
		this.hotkeyGroupBox.Controls.Add( this.trayModifierComboBox );
		this.hotkeyGroupBox.Controls.Add( this.trayPlusLabel );
		this.hotkeyGroupBox.Controls.Add( this.trayKeyComboBox );
		this.hotkeyGroupBox.Location = new Point( 8, 198 );
		this.hotkeyGroupBox.Name = "hotkeyGroupBox";
		this.hotkeyGroupBox.Size = new Size( 294, 156 );
		this.hotkeyGroupBox.TabIndex = 1;
		this.hotkeyGroupBox.TabStop = false;
		this.hotkeyGroupBox.Text = "ホットキー(&H)";
		//
		// playLabel
		//
		this.playLabel.AutoSize = true;
		this.playLabel.Location = new Point( 12, 27 );
		this.playLabel.Name = "playLabel";
		this.playLabel.Size = new Size( 31, 15 );
		this.playLabel.TabIndex = 0;
		this.playLabel.Text = "再生";
		//
		// playModifierComboBox
		//
		this.playModifierComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.playModifierComboBox.Location = new Point( 54, 23 );
		this.playModifierComboBox.Name = "playModifierComboBox";
		this.playModifierComboBox.Size = new Size( 104, 23 );
		this.playModifierComboBox.TabIndex = 1;
		//
		// playPlusLabel
		//
		this.playPlusLabel.AutoSize = true;
		this.playPlusLabel.Location = new Point( 168, 27 );
		this.playPlusLabel.Name = "playPlusLabel";
		this.playPlusLabel.Size = new Size( 15, 15 );
		this.playPlusLabel.TabIndex = 2;
		this.playPlusLabel.Text = "+";
		//
		// playKeyComboBox
		//
		this.playKeyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.playKeyComboBox.Location = new Point( 190, 23 );
		this.playKeyComboBox.Name = "playKeyComboBox";
		this.playKeyComboBox.Size = new Size( 92, 23 );
		this.playKeyComboBox.TabIndex = 3;
		//
		// stopLabel
		//
		this.stopLabel.AutoSize = true;
		this.stopLabel.Location = new Point( 12, 60 );
		this.stopLabel.Name = "stopLabel";
		this.stopLabel.Size = new Size( 31, 15 );
		this.stopLabel.TabIndex = 4;
		this.stopLabel.Text = "停止";
		//
		// stopModifierComboBox
		//
		this.stopModifierComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.stopModifierComboBox.Location = new Point( 54, 56 );
		this.stopModifierComboBox.Name = "stopModifierComboBox";
		this.stopModifierComboBox.Size = new Size( 104, 23 );
		this.stopModifierComboBox.TabIndex = 5;
		//
		// stopPlusLabel
		//
		this.stopPlusLabel.AutoSize = true;
		this.stopPlusLabel.Location = new Point( 168, 60 );
		this.stopPlusLabel.Name = "stopPlusLabel";
		this.stopPlusLabel.Size = new Size( 15, 15 );
		this.stopPlusLabel.TabIndex = 6;
		this.stopPlusLabel.Text = "+";
		//
		// stopKeyComboBox
		//
		this.stopKeyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.stopKeyComboBox.Location = new Point( 190, 56 );
		this.stopKeyComboBox.Name = "stopKeyComboBox";
		this.stopKeyComboBox.Size = new Size( 92, 23 );
		this.stopKeyComboBox.TabIndex = 7;
		//
		// recordLabel
		//
		this.recordLabel.AutoSize = true;
		this.recordLabel.Location = new Point( 12, 93 );
		this.recordLabel.Name = "recordLabel";
		this.recordLabel.Size = new Size( 31, 15 );
		this.recordLabel.TabIndex = 8;
		this.recordLabel.Text = "記録";
		//
		// recordModifierComboBox
		//
		this.recordModifierComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.recordModifierComboBox.Location = new Point( 54, 89 );
		this.recordModifierComboBox.Name = "recordModifierComboBox";
		this.recordModifierComboBox.Size = new Size( 104, 23 );
		this.recordModifierComboBox.TabIndex = 9;
		//
		// recordPlusLabel
		//
		this.recordPlusLabel.AutoSize = true;
		this.recordPlusLabel.Location = new Point( 168, 93 );
		this.recordPlusLabel.Name = "recordPlusLabel";
		this.recordPlusLabel.Size = new Size( 15, 15 );
		this.recordPlusLabel.TabIndex = 10;
		this.recordPlusLabel.Text = "+";
		//
		// recordKeyComboBox
		//
		this.recordKeyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.recordKeyComboBox.Location = new Point( 190, 89 );
		this.recordKeyComboBox.Name = "recordKeyComboBox";
		this.recordKeyComboBox.Size = new Size( 92, 23 );
		this.recordKeyComboBox.TabIndex = 11;
		//
		// trayLabel
		//
		this.trayLabel.AutoSize = true;
		this.trayLabel.Location = new Point( 12, 126 );
		this.trayLabel.Name = "trayLabel";
		this.trayLabel.Size = new Size( 36, 15 );
		this.trayLabel.TabIndex = 12;
		this.trayLabel.Text = "トレイ";
		//
		// trayModifierComboBox
		//
		this.trayModifierComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.trayModifierComboBox.Location = new Point( 54, 122 );
		this.trayModifierComboBox.Name = "trayModifierComboBox";
		this.trayModifierComboBox.Size = new Size( 104, 23 );
		this.trayModifierComboBox.TabIndex = 13;
		//
		// trayPlusLabel
		//
		this.trayPlusLabel.AutoSize = true;
		this.trayPlusLabel.Location = new Point( 168, 126 );
		this.trayPlusLabel.Name = "trayPlusLabel";
		this.trayPlusLabel.Size = new Size( 15, 15 );
		this.trayPlusLabel.TabIndex = 14;
		this.trayPlusLabel.Text = "+";
		//
		// trayKeyComboBox
		//
		this.trayKeyComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.trayKeyComboBox.Location = new Point( 190, 122 );
		this.trayKeyComboBox.Name = "trayKeyComboBox";
		this.trayKeyComboBox.Size = new Size( 92, 23 );
		this.trayKeyComboBox.TabIndex = 15;
		//
		// trayGroupBox
		//
		this.trayGroupBox.Controls.Add( this.trayOnStartCheckBox );
		this.trayGroupBox.Controls.Add( this.trayOnCloseCheckBox );
		this.trayGroupBox.Controls.Add( this.trayWhileRunningCheckBox );
		this.trayGroupBox.Location = new Point( 8, 360 );
		this.trayGroupBox.Name = "trayGroupBox";
		this.trayGroupBox.Size = new Size( 294, 98 );
		this.trayGroupBox.TabIndex = 2;
		this.trayGroupBox.TabStop = false;
		this.trayGroupBox.Text = "タスクトレイ(&T)";
		//
		// trayOnStartCheckBox
		//
		this.trayOnStartCheckBox.AutoSize = true;
		this.trayOnStartCheckBox.Location = new Point( 12, 22 );
		this.trayOnStartCheckBox.Name = "trayOnStartCheckBox";
		this.trayOnStartCheckBox.Size = new Size( 174, 19 );
		this.trayOnStartCheckBox.TabIndex = 0;
		this.trayOnStartCheckBox.Text = "起動時にタスクトレイに収納する";
		this.trayOnStartCheckBox.UseVisualStyleBackColor = true;
		//
		// trayOnCloseCheckBox
		//
		this.trayOnCloseCheckBox.AutoSize = true;
		this.trayOnCloseCheckBox.Location = new Point( 12, 46 );
		this.trayOnCloseCheckBox.Name = "trayOnCloseCheckBox";
		this.trayOnCloseCheckBox.Size = new Size( 231, 19 );
		this.trayOnCloseCheckBox.TabIndex = 1;
		this.trayOnCloseCheckBox.Text = "閉じるボタンで終了せずタスクトレイに収納する";
		this.trayOnCloseCheckBox.UseVisualStyleBackColor = true;
		//
		// trayWhileRunningCheckBox
		//
		this.trayWhileRunningCheckBox.AutoSize = true;
		this.trayWhileRunningCheckBox.Location = new Point( 12, 70 );
		this.trayWhileRunningCheckBox.Name = "trayWhileRunningCheckBox";
		this.trayWhileRunningCheckBox.Size = new Size( 210, 19 );
		this.trayWhileRunningCheckBox.TabIndex = 2;
		this.trayWhileRunningCheckBox.Text = "再生・記録中はタスクトレイに収納する";
		this.trayWhileRunningCheckBox.UseVisualStyleBackColor = true;
		//
		// okButton
		//
		this.okButton.Location = new Point( 40, 470 );
		this.okButton.Name = "okButton";
		this.okButton.Size = new Size( 92, 28 );
		this.okButton.TabIndex = 3;
		this.okButton.Text = "OK";
		this.okButton.UseVisualStyleBackColor = true;
		this.okButton.Click +=  this.OkButton_Click ;
		//
		// cancelButton
		//
		this.cancelButton.DialogResult = DialogResult.Cancel;
		this.cancelButton.Location = new Point( 178, 470 );
		this.cancelButton.Name = "cancelButton";
		this.cancelButton.Size = new Size( 92, 28 );
		this.cancelButton.TabIndex = 4;
		this.cancelButton.Text = "Cancel";
		this.cancelButton.UseVisualStyleBackColor = true;
		//
		// SettingsForm
		//
		this.AcceptButton = this.okButton;
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.CancelButton = this.cancelButton;
		this.ClientSize = new Size( 310, 508 );
		this.Controls.Add( this.recordGroupBox );
		this.Controls.Add( this.hotkeyGroupBox );
		this.Controls.Add( this.trayGroupBox );
		this.Controls.Add( this.okButton );
		this.Controls.Add( this.cancelButton );
		this.Font = new Font( "Yu Gothic UI", 9F );
		this.FormBorderStyle = FormBorderStyle.FixedDialog;
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.Name = "SettingsForm";
		this.ShowIcon = false;
		this.ShowInTaskbar = false;
		this.StartPosition = FormStartPosition.CenterParent;
		this.Text = "設定";
		this.recordGroupBox.ResumeLayout( false );
		this.recordGroupBox.PerformLayout();
		this.hotkeyGroupBox.ResumeLayout( false );
		this.hotkeyGroupBox.PerformLayout();
		this.trayGroupBox.ResumeLayout( false );
		this.trayGroupBox.PerformLayout();
		this.ResumeLayout( false );
	}

	#endregion

	private GroupBox recordGroupBox;
	private RadioButton lowLevelRadioButton;
	private CheckBox skipIdleCheckBox;
	private CheckBox relativeMouseCheckBox;
	private CheckBox backgroundCheckBox;
	private RadioButton highLevelRadioButton;
	private CheckBox copyAfterRecordCheckBox;
	private GroupBox hotkeyGroupBox;
	private Label playLabel;
	private ComboBox playModifierComboBox;
	private Label playPlusLabel;
	private ComboBox playKeyComboBox;
	private Label stopLabel;
	private ComboBox stopModifierComboBox;
	private Label stopPlusLabel;
	private ComboBox stopKeyComboBox;
	private Label recordLabel;
	private ComboBox recordModifierComboBox;
	private Label recordPlusLabel;
	private ComboBox recordKeyComboBox;
	private Label trayLabel;
	private ComboBox trayModifierComboBox;
	private Label trayPlusLabel;
	private ComboBox trayKeyComboBox;
	private Button okButton;
	private Button cancelButton;
	private GroupBox trayGroupBox;
	private CheckBox trayOnStartCheckBox;
	private CheckBox trayOnCloseCheckBox;
	private CheckBox trayWhileRunningCheckBox;
}
