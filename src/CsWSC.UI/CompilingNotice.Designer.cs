namespace CsWSC.UI;

partial class CompilingNotice {
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
		this.components = new System.ComponentModel.Container();
		this.messageLabel = new Label();
		this.showTimer = new System.Windows.Forms.Timer( this.components );
		this.SuspendLayout();
		//
		// messageLabel
		//
		this.messageLabel.AutoSize = true;
		this.messageLabel.ForeColor = SystemColors.InfoText;
		this.messageLabel.Location = new Point( 1, 1 );
		this.messageLabel.Margin = new Padding( 0 );
		this.messageLabel.Name = "messageLabel";
		this.messageLabel.Padding = new Padding( 16, 12, 16, 12 );
		this.messageLabel.Size = new Size( 211, 54 );
		this.messageLabel.TabIndex = 0;
		this.messageLabel.Text = "スクリプトをコンパイルしています…\r\n(初回は時間がかかります)";
		//
		// showTimer
		//
		this.showTimer.Interval = 300;
		this.showTimer.Tick += this.showTimer_Tick;
		//
		// CompilingNotice
		//
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.AutoSize = true;
		this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		this.BackColor = SystemColors.Info;
		this.ClientSize = new Size( 213, 56 );
		this.Controls.Add( this.messageLabel );
		this.Font = new Font( "Yu Gothic UI", 9F );
		this.FormBorderStyle = FormBorderStyle.None;
		this.Name = "CompilingNotice";
		this.Padding = new Padding( 1 );
		this.ShowInTaskbar = false;
		this.StartPosition = FormStartPosition.Manual;
		this.Text = "CsWSC";
		this.ResumeLayout( false );
		this.PerformLayout();
	}

	#endregion

	private Label messageLabel;
	private System.Windows.Forms.Timer showTimer;
}
