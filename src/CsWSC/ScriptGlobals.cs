using System.Diagnostics;
using static CsWSC.NativeMethods;

namespace CsWSC;

/// <summary>
/// スクリプトのトップレベルから直接呼べる関数群。
/// public メンバーがそのままスクリプトの組み込み API になる。
/// </summary>
/// <remarks>
/// ここに書いた XML ドキュメントは、VS Code 拡張の補完・ホバー・引数ヒントにそのまま表示される。
/// summary は 1〜2 文で要点を、param は引数ごとに単位・省略時の動作を、remarks に注意点を書く。
/// </remarks>
public partial class ScriptGlobals( Action<string> output, CancellationToken cancellation, ILogWindow? logWindow = null ) {
	// 停止要求に加え、メインスクリプト終了時にも THREAD を止めるためのトークン
	private readonly CancellationTokenSource _lifetime = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
	private readonly List<System.Threading.Thread> _threads = [];
	private Exception? _threadError;

	/// <summary>
	/// スクリプトの停止要求を表すトークン。
	/// </summary>
	/// <remarks>
	/// 停止ボタン・停止ホットキー・Pause キーで停止が要求される。
	/// 組み込みコマンドは呼び出し時に自動で確認するが、組み込みコマンドを呼ばない長い計算ループでは
	/// <c>Cancellation.ThrowIfCancellationRequested()</c> を呼ぶと停止できるようになる。
	/// </remarks>
	/// <example>
	/// <code>
	/// while (true)
	/// {
	///     Cancellation.ThrowIfCancellationRequested();
	///     // 重い計算
	/// }
	/// </code>
	/// </example>
	public CancellationToken Cancellation => _lifetime.Token;

	// ---- 吹き出し ----

	private readonly BalloonHost _balloons = new();

	/// <summary>
	/// 吹き出しを表示し、その吹き出しを返す。呼ぶたびに新しい吹き出しが増える。UWSC の FUKIDASI。
	/// </summary>
	/// <param name="message">表示する文字列。改行で複数行になる。</param>
	/// <param name="point">しっぽの先が指す座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。省略するとマウスの位置。</param>
	/// <param name="tail">しっぽの位置。既定は <see cref="BalloonTail.None"/> (しっぽなし)。</param>
	/// <param name="fontSize">文字の大きさ (ポイント)。既定は 10。</param>
	/// <param name="backColor">背景色。省略すると薄い黄色。</param>
	/// <param name="foreColor">文字色。省略すると黒。</param>
	/// <returns>表示した吹き出し。<see cref="Balloon.Update"/> で内容・位置を変え、<see cref="Balloon.Close"/> で消す。</returns>
	/// <remarks>
	/// 複数の吹き出しを同時に表示できる。同じ吹き出しを書き換えたいときは、返り値の <see cref="Balloon.Update"/> を使う。
	/// 前面に出ずフォーカスも奪わず、クリックは下のウィンドウに届く。画面からはみ出す場合は内側に寄せる。
	/// スクリプトが終わると自動で消える。すべて消すには <see cref="FukidasiClear"/>。
	/// </remarks>
	/// <example>
	/// <code>
	/// var b = Fukidasi("処理中です…");
	/// b.Update("あと少し…");
	/// b.Close();
	///
	/// var a = Fukidasi("ここ", new Point(600, 300), BalloonTail.BottomRight, 14, Color.LightBlue);
	/// var c = Fukidasi("そこ", new Point(900, 300));
	/// FukidasiClear(); // 全部消す
	///
	/// using (Fukidasi("この間だけ表示")) { Sleep(2); }
	/// </code>
	/// </example>
	public Balloon Fukidasi( string message, Point? point= null, BalloonTail tail = BalloonTail.None,
		float fontSize = 10, Color? backColor = null, Color? foreColor = null ) {
		ArgumentNullException.ThrowIfNull( message );
		var pos = point is { } p ? ToScreen( p ) : Cursor.Position;
		return new Balloon( _balloons, BalloonHost.Normalize( message ), pos, tail, fontSize,
			backColor ?? Color.FromArgb( 255, 255, 225 ), foreColor ?? Color.Black );
	}

	/// <summary>
	/// 表示中の吹き出しをすべて消す。
	/// </summary>
	public void FukidasiClear() => _balloons.CloseAll();

	// ---- スレッド ----

	/// <summary>
	/// 処理を別スレッドで並行に実行する。UWSC の THREAD。
	/// </summary>
	/// <param name="action">実行する処理。<c>() =&gt; { ... }</c> の形で渡す。</param>
	/// <remarks>
	/// メインスクリプトが終わると、スレッド内で組み込みコマンド (Sleep など) を呼んだ時点でスレッドも止まる。
	/// スレッド内でエラーが起きると、スクリプト全体が停止してエラーを表示する。
	/// この関数があるため、スクリプト内で <c>Thread.Sleep</c> などとは書けない。
	/// 待機には <see cref="Sleep"/> を使い、型が必要なら <c>System.Threading.Thread</c> と書く。
	/// </remarks>
	/// <example>
	/// <code>
	/// // エラーダイアログが出たら自動で閉じる
	/// Thread(() =>
	/// {
	///     while (true)
	///     {
	///         GetWindow("エラー", timeout: 0)?.Close();
	///         Sleep(0.5);
	///     }
	/// });
	/// </code>
	/// </example>
	public void Thread( Action action ) {
		Check();
		var t = new System.Threading.Thread( () => {
			try {
				action();
			} catch( OperationCanceledException ) { } catch( Exception e ) {
				Interlocked.CompareExchange( ref _threadError, e, null );
				_lifetime.Cancel();
			}
		} ) { IsBackground = true, Name = "CsWSC THREAD" };
		t.SetApartmentState( ApartmentState.STA ); // クリップボード・UI Automation 用
		lock( _threads )
			_threads.Add( t );
		t.Start();
	}

	/// <summary>スレッドで発生した最初のエラー。</summary>
	internal Exception? ThreadError => _threadError;

	/// <summary>メインスクリプト終了時に呼ぶ。THREAD に停止を要求して少しの間終了を待ち、吹き出しを消す。</summary>
	internal void EndThreads( TimeSpan wait ) {
		_lifetime.Cancel();
		System.Threading.Thread[] threads;
		lock( _threads )
			threads = [.. _threads];
		var deadline = DateTime.UtcNow + wait;
		foreach( var t in threads ) {
			var left = deadline - DateTime.UtcNow;
			if( left <= TimeSpan.Zero || !t.Join( left ) )
				break;
		}
		_balloons.Dispose();
		_inputLock.Dispose();
	}

	// ---- 出力・待機 ----

