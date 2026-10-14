using static CsWSC.NativeMethods;

namespace CsWSC;

/// <summary>
/// ウィンドウの中のコントロール (ボタン・入力欄などの子ウィンドウ) です。UWSC の GETID(GET_FROMPOINT_OBJ) や GETCTLHND で得るハンドルに相当します。
/// </summary>
/// <remarks>
/// HWND を持つコントロール (Win32・WinForms など) が対象です。WPF・ブラウザーなど、コントロールが子ウィンドウになっていないアプリでは取得できません。
/// </remarks>
public sealed partial class Control : ControlBase {
	/// <summary>ハンドルを検証せずに Control を作ります。</summary>
	/// <param name="handle">子ウィンドウのハンドルです。</param>
	internal Control( IntPtr handle ) : base( handle ) { }

	/// <summary>親 (コントロールの中にあれば <see cref="Control"/>、ウィンドウ直下なら <see cref="Window"/>) です。取得できなければ null です。</summary>
	public ControlBase? Parent {
		get {
			var p = GetAncestor( Opened(), GA_PARENT );
			return p == IntPtr.Zero || !IsWindow( p ) ? null : Wrap( p );
		}
	}

	/// <summary>このコントロールがあるトップレベルウィンドウです。取得できなければ null です。</summary>
	public Window? TopWindow {
		get {
			var r = GetAncestor( Opened(), GA_ROOT );
			return r == IntPtr.Zero || r == Handle ? null : new Window( r );
		}
	}

	/// <summary>テキストを取得します (GETSTR)。<see cref="ControlBase.Text"/> と同じです。</summary>
	/// <returns>コントロールのテキストです。</returns>
	public string GetText() {
		ScriptStop.Check();
		return Text;
	}

	/// <summary>テキストを設定します (SENDSTR)。append = true なら末尾に追記します。</summary>
	/// <param name="text">設定する文字列です。</param>
	/// <param name="append">true なら末尾に追記 (エディット系のみ)、false なら置き換えます。</param>
	/// <returns>コントロールが受け付ければ true です (応答が無い・拒否されたら false)。追記は送れたかだけを見ます。</returns>
	public bool SetText( string text, bool append = false ) {
		ScriptStop.Check();
		return SetText( Opened(), text, append );
	}

	/// <summary>指定ハンドルのコントロールへテキストを設定します。</summary>
	/// <param name="target">コントロールのハンドルです。</param>
	/// <param name="text">設定する文字列です。</param>
	/// <param name="append">true なら末尾に追記、false なら置き換えます。</param>
	/// <returns>コントロールが受け付ければ true です (応答が無い・拒否されたら false)。追記は送れたかだけを見ます。</returns>
	internal static bool SetText( IntPtr target, string text, bool append ) {
		if( append ) {
			// キャレットを末尾へ (EM_SETSEL の -1 は選択解除だけで末尾には移らない)
			var end = GetControlText( target ).Length;
			SendMessageTimeout( target, EM_SETSEL, end, end, SMTO_ABORTIFHUNG, 1000, out _ );
			// EM_REPLACESEL は結果を返さない
			return SendMessageTimeout( target, EM_REPLACESEL, 0, text, SMTO_ABORTIFHUNG, 1000, out _ ) != 0;
		}
		// WM_SETTEXT は設定できれば TRUE。エディットの容量不足は FALSE、リスト・コンボは負のエラー値 (LB_ERRSPACE など)
		return SendMessageTimeout( target, WM_SETTEXT, 0, text, SMTO_ABORTIFHUNG, 1000, out var result ) != 0
			&& (long)result > 0;
	}

