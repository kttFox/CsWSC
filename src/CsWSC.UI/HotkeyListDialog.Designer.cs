namespace CsWSC.UI;

partial class HotkeyListDialog {
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
		this.hotkeyListBox = new ListBox();
		this.addButton = new Button();
		this.editButton = new Button();
		this.removeButton = new Button();
		this.okButton = new Button();
		this.cancelButton = new Button();
		this.SuspendLayout();
		//
		// hotkeyListBox
		//
		this.hotkeyListBox.FormattingEnabled = true;
		this.hotkeyListBox.IntegralHeight = false;
		this.hotkeyListBox.Location = new Point( 12, 12 );
		this.hotkeyListBox.Name = "hotkeyListBox";
		this.hotkeyListBox.Size = new Size( 286, 190 );
		this.hotkeyListBox.TabIndex = 0;
		this.hotkeyListBox.SelectedIndexChanged += this.HotkeyListBox_SelectedIndexChanged;
		this.hotkeyListBox.DoubleClick += this.EditButton_Click;
		//
		// addButton
		//
		this.addButton.Location = new Point( 304, 12 );
		this.addButton.Name = "addButton";
		this.addButton.Size = new Size( 88, 28 );
		this.addButton.TabIndex = 1;
		this.addButton.Text = "追加(&A)...";
		this.addButton.UseVisualStyleBackColor = true;
		this.addButton.Click += this.AddButton_Click;
		//
		// editButton
		//
		this.editButton.Location = new Point( 304, 46 );
		this.editButton.Name = "editButton";
		this.editButton.Size = new Size( 88, 28 );
		this.editButton.TabIndex = 2;
		this.editButton.Text = "変更(&E)...";
		this.editButton.UseVisualStyleBackColor = true;
		this.editButton.Click += this.EditButton_Click;
		//
		// removeButton
		//
		this.removeButton.Location = new Point( 304, 80 );
		this.removeButton.Name = "removeButton";
		this.removeButton.Size = new Size( 88, 28 );
		this.removeButton.TabIndex = 3;
		this.removeButton.Text = "削除(&D)";
		this.removeButton.UseVisualStyleBackColor = true;
		this.removeButton.Click += this.RemoveButton_Click;
		//
		// okButton
		//
		this.okButton.DialogResult = DialogResult.OK;
		this.okButton.Location = new Point( 206, 214 );
		this.okButton.Name = "okButton";
		this.okButton.Size = new Size( 92, 28 );
		this.okButton.TabIndex = 4;
		this.okButton.Text = "OK";
		this.okButton.UseVisualStyleBackColor = true;
		//
		// cancelButton
		//
		this.cancelButton.DialogResult = DialogResult.Cancel;
		this.cancelButton.Location = new Point( 304, 214 );
		this.cancelButton.Name = "cancelButton";
		this.cancelButton.Size = new Size( 92, 28 );
		this.cancelButton.TabIndex = 5;
		this.cancelButton.Text = "キャンセル";
		this.cancelButton.UseVisualStyleBackColor = true;
		//
		// HotkeyListDialog
		//
		this.AcceptButton = this.okButton;
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.CancelButton = this.cancelButton;
		this.ClientSize = new Size( 408, 254 );
		this.Controls.Add( this.hotkeyListBox );
		this.Controls.Add( this.addButton );
		this.Controls.Add( this.editButton );
		this.Controls.Add( this.removeButton );
		this.Controls.Add( this.okButton );
		this.Controls.Add( this.cancelButton );
		this.Font = new Font( "Yu Gothic UI", 9F );
		this.FormBorderStyle = FormBorderStyle.FixedDialog;
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.Name = "HotkeyListDialog";
		this.ShowIcon = false;
		this.ShowInTaskbar = false;
		this.StartPosition = FormStartPosition.CenterParent;
		this.Text = "ホットキーの一覧";
		this.ResumeLayout( false );
	}

	#endregion

	private ListBox hotkeyListBox;
	private Button addButton;
	private Button editButton;
	private Button removeButton;
	private Button okButton;
	private Button cancelButton;
}