	/// <summary>
	/// PRINT 窓に値を 1 行出力する。UWSC の PRINT。
	/// </summary>
	/// <param name="value">出力する値。文字列以外は ToString() した結果を出す。null は空行。</param>
	/// <example>
	/// <code>
	/// Print("開始");
	/// Print($"x = {x}, y = {y}");
	/// </code>
	/// </example>
	public void Print( object? value ) => output( value?.ToString() ?? "" );

	/// <summary>
	/// PRINT 窓の表示/非表示を切り替える。UWSC の LOGPRINT。
	/// </summary>
	/// <param name="show">true で表示、false で非表示。非表示にすると、以降 Print しても窓は自動で表示されない。</param>
	/// <example>
	/// <code>
	/// LogPrint(false); // PRINT 窓を出さずに実行
	/// </code>
	/// </example>
	public void LogPrint( bool show ) => logWindow?.SetVisible( show );

	/// <summary>
	/// PRINT 窓の表示/非表示を切り替え、位置とサイズを変える。UWSC の LOGPRINT(表示, x, y, 幅, 高さ)。
	/// </summary>
	/// <param name="show">true で表示、false で非表示。</param>
	/// <param name="x">左上の X 座標 (画面座標)。</param>
	/// <param name="y">左上の Y 座標 (画面座標)。</param>
	/// <param name="width">幅。省略すると変えない。</param>
	/// <param name="height">高さ。省略すると変えない。</param>
	/// <example>
	/// <code>
	/// LogPrint(true, 0, 0, 400, 300);
	/// </code>
	/// </example>
	public void LogPrint( bool show, int x, int y, int? width = null, int? height = null ) {
		logWindow?.SetBounds( x, y, width, height );
		logWindow?.SetVisible( show );
	}

	/// <summary>
	/// PRINT 窓の内容を消去する。
	/// </summary>
	public void LogClear() => logWindow?.Clear();

	/// <summary>
	/// 指定した秒数だけ待つ。UWSC の SLEEP。
	/// </summary>
	/// <param name="seconds">待つ秒数。小数も指定できる (0.5 で 500 ミリ秒)。</param>
	/// <remarks>待っている間に停止が要求されると、すぐに中断してスクリプトを止める。</remarks>
	/// <example>
	/// <code>
	/// Sleep(0.5);
	/// </code>
	/// </example>
	public void Sleep( double seconds ) {
		Cancellation.WaitHandle.WaitOne( TimeSpan.FromSeconds( seconds ) );
		Cancellation.ThrowIfCancellationRequested();
	}

	/// <summary>
	/// スクリプト全体を終了する。UWSC の EXITEXIT。
	/// </summary>
	/// <remarks>関数の中や <see cref="Thread"/> の中から呼んでも、メインスクリプトと全スレッドに停止を要求する。</remarks>
	/// <example>
	/// <code>
	/// if (GetWindow("メモ帳", timeout: 0) == null) ExitExit();
	/// </code>
	/// </example>
	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	public void ExitExit() {
		_lifetime.Cancel();
		throw new OperationCanceledException( _lifetime.Token );
	}

	/// <summary>
	/// メッセージボックスを表示し、押されたボタンを返す。UWSC の MSGBOX。
	/// </summary>
	/// <param name="message">表示する内容。文字列以外は ToString() した結果を出す。</param>
	/// <param name="buttons">表示するボタンの組み合わせ。既定は OK のみ。</param>
	/// <returns>押されたボタン (<see cref="DialogResult.OK"/>、<see cref="DialogResult.Yes"/> など)。</returns>
	/// <remarks>閉じられるまでスクリプトは止まる。</remarks>
	/// <example>
	/// <code>
	/// if (MsgBox("続行しますか？", MessageBoxButtons.YesNo) == DialogResult.No) return;
	/// </code>
	/// </example>
	public DialogResult MsgBox( object? message, MessageBoxButtons buttons = MessageBoxButtons.OK ) =>
		MessageBox.Show( message?.ToString(), "CsWSC", buttons );

	/// <summary>
	/// 入力ダイアログを表示し、入力された文字列を返す。UWSC の INPUT。
	/// </summary>
	/// <param name="prompt">ダイアログに表示する説明文。</param>
	/// <param name="defaultValue">入力欄に最初から入れておく文字列。</param>
	/// <returns>入力された文字列。キャンセルされた場合は空文字。</returns>
	/// <example>
	/// <code>
	/// var name = InputBox("名前を入力", "山田");
	/// if (name == "") return;
	/// </code>
	/// </example>
	public string InputBox( string prompt, string defaultValue = "" ) =>
		Microsoft.VisualBasic.Interaction.InputBox( prompt, "CsWSC", defaultValue );

	// ---- マウス ----

	// MouseOrg で設定した座標の基準。null なら画面座標
	private Window? _orgWindow;
	private MouseOrigin _orgKind = MouseOrigin.Screen;

	/// <summary>
	/// 以降の座標指定の基準を、指定ウィンドウの左上 (またはクライアント領域の左上) に切り替える。UWSC の MOUSEORG。
	/// </summary>
	/// <param name="window">基準にするウィンドウ。null で画面座標に戻す。</param>
	/// <param name="origin">
	/// <see cref="MouseOrigin.Window"/> (ウィンドウ左上、既定)・<see cref="MouseOrigin.Client"/> (クライアント領域左上)・<see cref="MouseOrigin.Screen"/> (画面座標)。
	/// </param>
	/// <returns>破棄すると直前の基準に戻るオブジェクト。<c>using</c> で範囲を限定できる。</returns>
	/// <remarks>
	/// 対象は <see cref="MouseMove(Point, int)"/>・<see cref="Click(Point, MouseButton)"/>・<see cref="MousePos"/>・<see cref="PeekColor(Point)"/>・
	/// <see cref="ChkImg"/>・<see cref="SaveImg"/>・<see cref="Fukidasi"/>。
	/// 基準の位置は呼び出しのたびにウィンドウの現在位置から求めるため、途中でウィンドウが動いてもずれない。
	/// 引数なしの <c>MouseOrg()</c> で画面座標に戻る。
	/// </remarks>
	/// <example>
	/// <code>
	/// var w = GetWindow("メモ帳");
	/// MouseOrg(w, MouseOrigin.Client);
	/// Click(10, 20);   // クライアント領域の (10, 20)
	/// MouseOrg();      // 画面座標に戻す
	///
	/// using (MouseOrg(w)) {
	///     Click(10, 20); // ブロック内だけウィンドウ基準
	/// }
	/// </code>
	/// </example>
	public IDisposable MouseOrg( Window? window = null, MouseOrigin origin = MouseOrigin.Window ) {
		var (prevWindow, prevKind) = (_orgWindow, _orgKind);
		(_orgWindow, _orgKind) = window == null || origin == MouseOrigin.Screen ? (null, MouseOrigin.Screen) : (window, origin);
		return new OrgScope( () => (_orgWindow, _orgKind) = (prevWindow, prevKind) );
	}