	/// <summary>位置を変更します。サイズを省略すると現在のサイズのままです。</summary>
	/// <param name="x">左端の X 座標です (親のクライアント領域の左上が基準)。</param>
	/// <param name="y">上端の Y 座標です (親のクライアント領域の左上が基準)。</param>
	/// <param name="width">幅です。null なら現在の幅です。</param>
	/// <param name="height">高さです。null なら現在の高さです。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control MoveControl( int x, int y, int? width = null, int? height = null ) {
		ScriptStop.Check();
		var b = RawBounds;
		MoveWindow( Handle, x, y, width ?? b.Width, height ?? b.Height, true );
		return this;
	}

	/// <summary><see cref="MoveControl(int, int, int?, int?)"/> の座標を Point で指定する版です。</summary>
	/// <param name="point">左上の座標です (親のクライアント領域の左上が基準)。</param>
	/// <param name="width">幅です。null なら現在の幅です。</param>
	/// <param name="height">高さです。null なら現在の高さです。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control MoveControl( Point point, int? width = null, int? height = null ) => MoveControl( point.X, point.Y, width, height );

	/// <summary>サイズだけ変更します。</summary>
	/// <param name="width">幅です。</param>
	/// <param name="height">高さです。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control Resize( int width, int height ) {
		ScriptStop.Check();
		SetWindowPos( Opened(), IntPtr.Zero, 0, 0, width, height, SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER );
		return this;
	}

	/// <summary>入力の有効・無効を切り替えます。</summary>
	/// <param name="enabled">true なら有効、false なら無効です。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control SetEnabled( bool enabled = true ) {
		SetEnabledCore( enabled );
		return this;
	}

	/// <summary>非表示にします。</summary>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control Hide() {
		ShowCore( SW_HIDE );
		return this;
	}

	/// <summary>表示します。</summary>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control Show() {
		ShowCore( SW_SHOW );
		return this;
	}

	/// <summary>コントロール相対座標 (クライアント領域基準) へマウスを移動します。UWSC の MOUSEORG + MMV に相当します。</summary>
	/// <param name="point">移動先です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。UWSC の MMV の第 3 引数と同じく、待ってから移動します。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Control Move( Point point, int delayMs = 0 ) {
		MouseMoveCore( point, delayMs );
		return this;
	}

	/// <inheritdoc cref="Move(Point, int)"/>
	/// <param name="x">X 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Control Move( int x, int y, int delayMs = 0 ) => Move( new Point( x, y ), delayMs );

	/// <summary>コントロール相対座標 (クライアント領域基準) へマウスを移動してからボタンを操作します。UWSC の MOUSEORG + BTN に相当します。</summary>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="point">操作する位置です (コントロールのクライアント領域の左上が基準)。null なら移動せず現在のマウス位置で操作します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動・操作します。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <remarks><see cref="KeyAction.Down"/> と <see cref="KeyAction.Up"/> を組み合わせるとドラッグできます。</remarks>
	public Control Mouse( MouseButton button, KeyAction action, Point? point = null, int delayMs = 0 ) {
		MouseCore( point, button, action, delayMs );
		return this;
	}

	/// <inheritdoc cref="Mouse(MouseButton, KeyAction, Point?, int)"/>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="x">X 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Control Mouse( MouseButton button, KeyAction action, int x, int y, int delayMs = 0 ) => Mouse( button, action, new Point( x, y ), delayMs );

	/// <summary>コントロール相対座標 (クライアント領域基準) へマウスを移動してからホイールを回します。UWSC の MOUSEORG + BTN(WHEEL) に相当します。</summary>
	/// <param name="notches">回す量です (ノッチ数)。正の値で下 (手前) 方向、負の値で上方向です。</param>
	/// <param name="point">操作する位置です (コントロールのクライアント領域の左上が基準)。null なら移動せず現在のマウス位置で回します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動・操作します。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Control Wheel( int notches, Point? point = null, int delayMs = 0 ) {
		WheelCore( notches, point, delayMs );
		return this;
	}

	/// <inheritdoc cref="Wheel(int, Point?, int)"/>
	/// <param name="notches">回す量です (ノッチ数)。正の値で下 (手前) 方向、負の値で上方向です。</param>
	/// <param name="x">X 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Control Wheel( int notches, int x, int y, int delayMs = 0 ) => Wheel( notches, new Point( x, y ), delayMs );

	/// <summary>コントロール相対座標 (クライアント領域基準) にある最も深い子ウィンドウ (無ければ自身) へボタン操作を送ります。前面に出しません。UWSC の MOUSEORG(id, MORG_DIRECT) + BTN に相当します。</summary>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="point">操作する位置です (コントロールのクライアント領域の左上が基準)。null なら現在のマウス位置です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから送ります。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <remarks>マウスカーソルは動きません。アプリによっては反応しません (特に Down/Up によるドラッグ)。</remarks>
	public Control PostMouse( MouseButton button, KeyAction action, Point? point = null, int delayMs = 0 ) {
		PostMouseCore( point, button, action, delayMs );
		return this;
	}

	/// <inheritdoc cref="PostMouse(MouseButton, KeyAction, Point?, int)"/>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="x">X 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (コントロールのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Control PostMouse( MouseButton button, KeyAction action, int x, int y, int delayMs = 0 ) => PostMouse( button, action, new Point( x, y ), delayMs );

	/// <summary>このコントロールへキーを送ります。前面に出しません。</summary>
	/// <param name="key">送るキーです。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから送ります。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control PostKey( Keys key, int delayMs = 0 ) {
		PostKeyCore( key, delayMs );
		return this;
	}

	/// <summary>このコントロールへ文字列を送ります (WM_CHAR)。前面に出しません。</summary>
	/// <param name="text">送る文字列です。改行は CR に変換します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから送ります。</param>
	/// <returns>このコントロールです (メソッドチェーン用)。</returns>
	public Control PostText( string text, int delayMs = 0 ) {
		PostTextCore( text, delayMs );
		return this;
	}

	/// <inheritdoc/>
	private protected override IntPtr KeyTarget() => Handle;

	/// <summary>"Control(0xハンドル, "クラス名", "テキスト")" 形式の文字列です。</summary>
	public override string ToString() => $"Control(0x{Handle:X}, \"{GetClass( Handle )}\", \"{GetControlText( Handle )}\")"; // 閉じていても例外にしない

	/// <summary>スクリーン座標の位置のコントロールです (WindowFromPoint)。</summary>
	/// <param name="x">X 座標です (画面座標)。</param>
	/// <param name="y">Y 座標です (画面座標)。</param>
	/// <returns>その位置のコントロールです。無いか、トップレベルウィンドウしか無ければ null です。</returns>
	internal static Control? FromPoint( int x, int y ) {
		var h = WindowFromPoint( new POINT { X = x, Y = y } );
		return h == IntPtr.Zero || IsTopLevel( h ) ? null : new Control( h );
	}
}
