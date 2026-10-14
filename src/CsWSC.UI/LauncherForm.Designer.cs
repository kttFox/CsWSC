namespace CsWSC.UI;

partial class LauncherForm {
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
		this.profileLabel = new Label();
		this.profileComboBox = new ComboBox();
		this.addProfileButton = new Button();
		this.removeProfileButton = new Button();
		this.profileUpButton = new Button();
		this.profileDownButton = new Button();
		this.targetAppsLabel = new Label();
		this.targetAppsTextBox = new TextBox();
		this.targetListButton = new Button();
		this.targetTitleLabel = new Label();
		this.targetTitleTextBox = new TextBox();
		this.targetSeparator = new Label();
		this.levelLabel = new Label();
		this.parentButton = new Button();
		this.listView = new ListView();
		this.keyColumnHeader = new ColumnHeader();
		this.titleColumnHeader = new ColumnHeader();
		this.contentColumnHeader = new ColumnHeader();
		this.hotkeyColumnHeader = new ColumnHeader();
		this.editButton = new Button();
		this.openButton = new Button();
		this.clearButton = new Button();
		this.upButton = new Button();
		this.downButton = new Button();
		this.menuHotkeyLabel = new Label();
		this.menuHotkeyTextBox = new TextBox();
		this.menuHotkeyButton = new Button();
		this.okButton = new Button();
		this.cancelButton = new Button();
		this.SuspendLayout();
		// 
		// profileLabel
		// 
		this.profileLabel.AutoSize = true;
		this.profileLabel.Location = new Point( 12, 13 );
		this.profileLabel.Name = "profileLabel";
		this.profileLabel.Size = new Size( 71, 15 );
		this.profileLabel.TabIndex = 0;
		this.profileLabel.Text = "対象アプリ(&T)";
		// 
		// profileComboBox
		// 
		this.profileComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		this.profileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
		this.profileComboBox.Location = new Point( 100, 9 );
		this.profileComboBox.Name = "profileComboBox";
		this.profileComboBox.Size = new Size( 204, 23 );
		this.profileComboBox.TabIndex = 1;
		this.profileComboBox.SelectedIndexChanged += this.ProfileComboBox_SelectedIndexChanged;
		// 
		// addProfileButton
		// 
		this.addProfileButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.addProfileButton.Location = new Point( 374, 8 );
		this.addProfileButton.Name = "addProfileButton";
		this.addProfileButton.Size = new Size( 88, 26 );
		this.addProfileButton.TabIndex = 2;
		this.addProfileButton.Text = "追加(&I)";
		this.addProfileButton.UseVisualStyleBackColor = true;
		this.addProfileButton.Click += this.AddProfileButton_Click;
		// 
		// removeProfileButton
		// 
		this.removeProfileButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.removeProfileButton.Location = new Point( 468, 8 );
		this.removeProfileButton.Name = "removeProfileButton";
		this.removeProfileButton.Size = new Size( 88, 26 );
		this.removeProfileButton.TabIndex = 3;
		this.removeProfileButton.Text = "削除(&R)";
		this.removeProfileButton.UseVisualStyleBackColor = true;
		this.removeProfileButton.Click += this.RemoveProfileButton_Click;
		// 
		// profileUpButton
		// 
		this.profileUpButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.profileUpButton.Location = new Point( 308, 8 );
		this.profileUpButton.Name = "profileUpButton";
		this.profileUpButton.Size = new Size( 28, 26 );
		this.profileUpButton.TabIndex = 2;
		this.profileUpButton.Text = "▲";
		this.profileUpButton.UseVisualStyleBackColor = true;
		this.profileUpButton.Click += this.ProfileUpButton_Click;
		// 
		// profileDownButton
		// 
		this.profileDownButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.profileDownButton.Location = new Point( 340, 8 );
		this.profileDownButton.Name = "profileDownButton";
		this.profileDownButton.Size = new Size( 28, 26 );
		this.profileDownButton.TabIndex = 2;
		this.profileDownButton.Text = "▼";
		this.profileDownButton.UseVisualStyleBackColor = true;
		this.profileDownButton.Click += this.ProfileDownButton_Click;
		// 
		// targetAppsLabel
		// 
		this.targetAppsLabel.AutoSize = true;
		this.targetAppsLabel.Location = new Point( 12, 45 );
		this.targetAppsLabel.Name = "targetAppsLabel";
		this.targetAppsLabel.Size = new Size( 72, 15 );
		this.targetAppsLabel.TabIndex = 4;
		this.targetAppsLabel.Text = "プロセス名(&A)";
		// 
		// targetAppsTextBox
		// 
		this.targetAppsTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		this.targetAppsTextBox.Location = new Point( 100, 42 );
		this.targetAppsTextBox.Name = "targetAppsTextBox";
		this.targetAppsTextBox.PlaceholderText = "全体一致 (; 区切り、* ? 可)";
		this.targetAppsTextBox.Size = new Size( 362, 23 );
		this.targetAppsTextBox.TabIndex = 5;
		this.targetAppsTextBox.TextChanged += this.TargetTextBox_TextChanged;
		// 
		// targetListButton
		// 
		this.targetListButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.targetListButton.Location = new Point( 468, 41 );
		this.targetListButton.Name = "targetListButton";
		this.targetListButton.Size = new Size( 88, 25 );
		this.targetListButton.TabIndex = 6;
		this.targetListButton.Text = "一覧(&L) ▼";
		this.targetListButton.UseVisualStyleBackColor = true;
		this.targetListButton.Click += this.TargetListButton_Click;
		// 
		// targetTitleLabel
		// 
		this.targetTitleLabel.AutoSize = true;
		this.targetTitleLabel.Location = new Point( 12, 76 );
		this.targetTitleLabel.Name = "targetTitleLabel";
		this.targetTitleLabel.Size = new Size( 74, 15 );
		this.targetTitleLabel.TabIndex = 7;
		this.targetTitleLabel.Text = "窓タイトル(&W)";
		// 
		// targetTitleTextBox
		// 
		this.targetTitleTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		this.targetTitleTextBox.Location = new Point( 100, 73 );
		this.targetTitleTextBox.Name = "targetTitleTextBox";
		this.targetTitleTextBox.PlaceholderText = "部分一致 (* ? を含むと全体一致。例: *メモ*)";
		this.targetTitleTextBox.Size = new Size( 362, 23 );
		this.targetTitleTextBox.TabIndex = 8;
		this.targetTitleTextBox.TextChanged += this.TargetTextBox_TextChanged;
		// 
		// targetSeparator
		// 
		this.targetSeparator.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		this.targetSeparator.BorderStyle = BorderStyle.Fixed3D;
		this.targetSeparator.Location = new Point( 12, 105 );
		this.targetSeparator.Name = "targetSeparator";
		this.targetSeparator.Size = new Size( 544, 2 );
		this.targetSeparator.TabIndex = 22;
		// 
		// levelLabel
		// 
		this.levelLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		this.levelLabel.AutoEllipsis = true;
		this.levelLabel.Location = new Point( 12, 117 );
		this.levelLabel.Name = "levelLabel";
		this.levelLabel.Size = new Size( 448, 18 );
		this.levelLabel.TabIndex = 9;
		this.levelLabel.Text = "ルート";
		// 
		// parentButton
		// 
		this.parentButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.parentButton.Location = new Point( 468, 112 );
		this.parentButton.Name = "parentButton";
		this.parentButton.Size = new Size( 88, 26 );
		this.parentButton.TabIndex = 10;
		this.parentButton.Text = "上の階層(&U)";
		this.parentButton.UseVisualStyleBackColor = true;
		this.parentButton.Click += this.ParentButton_Click;
		// 
		// listView
		// 
		this.listView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		this.listView.Columns.AddRange( new ColumnHeader[] { this.keyColumnHeader, this.titleColumnHeader, this.contentColumnHeader, this.hotkeyColumnHeader } );
		this.listView.FullRowSelect = true;
		this.listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
		this.listView.Location = new Point( 12, 144 );
		this.listView.MultiSelect = false;
		this.listView.Name = "listView";
		this.listView.ShowItemToolTips = true;
		this.listView.Size = new Size( 448, 292 );
		this.listView.TabIndex = 11;
		this.listView.UseCompatibleStateImageBehavior = false;
		this.listView.View = View.Details;
		this.listView.SelectedIndexChanged += this.ListView_SelectedIndexChanged;
		this.listView.DoubleClick += this.ListView_DoubleClick;
		this.listView.KeyDown += this.ListView_KeyDown;
		// 
		// keyColumnHeader
		// 
		this.keyColumnHeader.Text = "キー";
		this.keyColumnHeader.Width = 40;
		// 
		// titleColumnHeader
		// 
		this.titleColumnHeader.Text = "タイトル";
		this.titleColumnHeader.Width = 170;
		// 
		// contentColumnHeader
		// 
		this.contentColumnHeader.Text = "内容";
		this.contentColumnHeader.Width = 110;
		// 
		// hotkeyColumnHeader
		// 
		this.hotkeyColumnHeader.Text = "ホットキー";
		this.hotkeyColumnHeader.Width = 120;
		// 
		// editButton
		// 
		this.editButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.editButton.Location = new Point( 468, 144 );
		this.editButton.Name = "editButton";
		this.editButton.Size = new Size( 88, 28 );
		this.editButton.TabIndex = 12;
		this.editButton.Text = "編集(&E)...";
		this.editButton.UseVisualStyleBackColor = true;
		this.editButton.Click += this.EditButton_Click;
		// 
		// openButton
		// 
		this.openButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.openButton.Location = new Point( 468, 178 );
		this.openButton.Name = "openButton";
		this.openButton.Size = new Size( 88, 28 );
		this.openButton.TabIndex = 13;
		this.openButton.Text = "階層を開く(&O)";
		this.openButton.UseVisualStyleBackColor = true;
		this.openButton.Click += this.OpenButton_Click;
		// 
		// clearButton
		// 
		this.clearButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.clearButton.Location = new Point( 468, 212 );
		this.clearButton.Name = "clearButton";
		this.clearButton.Size = new Size( 88, 28 );
		this.clearButton.TabIndex = 14;
		this.clearButton.Text = "クリア(&D)";
		this.clearButton.UseVisualStyleBackColor = true;
		this.clearButton.Click += this.ClearButton_Click;
		// 
		// upButton
		// 
		this.upButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.upButton.Location = new Point( 468, 254 );
		this.upButton.Name = "upButton";
		this.upButton.Size = new Size( 88, 28 );
		this.upButton.TabIndex = 15;
		this.upButton.Text = "上へ(&P)";
		this.upButton.UseVisualStyleBackColor = true;
		this.upButton.Click += this.UpButton_Click;
		// 
		// downButton
		// 
		this.downButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
		this.downButton.Location = new Point( 468, 288 );
		this.downButton.Name = "downButton";
		this.downButton.Size = new Size( 88, 28 );
		this.downButton.TabIndex = 16;
		this.downButton.Text = "下へ(&N)";
		this.downButton.UseVisualStyleBackColor = true;
		this.downButton.Click += this.DownButton_Click;
		// 
		// menuHotkeyLabel
		// 
		this.menuHotkeyLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
		this.menuHotkeyLabel.AutoSize = true;
		this.menuHotkeyLabel.Location = new Point( 12, 452 );
		this.menuHotkeyLabel.Name = "menuHotkeyLabel";
		this.menuHotkeyLabel.Size = new Size( 111, 15 );
		this.menuHotkeyLabel.TabIndex = 17;
		this.menuHotkeyLabel.Text = "メニューを開くホットキー";
		// 
		// menuHotkeyTextBox
		// 
		this.menuHotkeyTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		this.menuHotkeyTextBox.Location = new Point( 168, 448 );
		this.menuHotkeyTextBox.Name = "menuHotkeyTextBox";
		this.menuHotkeyTextBox.ReadOnly = true;
		this.menuHotkeyTextBox.Size = new Size( 292, 23 );
		this.menuHotkeyTextBox.TabIndex = 18;
		this.menuHotkeyTextBox.TabStop = false;
		// 
		// menuHotkeyButton
		// 
		this.menuHotkeyButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		this.menuHotkeyButton.Location = new Point( 468, 447 );
		this.menuHotkeyButton.Name = "menuHotkeyButton";
		this.menuHotkeyButton.Size = new Size( 88, 25 );
		this.menuHotkeyButton.TabIndex = 19;
		this.menuHotkeyButton.Text = "変更(&K)...";
		this.menuHotkeyButton.UseVisualStyleBackColor = true;
		this.menuHotkeyButton.Click += this.MenuHotkeyButton_Click;
		// 
		// okButton
		// 
		this.okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		this.okButton.Location = new Point( 378, 486 );
		this.okButton.Name = "okButton";
		this.okButton.Size = new Size( 84, 28 );
		this.okButton.TabIndex = 20;
		this.okButton.Text = "OK";
		this.okButton.UseVisualStyleBackColor = true;
		this.okButton.Click += this.OkButton_Click;
		// 
		// cancelButton
		// 
		this.cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		this.cancelButton.DialogResult = DialogResult.Cancel;
		this.cancelButton.Location = new Point( 472, 486 );
		this.cancelButton.Name = "cancelButton";
		this.cancelButton.Size = new Size( 84, 28 );
		this.cancelButton.TabIndex = 21;
		this.cancelButton.Text = "キャンセル";
		this.cancelButton.UseVisualStyleBackColor = true;
		// 
		// LauncherForm
		// 
		this.AutoScaleDimensions = new SizeF( 7F, 15F );
		this.AutoScaleMode = AutoScaleMode.Font;
		this.CancelButton = this.cancelButton;
		this.ClientSize = new Size( 568, 526 );
		this.Controls.Add( this.profileLabel );
		this.Controls.Add( this.profileComboBox );
		this.Controls.Add( this.addProfileButton );
		this.Controls.Add( this.removeProfileButton );
		this.Controls.Add( this.profileUpButton );
		this.Controls.Add( this.profileDownButton );
		this.Controls.Add( this.targetAppsLabel );
		this.Controls.Add( this.targetAppsTextBox );
		this.Controls.Add( this.targetListButton );
		this.Controls.Add( this.targetTitleLabel );
		this.Controls.Add( this.targetTitleTextBox );
		this.Controls.Add( this.targetSeparator );
		this.Controls.Add( this.levelLabel );
		this.Controls.Add( this.parentButton );
		this.Controls.Add( this.listView );
		this.Controls.Add( this.editButton );
		this.Controls.Add( this.openButton );
		this.Controls.Add( this.clearButton );
		this.Controls.Add( this.upButton );
		this.Controls.Add( this.downButton );
		this.Controls.Add( this.menuHotkeyLabel );
		this.Controls.Add( this.menuHotkeyTextBox );
		this.Controls.Add( this.menuHotkeyButton );
		this.Controls.Add( this.okButton );
		this.Controls.Add( this.cancelButton );
		this.MaximizeBox = false;
		this.MinimizeBox = false;
		this.MinimumSize = new Size( 480, 456 );
		this.Name = "LauncherForm";
		this.ShowInTaskbar = false;
		this.StartPosition = FormStartPosition.CenterParent;
		this.Text = "ランチャーの設定";
		this.ResumeLayout( false );
		this.PerformLayout();
	}

	#endregion

	private Label profileLabel;
	private ComboBox profileComboBox;
	private Button addProfileButton;
	private Button removeProfileButton;
	private Button profileUpButton;
	private Button profileDownButton;
	private Label targetAppsLabel;
	private TextBox targetAppsTextBox;
	private Button targetListButton;
	private Label targetTitleLabel;
	private TextBox targetTitleTextBox;
	private Label targetSeparator;
	private Label levelLabel;
	private Button parentButton;
	private ListView listView;
	private ColumnHeader keyColumnHeader;
	private ColumnHeader titleColumnHeader;
	private ColumnHeader contentColumnHeader;
	private ColumnHeader hotkeyColumnHeader;
	private Button editButton;
	private Button openButton;
	private Button clearButton;
	private Button upButton;
	private Button downButton;
	private Label menuHotkeyLabel;
	private TextBox menuHotkeyTextBox;
	private Button menuHotkeyButton;
	private Button okButton;
	private Button cancelButton;
}
