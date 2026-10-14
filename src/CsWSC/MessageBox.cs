using System.Media;

namespace CsWSC;

/// <summary><see cref="ScriptGlobals.MessageBox"/> のボタンの組み合わせです。</summary>
public enum MsgButtons {
	/// <summary>OK です。</summary>
	OK,
	/// <summary>OK・キャンセルです。</summary>
	OKCancel,
	/// <summary>はい・いいえです。</summary>
	YesNo,
	/// <summary>はい・いいえ・キャンセルです。</summary>
	YesNoCancel,
	/// <summary>再試行・キャンセルです。</summary>
	RetryCancel,
	/// <summary>中止・再試行・無視です。</summary>
	AbortRetryIgnore,
}

/// <summary><see cref="ScriptGlobals.MessageBox"/> のアイコンです。表示に合わせたシステムの通知音も鳴ります。</summary>
public enum MsgIcon {
	/// <summary>アイコンなしです。</summary>
	None,
	/// <summary>情報です (i)。</summary>
	Information,
	/// <summary>警告です (!)。</summary>
	Warning,
	/// <summary>エラーです (×)。</summary>
	Error,
	/// <summary>問い合わせです (?)。</summary>
	Question,
}

/// <summary><see cref="ScriptGlobals.MessageBox"/> の結果です。</summary>
public enum MsgResult {
	/// <summary>結果なしです (通常は返らない)。</summary>
	None,
	/// <summary>OK です。</summary>
	OK,
	/// <summary>キャンセルです (キャンセルボタンがあるときの × ボタン・Esc を含む)。</summary>
	Cancel,
	/// <summary>はいです。</summary>
	Yes,
	/// <summary>いいえです。</summary>
	No,
	/// <summary>再試行です。</summary>
	Retry,
	/// <summary>中止です。</summary>
	Abort,
	/// <summary>無視です。</summary>
	Ignore,
	/// <summary>タイムアウトで閉じました。</summary>
	Timeout,
}

/// <summary>MSGBOX のダイアログです。スクリプトのスレッド (STA) でモーダル表示します。</summary>
internal sealed class MessageBoxForm : Form {
	private readonly MsgIcon _icon;
	private MsgResult _result = MsgResult.None;
	private MsgResult _escapeResult = MsgResult.None;

	public MessageBoxForm( string message, string? title, MsgButtons buttons, MsgIcon icon ) {
		_icon = icon;
		Text = title ?? "CsWSC";
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = MinimizeBox = false;
		ShowInTaskbar = true;
		StartPosition = FormStartPosition.CenterScreen;
		TopMost = true;
		AutoSize = true;
		AutoSizeMode = AutoSizeMode.GrowAndShrink;
		Padding = new Padding( 12 );
		Font = SystemFonts.MessageBoxFont ?? Font;

		var layout = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
		Controls.Add( layout );
		if( SystemIconOf( icon ) is { } sys ) {
			layout.Controls.Add( new PictureBox { Image = sys.ToBitmap(), Size = new Size( 32, 32 ), Margin = new Padding( 0, 0, 12, 0 ) }, 0, 0 );
		}
		layout.Controls.Add( new Label { Text = message.ReplaceLineEndings( "\r\n" ), AutoSize = true, MinimumSize = new Size( 0, 32 ), MaximumSize = new Size( 600, 0 ), TextAlign = ContentAlignment.MiddleLeft }, 1, 0 );

		var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding( 0, 12, 0, 0 ) };
		layout.Controls.Add( panel, 0, 1 );
		layout.SetColumnSpan( panel, 2 );

		var defs = ButtonsOf( buttons );
		Button? first = null;
		foreach( var (text, result) in defs.Reverse() ) { // RightToLeft なので逆順に追加
			var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size( 80, 0 ) };
			b.Click += ( _, _ ) => { _result = result; DialogResult = DialogResult.OK; };
			panel.Controls.Add( b );
			first = b;
			// Esc / × で閉じたときの結果。Win32 の MessageBox と同じく、キャンセルが無ければ OK のみの場合だけ OK
			if( result == MsgResult.Cancel || buttons == MsgButtons.OK ) {
				CancelButton = b;
				_escapeResult = result;
			}
		}
		AcceptButton = first;
		ActiveControl = first;
		// はい・いいえ / 中止・再試行・無視 は × ボタンで閉じられない (Win32 の MessageBox と同じ)
		ControlBox = CancelButton != null;
	}

	private static (string, MsgResult)[] ButtonsOf( MsgButtons buttons ) => buttons switch {
		MsgButtons.OKCancel => [("OK", MsgResult.OK), ("キャンセル", MsgResult.Cancel)],
		MsgButtons.YesNo => [("はい", MsgResult.Yes), ("いいえ", MsgResult.No)],
		MsgButtons.YesNoCancel => [("はい", MsgResult.Yes), ("いいえ", MsgResult.No), ("キャンセル", MsgResult.Cancel)],
		MsgButtons.RetryCancel => [("再試行", MsgResult.Retry), ("キャンセル", MsgResult.Cancel)],
		MsgButtons.AbortRetryIgnore => [("中止", MsgResult.Abort), ("再試行", MsgResult.Retry), ("無視", MsgResult.Ignore)],
		_ => [("OK", MsgResult.OK)],
	};

	private static Icon? SystemIconOf( MsgIcon icon ) => icon switch {
		MsgIcon.Information => SystemIcons.Information,
		MsgIcon.Warning => SystemIcons.Warning,
		MsgIcon.Error => SystemIcons.Error,
		MsgIcon.Question => SystemIcons.Question,
		_ => null,
	};

	private static SystemSound? SoundOf( MsgIcon icon ) => icon switch {
		MsgIcon.Information => SystemSounds.Asterisk,
		MsgIcon.Warning => SystemSounds.Exclamation,
		MsgIcon.Error => SystemSounds.Hand,
		MsgIcon.Question => SystemSounds.Question,
		_ => null,
	};

	/// <summary>モーダル表示して結果を返します。timeoutMs ミリ秒 (0 以下で無制限) で閉じます。停止要求でも閉じ、OperationCanceledException を投げます。</summary>
	public MsgResult Run( int timeoutMs, CancellationToken cancellation ) {
		var timedOut = false;
		using var timer = new System.Windows.Forms.Timer();
		if( timeoutMs > 0 ) {
			timer.Interval = timeoutMs;
			timer.Tick += ( _, _ ) => { timer.Stop(); timedOut = true; Close(); };
			Shown += ( _, _ ) => timer.Start();
		}
		using var reg = cancellation.Register( () => {
			try {
				if( IsHandleCreated ) {
					BeginInvoke( Close );
				}
			} catch( InvalidOperationException ) { }
		} );
		Shown += ( _, _ ) => {
			if( cancellation.IsCancellationRequested ) { Close(); return; }
			Activate();
			SoundOf( _icon )?.Play();
		};
		ShowDialog();
		cancellation.ThrowIfCancellationRequested();
		if( timedOut ) {
			return MsgResult.Timeout;
		}
		return _result == MsgResult.None ? _escapeResult : _result;
	}
}
