namespace CsWSC;

/// <summary>INPUT のダイアログです。UWSC と同じく説明文・入力欄・OK/キャンセルを縦に並べ、最前面にモーダル表示します。</summary>
internal sealed partial class InputBoxForm : Form {
	/// <summary>デザイナー用のコンストラクターです。</summary>
	public InputBoxForm() {
		InitializeComponent();
	}

	public InputBoxForm( string prompt, string defaultValue, string scriptName ) : this() {
		Text = scriptName == "" ? "CsWSC" : $"CsWSC - {scriptName}";
		Font = SystemFonts.MessageBoxFont ?? Font;
		promptLabel.Text = prompt.ReplaceLineEndings( "\r\n" );
		inputTextBox.Text = defaultValue;
		ActiveControl = inputTextBox;
	}

	/// <summary>モーダル表示して入力された文字列を返します。キャンセル時は空文字です。停止要求でも閉じ、OperationCanceledException を投げます。</summary>
	public string Run( CancellationToken cancellation ) {
		using var reg = cancellation.Register( () => {
			try {
				if( IsHandleCreated ) {
					BeginInvoke( Close );
				}
			} catch( InvalidOperationException ) { }
		} );
		Shown += ( _, _ ) => {
			if( cancellation.IsCancellationRequested ) { Close(); return; }
			ForceForeground();
			inputTextBox.Focus();
			inputTextBox.SelectAll();
		};
		var result = ShowDialog();
		cancellation.ThrowIfCancellationRequested();
		return result == DialogResult.OK ? inputTextBox.Text : "";
	}

	// 別のアプリが前面にあると SetForegroundWindow は拒否される (タスクバーが点滅するだけ)。
	// 直前にキー入力があると許可されるので、Alt を押して離してからアクティブにする
	// (AttachThreadInput で奪う方法は、入力はできてもタイトルバーが非アクティブ色のままになる)
	private void ForceForeground() {
		const ushort VK_MENU = 0x12;
		if( NativeMethods.GetForegroundWindow() != Handle ) {
			NativeMethods.Send( NativeMethods.Key( VK_MENU, 0, NativeMethods.KEYEVENTF_KEYDOWN ), NativeMethods.Key( VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP ) );
		}
		NativeMethods.SetForegroundWindow( Handle );
		Activate();
	}
}
