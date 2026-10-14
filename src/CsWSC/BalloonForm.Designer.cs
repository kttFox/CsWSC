#nullable enable

namespace CsWSC;

partial class BalloonForm {
	/// <summary>
	/// 必要なデザイナー変数です。
	/// </summary>
	private System.ComponentModel.IContainer? components = null;

	/// <summary>
	/// 使用中のリソースをすべてクリーンアップします。
	/// </summary>
	/// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
	protected override void Dispose( bool disposing ) {
		if( disposing ) {
			components?.Dispose();
			_path?.Dispose();
		}
		base.Dispose( disposing );
	}

	#region Windows フォーム デザイナーで生成されたコード

	/// <summary>
	/// デザイナー サポートに必要なメソッドです。このメソッドの内容を
	/// コード エディターで変更しないでください。
	/// </summary>
	private void InitializeComponent() {
		SuspendLayout();
		// 
		// BalloonForm
		// 
		AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb( 255, 255, 225 );
		ClientSize = new Size( 200, 60 );
		FormBorderStyle = FormBorderStyle.None;
		Font = new Font( "Yu Gothic UI", 10F );
		ForeColor = Color.Black;
		Name = "BalloonForm";
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.Manual;
		TopMost = true;
		ResumeLayout( false );
	}

	#endregion
}
