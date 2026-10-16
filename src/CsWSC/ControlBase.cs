using static CsWSC.NativeMethods;

namespace CsWSC;

/// <summary>
/// ウィンドウハンドル (HWND) を持つもの (<see cref="Window"/> と <see cref="Control"/>) の共通部分です。UWSC のウィンドウ ID に相当します。
/// </summary>
public abstract partial class ControlBase {
	/// <summary>ウィンドウハンドルです (HWND)。</summary>
	public IntPtr Handle { get; }

	/// <summary>ハンドルを検証せずに作ります。</summary>
	/// <param name="handle">ウィンドウハンドルです。</param>
	private protected ControlBase( IntPtr handle ) => Handle = handle;

	/// <summary>まだ存在するかどうかです (ST_ISID)。</summary>
	public bool Exists => IsWindow( Handle );
	/// <summary>クラス名です (ST_CLASS)。</summary>
	public string ClassName => GetClass( Opened() );
	/// <summary>WM_GETTEXT で得られるテキストです (エディットなら内容、ボタンなら表示名、ウィンドウならタイトル)。</summary>
	public string Text => GetControlText( Opened() );
	/// <summary>表示されているかどうかです (ST_VISIBLE)。</summary>
	public bool Visible => IsWindowVisible( Handle );
	/// <summary>入力を受け付けるかどうかです。</summary>
	public bool Enabled => IsWindowEnabled( Handle );

	/// <summary>範囲です (画面座標)。ウィンドウなら影 (DWM の不可視枠) を除いた見た目どおりの範囲です。</summary>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Rectangle Bounds => GetVisibleRect( Handle );

	/// <summary>GetWindowRect の範囲です (影を含む)。MoveWindow 等の Win32 API はこの座標系です。</summary>
	private protected Rectangle RawBounds {
		get {
			if( !GetWindowRect( Handle, out var r ) ) {
				throw new InvalidOperationException( $"ウィンドウ (0x{Handle:X}) は閉じられています" );
			}
			return Rectangle.FromLTRB( r.Left, r.Top, r.Right, r.Bottom );
		}
	}

	/// <summary>影を除いたウィンドウ範囲です。DWM から取得できなければ GetWindowRect の値です。</summary>
	/// <param name="handle">ウィンドウハンドルです。</param>
	/// <returns>ウィンドウの範囲です (画面座標)。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウが閉じられています。</exception>
	internal static Rectangle GetVisibleRect( IntPtr handle ) {
		if( DwmGetWindowAttribute( handle, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, System.Runtime.InteropServices.Marshal.SizeOf<RECT>() ) != 0
			&& !GetWindowRect( handle, out r ) ) {
			throw new InvalidOperationException( $"ウィンドウ (0x{handle:X}) は閉じられています" );
		}

		return Rectangle.FromLTRB( r.Left, r.Top, r.Right, r.Bottom );
	}

	/// <summary>左端の X 座標です (画面座標, ST_X)。</summary>
	public int X => Bounds.X;
	/// <summary>上端の Y 座標です (画面座標, ST_Y)。</summary>
	public int Y => Bounds.Y;
	/// <summary>幅です (ST_WIDTH)。</summary>
	public int Width => Bounds.Width;
	/// <summary>高さです (ST_HEIGHT)。</summary>
	public int Height => Bounds.Height;

