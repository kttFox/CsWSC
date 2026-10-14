using static CsWSC.NativeMethods;

namespace CsWSC;

/// <summary>スクリプトから操作するトップレベルウィンドウです。UWSC のウィンドウ ID に相当します。中のボタンや入力欄は <see cref="Control"/> です。</summary>
public sealed partial class Window : ControlBase {
	/// <summary>ハンドルを検証せずに Window を作ります。</summary>
	/// <param name="handle">ウィンドウハンドルです。</param>
	internal Window( IntPtr handle ) : base( handle ) { }

	/// <summary>ウィンドウハンドルから Window を作ります。無効なハンドルなら null です。</summary>
	/// <param name="handle">ウィンドウハンドルです。</param>
	/// <returns>作成した Window です。ハンドルが 0 または無効なら null です。</returns>
	public static Window? FromHandle( IntPtr handle ) => handle != IntPtr.Zero && IsWindow( handle ) ? new( handle ) : null;

	/// <summary>タイトルです (ST_TITLE)。</summary>
	public string Title => GetTitle( Opened() );

	/// <summary>見た目の範囲 → GetWindowRect の範囲に変換します (影の幅を足す)。</summary>
	/// <param name="x">見た目の左端です (画面座標)。</param>
	/// <param name="y">見た目の上端です (画面座標)。</param>
	/// <param name="width">見た目の幅です。</param>
	/// <param name="height">見た目の高さです。</param>
	/// <returns>影を含めた範囲です。</returns>
	private Rectangle ToRaw( int x, int y, int width, int height ) {
		var raw = RawBounds; var vis = Bounds;
		int l = vis.Left - raw.Left, t = vis.Top - raw.Top, r = raw.Right - vis.Right, b = raw.Bottom - vis.Bottom;
		return new Rectangle( x - l, y - t, width + l + r, height + t + b );
	}

	/// <summary>前面に出してアクティブにします (最小化されていれば元に戻す)。</summary>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window Activate() {
		ScriptStop.Check();
		if( IsIconic( Opened() ) ) {
			ShowWindow( Handle, SW_RESTORE );
		}

