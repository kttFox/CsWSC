#nullable disable

namespace CsWSC;

partial class InputBoxForm {
	/// <summary>
	/// 必要なデザイナー変数です。
	/// </summary>
	private System.ComponentModel.IContainer components = null;

	/// <summary>
	/// 使用中のリソースをすべてクリーンアップします。
	/// </summary>
	/// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
	protected override void Dispose( bool disposing ) {
		if( disposing ) {
			components?.Dispose();
		}
		base.Dispose( disposing );
	}

	#region Windows フォーム デザイナーで生成されたコード

	/// <summary>
	/// デザイナー サポートに必要なメソッドです。このメソッドの内容を
	/// コード エディターで変更しないでください。
	/// </summary>
	private void InitializeComponent() {
		layout = new TableLayoutPanel();
		promptLabel = new Label();
		inputTextBox = new TextBox();
		buttonPanel = new FlowLayoutPanel();
		cancelButton = new Button();
		okButton = new Button();
		layout.SuspendLayout();
		buttonPanel.SuspendLayout();
		SuspendLayout();
		//
		// layout
		//
		layout.AutoSize = true;
		layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		layout.ColumnCount = 1;
		layout.ColumnStyles.Add( new ColumnStyle() );
		layout.Controls.Add( promptLabel, 0, 0 );
		layout.Controls.Add( inputTextBox, 0, 1 );
		layout.Controls.Add( buttonPanel, 0, 2 );
		layout.Dock = DockStyle.Fill;
		layout.Location = new Point( 12, 12 );
		layout.Name = "layout";
		layout.RowCount = 3;
		layout.RowStyles.Add( new RowStyle() );
		layout.RowStyles.Add( new RowStyle() );
		layout.RowStyles.Add( new RowStyle() );
		layout.Size = new Size( 260, 88 );
		layout.TabIndex = 0;
		//
		// promptLabel
		//
		promptLabel.AutoSize = true;
		promptLabel.Location = new Point( 0, 0 );
		promptLabel.Margin = new Padding( 0, 0, 0, 6 );
		promptLabel.MaximumSize = new Size( 600, 0 );
		promptLabel.Name = "promptLabel";
		promptLabel.Size = new Size( 31, 15 );
		promptLabel.TabIndex = 0;
		promptLabel.Text = "prompt";
		//
		// inputTextBox
		//
		inputTextBox.Dock = DockStyle.Fill;
		inputTextBox.Location = new Point( 0, 21 );
		inputTextBox.Margin = new Padding( 0 );
		inputTextBox.Name = "inputTextBox";
		inputTextBox.Size = new Size( 260, 23 );
		inputTextBox.TabIndex = 1;
		//
		// buttonPanel
		//
		buttonPanel.AutoSize = true;
		buttonPanel.Controls.Add( cancelButton );
		buttonPanel.Controls.Add( okButton );
		buttonPanel.Dock = DockStyle.Fill;
		buttonPanel.FlowDirection = FlowDirection.RightToLeft;
		buttonPanel.Location = new Point( 0, 56 );
		buttonPanel.Margin = new Padding( 0, 12, 0, 0 );
		buttonPanel.Name = "buttonPanel";
		buttonPanel.Size = new Size( 260, 32 );
		buttonPanel.TabIndex = 2;
		//
		// cancelButton
		//
		cancelButton.AutoSize = true;
		cancelButton.DialogResult = DialogResult.Cancel;
		cancelButton.Location = new Point( 177, 3 );
		cancelButton.Margin = new Padding( 3, 3, 0, 3 );
		cancelButton.MinimumSize = new Size( 80, 0 );
		cancelButton.Name = "cancelButton";
		cancelButton.Size = new Size( 83, 26 );
		cancelButton.TabIndex = 1;
		cancelButton.Text = "キャンセル";
		cancelButton.UseVisualStyleBackColor = true;
		//
		// okButton
		//
		okButton.AutoSize = true;
		okButton.DialogResult = DialogResult.OK;
		okButton.Location = new Point( 91, 3 );
		okButton.MinimumSize = new Size( 80, 0 );
		okButton.Name = "okButton";
		okButton.Size = new Size( 80, 26 );
		okButton.TabIndex = 0;
		okButton.Text = "OK";
		okButton.UseVisualStyleBackColor = true;
		//
		// InputBoxForm
		//
		AcceptButton = okButton;
		AutoScaleMode = AutoScaleMode.Font;
		AutoSize = true;
		AutoSizeMode = AutoSizeMode.GrowAndShrink;
		CancelButton = cancelButton;
		ClientSize = new Size( 284, 112 );
		Controls.Add( layout );
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		Name = "InputBoxForm";
		Padding = new Padding( 12 );
		StartPosition = FormStartPosition.CenterScreen;
		Text = "CsWSC";
		TopMost = true;
		layout.ResumeLayout( false );
		layout.PerformLayout();
		buttonPanel.ResumeLayout( false );
		buttonPanel.PerformLayout();
		ResumeLayout( false );
		PerformLayout();
	}

	#endregion

	private TableLayoutPanel layout;
	private Label promptLabel;
	private TextBox inputTextBox;
	private FlowLayoutPanel buttonPanel;
	private Button cancelButton;
	private Button okButton;
}