	/// <summary>クライアント領域です (スクリーン座標)。ST_CLX / ST_CLY / ST_CLWIDTH / ST_CLHEIGHT に相当します。</summary>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Rectangle ClientBounds {
		get {
			if( !GetClientRect( Handle, out var r ) ) {
				throw new InvalidOperationException( $"ウィンドウ (0x{Handle:X}) は閉じられています" );
			}
			return new Rectangle( GetClientOrigin( Handle ), new Size( r.Right - r.Left, r.Bottom - r.Top ) );
		}
	}

	/// <summary>クライアント領域の中央です (クライアント領域基準)。</summary>
	/// <value>中央の座標です (クライアント領域の左上が基準)。<c>Mouse</c> などの座標にそのまま渡せます。</value>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <example>
	/// <code>
	/// w.Mouse(MouseButton.Left, KeyAction.Click, w.ClientCenter);
	/// </code>
	/// </example>
	public Point ClientCenter {
		get {
			if( !GetClientRect( Handle, out var r ) ) {
				throw new InvalidOperationException( $"ウィンドウ (0x{Handle:X}) は閉じられています" );
			}
			return new( ( r.Right - r.Left ) / 2, ( r.Bottom - r.Top ) / 2 );
		}
	}

	/// <summary>クライアント領域の左上です (スクリーン座標)。ウィンドウ相対座標の原点です。</summary>
	/// <param name="handle">ウィンドウハンドルです。</param>
	/// <returns>クライアント領域の左上です (画面座標)。</returns>
	internal static Point GetClientOrigin( IntPtr handle ) {
		var p = new POINT();
		ClientToScreen( handle, ref p );
		return new Point( p.X, p.Y );
	}

	/// <summary>プロセス ID です (ST_PROCESS)。</summary>
	public int ProcessId {
		get {
			GetWindowThreadProcessId( Opened(), out var pid );
			return (int)pid;
		}
	}

	/// <summary>閉じる (なくなる) まで待ちます。timeoutMs ミリ秒 (負なら無制限) 内に閉じれば true です。</summary>
	/// <param name="timeoutMs">待つミリ秒数です。負の値なら無制限です。</param>
	/// <returns>時間内に閉じれば true、タイムアウトしたら false です。</returns>
	public bool WaitClose( int timeoutMs = -1 ) {
		ScriptStop.Check();
		return WaitUntil( () => !Exists, timeoutMs );
	}

	/// <summary>条件が true になるまで 100 ミリ秒間隔で確認しながら待ちます。</summary>
	/// <param name="condition">待つ条件です。</param>
	/// <param name="timeoutMs">待つミリ秒数です。負の値なら無制限です。</param>
	/// <returns>時間内に条件を満たせば true、タイムアウトしたら false です。</returns>
	private protected static bool WaitUntil( Func<bool> condition, int timeoutMs ) {
		var sw = System.Diagnostics.Stopwatch.StartNew();
		while( !condition() ) {
			if( timeoutMs >= 0 && sw.ElapsedMilliseconds >= timeoutMs ) {
				return false;
			}

			ScriptStop.Sleep( 100 );
		}
		return true;
	}

	// ---- 子ウィンドウ (UWSC の GETCTLHND に相当) ----

	/// <summary>すべての子孫コントロールです (子ウィンドウ)。className を指定するとクラス名で絞り込みます (部分一致)。</summary>
	/// <param name="className">クラス名です (部分一致, 大文字小文字を区別しない)。null または空ならすべてです。</param>
	/// <returns>子孫コントロールの配列です (列挙順)。</returns>
	public Control[] GetChildren( string? className = null ) {
		ScriptStop.Check();
		var list = new List<Control>();
		EnumChildWindows( Handle, ( h, _ ) => {
			if( string.IsNullOrEmpty( className ) || GetClass( h ).Contains( className, StringComparison.OrdinalIgnoreCase ) ) {
				list.Add( new Control( h ) );
			}

			return true;
		}, IntPtr.Zero );
		return [.. list];
	}

	/// <summary>テキスト (ボタン名など, 部分一致) で子孫コントロールを探します (GETCTLHND)。無ければ null です。</summary>
	/// <param name="text">探すテキストです (部分一致, 大文字小文字を区別しない)。</param>
	/// <param name="className">クラス名で絞り込みます (部分一致)。null または空ならすべてです。</param>
	/// <returns>最初に見つかったコントロールです。無ければ null です。</returns>
	public Control? FindChild( string text, string? className = null ) =>
		GetChildren( className ).FirstOrDefault( c => GetControlText( c.Handle ).Contains( text, StringComparison.OrdinalIgnoreCase ) );

	// ---- 派生クラスがメソッドチェーン用に戻り値の型を変えて公開する操作 ----

	/// <summary>表示・非表示を切り替えます (ShowWindow)。</summary>
	/// <param name="command">SW_ の値です。</param>
	private protected void ShowCore( int command ) {
		ScriptStop.Check();
		ShowWindow( Opened(), command );
	}

	/// <summary>入力の有効・無効を切り替えます。</summary>
	/// <param name="enabled">true なら有効、false なら無効です。</param>
	private protected void SetEnabledCore( bool enabled ) {
		// 有効に戻すのは後片付け (finally) にも使うので確認しない
		if( !enabled ) {
			ScriptStop.Check();
		}
		EnableWindow( Opened(), enabled );
	}

	/// <summary>クライアント領域基準の位置へマウスを移動してからボタンを操作します。</summary>
	/// <param name="point">操作する位置です (クライアント領域の左上が基準)。null なら移動せず現在のマウス位置です。</param>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action">押して離す・押す・離す。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動します。</param>
	private protected void MouseCore( Point? point, MouseButton button, KeyAction action, int delayMs = 0 ) {
		MoveOrWait( point, delayMs );
		Input.Button( button, action );
	}

	/// <summary>クライアント領域基準の位置へマウスを移動してからホイールを回します。</summary>
	/// <param name="notches">回す量です (ノッチ数)。正で下方向です。</param>
	/// <param name="point">操作する位置です (クライアント領域の左上が基準)。null なら移動せず現在のマウス位置です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動します。</param>
	private protected void WheelCore( int notches, Point? point, int delayMs ) {
		MoveOrWait( point, delayMs );
		Input.Wheel( notches );
	}

	// point があれば待ってから移動、null なら待つだけ。どちらも閉じていれば例外
	private void MoveOrWait( Point? point, int delayMs ) {
		if( point is Point p ) {
			MouseMoveCore( p, delayMs );
		} else {
			ScriptStop.Sleep( delayMs );
			ClientOrigin(); // 閉じていれば例外
		}
	}

	/// <summary>クライアント領域基準の位置へマウスを移動します。</summary>
	/// <param name="point">移動先です (クライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。UWSC の MMV の第 3 引数と同じく、待ってから移動します。</param>
	private protected void MouseMoveCore( Point point, int delayMs = 0 ) {
		ScriptStop.Sleep( delayMs );
		var o = ClientOrigin();
		Input.MoveMouse( o.X + point.X, o.Y + point.Y );
	}
	/// <summary>クライアント領域基準の位置にある最も深い子ウィンドウへボタン操作を送ります。前面に出しません。</summary>
	/// <param name="point">操作する位置です (クライアント領域の左上が基準)。null なら現在のマウス位置です。</param>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action">押して離す・押す・離す。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	internal void PostMouseCore( Point? point, MouseButton button, KeyAction action, int delayMs = 0 ) {
		ScriptStop.Sleep( delayMs );
		var o = ClientOrigin();
		POINT screen;
		if( point is Point p ) {
			screen = new POINT { X = o.X + p.X, Y = o.Y + p.Y };
		} else {
			GetCursorPos( out screen );
		}
		var target = DeepestChildAt( screen );
		var client = screen;
		ScreenToClient( target, ref client );
		var lp = MakeLParam( client.X, client.Y );
		var (down, up, mk) = button switch {
			MouseButton.Right => (WM_RBUTTONDOWN, WM_RBUTTONUP, MK_RBUTTON),
			MouseButton.Middle => (WM_MBUTTONDOWN, WM_MBUTTONUP, MK_MBUTTON),
			_ => (WM_LBUTTONDOWN, WM_LBUTTONUP, MK_LBUTTON),
		};
		PostMessage( target, WM_MOUSEMOVE, action == KeyAction.Up ? mk : 0, lp );
		if( action != KeyAction.Up ) PostMessage( target, (uint)down, mk, lp );
		if( action != KeyAction.Down ) PostMessage( target, (uint)up, 0, lp );
	}

	/// <summary>キー入力 (PostKey / PostText) を送る先です。</summary>
	/// <returns>送り先のハンドルです。</returns>
	private protected abstract IntPtr KeyTarget();

	/// <summary>キーを送ります。前面に出しません。</summary>
	/// <param name="key">送るキーです。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	private protected void PostKeyCore( Keys key, int delayMs ) {
		ScriptStop.Sleep( delayMs );
		ThrowIfClosed();
		var target = KeyTarget();
		var vk = Input.ToVirtualKey( key );
		// lParam: bit 0-15 リピート回数 / 16-23 スキャンコード / 24 拡張キー / 30 直前の状態 / 31 遷移 (離したときはどちらも 1)
		int lParam = 1 | ( Input.ScanCode( vk ) << 16 ) | ( Input.IsExtendedKey( vk ) ? 1 << 24 : 0 );
		PostMessage( target, WM_KEYDOWN, vk, lParam );
		PostMessage( target, WM_KEYUP, vk, unchecked(lParam | (int)0xC0000000) );
	}

	/// <summary>文字列を送ります (WM_CHAR)。前面に出しません。</summary>
	/// <param name="text">送る文字列です。改行は CR に変換します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	private protected void PostTextCore( string text, int delayMs ) {
		ScriptStop.Sleep( delayMs );
		ThrowIfClosed();
		var target = KeyTarget();
		foreach( var ch in text.ReplaceLineEndings( "\r" ) ) {
			PostMessage( target, WM_CHAR, ch, 1 );
		}
	}

	/// <summary>指定位置にある最も深い (可視・非透過の) 子ウィンドウです。無ければ自身です。</summary>
	/// <param name="screen">位置です (画面座標)。</param>
	/// <returns>子ウィンドウのハンドルです。</returns>
	private IntPtr DeepestChildAt( POINT screen ) {
		var current = Handle;
		while( true ) {
			var p = screen;
			ScreenToClient( current, ref p );
			var child = ChildWindowFromPointEx( current, p, CWP_SKIPINVISIBLE | CWP_SKIPTRANSPARENT );
			if( child == IntPtr.Zero || child == current ) {
				return current;
			}

			current = child;
		}
	}

	// ---- UI Automation (高レベル操作) ----

	/// <summary>
	/// 名前 (ボタンの表示文字列など) でコントロールを探して操作します。UWSC の CLKITEM に相当します。
	/// 見つからなければ timeoutMs ミリ秒まで待ち、それでも無ければ false です。
	/// </summary>
	/// <param name="name">コントロールの名前です (ボタンの表示文字列など)。</param>
	/// <param name="timeoutMs">見つかるまで待つミリ秒数です。0 以下なら 1 回だけ探します。</param>
	/// <returns>操作できれば true です。</returns>
	public bool ClickItem( string name, int timeoutMs = 2000 ) {
		ScriptStop.Check();
		return UiAutomation.ClickItem( Opened(), name, timeoutMs );
	}

	/// <summary>ボタン類 (チェックボックス・ラジオ・トグル) の状態です (CHKBTN)。1: オン, 0: オフ (普通のボタンも 0), 2: 不定, -1: 見つからない / 無効 / ボタン類ではありません。</summary>
	/// <param name="name">ボタンの名前です (表示文字列)。</param>
	/// <returns>1: オン, 0: オフ (普通のボタンも 0), 2: 不定, -1: 見つからない / 無効 / ボタン類ではありません。</returns>
	public int CheckButton( string name ) {
		ScriptStop.Check();
		return UiAutomation.CheckState( Opened(), name );
	}

	/// <summary>キャプション文字やリスト項目などを列挙します (GETITEM)。</summary>
	/// <param name="kinds">列挙する項目の種類です (フラグの組み合わせ可)。</param>
	/// <returns>見つかった文字列の配列です。</returns>
	public string[] GetItem( ItemKind kinds = ItemKind.All ) {
		ScriptStop.Check();
		return UiAutomation.GetItems( Opened(), kinds );
	}

	/// <summary>n 番目 (1 から) のリスト / コンボ / リストビュー / ツリーで選択中の項目です (GETSLCTLST)。</summary>
	/// <param name="index">何番目のコントロールかを 1 から数えた値です。</param>
	/// <returns>選択中の項目の配列です。無ければ空配列です。</returns>
	public string[] GetSelectedItems( int index = 1 ) {
		ScriptStop.Check();
		return UiAutomation.GetSelected( Opened(), index );
	}

	/// <summary>n 番目 (1 から) のスライダー・スクロールバー・スピン・プログレスバーの値です (GETSLIDER)。見つからなければ null です。</summary>
	/// <param name="index">何番目のコントロールかを 1 から数えた値です。</param>
	/// <returns>値と範囲の情報です。見つからなければ null です。</returns>
	public SliderInfo? GetSlider( int index = 1 ) {
		ScriptStop.Check();
		return UiAutomation.GetSlider( Opened(), index );
	}

	/// <summary>n 番目 (1 から) のスライダー類に値を設定します (SETSLIDER)。範囲外は丸めます。成功すれば true です。</summary>
	/// <param name="value">設定する値です。</param>
	/// <param name="index">何番目のコントロールかを 1 から数えた値です。</param>
	/// <returns>設定できれば true です。</returns>
	public bool SetSlider( double value, int index = 1 ) {
		ScriptStop.Check();
		return UiAutomation.SetSlider( Opened(), index, value );
	}

	/// <summary>相対座標にある文字 (コントロール名・値) を取得します (POSACC)。</summary>
	/// <param name="point">位置です (クライアント領域の左上が基準)。</param>
	/// <returns>取得した文字列です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public string TextAtPoint( Point point ) {
		ScriptStop.Check();
		var o = ClientOrigin();
		return UiAutomation.TextAt( o.X + point.X, o.Y + point.Y );
	}

	/// <inheritdoc cref="TextAtPoint(Point)"/>
	/// <param name="x">位置の X です (クライアント領域の左上が基準)。</param>
	/// <param name="y">位置の Y です (クライアント領域の左上が基準)。</param>
	public string TextAtPoint( int x, int y ) => TextAtPoint( new Point( x, y ) );

	/// <summary>指定位置の色を取得します (PEEKCOLOR)。</summary>
	/// <param name="point">位置です (クライアント領域の左上が基準)。</param>
	/// <returns>その位置の色です。<c>.R</c> <c>.G</c> <c>.B</c> で各成分 (0〜255) を取り出せます。</returns>
	/// <remarks>画面に表示されている色を読みます。他のウィンドウが重なっていればその色になります。同じ画面で何か所も調べるときは <see cref="Capture(Rectangle?)"/> を使います。</remarks>
	/// <exception cref="ArgumentOutOfRangeException">画面の外など、色を取得できない位置です。</exception>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <example>
	/// <code>
	/// var c = w.PeekColor(10, 10);
	/// if (c.R &gt; 200 &amp;&amp; c.G &lt; 50 &amp;&amp; c.B &lt; 50) Print("赤っぽい");
	/// </code>
	/// </example>
	public Color PeekColor( Point point ) {
		ScriptStop.Check();
		return ScreenImage.Peek( ToScreen( point ) );
	}

	/// <inheritdoc cref="PeekColor(Point)"/>
	/// <param name="x">位置の X です (クライアント領域の左上が基準)。</param>
	/// <param name="y">位置の Y です (クライアント領域の左上が基準)。</param>
	public Color PeekColor( int x, int y ) => PeekColor( new Point( x, y ) );

	/// <summary>クライアント領域 (または指定範囲) を画像ファイルとして保存します (SAVEIMG)。形式は拡張子で決まります。</summary>
	/// <param name="path">保存先のパスです。</param>
	/// <param name="area">保存する範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <exception cref="ArgumentException">範囲の大きさが 0 です (最小化中など)。</exception>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <remarks>取り込んだ画面を調べたり <see cref="Bitmap"/> として受け取ったりするには <see cref="Capture(Rectangle?)"/> を使います。</remarks>
	public void SaveImage( string path, Rectangle? area = null ) {
		ScriptStop.Check();
		ScreenImage.Save( path, ToScreenArea( area ) );
	}

	/// <summary>クライアント領域 (または指定範囲) をキャプチャして <see cref="CsWSC.Capture"/> を返します。</summary>
	/// <param name="area">キャプチャする範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <returns>キャプチャした画面です。座標はクライアント領域基準で受け取り、同じ座標系で返します。</returns>
	/// <exception cref="ArgumentException">範囲の大きさが 0 です (最小化中など)。</exception>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Capture Capture( Rectangle? area = null ) {
		ScriptStop.Check();
		var screen = ToScreenArea( area );
		var o = ClientOrigin();
		return CsWSC.Capture.FromScreen( screen, new Point( screen.X - o.X, screen.Y - o.Y ) );
	}

	/// <summary>クライアント領域内で指定の色を探し、最初に見つかった位置 (上から順、同じ行は左から順) を返します。</summary>
	/// <param name="color">探す色です。アルファ値は無視します。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>見つかった位置です (クライアント領域基準)。無ければ null です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public Point? CheckColor( Color color, Rectangle? area = null, int tolerance = 0 ) =>
		CheckColors( color, area, tolerance, 1 ) is [var p, ..] ? p : null;

	/// <summary>クライアント領域内の指定位置の色が指定の色と一致するかを調べます。</summary>
	/// <param name="color">比べる色です。アルファ値は無視します。</param>
	/// <param name="point">調べる位置です (クライアント領域の左上が基準)。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>一致すれば true です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public bool CheckColor( Color color, Point point, int tolerance = 0 ) =>
		CheckColors( color, new Rectangle( point, new Size( 1, 1 ) ), tolerance, 1 ).Length > 0;

	/// <summary>クライアント領域内で指定の色のピクセルを全て探し、位置を上から順 (同じ行は左から順) に返します。</summary>
	/// <param name="color">探す色です。アルファ値は無視します。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった位置 (クライアント領域基準) の配列です。無ければ空の配列です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point[] CheckColors( Color color, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) {
		ScriptStop.Check();
		var o = ClientOrigin();
		return Array.ConvertAll( ScreenImage.FindColor( color, ToScreenArea( area ), tolerance, max ), p => new Point( p.X - o.X, p.Y - o.Y ) );
	}

	/// <summary>クライアント領域内でいずれかの色を探し、最初に見つかったピクセル (上から順、同じ行は左から順) の色と位置を返します。</summary>
	/// <param name="colors">探す色です。アルファ値は無視します。1 つのピクセルが複数の色に一致するときは先に並ぶ色を返します。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>一致した色 (<paramref name="colors"/> の要素) と位置です (クライアント領域基準)。無ければ null です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public (Color Color, Point Point)? CheckColor( Color[] colors, Rectangle? area = null, int tolerance = 0 ) =>
		CheckColors( colors, area, tolerance, 1 ) is [var f, ..] ? f : null;

	/// <summary>クライアント領域内でいずれかの色のピクセルを全て探し、色と位置を上から順 (同じ行は左から順) に返します。</summary>
	/// <param name="colors">探す色です。アルファ値は無視します。1 つのピクセルが複数の色に一致するときは先に並ぶ色を返します。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>一致した色 (<paramref name="colors"/> の要素) と位置 (クライアント領域基準) の配列です。無ければ空の配列です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public (Color Color, Point Point)[] CheckColors( Color[] colors, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) {
		ScriptStop.Check();
		var o = ClientOrigin();
		return Array.ConvertAll( ScreenImage.FindColors( colors, ToScreenArea( area ), tolerance, max ), f => (f.Color, new Point( f.Point.X - o.X, f.Point.Y - o.Y )) );
	}

	/// <inheritdoc cref="CheckImage(string, TransColor, Rectangle?, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point? CheckImage( string imagePath, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImage( imagePath, TransColor.None, area, tolerance );

	/// <summary>クライアント領域内で画像を色で探します (CHKIMG)。見つかれば相対 (クライアント領域基準) の左上座標、無ければ null です。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。タイトルバーや枠は含みません。<c>ImageMatch.Color(tolerance)</c> で照合するのと同じです。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// </remarks>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	/// <returns>見つかった位置の左上です (クライアント領域基準)。無ければ null です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point? CheckImage( string imagePath, TransColor transColor, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImageCore( imagePath, transColor, area, ImageMatch.Color( tolerance ) );

	/// <inheritdoc cref="CheckImage(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point? CheckImage( string imagePath, ImageMatch match ) =>
		CheckImageCore( imagePath, TransColor.None, null, match );

	/// <inheritdoc cref="CheckImage(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point? CheckImage( string imagePath, Rectangle area, ImageMatch match ) =>
		CheckImageCore( imagePath, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImage(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point? CheckImage( string imagePath, TransColor transColor, ImageMatch match ) =>
		CheckImageCore( imagePath, transColor, null, match );

	/// <summary>クライアント領域内で画像を指定した照合方法で探します (CHKIMG)。見つかれば相対 (クライアント領域基準) の左上座標、無ければ null です。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。タイトルバーや枠は含みません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/> (色無視)・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった位置の左上です (クライアント領域基準)。無ければ null です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point? CheckImage( string imagePath, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImageCore( imagePath, transColor, area, match );

	private Point? CheckImageCore( string imagePath, TransColor transColor, Rectangle? area, ImageMatch match ) {
		ScriptStop.Check();
		var o = ClientOrigin();
		return ScreenImage.Find( imagePath, ToScreenArea( area ), match, transColor ) is { } p ? new Point( p.X - o.X, p.Y - o.Y ) : null;
	}

	/// <inheritdoc cref="CheckImage(Capture, TransColor, Rectangle?, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point? CheckImage( Capture capture, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImage( capture, TransColor.None, area, tolerance );

	/// <summary>クライアント領域内で画像を色で探します (CHKIMG)。見つかれば相対 (クライアント領域基準) の左上座標、無ければ null です。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。タイトルバーや枠は含みません。<c>ImageMatch.Color(tolerance)</c> で照合するのと同じです。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// </remarks>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	/// <returns>見つかった位置の左上です (クライアント領域基準)。無ければ null です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point? CheckImage( Capture capture, TransColor transColor, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImageCore( capture, transColor, area, ImageMatch.Color( tolerance ) );

	/// <inheritdoc cref="CheckImage(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point? CheckImage( Capture capture, ImageMatch match ) =>
		CheckImageCore( capture, TransColor.None, null, match );

	/// <inheritdoc cref="CheckImage(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point? CheckImage( Capture capture, Rectangle area, ImageMatch match ) =>
		CheckImageCore( capture, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImage(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point? CheckImage( Capture capture, TransColor transColor, ImageMatch match ) =>
		CheckImageCore( capture, transColor, null, match );

	/// <summary>クライアント領域内で画像を指定した照合方法で探します (CHKIMG)。見つかれば相対 (クライアント領域基準) の左上座標、無ければ null です。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。タイトルバーや枠は含みません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/> (色無視)・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった位置の左上です (クライアント領域基準)。無ければ null です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point? CheckImage( Capture capture, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImageCore( capture, transColor, area, match );

	private Point? CheckImageCore( Capture capture, TransColor transColor, Rectangle? area, ImageMatch match ) {
		ScriptStop.Check();
		var o = ClientOrigin();
		return ScreenImage.Find( capture, ToScreenArea( area ), match, transColor ) is { } p ? new Point( p.X - o.X, p.Y - o.Y ) : null;
	}

	/// <inheritdoc cref="CheckImages(string, TransColor, Rectangle?, int, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point[] CheckImages( string imagePath, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImages( imagePath, TransColor.None, area, tolerance, max );

	/// <summary>クライアント領域内で画像を色で全て探します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。見つかった左上座標 (クライアント領域基準) を上から順 (同じ行は左から順) に返します。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// </remarks>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった位置の左上 (クライアント領域基準) の配列です。無ければ空の配列です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point[] CheckImages( string imagePath, TransColor transColor, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImagesCore( imagePath, transColor, area, ImageMatch.Color( tolerance ), max );

	/// <inheritdoc cref="CheckImages(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point[] CheckImages( string imagePath, ImageMatch match ) =>
		CheckImagesCore( imagePath, TransColor.None, null, match );

	/// <inheritdoc cref="CheckImages(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point[] CheckImages( string imagePath, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( imagePath, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImages(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point[] CheckImages( string imagePath, TransColor transColor, ImageMatch match ) =>
		CheckImagesCore( imagePath, transColor, null, match );

	/// <summary>クライアント領域内で画像を指定した照合方法で全て探します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。見つかった左上座標 (クライアント領域基準) を上から順 (同じ行は左から順) に返します。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/> (色無視)・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった位置の左上 (クライアント領域基準) の配列です。無ければ空の配列です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point[] CheckImages( string imagePath, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( imagePath, transColor, area, match );

	private Point[] CheckImagesCore( string imagePath, TransColor transColor, Rectangle? area, ImageMatch match, int max = int.MaxValue ) {
		ScriptStop.Check();
		var o = ClientOrigin();
		return Array.ConvertAll( ScreenImage.FindAll( imagePath, ToScreenArea( area ), match, transColor, max ), p => new Point( p.X - o.X, p.Y - o.Y ) );
	}

	/// <inheritdoc cref="CheckImages(Capture, TransColor, Rectangle?, int, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point[] CheckImages( Capture capture, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImages( capture, TransColor.None, area, tolerance, max );

	/// <summary>クライアント領域内で画像を色で全て探します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。見つかった左上座標 (クライアント領域基準) を上から順 (同じ行は左から順) に返します。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// </remarks>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった位置の左上 (クライアント領域基準) の配列です。無ければ空の配列です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point[] CheckImages( Capture capture, TransColor transColor, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImagesCore( capture, transColor, area, ImageMatch.Color( tolerance ), max );

	/// <inheritdoc cref="CheckImages(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point[] CheckImages( Capture capture, ImageMatch match ) =>
		CheckImagesCore( capture, TransColor.None, null, match );

	/// <inheritdoc cref="CheckImages(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point[] CheckImages( Capture capture, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( capture, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImages(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point[] CheckImages( Capture capture, TransColor transColor, ImageMatch match ) =>
		CheckImagesCore( capture, transColor, null, match );

	/// <summary>クライアント領域内で画像を指定した照合方法で全て探します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。見つかった左上座標 (クライアント領域基準) を上から順 (同じ行は左から順) に返します。</summary>
	/// <remarks>
	/// 省略時に探す範囲はクライアント領域です。既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (クライアント領域の左上が基準)。省略するとクライアント領域全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/> (色無視)・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった位置の左上 (クライアント領域基準) の配列です。無ければ空の配列です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	public Point[] CheckImages( Capture capture, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( capture, transColor, area, match );

	private Point[] CheckImagesCore( Capture capture, TransColor transColor, Rectangle? area, ImageMatch match, int max = int.MaxValue ) {
		ScriptStop.Check();
		var o = ClientOrigin();
		return Array.ConvertAll( ScreenImage.FindAll( capture, ToScreenArea( area ), match, transColor, max ), p => new Point( p.X - o.X, p.Y - o.Y ) );
	}

	// クライアント領域の左上 (画面座標)。ウィンドウが閉じられていれば例外
	private protected Point ClientOrigin() =>
		GetClientOrigin( Opened() );

	/// <summary>閉じられていれば <see cref="InvalidOperationException"/> を投げます。</summary>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	private protected void ThrowIfClosed() {
		if( !Exists ) {
			throw new InvalidOperationException( $"ウィンドウ (0x{Handle:X}) は閉じられています" );
		}
	}

	/// <summary>閉じられていないことを確かめてハンドルを返します。</summary>
	/// <returns><see cref="Handle"/> です。</returns>
	/// <exception cref="InvalidOperationException">ウィンドウ・コントロールが閉じられています。</exception>
	private protected IntPtr Opened() {
		ThrowIfClosed();
		return Handle;
	}

	// クライアント領域基準の位置を画面座標にする
	private Point ToScreen( Point p ) {
		var o = ClientOrigin();
		return new Point( p.X + o.X, p.Y + o.Y );
	}

	// クライアント領域基準の範囲を画面座標にする。null ならクライアント領域全体。ウィンドウが閉じられていれば例外
	private Rectangle ToScreenArea( Rectangle? area ) {
		if( area is not { } a ) {
			return ClientBounds; // 閉じていれば例外
		}

		var o = ClientOrigin();
		return new Rectangle( a.X + o.X, a.Y + o.Y, a.Width, a.Height );
	}

	/// <summary>同じハンドルなら等しくなります (Window と Control でも、ハンドルが同じなら等しい)。</summary>
	/// <param name="obj">比較する対象です。</param>
	public override bool Equals( object? obj ) => obj is ControlBase w && w.Handle == Handle;
	/// <summary>ハンドルに基づくハッシュ値です。</summary>
	public override int GetHashCode() => Handle.GetHashCode();

	/// <summary>同じハンドルなら true です。どちらも null でも true です。</summary>
	/// <example>
	/// <code>
	/// if (ActiveWindow() == w) Print("アクティブ");
	/// </code>
	/// </example>
	public static bool operator ==( ControlBase? left, ControlBase? right ) => left is null ? right is null : left.Equals( right );

	/// <summary>違うハンドルなら true です。</summary>
	public static bool operator !=( ControlBase? left, ControlBase? right ) => !( left == right );

	/// <summary>ハンドルから、トップレベルなら <see cref="Window"/>、子ウィンドウなら <see cref="Control"/> を作ります。</summary>
	/// <param name="handle">ウィンドウハンドルです (0 以外)。</param>
	/// <returns>作成したオブジェクトです。</returns>
	internal static ControlBase Wrap( IntPtr handle ) => IsTopLevel( handle ) ? new Window( handle ) : new Control( handle );

	/// <summary>トップレベルウィンドウかどうかです (自身がルート)。</summary>
	/// <param name="handle">ウィンドウハンドルです。</param>
	/// <returns>トップレベルなら true です。</returns>
	internal static bool IsTopLevel( IntPtr handle ) => GetAncestor( handle, GA_ROOT ) == handle;
}