		SetForegroundWindow( Handle );
		return this;
	}

	/// <summary>位置を変更します。サイズを省略すると現在のサイズのままです。</summary>
	/// <param name="x">左端の X 座標です (画面座標)。</param>
	/// <param name="y">上端の Y 座標です (画面座標)。</param>
	/// <param name="width">幅です。null なら現在の幅です。</param>
	/// <param name="height">高さです。null なら現在の高さです。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window MoveWindow( int x, int y, int? width = null, int? height = null ) {
		ScriptStop.Check();
		var b = Bounds;
		var raw = ToRaw( x, y, width ?? b.Width, height ?? b.Height );
		NativeMethods.MoveWindow( Handle, raw.X, raw.Y, raw.Width, raw.Height, true );
		return this;
	}

	/// <summary><see cref="MoveWindow(int, int, int?, int?)"/> の座標を Point で指定する版です。</summary>
	/// <param name="point">左上の座標です (画面座標)。</param>
	/// <param name="width">幅です。null なら現在の幅です。</param>
	/// <param name="height">高さです。null なら現在の高さです。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window MoveWindow( Point point, int? width = null, int? height = null ) => MoveWindow( point.X, point.Y, width, height );

	// ---- 状態 (UWSC の STATUS に相当) ----

	/// <summary>アクティブかどうかです (ST_ACTIVE)。</summary>
	public bool IsActive => GetForegroundWindow() == Handle;
	/// <summary>最小化されているかどうかです (ST_ICONIC)。</summary>
	public bool Minimized => IsIconic( Handle );
	/// <summary>最大化されているかどうかです (ST_MAXIMIZED)。</summary>
	public bool Maximized => IsZoomed( Handle );
	/// <summary>応答なしかどうかです (ST_BUSY)。</summary>
	public bool Busy => IsHungAppWindow( Handle );
	/// <summary>常に手前かどうかです (ST_TOPMOST)。</summary>
	public bool Topmost => ( GetWindowLongPtr( Handle, GWL_EXSTYLE ).ToInt64() & WS_EX_TOPMOST ) != 0;

	/// <summary>プロセス名です (拡張子なし)。取得できなければ空文字です。</summary>
	public string ProcessName {
		get {
			try {
				using var p = System.Diagnostics.Process.GetProcessById( ProcessId );
				return p.ProcessName;
			} catch( Exception ) {
				return "";
			}
		}
	}

	/// <summary>実行ファイルのパスです (ST_PATH)。権限不足などで取得できなければ空文字です。</summary>
	public string ProcessPath {
		get {
			try {
				using var p = System.Diagnostics.Process.GetProcessById( ProcessId );
				return p.MainModule?.FileName ?? "";
			} catch( Exception ) {
				return "";
			}
		}
	}

	// ---- 制御 (UWSC の CTRLWIN / ACW に相当) ----

	/// <summary>閉じる要求 (WM_CLOSE) を送ります (CTRLWIN の CLOSE)。閉じるのを待たずに戻ります。</summary>
	public void Close() => PostMessage( Opened(), WM_CLOSE, 0, 0 );

	/// <summary>プロセスごと強制終了します (CTRLWIN の CLOSE2)。</summary>
	public void Kill() {
		using var p = System.Diagnostics.Process.GetProcessById( ProcessId );
		p.Kill();
	}

	/// <summary>サイズだけ変更します。</summary>
	/// <param name="width">幅です (影を除いた見た目のサイズ)。</param>
	/// <param name="height">高さです (影を除いた見た目のサイズ)。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window Resize( int width, int height ) {
		ScriptStop.Check();
		var raw = ToRaw( 0, 0, width, height );
		SetWindowPos( Opened(), IntPtr.Zero, 0, 0, raw.Width, raw.Height, SWP_NOMOVE | SWP_NOACTIVATE | SWP_NOZORDER );
		return this;
	}

	/// <summary>常に手前に表示するかを設定します (CTRLWIN の TOPMOST / NOTOPMOST)。</summary>
	/// <param name="topmost">true なら常に手前、false なら解除します。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window SetTopmost( bool topmost = true ) {
		// 解除は後片付け (finally) にも使うので確認しない
		if( topmost ) {
			ScriptStop.Check();
		}
		SetWindowPos( Opened(), topmost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE );
		return this;
	}

	/// <summary>入力の有効・無効を切り替えます。</summary>
	/// <param name="enabled">true なら有効、false なら無効です。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window SetEnabled( bool enabled = true ) {
		SetEnabledCore( enabled );
		return this;
	}

	/// <summary>アクティブになるまで待ちます。</summary>
	/// <param name="timeoutMs">待つミリ秒数です。負の値なら無制限です。</param>
	/// <returns>時間内にアクティブになれば true、タイムアウトしたら false です。</returns>
	public bool WaitActive( int timeoutMs = 2000 ) {
		ScriptStop.Check();
		return WaitUntil( () => IsActive, timeoutMs );
	}

	// ---- 文字列 (UWSC の GETSTR / SENDSTR に相当) ----

	/// <summary>
	/// 指定クラス (既定: Edit 系) の n 番目 (1 から) の子孫コントロールのテキストを取得します (GETSTR)。
	/// 無ければ空文字です。
	/// </summary>
	/// <param name="index">何番目のコントロールかを 1 から数えた値です。</param>
	/// <param name="className">対象のクラス名です (部分一致)。</param>
	/// <returns>コントロールのテキストです。該当が無ければ空文字です。</returns>
	public string GetText( int index = 1, string className = "Edit" ) {
		var c = GetChildren( className );
		return index >= 1 && index <= c.Length ? c[index - 1].Text : "";
	}

	/// <summary>
	/// 指定クラス (既定: Edit 系) の n 番目 (1 から) の子孫コントロールへ文字列を設定します (SENDSTR)。
	/// append = true なら末尾に追記します。index = 0 ならフォーカスのあるコントロールへ設定します。成功すれば true です。
	/// </summary>
	/// <param name="text">設定する文字列です。</param>
	/// <param name="index">何番目のコントロールかを 1 から数えた値です。0 ならフォーカスのあるコントロールです。</param>
	/// <param name="append">true なら末尾に追記、false なら置き換えます。</param>
	/// <param name="className">対象のクラス名です (部分一致)。index = 0 のときは使いません。</param>
	/// <returns>コントロールが受け付ければ true です (該当が無い・応答が無い・拒否されたら false)。追記は送れたかだけを見ます。</returns>
	public bool SetText( string text, int index = 1, bool append = false, string className = "Edit" ) {
		ScriptStop.Check();
		IntPtr target;
		if( index == 0 ) {
			target = FocusedChild();
		} else {
			var c = GetChildren( className );
			if( index < 1 || index > c.Length ) {
				return false;
			}

			target = c[index - 1].Handle;
		}
		return Control.SetText( target, text, append );
	}

	/// <summary>最小化します (CTRLWIN の MIN)。</summary>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window Minimize() {
		ShowCore( SW_MINIMIZE );
		return this;
	}

	/// <summary>最大化します (CTRLWIN の MAX)。</summary>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window Maximize() {
		ShowCore( SW_MAXIMIZE );
		return this;
	}

	/// <summary>最小化・最大化を元に戻します (CTRLWIN の NORMAL)。</summary>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window Restore() {
		ShowCore( SW_RESTORE );
		return this;
	}

	/// <summary>非表示にします (CTRLWIN の HIDE)。</summary>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window Hide() {
		ShowCore( SW_HIDE );
		return this;
	}

	/// <summary>表示します (CTRLWIN の SHOW)。</summary>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window Show() {
		ShowCore( SW_SHOW );
		return this;
	}

	/// <summary>ウィンドウ相対座標 (クライアント領域基準) へマウスを移動します。UWSC の MOUSEORG + MMV に相当します。</summary>
	/// <param name="point">移動先です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。UWSC の MMV の第 3 引数と同じく、待ってから移動します。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウが閉じられています。</exception>
	public Window Move( Point point, int delayMs = 0 ) {
		MouseMoveCore( point, delayMs );
		return this;
	}

	/// <inheritdoc cref="Move(Point, int)"/>
	/// <param name="x">X 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Window Move( int x, int y, int delayMs = 0 ) => Move( new Point( x, y ), delayMs );

	/// <summary>ウィンドウ相対座標 (クライアント領域基準) へマウスを移動してからボタンを操作します。UWSC の MOUSEORG + BTN に相当します。</summary>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="point">操作する位置です (ウィンドウのクライアント領域の左上が基準)。null なら移動せず現在のマウス位置で操作します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動・操作します。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウが閉じられています。</exception>
	/// <remarks><see cref="KeyAction.Down"/> と <see cref="KeyAction.Up"/> を組み合わせるとドラッグできます。</remarks>
	public Window Mouse( MouseButton button, KeyAction action, Point? point = null, int delayMs = 0 ) {
		MouseCore( point, button, action, delayMs );
		return this;
	}

	/// <inheritdoc cref="Mouse(MouseButton, KeyAction, Point?, int)"/>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="x">X 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Window Mouse( MouseButton button, KeyAction action, int x, int y, int delayMs = 0 ) => Mouse( button, action, new Point( x, y ), delayMs );

	/// <summary>ウィンドウ相対座標 (クライアント領域基準) へマウスを移動してからホイールを回します。UWSC の MOUSEORG + BTN(WHEEL) に相当します。</summary>
	/// <param name="notches">回す量です (ノッチ数)。正の値で下 (手前) 方向、負の値で上方向です。</param>
	/// <param name="point">操作する位置です (ウィンドウのクライアント領域の左上が基準)。null なら移動せず現在のマウス位置で回します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動・操作します。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウが閉じられています。</exception>
	public Window Wheel( int notches, Point? point = null, int delayMs = 0 ) {
		WheelCore( notches, point, delayMs );
		return this;
	}

	/// <inheritdoc cref="Wheel(int, Point?, int)"/>
	/// <param name="notches">回す量です (ノッチ数)。正の値で下 (手前) 方向、負の値で上方向です。</param>
	/// <param name="x">X 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Window Wheel( int notches, int x, int y, int delayMs = 0 ) => Wheel( notches, new Point( x, y ), delayMs );

	/// <summary>アクティブにし、なったのを確認してから文字列を入力します。</summary>
	/// <param name="text">入力する文字列です。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">アクティブにできませんでした。</exception>
	public Window SendText( string text ) {
		ActivateOrThrow();
		Input.SendText( text );
		return this;
	}

	/// <summary>アクティブにし、なったのを確認してからキーを押します。複数指定で同時押しします (例: Keys.ControlKey, Keys.S)。</summary>
	/// <param name="keys">押すキーです。複数なら指定順に押し、逆順に離します。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">アクティブにできませんでした。</exception>
	public Window ShortcutKey( params IEnumerable<Keys> keys ) {
		ActivateOrThrow();
		Input.ShortcutKey( keys );
		return this;
	}

	/// <summary>アクティブにして、なるまで待ちます。2 秒以内にならなければ例外です。</summary>
	private void ActivateOrThrow() {
		if( !Activate().WaitActive( 2000 ) ) {
			throw new InvalidOperationException( $"{this} をアクティブにできませんでした。" );
		}
	}

	// ---- バックグラウンド操作 (アクティブにせずメッセージを送る) ----

	/// <summary>ウィンドウ相対座標 (クライアント領域基準) にある最も深い子ウィンドウ (無ければ自身) へボタン操作を送ります。前面に出しません。UWSC の MOUSEORG(id, MORG_DIRECT) + BTN に相当します。</summary>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="point">操作する位置です (ウィンドウのクライアント領域の左上が基準)。null なら現在のマウス位置です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから送ります。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウが閉じられています。</exception>
	/// <remarks>マウスカーソルは動きません。アプリによっては反応しません (特に Down/Up によるドラッグ)。</remarks>
	public Window PostMouse( MouseButton button, KeyAction action, Point? point = null, int delayMs = 0 ) {
		PostMouseCore( point, button, action, delayMs );
		return this;
	}

	/// <inheritdoc cref="PostMouse(MouseButton, KeyAction, Point?, int)"/>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action"><see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す) です。</param>
	/// <param name="x">X 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (ウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public Window PostMouse( MouseButton button, KeyAction action, int x, int y, int delayMs = 0 ) => PostMouse( button, action, new Point( x, y ), delayMs );

	/// <summary>このウィンドウのスレッドでフォーカスのあるコントロールへキーを送ります。前面に出しません。</summary>
	/// <param name="key">送るキーです。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから送ります。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window PostKey( Keys key, int delayMs = 0 ) {
		PostKeyCore( key, delayMs );
		return this;
	}

	/// <summary>このウィンドウのスレッドでフォーカスのあるコントロールへ文字列を送ります (WM_CHAR)。前面に出しません。</summary>
	/// <param name="text">送る文字列です。改行は CR に変換します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから送ります。</param>
	/// <returns>このウィンドウです (メソッドチェーン用)。</returns>
	public Window PostText( string text, int delayMs = 0 ) {
		PostTextCore( text, delayMs );
		return this;
	}

	/// <inheritdoc/>
	private protected override IntPtr KeyTarget() => FocusedChild();

	/// <summary>このウィンドウのスレッドでフォーカスを持つコントロールです。取得できなければこのウィンドウ自身です。</summary>
	/// <returns>コントロールのハンドルです。</returns>
	private IntPtr FocusedChild() {
		var info = new GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
		var tid = GetWindowThreadProcessId( Handle, out _ );
		return GetGUIThreadInfo( tid, ref info ) && info.hwndFocus != IntPtr.Zero ? info.hwndFocus : Handle;
	}

	/// <summary>"Window(0xハンドル, "タイトル")" 形式の文字列です。</summary>
	public override string ToString() => $"Window(0x{Handle:X}, \"{GetTitle( Handle )}\")"; // 閉じていても例外にしない

	/// <summary>タイトル (部分一致) とクラス名 (部分一致) で可視ウィンドウを探します。</summary>
	/// <param name="title">タイトルです (部分一致)。null または空なら、クラス名指定時はタイトルで絞り込まず、クラス名も無ければタイトルのあるウィンドウです。</param>
	/// <param name="className">クラス名です (部分一致)。null または空なら絞り込みません。</param>
	/// <returns>最も手前の一致したウィンドウです。無ければ null です。</returns>
	internal static Window? Find( string title, string? className ) => FindAll( title, className, firstOnly: true ).FirstOrDefault();

	/// <summary>条件に合う可視トップレベルウィンドウを Z オーダー順 (手前から) に列挙します。</summary>
	/// <param name="title">タイトルです (部分一致)。null または空なら、クラス名指定時はタイトルで絞り込まず、クラス名も無ければタイトルのあるウィンドウすべてです。</param>
	/// <param name="className">クラス名です (部分一致)。null または空なら絞り込みません。</param>
	/// <param name="firstOnly">true なら最初の 1 件で打ち切ります。</param>
	/// <returns>一致したウィンドウのリストです。</returns>
	internal static List<Window> FindAll( string? title, string? className, bool firstOnly = false ) {
		var list = new List<Window>();
		EnumWindows( ( h, _ ) => {
			if( !IsWindowVisible( h ) ) {
				return true;
			}

			var t = GetTitle( h );
			// タイトル省略時: クラス名指定があればタイトルで絞り込まない (タスクバーなどタイトルが空のウィンドウも対象)。無ければタイトルのあるウィンドウだけ
			if( string.IsNullOrEmpty( title ) ? string.IsNullOrEmpty( className ) && t.Length == 0 : !t.Contains( title, StringComparison.OrdinalIgnoreCase ) ) {
				return true;
			}

			if( !string.IsNullOrEmpty( className ) && !GetClass( h ).Contains( className, StringComparison.OrdinalIgnoreCase ) ) {
				return true;
			}

			list.Add( new Window( h ) );
			return !firstOnly;
		}, IntPtr.Zero );
		return list;
	}

	/// <summary>スクリーン座標にあるトップレベルウィンドウです。</summary>
	/// <param name="x">X 座標です (画面座標)。</param>
	/// <param name="y">Y 座標です (画面座標)。</param>
	/// <returns>その位置のウィンドウです。無ければ null です。</returns>
	internal static Window? FromPoint( int x, int y ) {
		var h = WindowFromPoint( new POINT { X = x, Y = y } );
		return h == IntPtr.Zero ? null : new Window( GetAncestor( h, GA_ROOT ) );
	}
}
