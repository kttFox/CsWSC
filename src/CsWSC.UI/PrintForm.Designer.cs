namespace CsWSC.UI;

partial class PrintForm
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
		this.outputTextBox = new TextBox();
		this.SuspendLayout();
		// 
		// outputTextBox
		// 
		this.outputTextBox.BackColor = SystemColors.Window;
		this.outputTextBox.Dock = DockStyle.Fill;
		this.outputTextBox.Font = new Font( "ＭＳ ゴシック", 10F );
		this.outputTextBox.Location = new Point( 0, 0 );
		this.outputTextBox.Multiline = true;
		this.outputTextBox.Name = "outputTextBox";
		this.outputTextBox.ReadOnly = true;
		this.outputTextBox.ScrollBars = ScrollBars.Both;
		this.outputTextBox.Size = new Size( 434, 261 );
		this.outputTextBox.TabIndex = 0;
		this.outputTextBox.WordWrap = false;
		// 
		// PrintForm
		// 
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.ClientSize = new Size( 434, 261 );
		this.Controls.Add( this.outputTextBox );
		this.Font = new Font( "Yu Gothic UI", 9F );
		this.Icon = Properties.Resources.AppIcon;
		this.Name = "PrintForm";
		this.ShowInTaskbar = false;
		this.StartPosition = FormStartPosition.Manual;
		this.Text = "PRINT";
		this.ResumeLayout( false );
		this.PerformLayout();
	}

	#endregion

	private TextBox outputTextBox;
}
