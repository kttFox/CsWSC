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
		this.relativeMouseCheckBox = new CheckBox();
		this.backgroundCheckBox = new CheckBox();
		this.highLevelRadioButton = new RadioButton();
		this.skipIdleCheckBox = new CheckBox();
		this.copyAfterRecordCheckBox = new CheckBox();
		this.hotkeyGroupBox = new GroupBox();
		this.playLabel = new Label();
		this.playHotkeyTextBox = new TextBox();
		this.playHotkeyButton = new Button();
		this.stopLabel = new Label();
		this.stopHotkeyTextBox = new TextBox();
		this.stopHotkeyButton = new Button();
		this.recordLabel = new Label();
		this.recordHotkeyTextBox = new TextBox();
		this.recordHotkeyButton = new Button();
		this.trayLabel = new Label();
		this.trayHotkeyTextBox = new TextBox();
		this.trayHotkeyButton = new Button();
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
		this.recordGroupBox.Controls.Add( this.relativeMouseCheckBox );
		this.recordGroupBox.Controls.Add( this.backgroundCheckBox );
		this.recordGroupBox.Controls.Add( this.highLevelRadioButton );
		this.recordGroupBox.Controls.Add( this.skipIdleCheckBox );
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
		this.lowLevelRadioButton.Size = new Size( 90, 19 );
		this.lowLevelRadioButton.TabIndex = 0;
		this.lowLevelRadioButton.TabStop = true;
		this.lowLevelRadioButton.Text = "低レベル記録";
		this.lowLevelRadioButton.UseVisualStyleBackColor = true;
		this.lowLevelRadioButton.CheckedChanged += this.LowLevelRadioButton_CheckedChanged;
		// 
		// relativeMouseCheckBox
		// 
		this.relativeMouseCheckBox.AutoSize = true;
		this.relativeMouseCheckBox.Location = new Point( 32, 46 );
		this.relativeMouseCheckBox.Name = "relativeMouseCheckBox";
		this.relativeMouseCheckBox.Size = new Size( 187, 19 );
		this.relativeMouseCheckBox.TabIndex = 1;
		this.relativeMouseCheckBox.Text = "マウス座標を相対座標で記録する";
		this.relativeMouseCheckBox.UseVisualStyleBackColor = true;
		// 
		// backgroundCheckBox
		// 
		this.backgroundCheckBox.AutoSize = true;
		this.backgroundCheckBox.Location = new Point( 32, 70 );
		this.backgroundCheckBox.Name = "backgroundCheckBox";
		this.backgroundCheckBox.Size = new Size( 204, 19 );
		this.backgroundCheckBox.TabIndex = 2;
		this.backgroundCheckBox.Text = "バックグラウンドで実行できる形で記録";
		this.backgroundCheckBox.UseVisualStyleBackColor = true;
		// 
		// highLevelRadioButton
		// 
		this.highLevelRadioButton.AutoSize = true;
		this.highLevelRadioButton.Location = new Point( 12, 102 );
		this.highLevelRadioButton.Name = "highLevelRadioButton";
		this.highLevelRadioButton.Size = new Size( 90, 19 );
		this.highLevelRadioButton.TabIndex = 3;
		this.highLevelRadioButton.Text = "高レベル記録";
		this.highLevelRadioButton.UseVisualStyleBackColor = true;
		// 
		// skipIdleCheckBox
		// 
		this.skipIdleCheckBox.AutoSize = true;
		this.skipIdleCheckBox.Location = new Point( 12, 130 );
		this.skipIdleCheckBox.Name = "skipIdleCheckBox";
		this.skipIdleCheckBox.Size = new Size( 205, 19 );
		this.skipIdleCheckBox.TabIndex = 4;
		this.skipIdleCheckBox.Text = "余分な時間、マウス移動は記録しない";
		this.skipIdleCheckBox.UseVisualStyleBackColor = true;
		// 
		// copyAfterRecordCheckBox
		// 
		this.copyAfterRecordCheckBox.AutoSize = true;
		this.copyAfterRecordCheckBox.Location = new Point( 12, 154 );
		this.copyAfterRecordCheckBox.Name = "copyAfterRecordCheckBox";
		this.copyAfterRecordCheckBox.Size = new Size( 176, 19 );
		this.copyAfterRecordCheckBox.TabIndex = 5;
		this.copyAfterRecordCheckBox.Text = "記録後クリップボードへコピーする";
		this.copyAfterRecordCheckBox.UseVisualStyleBackColor = true;
		// 
		// hotkeyGroupBox
		// 
		this.hotkeyGroupBox.Controls.Add( this.playLabel );
		this.hotkeyGroupBox.Controls.Add( this.playHotkeyTextBox );
		this.hotkeyGroupBox.Controls.Add( this.playHotkeyButton );
		this.hotkeyGroupBox.Controls.Add( this.stopLabel );
		this.hotkeyGroupBox.Controls.Add( this.stopHotkeyTextBox );
		this.hotkeyGroupBox.Controls.Add( this.stopHotkeyButton );
		this.hotkeyGroupBox.Controls.Add( this.recordLabel );
		this.hotkeyGroupBox.Controls.Add( this.recordHotkeyTextBox );
		this.hotkeyGroupBox.Controls.Add( this.recordHotkeyButton );
		this.hotkeyGroupBox.Controls.Add( this.trayLabel );
		this.hotkeyGroupBox.Controls.Add( this.trayHotkeyTextBox );
		this.hotkeyGroupBox.Controls.Add( this.trayHotkeyButton );
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
		// playHotkeyTextBox
		// 
		this.playHotkeyTextBox.Location = new Point( 54, 23 );
		this.playHotkeyTextBox.Name = "playHotkeyTextBox";
		this.playHotkeyTextBox.ReadOnly = true;
		this.playHotkeyTextBox.Size = new Size( 160, 23 );
		this.playHotkeyTextBox.TabIndex = 3;
		this.playHotkeyTextBox.TabStop = false;
		// 
		// playHotkeyButton
		// 
		this.playHotkeyButton.Location = new Point( 220, 22 );
		this.playHotkeyButton.Name = "playHotkeyButton";
		this.playHotkeyButton.Size = new Size( 62, 25 );
		this.playHotkeyButton.TabIndex = 4;
		this.playHotkeyButton.Text = "変更...";
		this.playHotkeyButton.UseVisualStyleBackColor = true;
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
		// stopHotkeyTextBox
		// 
		this.stopHotkeyTextBox.Location = new Point( 54, 56 );
		this.stopHotkeyTextBox.Name = "stopHotkeyTextBox";
		this.stopHotkeyTextBox.ReadOnly = true;
		this.stopHotkeyTextBox.Size = new Size( 160, 23 );
		this.stopHotkeyTextBox.TabIndex = 7;
		this.stopHotkeyTextBox.TabStop = false;
		// 
		// stopHotkeyButton
		// 
		this.stopHotkeyButton.Location = new Point( 220, 55 );
		this.stopHotkeyButton.Name = "stopHotkeyButton";
		this.stopHotkeyButton.Size = new Size( 62, 25 );
		this.stopHotkeyButton.TabIndex = 8;
		this.stopHotkeyButton.Text = "変更...";
		this.stopHotkeyButton.UseVisualStyleBackColor = true;
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
		// recordHotkeyTextBox
		// 
		this.recordHotkeyTextBox.Location = new Point( 54, 89 );
		this.recordHotkeyTextBox.Name = "recordHotkeyTextBox";
		this.recordHotkeyTextBox.ReadOnly = true;
		this.recordHotkeyTextBox.Size = new Size( 160, 23 );
		this.recordHotkeyTextBox.TabIndex = 11;
		this.recordHotkeyTextBox.TabStop = false;
		// 
		// recordHotkeyButton
		// 
		this.recordHotkeyButton.Location = new Point( 220, 88 );
		this.recordHotkeyButton.Name = "recordHotkeyButton";
		this.recordHotkeyButton.Size = new Size( 62, 25 );
		this.recordHotkeyButton.TabIndex = 12;
		this.recordHotkeyButton.Text = "変更...";
		this.recordHotkeyButton.UseVisualStyleBackColor = true;
		// 
		// trayLabel
		// 
		this.trayLabel.AutoSize = true;
		this.trayLabel.Location = new Point( 12, 126 );
		this.trayLabel.Name = "trayLabel";
		this.trayLabel.Size = new Size( 33, 15 );
		this.trayLabel.TabIndex = 12;
		this.trayLabel.Text = "トレイ";
		// 
		// trayHotkeyTextBox
		// 
		this.trayHotkeyTextBox.Location = new Point( 54, 122 );
		this.trayHotkeyTextBox.Name = "trayHotkeyTextBox";
		this.trayHotkeyTextBox.ReadOnly = true;
		this.trayHotkeyTextBox.Size = new Size( 160, 23 );
		this.trayHotkeyTextBox.TabIndex = 15;
		this.trayHotkeyTextBox.TabStop = false;
		// 
		// trayHotkeyButton
		// 
		this.trayHotkeyButton.Location = new Point( 220, 121 );
		this.trayHotkeyButton.Name = "trayHotkeyButton";
		this.trayHotkeyButton.Size = new Size( 62, 25 );
		this.trayHotkeyButton.TabIndex = 16;
		this.trayHotkeyButton.Text = "変更...";
		this.trayHotkeyButton.UseVisualStyleBackColor = true;
		// 
		// okButton
		// 
		this.okButton.Location = new Point( 108, 470 );
		this.okButton.Name = "okButton";
		this.okButton.Size = new Size( 92, 28 );
		this.okButton.TabIndex = 3;
		this.okButton.Text = "OK";
		this.okButton.UseVisualStyleBackColor = true;
		this.okButton.Click += this.OkButton_Click;
		// 
		// cancelButton
		// 
		this.cancelButton.DialogResult = DialogResult.Cancel;
		this.cancelButton.Location = new Point( 206, 470 );
		this.cancelButton.Name = "cancelButton";
		this.cancelButton.Size = new Size( 92, 28 );
		this.cancelButton.TabIndex = 4;
		this.cancelButton.Text = "キャンセル";
		this.cancelButton.UseVisualStyleBackColor = true;
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
		this.trayOnStartCheckBox.Size = new Size( 176, 19 );
		this.trayOnStartCheckBox.TabIndex = 0;
		this.trayOnStartCheckBox.Text = "起動時にタスクトレイに収納する";
		this.trayOnStartCheckBox.UseVisualStyleBackColor = true;
		// 
		// trayOnCloseCheckBox
		// 
		this.trayOnCloseCheckBox.AutoSize = true;
		this.trayOnCloseCheckBox.Location = new Point( 12, 46 );
		this.trayOnCloseCheckBox.Name = "trayOnCloseCheckBox";
		this.trayOnCloseCheckBox.Size = new Size( 243, 19 );
		this.trayOnCloseCheckBox.TabIndex = 1;
		this.trayOnCloseCheckBox.Text = "閉じるボタンで終了せずタスクトレイに収納する";
		this.trayOnCloseCheckBox.UseVisualStyleBackColor = true;
		// 
		// trayWhileRunningCheckBox
		// 
		this.trayWhileRunningCheckBox.AutoSize = true;
		this.trayWhileRunningCheckBox.Location = new Point( 12, 70 );
		this.trayWhileRunningCheckBox.Name = "trayWhileRunningCheckBox";
		this.trayWhileRunningCheckBox.Size = new Size( 207, 19 );
		this.trayWhileRunningCheckBox.TabIndex = 2;
		this.trayWhileRunningCheckBox.Text = "再生・記録中はタスクトレイに収納する";
		this.trayWhileRunningCheckBox.UseVisualStyleBackColor = true;
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
	private TextBox playHotkeyTextBox;
	private Button playHotkeyButton;
	private Label stopLabel;
	private TextBox stopHotkeyTextBox;
	private Button stopHotkeyButton;
	private Label recordLabel;
	private TextBox recordHotkeyTextBox;
	private Button recordHotkeyButton;
	private Label trayLabel;
	private TextBox trayHotkeyTextBox;
	private Button trayHotkeyButton;
	private Button okButton;
	private Button cancelButton;
	private GroupBox trayGroupBox;
	private CheckBox trayOnStartCheckBox;
	private CheckBox trayOnCloseCheckBox;
	private CheckBox trayWhileRunningCheckBox;
}