	private sealed class OrgScope( Action restore ) : IDisposable {
		private Action? _restore = restore;
		public void Dispose() { _restore?.Invoke(); _restore = null; }
	}

	// 現在の基準の左上 (画面座標)
	private Point OrgOffset() => _orgWindow switch {
		null => Point.Empty,
		var w when _orgKind == MouseOrigin.Client => w.ClientBounds.Location,
		var w => w.Bounds.Location,
	};

	private Point ToScreen( Point p ) { var o = OrgOffset(); return new( p.X + o.X, p.Y + o.Y ); }

	private Point FromScreen( Point p ) { var o = OrgOffset(); return new( p.X - o.X, p.Y - o.Y ); }

	private Rectangle ToScreen( Rectangle r ) => new( ToScreen( r.Location ), r.Size );

	/// <summary>
	/// マウスカーソルを移動する。UWSC の MMV。
	/// </summary>
	/// <param name="point">移動先の座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <param name="ms">実行までの待ち時間 (ミリ秒)。UWSC の MMV の第 3 引数と同じく、待ってから移動する。</param>
	public void MouseMove( Point point, int ms = 0 ) { Delay( ms ); var p = ToScreen( point ); Input.MoveMouse( p.X, p.Y ); }

	/// <inheritdoc cref="MouseMove(Point, int)"/>
	/// <param name="x">移動先の X 座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <param name="y">移動先の Y 座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <param name="ms">実行までの待ち時間 (ミリ秒)。</param>
	public void MouseMove( int x, int y, int ms = 0 ) => MouseMove( new Point( x, y ), ms );

	/// <summary>
	/// 画面上の指定位置をクリックする。
	/// </summary>
	/// <param name="point">クリックする座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <param name="button">押すボタン。既定は左ボタン。</param>
	/// <remarks>
	/// マウスカーソルを移動してからクリックする。
	/// ウィンドウ内の位置で指定したい場合は <see cref="Window.Click(Point, MouseButton)"/> (ウィンドウ左上からの相対座標) を使う。
	/// </remarks>
	/// <example>
	/// <code>
	/// Click(new Point(300, 250));
	/// Click(300, 250, MouseButton.Right);
	/// if (ChkImg(@"C:\img\ok.png") is Point p) Click(p);
	/// </code>
	/// </example>
	public void Click( Point point, MouseButton button = MouseButton.Left ) {
		Check();
		var p = ToScreen( point );
		Input.Click( p.X, p.Y, button );
	}

	/// <inheritdoc cref="Click(Point, MouseButton)"/>
	/// <param name="x">クリックする X 座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <param name="y">クリックする Y 座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <param name="button">押すボタン。既定は左ボタン。</param>
	public void Click( int x, int y, MouseButton button = MouseButton.Left ) => Click( new Point( x, y ), button );

	/// <summary>
	/// マウスのホイールを回す。
	/// </summary>
	/// <param name="notches">回す量 (ノッチ数)。正の値で下 (手前) 方向、負の値で上方向。</param>
	/// <example>
	/// <code>
	/// Wheel(3);  // 3 ノッチ下へ
	/// Wheel(-1); // 1 ノッチ上へ
	/// </code>
	/// </example>
	public void Wheel( int notches ) {
		Check();
		Input.Wheel( notches );
	}

	/// <summary>
	/// 現在のマウスカーソルの位置 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。UWSC の G_MOUSE_X / G_MOUSE_Y。
	/// </summary>
	/// <example>
	/// <code>
	/// var p = MousePos;
	/// Print($"{p.X}, {p.Y}");
	/// </code>
	/// </example>
	public Point MousePos => FromScreen( Cursor.Position );

	/// <summary>
	/// 画面上の指定位置の色を取得する。UWSC の PEEKCOLOR。
	/// </summary>
	/// <param name="point">座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <returns>その位置の色。<c>.R</c> <c>.G</c> <c>.B</c> で各成分 (0〜255) を取り出せる。</returns>
	/// <example>
	/// <code>
	/// var c = PeekColor(10, 10);
	/// if (c.R &gt; 200 &amp;&amp; c.G &lt; 50 &amp;&amp; c.B &lt; 50) Print("赤っぽい");
	/// </code>
	/// </example>
	public Color PeekColor( Point point ) {
		var p = ToScreen( point );
		var hdc = GetDC( IntPtr.Zero );
		try {
			var bgr = GetPixel( hdc, p.X, p.Y );
			return Color.FromArgb( (int)( bgr & 0xFF ), (int)( ( bgr >> 8 ) & 0xFF ), (int)( ( bgr >> 16 ) & 0xFF ) );
		} finally {
			ReleaseDC( IntPtr.Zero, hdc );
		}
	}

	/// <inheritdoc cref="PeekColor(Point)"/>
	/// <param name="x">X 座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	/// <param name="y">Y 座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。</param>
	public Color PeekColor( int x, int y ) => PeekColor( new Point( x, y ) );

	/// <summary>
	/// 現在のマウスカーソルの種類 (矢印・砂時計・I ビームなど)。UWSC の MUSCUR。
	/// </summary>
	/// <remarks>
	/// 標準のカーソル以外は <see cref="CursorKind.Other"/>、カーソルが非表示なら <see cref="CursorKind.Hidden"/>。
	/// </remarks>
	/// <example>
	/// <code>
	/// // 砂時計の間は待つ
	/// while (MusCur == CursorKind.Wait || MusCur == CursorKind.AppStarting) Sleep(0.2);
	/// </code>
	/// </example>
	public CursorKind MusCur {
		get {
			var info = new CURSORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<CURSORINFO>() };
			if( !GetCursorInfo( ref info ) || info.hCursor == IntPtr.Zero )
				return CursorKind.Hidden;
			foreach( var kind in Enum.GetValues<CursorKind>() )
				if( kind > 0 && LoadCursor( IntPtr.Zero, (int)kind ) == info.hCursor )
					return kind;
			return CursorKind.Other;
		}
	}

	/// <summary>
	/// 画面を画像ファイルとして保存する。UWSC の SAVEIMG。
	/// </summary>
	/// <param name="path">保存先のパス。拡張子で形式が決まる (.png .jpg .bmp .gif、それ以外は PNG)。</param>
	/// <param name="area">保存する範囲 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。省略すると全画面 (マルチモニターはすべて)。</param>
	/// <remarks>ウィンドウ部分だけを保存するには <see cref="Window.SaveImg"/> を使う。</remarks>
	/// <example>
	/// <code>
	/// SaveImg(@"C:\temp\screen.png");
	/// SaveImg(@"C:\temp\part.png", new Rectangle(0, 0, 400, 300));
	/// </code>
	/// </example>
	public void SaveImg( string path, Rectangle? area = null ) =>
		ScreenImage.Save( path, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen );

	/// <summary>
	/// 画面上で画像を探し、見つかった位置を返す。UWSC の CHKIMG。
	/// </summary>
	/// <param name="imagePath">探す画像ファイルのパス (PNG・BMP など)。</param>
	/// <param name="tolerance">色の許容差。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなす。0 で完全一致。</param>
	/// <param name="area">探す範囲 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。省略すると全画面。</param>
	/// <returns>見つかった画像の左上の座標 (画面座標。<see cref="MouseOrg"/> 指定時はその基準)。見つからなければ null。</returns>
	/// <remarks>
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しない。
	/// 総当たりで探すため、大きな画像を全画面から探すと時間がかかる。<paramref name="area"/> で範囲を絞ると速い。
	/// </remarks>
	/// <example>
	/// <code>
	/// if (ChkImg(@"C:\img\ok.png", 10) is Point p) Click(p);
	/// </code>
	/// </example>
	public Point? ChkImg( string imagePath, int tolerance = 0, Rectangle? area = null ) {
		Check();
		var found = ScreenImage.Find( imagePath, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen, tolerance );
		return found is { } p ? FromScreen( p ) : null;
	}

	/// <summary>
	/// 画面上の指定位置にあるコントロールの文字 (ボタン名・ラベル・入力値など) を取得する。UWSC の POSACC。
	/// </summary>
	/// <param name="point">座標 (画面座標。<see cref="MouseOrg"/> の影響を受けない)。</param>
	/// <returns>コントロールの名前。名前が無ければ値。取得できなければ空文字。</returns>
	/// <remarks>UI Automation で取得する。ウィンドウ内の相対座標で指定するには <see cref="Window.PosAcc(Point)"/> を使う。</remarks>
	public string PosAcc( Point point ) => UiAutomation.TextAt( point.X, point.Y );

	/// <inheritdoc cref="PosAcc(Point)"/>
	/// <param name="x">X 座標 (画面座標。<see cref="MouseOrg"/> の影響を受けない)。</param>
	/// <param name="y">Y 座標 (画面座標。<see cref="MouseOrg"/> の影響を受けない)。</param>
	public string PosAcc( int x, int y ) => PosAcc( new Point( x, y ) );

	// ---- キーボード (アクティブウィンドウへ送信) ----

	/// <summary>
	/// キーを 1 つ押す・離す。UWSC の KBD。
	/// </summary>
	/// <param name="key">押すキー (<c>Keys.Enter</c>、<c>Keys.A</c>、<c>Keys.F5</c> など)。</param>
	/// <param name="action">
	/// <see cref="KeyAction.Click"/> (押して離す、既定)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す)。
	/// </param>
	/// <param name="ms">実行までの待ち時間 (ミリ秒)。UWSC の KBD と同じく、待ってから操作する。</param>
	/// <remarks>
	/// アクティブなウィンドウに送られる。
	/// 修飾キーとの同時押しは <see cref="Hotkey"/>、文字列の入力は <see cref="SendText"/> が簡単。
	/// </remarks>
	/// <example>
	/// <code>
	/// Kbd(Keys.Enter);
	/// Kbd(Keys.ShiftKey, KeyAction.Down); Kbd(Keys.Tab); Kbd(Keys.ShiftKey, KeyAction.Up);
	/// </code>
	/// </example>
	public void Kbd( Keys key, KeyAction action = KeyAction.Click, int ms = 0 ) {
		Delay( ms );
		Input.Key( key, action );
	}

	/// <summary>
	/// 複数のキーを同時押しする (ショートカットキー)。
	/// </summary>
	/// <param name="keys">
	/// 押すキーを順に並べる。前から順に押し、逆順に離す。
	/// 修飾キーは <c>Keys.ControlKey</c> (Ctrl)・<c>Keys.ShiftKey</c> (Shift)・<c>Keys.Menu</c> (Alt)・<c>Keys.LWin</c> (Windows)。
	/// </param>
	/// <remarks>アクティブなウィンドウに送られる。</remarks>
	/// <example>
	/// <code>
	/// Hotkey(Keys.ControlKey, Keys.S);   // Ctrl+S
	/// Hotkey(Keys.Menu, Keys.F4);        // Alt+F4
	/// </code>
	/// </example>
	public void Hotkey( params Keys[] keys ) {
		Check();
		Input.Hotkey( keys );
	}

	/// <summary>
	/// 文字列をキー入力する。
	/// </summary>
	/// <param name="text">入力する文字列。日本語も IME を通さずにそのまま入力される。改行は Enter になる。</param>
	/// <remarks>
	/// アクティブなウィンドウに送られる。特定のウィンドウに送るには <see cref="Window.SendText"/> (前面に出してから入力) か
	/// <see cref="Window.PostText"/> (前面に出さずに送る) を使う。
	/// </remarks>
	/// <example>
	/// <code>
	/// SendText("こんにちは");
	/// </code>
	/// </example>
	public void SendText( string text ) {
		Check();
		Input.SendText( text );
	}

	/// <summary>
	/// キーまたはマウスボタンが今押されているかを返す。UWSC の GETKEYSTATE。
	/// </summary>
	/// <param name="key">調べるキー。マウスボタンは <see cref="Keys.LButton"/> / <see cref="Keys.RButton"/> / <see cref="Keys.MButton"/>。</param>
	/// <returns>押されていれば true。</returns>
	/// <example>
	/// <code>
	/// while (!GetKeyState(Keys.Escape)) { /* 処理 */ Sleep(0.1); }
	/// </code>
	/// </example>
	public bool GetKeyState( Keys key ) => ( NativeMethods.GetAsyncKeyState( (int)( key & Keys.KeyCode ) ) & 0x8000 ) != 0;

	/// <summary>
	/// トグルキー (CapsLock / NumLock / ScrollLock など) がオンかを返す。UWSC の GETKEYSTATE(TGL_xxx)。
	/// </summary>
	/// <param name="key">調べるキー。<see cref="Keys.CapsLock"/>、<see cref="Keys.NumLock"/>、<see cref="Keys.Scroll"/> など。</param>
	/// <returns>オンなら true。</returns>
	/// <example>
	/// <code>
	/// if (GetToggleState(Keys.CapsLock)) Kbd(Keys.CapsLock);
	/// </code>
	/// </example>
	public bool GetToggleState( Keys key ) => ( NativeMethods.GetKeyState( (int)( key & Keys.KeyCode ) ) & 1 ) != 0;

	// ---- ウィンドウ ----

	/// <summary>
	/// タイトルでウィンドウを探す。UWSC の GETID。
	/// </summary>
	/// <param name="title">ウィンドウタイトルの一部 (部分一致・大文字小文字を区別しない)。</param>
	/// <param name="className">クラス名の一部 (部分一致)。省略するとクラス名では絞り込まない。</param>
	/// <param name="timeout">見つからない場合に待つ秒数。既定は 1 秒。0 で待たない、負の値で見つかるまで待ち続ける。</param>
	/// <returns>見つかったウィンドウ。見つからなければ null。</returns>
	/// <remarks>表示されているウィンドウだけが対象。複数ある場合は手前のものを返す。すべて取得するには <see cref="GetAllWindows"/>。</remarks>
	/// <example>
	/// <code>
	/// var w = GetWindow("メモ帳", timeout: 5) ?? throw new Exception("メモ帳がありません");
	/// w.Activate();
	/// </code>
	/// </example>
	public Window? GetWindow( string title, string? className = null, double timeout = 1 ) {
		var sw = Stopwatch.StartNew();
		while( true ) {
			var w = Window.Find( title, className );
			if( w != null )
				return w;
			if( timeout >= 0 && sw.Elapsed.TotalSeconds >= timeout )
				return null;
			Sleep( 0.1 );
		}
	}

	/// <summary>
	/// 現在アクティブなウィンドウ。UWSC の GETID(GET_ACTIVE_WIN)。
	/// </summary>
	/// <example>
	/// <code>
	/// Print(ActiveWindow.Title);
	/// </code>
	/// </example>
	public Window ActiveWindow => Window.Active;

	/// <summary>
	/// 条件に合う表示中のウィンドウをすべて取得する。UWSC の GETALLWIN。
	/// </summary>
	/// <param name="title">ウィンドウタイトルの一部 (部分一致)。省略するとタイトルのあるすべてのウィンドウ。</param>
	/// <param name="className">クラス名の一部 (部分一致)。省略するとクラス名では絞り込まない。</param>
	/// <returns>見つかったウィンドウの配列 (手前から順)。無ければ空の配列。</returns>
	/// <example>
	/// <code>
	/// foreach (var w in GetAllWindows()) Print(w.Title);
	/// foreach (var w in GetAllWindows("メモ帳")) w.Close();
	/// </code>
	/// </example>
	public Window[] GetAllWindows( string? title = null, string? className = null ) => [.. Window.FindAll( title, className )];

	/// <summary>
	/// ウィンドウハンドルから <see cref="Window"/> を取得する。UWSC の HNDTOID。逆 (IDTOHND) は <see cref="Window.Handle"/>。
	/// </summary>
	/// <param name="handle">ウィンドウハンドル (HWND)。</param>
	/// <returns>対応するウィンドウ。無効なハンドルなら null。</returns>
	/// <example>
	/// <code>
	/// var w = WindowFromHandle(hwnd);
	/// IntPtr h = ActiveWindow.Handle;
	/// </code>
	/// </example>
	public Window? WindowFromHandle( IntPtr handle ) => Window.FromHandle( handle );

	/// <summary>
	/// 接続されているモニタの一覧。UWSC の MONITOR。
	/// </summary>
	/// <remarks>
	/// 各要素の <c>Bounds</c> が画面全体、<c>WorkingArea</c> がタスクバーを除いた領域 (いずれも画面座標)、
	/// <c>Primary</c> がメインモニタかどうか。モニタ数は <c>Monitors.Length</c>。
	/// </remarks>
	/// <example>
	/// <code>
	/// foreach (var m in Monitors) Print($"{m.DeviceName} {m.Bounds} primary={m.Primary}");
	/// </code>
	/// </example>
	public Screen[] Monitors => Screen.AllScreens;

	/// <summary>
	/// 画面上の指定位置にあるウィンドウを取得する。UWSC の GETID(GET_FROMPOINT_WIN)。
	/// </summary>
	/// <param name="point">座標 (画面座標。<see cref="MouseOrg"/> の影響を受けない)。</param>
	/// <returns>その位置にあるトップレベルウィンドウ。無ければ null。</returns>
	public Window? WindowFromPoint( Point point ) => Window.FromPoint( point.X, point.Y, child: false );

	/// <inheritdoc cref="WindowFromPoint(Point)"/>
	/// <param name="x">X 座標 (画面座標。<see cref="MouseOrg"/> の影響を受けない)。</param>
	/// <param name="y">Y 座標 (画面座標。<see cref="MouseOrg"/> の影響を受けない)。</param>
	public Window? WindowFromPoint( int x, int y ) => WindowFromPoint( new Point( x, y ) );

	/// <summary>
	/// マウスカーソルの下にあるウィンドウ (トップレベル)。
	/// </summary>
	/// <remarks>コントロール (ボタンなどの子ウィンドウ) を取得するには <see cref="ControlUnderMouse"/>。</remarks>
	public Window? WindowUnderMouse => Window.FromPoint( Cursor.Position.X, Cursor.Position.Y, child: false );

	/// <summary>
	/// マウスカーソルの下にあるコントロール (ボタンや入力欄などの子ウィンドウ)。UWSC の GETID(GET_FROMPOINT_OBJ)。
	/// </summary>
	/// <remarks>WPF・ブラウザーなど、コントロールが子ウィンドウになっていないアプリではウィンドウ全体が返る。</remarks>
	public Window? ControlUnderMouse => Window.FromPoint( Cursor.Position.X, Cursor.Position.Y, child: true );

	/// <summary>
	/// ウィンドウが現れるまで待つ。<see cref="GetWindow"/> と同じで、待ち時間の既定だけ 10 秒。
	/// </summary>
	/// <param name="title">ウィンドウタイトルの一部 (部分一致)。</param>
	/// <param name="className">クラス名の一部 (部分一致)。省略するとクラス名では絞り込まない。</param>
	/// <param name="timeout">待つ秒数。既定は 10 秒。負の値で見つかるまで待ち続ける。</param>
	/// <returns>見つかったウィンドウ。時間内に見つからなければ null。</returns>
	/// <example>
	/// <code>
	/// Click(100, 200); // ダイアログを開くボタン
	/// var dlg = WaitWindow("名前を付けて保存") ?? throw new Exception("ダイアログが開きません");
	/// </code>
	/// </example>
	public Window? WaitWindow( string title, string? className = null, double timeout = 10 ) => GetWindow( title, className, timeout );

	// ---- プロセス ----

	/// <summary>
	/// プログラムやファイルを起動し、そのメインウィンドウを返す。UWSC の EXEC。
	/// </summary>
	/// <param name="fileName">実行ファイルのパス、またはファイル・URL (関連付けられたアプリで開く)。</param>
	/// <param name="arguments">コマンドライン引数。</param>
	/// <returns>起動したプログラムのメインウィンドウ。取得できなければ null。</returns>
	/// <remarks>
	/// ウィンドウが入力待ちになるまで最大 5 秒待つ。
	/// ストアアプリ版のメモ帳などは、起動したプロセスとウィンドウのプロセスが別になるため null になることがある。
	/// その場合は <see cref="GetWindow"/> と組み合わせる。
	/// </remarks>
	/// <example>
	/// <code>
	/// var notepad = Exec("notepad.exe") ?? GetWindow("メモ帳", timeout: 5);
	/// </code>
	/// </example>
	public Window? Exec( string fileName, string arguments = "" ) {
		using var p = Process.Start( new ProcessStartInfo( fileName, arguments ) { UseShellExecute = true } );
		if( p is null )
			return null;
		try {
			p.WaitForInputIdle( 5000 );
			p.Refresh();
			return p.MainWindowHandle != IntPtr.Zero ? new Window( p.MainWindowHandle ) : null;
		} catch( InvalidOperationException ) {
			return null;
		}
	}

	/// <summary>
	/// プログラムを起動し、終了するまで待つ。UWSC の EXEC(..., TRUE)。
	/// </summary>
	/// <param name="fileName">実行ファイルのパス、またはファイル (関連付けられたアプリで開く)。</param>
	/// <param name="arguments">コマンドライン引数。</param>
	/// <returns>プログラムの終了コード。</returns>
	/// <remarks>待っている間に停止が要求されると、待つのをやめてスクリプトを止める (起動したプログラムは終了させない)。</remarks>
	/// <example>
	/// <code>
	/// var code = ExecWait("cmd.exe", "/c copy a.txt b.txt");
	/// if (code != 0) Print("失敗しました");
	/// </code>
	/// </example>
	public int ExecWait( string fileName, string arguments = "" ) {
		using var p = Process.Start( new ProcessStartInfo( fileName, arguments ) { UseShellExecute = true } )
			?? throw new InvalidOperationException( $"{fileName} を起動できませんでした" );
		p.WaitForExitAsync( Cancellation ).GetAwaiter().GetResult();
		return p.ExitCode;
	}

	// ---- 選択ダイアログ・メニュー ----

	/// <summary>
	/// 項目ごとのボタンを並べたダイアログを出し、押されたボタンの番号を返す。UWSC の SLCTBOX(SLCT_BTN, ...)。
	/// </summary>
	/// <param name="message">ダイアログに表示するメッセージ。</param>
	/// <param name="items">選択肢。</param>
	/// <returns>選ばれた項目の番号 (0 から)。閉じられたときは -1。</returns>
	/// <example>
	/// <code>
	/// var i = SlctBox("どれを実行しますか？", "集計", "印刷", "終了");
	/// if (i == 2 || i == -1) return;
	/// </code>
	/// </example>
	public int SlctBox( string message, params string[] items ) => SlctBox( SlctKind.Button, message, items );

	/// <summary>
	/// 選択ダイアログを出し、選ばれた項目の番号を返す。UWSC の SLCTBOX。
	/// </summary>
	/// <param name="kind">表示形式。<see cref="SlctKind.Button"/>・<see cref="SlctKind.Radio"/>・<see cref="SlctKind.Combo"/>・<see cref="SlctKind.List"/>。</param>
	/// <param name="message">ダイアログに表示するメッセージ。</param>
	/// <param name="items">選択肢。</param>
	/// <param name="timeout">この秒数で自動的に閉じる (-1 を返す)。0 以下で無制限。</param>
	/// <returns>選ばれた項目の番号 (0 から)。キャンセル・タイムアウト時は -1。</returns>
	/// <remarks>選ばれた文字列が欲しいときは <c>items[i]</c> で取り出す。複数選択は <see cref="SlctBoxMulti"/>。</remarks>
	/// <example>
	/// <code>
	/// string[] files = ["a.csv", "b.csv", "c.csv"];
	/// var i = SlctBox(SlctKind.List, "ファイルを選択", files, timeout: 30);
	/// if (i >= 0) Print(files[i]);
	/// </code>
	/// </example>
	public int SlctBox( SlctKind kind, string message, string[] items, double timeout = 0 ) {
		Check();
		var r = RunSta( () => {
			using var f = new SelectBoxForm( message, items, kind, false );
			return f.Run( timeout, Cancellation );
		} );
		return r.Length > 0 ? r[0] : -1;
	}

	/// <summary>
	/// 複数選択できる選択ダイアログを出し、選ばれた項目の番号の配列を返す。UWSC の SLCTBOX(SLCT_CHK / SLCT_LST, ...)。
	/// </summary>
	/// <param name="message">ダイアログに表示するメッセージ。</param>
	/// <param name="items">選択肢。</param>
	/// <param name="list">true でリストボックス (Ctrl / Shift で複数選択)、false (既定) でチェックボックス。</param>
	/// <param name="timeout">この秒数で自動的に閉じる。0 以下で無制限。</param>
	/// <returns>選ばれた項目の番号 (0 から) の配列。キャンセル・タイムアウト時は空の配列。</returns>
	/// <example>
	/// <code>
	/// string[] items = ["売上", "在庫", "顧客"];
	/// foreach (var i in SlctBoxMulti("出力する帳票", items)) Print(items[i]);
	/// </code>
	/// </example>
	public int[] SlctBoxMulti( string message, string[] items, bool list = false, double timeout = 0 ) {
		Check();
		return RunSta( () => {
			using var f = new SelectBoxForm( message, items, null, list );
			return f.Run( timeout, Cancellation );
		} );
	}

	/// <summary>
	/// ポップアップメニューを表示し、選ばれた項目を返す。UWSC の POPUPMENU。
	/// </summary>
	/// <param name="items">
	/// メニュー項目。"-" は区切り線。先頭に "&lt;&lt;" を付けると直前の項目のサブメニューになる ("&lt;&lt;&lt;&lt;" で 2 段下)。
	/// </param>
	/// <param name="point">表示位置。省略するとマウスの位置。<see cref="MouseOrg"/> の影響を受ける。</param>
	/// <returns>選ばれた項目の文字列 (先頭の "&lt;&lt;" を除く)。キャンセル時は null。</returns>
	/// <example>
	/// <code>
	/// var s = PopupMenu(["開く", "保存", "-", "エクスポート", "&lt;&lt;CSV", "&lt;&lt;Excel", "-", "終了"]);
	/// if (s == "CSV") { /* ... */ }
	/// </code>
	/// </example>
	public string? PopupMenu( string[] items, Point? point = null ) {
		Check();
		var p = point is { } pt ? ToScreen( pt ) : Cursor.Position;
		return RunSta( () => CsWSC.PopupMenu.Show( items, p, Cancellation ) );
	}

	// ---- 入力ロック ----

	private readonly InputLock _inputLock = new();

	/// <summary>
	/// ユーザーのキーボード・マウス入力をロック/解除する。UWSC の LOCKHARD。
	/// </summary>
	/// <param name="lockInput">true でロック、false で解除。</param>
	/// <remarks>
	/// スクリプトからの入力 (<see cref="Click(Point, MouseButton)"/>・<see cref="Kbd"/> など) は通る。
	/// 非常停止のため Pause キーはロック中も効く。スクリプトが終了すると自動で解除される。
	/// 管理者権限で動いているウィンドウへの入力はロックできない。
	/// </remarks>
	/// <example>
	/// <code>
	/// LockHard(true);
	/// try { /* 邪魔されたくない操作 */ }
	/// finally { LockHard(false); }
	/// </code>
	/// </example>
	public void LockHard( bool lockInput ) => LockHardEx( null, lockInput ? LockMode.All : LockMode.None );

	/// <summary>
	/// 入力の種類や対象ウィンドウを指定して入力をロックする。UWSC の LOCKHARDEX。
	/// </summary>
	/// <param name="window">ロックする対象のウィンドウ。null ですべてのウィンドウ。</param>
	/// <param name="mode">
	/// <see cref="LockMode.All"/> (既定)・<see cref="LockMode.Keyboard"/>・<see cref="LockMode.Mouse"/>。
	/// <see cref="LockMode.None"/> で解除。
	/// </param>
	/// <remarks>
	/// ウィンドウを指定すると、キーボードはそのウィンドウがアクティブなとき、マウスはカーソルがそのウィンドウ上にあるときだけ捨てる。
	/// 呼ぶたびに設定を置き換える (重ねがけはしない)。スクリプトからの入力と Pause キーは通る。
	/// </remarks>
	/// <example>
	/// <code>
	/// var w = GetWindow("Excel");
	/// LockHardEx(w, LockMode.Mouse); // Excel 上のマウス操作だけ止める
	/// // ...
	/// LockHardEx(null, LockMode.None);
	/// </code>
	/// </example>
	public void LockHardEx( Window? window = null, LockMode mode = LockMode.All ) {
		Check();
		_inputLock.Set( mode, window?.Handle ?? IntPtr.Zero );
	}

	// ---- COM ----

	[System.Runtime.InteropServices.DllImport( "ole32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false )]
	private static extern void CLSIDFromProgID( string progId, out Guid clsid );

	[System.Runtime.InteropServices.DllImport( "oleaut32.dll", PreserveSig = false )]
	private static extern void GetActiveObject( ref Guid clsid, IntPtr reserved, [System.Runtime.InteropServices.MarshalAs( System.Runtime.InteropServices.UnmanagedType.IUnknown )] out object obj );

	/// <summary>
	/// COM オブジェクトを作成する。UWSC の CREATEOLEOBJ。
	/// </summary>
	/// <param name="progId">ProgID ("Excel.Application"・"Scripting.FileSystemObject" など)。</param>
	/// <returns>作成したオブジェクト (dynamic なのでメソッド・プロパティをそのまま呼べる)。</returns>
	/// <example>
	/// <code>
	/// var excel = CreateOleObj("Excel.Application");
	/// excel.Visible = true;
	/// var book = excel.Workbooks.Add();
	/// </code>
	/// </example>
	public dynamic CreateOleObj( string progId ) {
		var type = Type.GetTypeFromProgID( progId ) ?? throw new ArgumentException( $"ProgID \"{progId}\" が見つかりません" );
		return Activator.CreateInstance( type ) ?? throw new InvalidOperationException( $"{progId} を作成できませんでした" );
	}

	/// <summary>
	/// 起動中のアプリケーションの COM オブジェクトを取得する。UWSC の GETACTIVEOLEOBJ。
	/// </summary>
	/// <param name="progId">ProgID ("Excel.Application"・"Word.Application" など)。</param>
	/// <returns>起動中のオブジェクト。起動していなければ null。</returns>
	/// <remarks>
	/// .NET 5 以降で削除された <c>Marshal.GetActiveObject</c> の代わり。
	/// 同じアプリが複数起動しているときは、先に起動したものが返ることが多い。
	/// </remarks>
	/// <example>
	/// <code>
	/// var excel = GetActiveOleObj("Excel.Application") ?? throw new Exception("Excel が起動していません");
	/// Print(excel.ActiveSheet.Range("A1").Value);
	/// </code>
	/// </example>
	public dynamic? GetActiveOleObj( string progId ) {
		CLSIDFromProgID( progId, out var clsid );
		try {
			GetActiveObject( ref clsid, IntPtr.Zero, out var obj );
			return obj;
		} catch( System.Runtime.InteropServices.COMException e ) when( e.HResult == unchecked((int)0x800401E3) ) { // MK_E_UNAVAILABLE
			return null;
		}
	}

	// ---- 音声 ----

	// SAPI のオブジェクトは作ったスレッド (アパートメント) で使う
	private readonly ThreadLocal<object?> _voice = new();

	/// <summary>
	/// 文字列を音声で読み上げる。UWSC の SPEAK。
	/// </summary>
	/// <param name="text">読み上げる文字列。</param>
	/// <param name="async">true で読み上げの終了を待たずに戻る。既定は終わるまで待つ。</param>
	/// <param name="interrupt">true で読み上げ中の音声を止めてから読み上げる。</param>
	/// <param name="rate">速さ (-10〜10、0 が標準)。</param>
	/// <param name="volume">音量 (0〜100)。</param>
	/// <remarks>Windows の音声合成 (SAPI) を使う。声は Windows の設定 (既定の音声) になる。</remarks>
	/// <example>
	/// <code>
	/// Speak("処理が完了しました");
	/// Speak("", interrupt: true); // 読み上げを止める
	/// </code>
	/// </example>
	public void Speak( string text, bool async = false, bool interrupt = false, int rate = 0, int volume = 100 ) {
		Check();
		const int SVSFlagsAsync = 1, SVSFPurgeBeforeSpeak = 2;
		dynamic voice = _voice.Value ??= (object)CreateOleObj( "SAPI.SpVoice" );
		voice.Rate = Math.Clamp( rate, -10, 10 );
		voice.Volume = Math.Clamp( volume, 0, 100 );
		voice.Speak( text, SVSFlagsAsync | ( interrupt ? SVSFPurgeBeforeSpeak : 0 ) );
		if( async ) return;
		// 停止要求を確認しながら終わるのを待つ
		while( !(bool)voice.WaitUntilDone( 100 ) ) {
			if( Cancellation.IsCancellationRequested ) {
				voice.Speak( "", SVSFlagsAsync | SVSFPurgeBeforeSpeak );
				Check();
			}
		}
	}

	// ---- コマンド実行 ----

	static ScriptGlobals() => System.Text.Encoding.RegisterProvider( System.Text.CodePagesEncodingProvider.Instance );

	/// <summary>
	/// コマンドプロンプト (cmd.exe) のコマンドを実行し、出力を返す。UWSC の DOSCMD。
	/// </summary>
	/// <param name="command">実行するコマンド。パイプ・リダイレクト・<c>&amp;&amp;</c> なども使える。</param>
	/// <param name="async">true で終了を待たずに戻る (戻り値は空文字列)。</param>
	/// <param name="show">true でコンソール画面を表示する (出力は取得できず、戻り値は空文字列)。</param>
	/// <returns>標準出力と標準エラー出力をつなげた文字列。</returns>
	/// <remarks>出力はコンソールのコードページ (日本語環境では Shift-JIS) で読むので文字化けしない。待っている間に停止が要求されると、コマンドを強制終了する。</remarks>
	/// <example>
	/// <code>
	/// Print(DosCmd("dir /b C:\\"));
	/// var ip = DosCmd("ipconfig | findstr IPv4");
	/// </code>
	/// </example>
	public string DosCmd( string command, bool async = false, bool show = false ) {
		var oem = System.Text.Encoding.GetEncoding( System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage );
		// /s: 先頭と末尾の引用符だけを外し、command はそのまま解釈させる
		var psi = new ProcessStartInfo( "cmd.exe" ) { Arguments = $"/d /s /c \"{command}\"" };
		return RunConsole( psi, oem, async, show );
	}

	/// <summary>
	/// PowerShell のコマンドを実行し、出力を返す。UWSC の POWERSHELL。
	/// </summary>
	/// <param name="command">実行するコマンド (複数行のスクリプトも可)。</param>
	/// <param name="async">true で終了を待たずに戻る (戻り値は空文字列)。</param>
	/// <param name="show">true で PowerShell の画面を表示する (出力は取得できず、戻り値は空文字列)。</param>
	/// <param name="core">true で PowerShell 7 (pwsh.exe) を使う。既定は Windows PowerShell (powershell.exe)。</param>
	/// <returns>標準出力と標準エラー出力をつなげた文字列。</returns>
	/// <remarks>実行ポリシーは無視 (Bypass)、プロファイルは読み込まない。待っている間に停止が要求されると、コマンドを強制終了する。</remarks>
	/// <example>
	/// <code>
	/// Print(PowerShell("Get-Process | Sort-Object CPU -Descending | Select-Object -First 5"));
	/// </code>
	/// </example>
	public string PowerShell( string command, bool async = false, bool show = false, bool core = false ) {
		// 出力を UTF-8 にしてから実行する。引用符の問題を避けるため Base64 (UTF-16LE) で渡す
		var script = show ? command : "[Console]::OutputEncoding = [Text.Encoding]::UTF8; $OutputEncoding = [Text.Encoding]::UTF8; $ProgressPreference = 'SilentlyContinue'\n" + command;
		var encoded = Convert.ToBase64String( System.Text.Encoding.Unicode.GetBytes( script ) );
		var psi = new ProcessStartInfo( core ? "pwsh.exe" : "powershell.exe" ) {
			Arguments = show
				? $"-NoProfile -ExecutionPolicy Bypass -NoExit -EncodedCommand {encoded}"
				: $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand {encoded}",
		};
		return RunConsole( psi, new System.Text.UTF8Encoding( false ), async, show );
	}

	private string RunConsole( ProcessStartInfo psi, System.Text.Encoding encoding, bool async, bool show ) {
		Check();
		psi.UseShellExecute = false;
		psi.CreateNoWindow = !show;
		if( !show ) {
			psi.RedirectStandardOutput = psi.RedirectStandardError = true;
			psi.RedirectStandardInput = true; // 入力待ちで止まらないよう、すぐ閉じる
			psi.StandardOutputEncoding = psi.StandardErrorEncoding = encoding;
		}
		using var p = Process.Start( psi ) ?? throw new InvalidOperationException( $"{psi.FileName} を起動できませんでした" );
		if( !show ) p.StandardInput.Close();
		if( async ) return "";
		if( show ) {
			WaitOrKill( p );
			return "";
		}
		// 両方を同時に読まないと、バッファが埋まってデッドロックする
		var stdout = p.StandardOutput.ReadToEndAsync();
		var stderr = p.StandardError.ReadToEndAsync();
		WaitOrKill( p );
		return stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
	}

	private void WaitOrKill( Process p ) {
		try {
			p.WaitForExitAsync( Cancellation ).GetAwaiter().GetResult();
		} catch( OperationCanceledException ) {
			try { p.Kill( entireProcessTree: true ); } catch( InvalidOperationException ) { }
			throw;
		}
	}

	// ---- クリップボード ----

	/// <summary>
	/// クリップボードの文字列を取得する。UWSC の GETSTR(0)。
	/// </summary>
	/// <returns>クリップボードの文字列。文字列が入っていなければ空文字。</returns>
	/// <example>
	/// <code>
	/// Hotkey(Keys.ControlKey, Keys.C);
	/// Sleep(0.2);
	/// Print(GetClipboard());
	/// </code>
	/// </example>
	public string GetClipboard() => RunSta( () => Clipboard.ContainsText() ? Clipboard.GetText() : "" );

	/// <summary>
	/// クリップボードに文字列を設定する。UWSC の SENDSTR(0, ...)。
	/// </summary>
	/// <param name="text">設定する文字列。空文字は指定できない。</param>
	/// <example>
	/// <code>
	/// SetClipboard("貼り付ける文字");
	/// Hotkey(Keys.ControlKey, Keys.V);
	/// </code>
	/// </example>
	public void SetClipboard( string text ) => RunSta( () => {
		Clipboard.SetText( text );
		return 0;
	} );

	private void Check() => Cancellation.ThrowIfCancellationRequested();

	// UWSC の BTN / KBD / MMV の ms 引数: 実行前に待つ (停止要求があれば中断)
	private void Delay( int ms ) {
		if( ms > 0 ) Cancellation.WaitHandle.WaitOne( ms );
		Check();
	}

	private static T RunSta<T>( Func<T> fn ) {
		if( System.Threading.Thread.CurrentThread.GetApartmentState() == ApartmentState.STA )
			return fn();
		T result = default!;
		Exception? error = null;
		var t = new System.Threading.Thread( () => {
			try {
				result = fn();
			} catch( Exception e ) {
				error = e;
			}
		} );
		t.SetApartmentState( ApartmentState.STA );
		t.Start();
		t.Join();
		if( error != null )
			throw error;
		return result;
	}
}
