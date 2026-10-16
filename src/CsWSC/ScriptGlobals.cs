using System.Diagnostics;
using static CsWSC.NativeMethods;

namespace CsWSC;

/// <summary>
/// スクリプトのトップレベルから直接呼べる関数群です。
/// public メンバーがそのままスクリプトの組み込み API になります。
/// </summary>
/// <remarks>
/// ここに書いた XML ドキュメントは、VS Code 拡張の補完・ホバー・引数ヒントにそのまま表示されます。
/// summary は 1〜2 文で要点を、param は引数ごとに単位・省略時の動作を、remarks に注意点を書きます。
/// </remarks>
public partial class ScriptGlobals( Action<string> output, CancellationToken cancellation, ILogWindow? logWindow = null, string? scriptPath = null ) {
	/// <summary>
	/// 実行中のスクリプトのファイル名 (フォルダーを除く) です。UWSC の GET_UWSC_NAME に相当します。
	/// </summary>
	/// <remarks>ファイルから実行していない場合は空文字です。</remarks>
	/// <example>
	/// <code>
	/// Print(ScriptName); // main.csws
	/// </code>
	/// </example>
	public string ScriptName { get; } = scriptPath is null ? "" : System.IO.Path.GetFileName( scriptPath );


	// 停止要求に加え、メインスクリプト終了時にも THREAD を止めるためのトークン
	private readonly CancellationTokenSource _lifetime = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
	private readonly List<System.Threading.Thread> _threads = [];
	private Exception? _threadError;

	/// <summary>
	/// スクリプトの停止要求を表すトークンです。
	/// </summary>
	/// <remarks>
	/// 停止ボタン・停止ホットキー・Pause キーで停止が要求されます。
	/// 組み込みコマンドと Window / Control などのメソッドの呼び出し時、ループの 1 周ごとに自動で確認します。
	/// ただし後片付けに使うもの (Close・Kill・BalloonClear・LockHard(false) など) は確認しません。
	/// それ以外の長い処理の途中で止めたいときは <c>Cancellation.ThrowIfCancellationRequested()</c> を呼びます。
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
	/// <see cref="Balloon(string, Point, BalloonTail, float?, string?, Color?, Color?, double?)"/> で引数を省略したときの初期値です。
	/// </summary>
	/// <example>
	/// <code>
	/// BalloonDefault.FontSize = 14;
	/// BalloonDefault.BackColor = Color.LightBlue;
	/// Balloon("こんにちは");
	/// </code>
	/// </example>
	public BalloonDefaults BalloonDefault { get; } = new();

	/// <summary>
	/// 吹き出しを表示し、その吹き出しを返します。呼ぶたびに新しい吹き出しが増えます。UWSC の FUKIDASI に相当します。
	/// </summary>
	/// <param name="message">表示する文字列です。改行で複数行になります。</param>
	/// <param name="point">しっぽの先が指す座標です。省略すると (0, 0) です。</param>
	/// <param name="tail">しっぽの位置です。既定は <see cref="BalloonTail.None"/> (しっぽなし) です。</param>
	/// <param name="fontSize">文字の大きさです (ポイント)。省略すると <see cref="BalloonDefault"/> の値 (既定 10) です。</param>
	/// <param name="fontName">フォント名です (例: "メイリオ")。省略すると <see cref="BalloonDefault"/> の値 (既定はシステムのメッセージ用フォント) です。見つからない場合は代替フォントになります。</param>
	/// <param name="foreColor">文字色です。省略すると <see cref="BalloonDefault"/> の値 (既定は黒) です。</param>
	/// <param name="backColor">背景色です。省略すると <see cref="BalloonDefault"/> の値 (既定は薄い黄色) です。</param>
	/// <param name="opacity">不透明度です (0.0 = 透明 ～ 1.0 = 不透明)。省略すると <see cref="BalloonDefault"/> の値 (既定 1.0) です。範囲外は丸めます。</param>
	/// <returns>表示した吹き出しです。<see cref="Balloon.Update(string?, Point?)"/> で内容・位置を変え、<see cref="Balloon.Close"/> で消します。</returns>
	/// <remarks>
	/// 複数の吹き出しを同時に表示できます。同じ吹き出しを書き換えたいときは、返り値の <see cref="Balloon.Update(string?, Point?)"/> を使います。
	/// 前面に出ずフォーカスも奪わず、クリックは下のウィンドウに届きます。画面からはみ出す場合は内側に寄せます。
	/// スクリプトが終わると自動で消えます。すべて消すには <see cref="BalloonClear"/> を使います。
	/// </remarks>
	/// <example>
	/// <code>
	/// var b = Balloon("処理中です…");
	/// b.Update("あと少し…");
	/// b.Close();
	///
	/// var a = Balloon("ここ", new Point(600, 300), BalloonTail.BottomRight, 14, backColor: Color.LightBlue);
	/// var c = Balloon("そこ", new Point(900, 300));
	/// BalloonClear(); // 全部消す
	///
	/// using (Balloon("この間だけ表示")) { Sleep(2000); }
	/// </code>
	/// </example>
	public Balloon Balloon( string message, Point point = default, BalloonTail tail = BalloonTail.None,
		float? fontSize = null, string? fontName = null, Color? foreColor = null, Color? backColor = null, double? opacity = null ) {
		Check();
		ArgumentNullException.ThrowIfNull( message );
		var pos = ToScreen( point );
		var d = BalloonDefault;
		return new Balloon( _balloons, BalloonHost.Normalize( message ), pos, tail, fontSize ?? d.FontSize,
			backColor ?? d.BackColor, foreColor ?? d.ForeColor, Math.Clamp( opacity ?? d.Opacity, 0.0, 1.0 ), fontName ?? d.FontName,
			ToScreen, FromScreen );
	}

	/// <summary><see cref="Balloon(string, Point, BalloonTail, float?, string?, Color?, Color?, double?)"/> の座標を x, y で指定する版です。</summary>
	public Balloon Balloon( string message, int x, int y, BalloonTail tail = BalloonTail.None,
		float? fontSize = null, string? fontName = null, Color? foreColor = null, Color? backColor = null, double? opacity = null )
		=> Balloon( message, new Point( x, y ), tail, fontSize, fontName, foreColor, backColor, opacity );

	/// <summary>
	/// 表示中の吹き出しをすべて消します。
	/// </summary>
	public void BalloonClear() => _balloons.CloseAll();

	// ---- スレッド ----

	/// <summary>
	/// 処理を別スレッドで並行に実行します。UWSC の THREAD に相当します。
	/// </summary>
	/// <param name="action">実行する処理です。<c>() =&gt; { ... }</c> の形で渡します。</param>
	/// <remarks>
	/// メインスクリプトが終わると、スレッド内で組み込みコマンド (Sleep など) を呼んだ時点でスレッドも止まります。
	/// スレッド内でエラーが起きると、スクリプト全体が停止してエラーを表示します。
	/// この関数があるため、スクリプト内で <c>Thread.Sleep</c> などとは書けません。
	/// 待機には <see cref="Sleep"/> を使い、型が必要なら <c>System.Threading.Thread</c> と書きます。
	/// </remarks>
	/// <example>
	/// <code>
	/// // エラーダイアログが出たら自動で閉じる
	/// Thread(() =>
	/// {
	///     while (true)
	///     {
	///         GetWindow("エラー")?.Close();
	///         Sleep(500);
	///     }
	/// });
	/// </code>
	/// </example>
	public void Thread( Action action ) {
		Check();
		var t = new System.Threading.Thread( () => {
			// MouseOrigin の基準は親から引き継がず、画面座標から始める
			_origin.Value = null;
			try {
				action();
			} catch( OperationCanceledException ) { } catch( Exception e ) {
				Interlocked.CompareExchange( ref _threadError, e, null );
				_lifetime.Cancel();
			}
		} ) { IsBackground = true, Name = "CsWSC THREAD" };
		t.SetApartmentState( ApartmentState.STA ); // クリップボード・UI Automation 用
		lock( _threads ) {
			_threads.Add( t );
		}

		t.Start();
	}

	/// <summary>スレッドで発生した最初のエラーです。</summary>
	internal Exception? ThreadError => _threadError;

	/// <summary>メインスクリプト終了時に呼びます。THREAD に停止を要求して少しの間終了を待ち、吹き出しを消します。</summary>
	internal void EndThreads( TimeSpan wait ) {
		_lifetime.Cancel();
		System.Threading.Thread[] threads;
		lock( _threads ) {
			threads = [.. _threads];
		}

		var deadline = DateTime.UtcNow + wait;
		var allEnded = true;
		foreach( var t in threads ) {
			var left = deadline - DateTime.UtcNow;
			if( left <= TimeSpan.Zero || !t.Join( left ) ) {
				allEnded = false;
				break;
			}
		}
		_balloons.Dispose();
		if( ScreenImage.Frame == _imgFrame ) {
			ScreenImage.Frame = null;
		}

		_imgFrame?.Dispose();
		_inputLock.Dispose();
		// 読み上げ用の COM オブジェクトを解放する
		foreach( var voice in _voice.Values ) {
			if( voice != null && System.Runtime.InteropServices.Marshal.IsComObject( voice ) ) {
				System.Runtime.InteropServices.Marshal.FinalReleaseComObject( voice );
			}
		}

		_voice.Dispose();
		// 終わっていない THREAD はまだ Cancellation を使うので、そのときは解放しない
		if( allEnded ) {
			_lifetime.Dispose();
		}
	}

	// ---- 出力・待機 ----

	/// <summary>
	/// PRINT 窓に値を 1 行出力します。UWSC の PRINT に相当します。
	/// </summary>
	/// <param name="value">出力する値です。文字列以外は ToString() した結果を出します。null は空行です。</param>
	/// <example>
	/// <code>
	/// Print("開始");
	/// Print($"x = {x}, y = {y}");
	/// </code>
	/// </example>
	public void Print( object? value ) {
		Check();
		output( value?.ToString() ?? "" );
	}

	/// <summary>
	/// PRINT 窓の表示/非表示を切り替えます。UWSC の LOGPRINT に相当します。
	/// </summary>
	/// <param name="visible">true で表示、false で非表示です。非表示にすると、以降 Print しても窓は自動で表示されません。</param>
	/// <example>
	/// <code>
	/// LogPrint(false); // PRINT 窓を出さずに実行
	/// </code>
	/// </example>
	public void LogPrint( bool visible ) {
		Check();
		logWindow?.SetVisible( visible );
	}

	/// <summary>
	/// PRINT 窓の表示/非表示を切り替え、位置とサイズを変えます。UWSC の LOGPRINT(表示, x, y, 幅, 高さ) に相当します。
	/// </summary>
	/// <param name="visible">true で表示、false で非表示です。</param>
	/// <param name="x">左上の X 座標です。</param>
	/// <param name="y">左上の Y 座標です。</param>
	/// <param name="width">幅です。省略すると変えません。</param>
	/// <param name="height">高さです。省略すると変えません。</param>
	/// <example>
	/// <code>
	/// LogPrint(true, 0, 0, 400, 300);
	/// </code>
	/// </example>
	public void LogPrint( bool visible, int x, int y, int? width = null, int? height = null ) {
		Check();
		var p = ToScreen( new Point( x, y ) );
		logWindow?.SetBounds( p.X, p.Y, width, height );
		logWindow?.SetVisible( visible );
	}

	/// <summary>
	/// PRINT 窓の表示/非表示と位置を設定します。<see cref="LogPrint(bool, int, int, int?, int?)"/> の座標を Point で指定する版です。
	/// </summary>
	public void LogPrint( bool visible, Point point, int? width = null, int? height = null )
		=> LogPrint( visible, point.X, point.Y, width, height );

	/// <summary>
	/// PRINT 窓の内容を消去します。
	/// </summary>
	public void LogClear() {
		Check();
		logWindow?.Clear();
	}

	/// <summary>
	/// 指定したミリ秒だけ待ちます。UWSC の SLEEP (UWSC は秒指定なので注意) に相当します。
	/// </summary>
	/// <param name="ms">待つミリ秒数です (1000 で 1 秒)。0 以下は待ちません。</param>
	/// <remarks>待っている間に停止が要求されると、すぐに中断してスクリプトを止めます。</remarks>
	/// <example>
	/// <code>
	/// Sleep(500);
	/// </code>
	/// </example>
	public void Sleep( int ms ) => ScriptStop.Sleep( ms, Cancellation );

	/// <summary>
	/// スクリプト全体を終了します。UWSC の EXITEXIT に相当します。
	/// </summary>
	/// <remarks>関数の中や <see cref="Thread"/> の中から呼んでも、メインスクリプトと全スレッドに停止を要求します。</remarks>
	/// <example>
	/// <code>
	/// if (GetWindow("メモ帳") == null) ExitScript();
	/// </code>
	/// </example>
	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	public void ExitScript() {
		_lifetime.Cancel();
		throw new OperationCanceledException( _lifetime.Token );
	}

	/// <summary>
	/// 入力ダイアログを表示し、入力された文字列を返します。UWSC の INPUT に相当します。
	/// </summary>
	/// <param name="prompt">ダイアログに表示する説明文です。</param>
	/// <param name="defaultValue">入力欄に最初から入れておく文字列です。</param>
	/// <returns>入力された文字列です。キャンセルされた場合は空文字です。</returns>
	/// <example>
	/// <code>
	/// var name = InputBox("名前を入力", "山田");
	/// if (name == "") return;
	/// </code>
	/// </example>
	public string InputBox( string prompt, string defaultValue = "" ) {
		Check();
		return RunSta( () => {
			using var f = new InputBoxForm( prompt, defaultValue, ScriptName );
			return f.Run( Cancellation );
		} );
	}

	// ---- マウス ----

	// MouseOrigin で設定した座標の基準 (Window が null なら画面座標) と、例外メッセージ用のタイトル
	// (閉じた後はタイトルを取れないので MouseOrigin の時点で覚えておく)
	private sealed record Origin( Window? Window, string Title ) {
		public static readonly Origin Screen = new( null, "" );
	}

	// スレッド・非同期の流れごとに分けるため AsyncLocal に置く (null は画面座標)。
	// async メソッドの中で切り替えた基準は、メソッドを抜けると呼び出し前に戻る (AsyncLocal の仕様)。
	// Thread で作ったスレッドは開始時に画面座標へ戻す (親の基準を引き継がない)
	private readonly AsyncLocal<Origin?> _origin = new();
	private Origin CurrentOrigin => _origin.Value ?? Origin.Screen;
	internal Window? OriginWindow => CurrentOrigin.Window;
	private string _originTitle => CurrentOrigin.Title;

	/// <summary>
	/// 以降の座標指定の基準を、指定ウィンドウのクライアント領域の左上 (null なら画面座標) に切り替えます。UWSC の MOUSEORG(id, MORG_CLIENT) に相当します。
	/// </summary>
	/// <param name="window">基準にするウィンドウです。null なら画面座標に戻します。</param>
	/// <returns>破棄すると直前の基準 (最初は画面座標) に戻るオブジェクトです。<c>using</c> で範囲を限定できます。</returns>
	/// <remarks>
	/// 座標を受け取る・返すグローバルのコマンドすべてが対象です。
	/// 基準の位置は呼び出しのたびにウィンドウの現在位置から求めるため、途中でウィンドウが動いてもずれません。
	/// 引数なしの <c>MouseOrigin()</c> で画面座標に戻ります。戻り値を破棄すると直前の基準に戻ります。
	/// 基準のウィンドウが閉じられた後に対象の操作を行うと <see cref="InvalidOperationException"/> になります。
	/// 基準はスレッドごとに別です。<see cref="Thread"/> で作ったスレッドは画面座標から始まり、その中で切り替えてもほかのスレッドには影響しません。
	/// async メソッドの中で切り替えた基準は、そのメソッドを抜けると呼び出し前の基準に戻ります。
	/// </remarks>
	/// <exception cref="InvalidOperationException">指定したウィンドウがすでに閉じられています。</exception>
	/// <example>
	/// <code>
	/// var w = GetWindow("メモ帳");
	/// MouseOrigin(w);
	/// Mouse(MouseButton.Left, KeyAction.Click, 10, 20);    // クライアント領域の (10, 20)
	/// MouseOrigin();   // 画面座標に戻す
	///
	/// using (MouseOrigin(w)) {
	///     Mouse(MouseButton.Left, KeyAction.Click, 10, 20); // ブロック内だけクライアント領域基準
	/// }
	/// </code>
	/// </example>
	public IDisposable MouseOrigin( Window? window = null ) {
		// 画面座標に戻す (null) ときは後片付けにも使うので確認しない
		if( window != null ) {
			Check();
		}
		if( window is { Exists: false } ) {
			throw new InvalidOperationException( $"MouseOrigin に指定したウィンドウ (0x{window.Handle:X}) は閉じられています" );
		}
		var prev = _origin.Value;
		_origin.Value = window == null ? null : new( window, window.Title ?? "" );
		return new OriginScope( () => _origin.Value = prev );
	}

	private sealed class OriginScope( Action restore ) : IDisposable {
		private Action? _restore = restore;
		public void Dispose() {
			_restore?.Invoke();
			_restore = null;
		}
	}

	// 現在の基準の左上 (画面座標)
	private Point OriginOffset() => OriginWindow switch {
		null => Point.Empty,
		{ Exists: false } => throw new InvalidOperationException( $"MouseOrigin の基準のウィンドウ \"{_originTitle}\" は閉じられています" ),
		var w => Window.GetClientOrigin( w.Handle ),
	};

	private Point ToScreen( Point p ) {
		var o = OriginOffset();
		return new( p.X + o.X, p.Y + o.Y );
	}

	private Point FromScreen( Point p ) {
		var o = OriginOffset();
		return new( p.X - o.X, p.Y - o.Y );
	}

	private Rectangle ToScreen( Rectangle r ) => new( ToScreen( r.Location ), r.Size );

	/// <summary>
	/// マウスカーソルを移動します。UWSC の MMV に相当します。
	/// </summary>
	/// <param name="point">移動先の座標です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。UWSC の MMV の第 3 引数と同じく、待ってから移動します。</param>
	public void Move( Point point, int delayMs = 0 ) {
		Sleep( delayMs );
		var p = ToScreen( point );
		Input.MoveMouse( p.X, p.Y );
	}

	/// <inheritdoc cref="Move(Point, int)"/>
	/// <param name="x">移動先の X 座標です。</param>
	/// <param name="y">移動先の Y 座標です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public void Move( int x, int y, int delayMs = 0 ) => Move( new Point( x, y ), delayMs );

	/// <summary>
	/// 指定位置へマウスを移動してからボタンを押す・離します。UWSC の BTN に相当します。
	/// </summary>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action">
	/// <see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す)。
	/// </param>
	/// <param name="point">移動先の座標です。<c>null</c> なら移動せず現在のマウス位置で操作します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。UWSC の BTN と同じく、待ってから移動・操作します。</param>
	/// <remarks><see cref="KeyAction.Down"/> と <see cref="KeyAction.Up"/> を組み合わせるとドラッグできます。</remarks>
	/// <example>
	/// <code>
	/// Mouse(MouseButton.Left, KeyAction.Down, new Point(100, 100));
	/// Mouse(MouseButton.Left, KeyAction.Up, 300, 200, 500);
	/// Mouse(MouseButton.Left, KeyAction.Click, null); // 現在位置でクリック
	/// </code>
	/// </example>
	public void Mouse( MouseButton button, KeyAction action, Point? point = null, int delayMs = 0 ) {
		if( point is Point p ) {
			Move( p, delayMs );
		} else {
			Sleep( delayMs );
		}
		Input.Button( button, action );
	}

	/// <inheritdoc cref="Mouse(MouseButton, KeyAction, Point?, int)"/>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action">
	/// <see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す)。
	/// </param>
	/// <param name="x">移動先の X 座標です。</param>
	/// <param name="y">移動先の Y 座標です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動・操作します。</param>
	public void Mouse( MouseButton button, KeyAction action, int x, int y, int delayMs = 0 ) => Mouse( button, action, new Point( x, y ), delayMs );

	/// <summary>
	/// <see cref="MouseOrigin"/> で指定したウィンドウへ、前面に出さずにボタン操作を送ります。UWSC の MOUSEORG(id, MORG_DIRECT) + BTN に相当します。
	/// </summary>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action">
	/// <see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す)。
	/// </param>
	/// <param name="point">操作する位置です (<see cref="MouseOrigin"/> のウィンドウのクライアント領域の左上が基準)。<c>null</c> なら現在のマウス位置です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから送ります。</param>
	/// <exception cref="InvalidOperationException"><see cref="MouseOrigin"/> でウィンドウを指定していない、またはそのウィンドウが閉じられています。</exception>
	/// <remarks>
	/// その位置にある最も深い子ウィンドウへメッセージを送ります。マウスカーソルは動きません。
	/// アプリによっては反応しません (特に Down/Up によるドラッグ)。
	/// </remarks>
	/// <example>
	/// <code>
	/// using (MouseOrigin(w)) {
	///     PostMouse(MouseButton.Left, KeyAction.Click, 10, 20);
	/// }
	/// </code>
	/// </example>
	public void PostMouse( MouseButton button, KeyAction action, Point? point = null, int delayMs = 0 ) {
		var w = OriginWindow ?? throw new InvalidOperationException( "PostMouse の前に MouseOrigin(w) で送り先のウィンドウを指定してください" );
		w.PostMouseCore( point, button, action, delayMs ); // 待った後で、閉じていれば例外。null なら現在のマウス位置
	}

	/// <inheritdoc cref="PostMouse(MouseButton, KeyAction, Point?, int)"/>
	/// <param name="button">操作するボタンです。</param>
	/// <param name="action">
	/// <see cref="KeyAction.Click"/> (押して離す)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す)。
	/// </param>
	/// <param name="x">X 座標です (<see cref="MouseOrigin"/> のウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="y">Y 座標です (<see cref="MouseOrigin"/> のウィンドウのクライアント領域の左上が基準)。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。</param>
	public void PostMouse( MouseButton button, KeyAction action, int x, int y, int delayMs = 0 ) => PostMouse( button, action, new Point( x, y ), delayMs );

	/// <summary>
	/// マウスのホイールを回します。
	/// </summary>
	/// <param name="notches">回す量です (ノッチ数)。正の値で下 (手前) 方向、負の値で上方向です。</param>
	/// <param name="point">移動先の座標です。<c>null</c> なら移動せず現在のマウス位置で回します。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動・操作します。</param>
	/// <example>
	/// <code>
	/// Wheel(3);  // 3 ノッチ下へ
	/// Wheel(-1); // 1 ノッチ上へ
	/// Wheel(2, 300, 200, 500); // 500ms 待って (300, 200) へ移動し 2 ノッチ下へ
	/// </code>
	/// </example>
	public void Wheel( int notches, Point? point = null, int delayMs = 0 ) {
		if( point is Point p ) {
			Move( p, delayMs );
		} else {
			Sleep( delayMs );
		}
		Check();
		Input.Wheel( notches );
	}

	/// <inheritdoc cref="Wheel(int, Point?, int)"/>
	/// <param name="notches">回す量です (ノッチ数)。正の値で下 (手前) 方向、負の値で上方向です。</param>
	/// <param name="x">移動先の X 座標です。</param>
	/// <param name="y">移動先の Y 座標です。</param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。待ってから移動・操作します。</param>
	public void Wheel( int notches, int x, int y, int delayMs = 0 ) => Wheel( notches, new Point( x, y ), delayMs );

	/// <summary>
	/// 現在のマウスカーソルの位置です。UWSC の G_MOUSE_X / G_MOUSE_Y に相当します。
	/// </summary>
	/// <example>
	/// <code>
	/// var p = MousePosition;
	/// Print($"{p.X}, {p.Y}");
	/// </code>
	/// </example>
	public Point MousePosition => FromScreen( Cursor.Position );

	/// <summary>
	/// 画面上の指定位置の色を取得します。UWSC の PEEKCOLOR に相当します。
	/// </summary>
	/// <param name="point">座標です。</param>
	/// <returns>その位置の色です。<c>.R</c> <c>.G</c> <c>.B</c> で各成分 (0〜255) を取り出せます。</returns>
	/// <exception cref="ArgumentOutOfRangeException">画面の外など、色を取得できない位置です。</exception>
	/// <remarks>同じ画面で何か所も調べるときは <see cref="Capture(Rectangle?)"/> を使います (1 回ずつ画面から読むより速い)。画像ファイルの色は <c>Capture(path).PeekColor(x, y)</c> で調べます。</remarks>
	/// <example>
	/// <code>
	/// var c = PeekColor(10, 10);
	/// if (c.R &gt; 200 &amp;&amp; c.G &lt; 50 &amp;&amp; c.B &lt; 50) Print("赤っぽい");
	/// </code>
	/// </example>
	public Color PeekColor( Point point ) {
		Check();
		return ScreenImage.Peek( ToScreen( point ) );
	}

	/// <inheritdoc cref="PeekColor(Point)"/>
	/// <param name="x">X 座標です。</param>
	/// <param name="y">Y 座標です。</param>
	public Color PeekColor( int x, int y ) => PeekColor( new Point( x, y ) );

	/// <summary>
	/// 現在のマウスカーソルの種類です (矢印・砂時計・I ビームなど)。UWSC の MUSCUR に相当します。
	/// </summary>
	/// <remarks>
	/// 標準のカーソル以外は <see cref="CursorKind.Other"/>、カーソルが非表示なら <see cref="CursorKind.Hidden"/> です。
	/// </remarks>
	/// <example>
	/// <code>
	/// // 砂時計の間は待つ
	/// while (MouseCursor() == CursorKind.Wait || MouseCursor() == CursorKind.AppStarting) Sleep(200);
	/// </code>
	/// </example>
	public CursorKind MouseCursor() {
		Check();
		var info = new CURSORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<CURSORINFO>() };
		if( !GetCursorInfo( ref info ) || info.hCursor == IntPtr.Zero ) {
			return CursorKind.Hidden;
		}

		foreach( var kind in Enum.GetValues<CursorKind>() ) {
			if( kind > 0 && LoadCursor( IntPtr.Zero, (int)kind ) == info.hCursor ) {
				return kind;
			}
		}

		return CursorKind.Other;
	}

	private ImageAreaFrame? _imgFrame;

	/// <summary>
	/// デバッグ用です。<see cref="SaveImage(string, Rectangle?)"/>・<see cref="Capture(Rectangle?)"/>・<see cref="CheckImage(string, TransColor, Rectangle, ImageMatch)"/>・<see cref="CheckImages(string, TransColor, Rectangle, ImageMatch)"/>・<see cref="CheckColor(Color, Rectangle?, int)"/>・<see cref="CheckColors(Color, Rectangle?, int, int)"/> (Window 版も含む)・<see cref="PeekColor(Point)"/> の対象範囲に枠を表示します。
	/// </summary>
	/// <param name="enabled">true で表示します。false で表示をやめ、枠を消します。</param>
	/// <param name="color">枠の色です。省略すると前回の指定のまま (初回は赤) です。</param>
	/// <param name="thickness">枠の太さ (ピクセル) です。省略すると前回の指定のまま (初回は 3) です。1 未満を指定すると例外になります。</param>
	/// <param name="durationMs">枠を表示し続けるミリ秒数です。省略すると前回の指定のまま (初回は 1000) です。0 以下なら次のキャプチャ (SaveImage・Capture・CheckImage・CheckColor・PeekColor など) まで表示し続けます。</param>
	/// <remarks>
	/// 枠は範囲の外側に描かれ、キャプチャの直前に隠してから取り込むため SaveImage / CheckImage の結果に影響しません。
	/// CheckImage / CheckImages / CheckColor / CheckColors では見つかった範囲にも枠を表示します。
	/// PeekColor では対象の 1 ピクセルを囲む枠になります。
	/// </remarks>
	/// <example>
	/// <code>
	/// ShowImageArea(true);
	/// CheckImage(@"C:\img\ok.png", new Rectangle(100, 100, 400, 300));
	/// </code>
	/// </example>
	public void ShowImageArea( bool enabled = true, Color? color = null, int? thickness = null, int? durationMs = null ) {
		if( thickness is < 1 ) {
			throw new ArgumentOutOfRangeException( nameof( thickness ), thickness, "枠の太さは 1 以上を指定してください。" );
		}

		if( !enabled ) {
			ScreenImage.Frame = null;
			_imgFrame?.Hide();
			return;
		}
		Check();
		_imgFrame ??= new ImageAreaFrame();
		if( color is { } c ) _imgFrame.Color = c;
		if( durationMs is { } ms ) _imgFrame.DurationMs = ms;
		if( thickness is { } t ) _imgFrame.Thickness = t;
		ScreenImage.Frame = _imgFrame;
	}

	/// <summary>
	/// 画面を画像ファイルとして保存します。UWSC の SAVEIMG に相当します。
	/// </summary>
	/// <param name="path">保存先のパスです。拡張子で形式が決まります (.png .jpg .jpeg .gif、それ以外は BMP)。</param>
	/// <param name="area">保存する範囲です。省略すると全画面 (マルチモニターはすべて) です。</param>
	/// <exception cref="ArgumentException"><paramref name="area"/> の大きさが 0 です。</exception>
	/// <remarks>
	/// ウィンドウ部分だけを保存するには <see cref="ControlBase.SaveImage(string, Rectangle?)"/> を使います。
	/// 取り込んだ画面を調べたり <see cref="Bitmap"/> として受け取ったりするには <see cref="Capture(Rectangle?)"/> を使います。
	/// </remarks>
	/// <example>
	/// <code>
	/// SaveImage(@"C:\temp\screen.png");
	/// SaveImage(@"C:\temp\part.png", new Rectangle(0, 0, 400, 300));
	/// </code>
	/// </example>
	public void SaveImage( string path, Rectangle? area = null ) {
		Check();
		ScreenImage.Save( path, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen );
	}

	/// <summary>
	/// 画面をキャプチャして <see cref="CsWSC.Capture"/> を返します。同じ瞬間の画面に対して、画像や色を何度でも速く調べられます。
	/// </summary>
	/// <param name="area">キャプチャする範囲です。省略すると全画面 (マルチモニターはすべて) です。</param>
	/// <returns>キャプチャした画面です。座標は画面座標 (<see cref="MouseOrigin"/> 指定時はその基準) で受け取り、同じ座標系で返します。</returns>
	/// <exception cref="ArgumentException"><paramref name="area"/> の大きさが 0 です。</exception>
	/// <example>
	/// <code>
	/// var c = Capture(new Rectangle(0, 0, 800, 600));
	/// if (c.CheckImage(@"C:\img\ok.png") is Point p) Mouse(MouseButton.Left, KeyAction.Click, p);
	/// else if (c.CheckColor(Color.Red, tolerance: 10) is Point q) Mouse(MouseButton.Left, KeyAction.Click, q);
	/// </code>
	/// </example>
	public Capture Capture( Rectangle? area = null ) {
		Check();
		var screen = area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen;
		return CsWSC.Capture.FromScreen( screen, FromScreen( screen.Location ) );
	}

	/// <summary>画像ファイルを読み込んで <see cref="CsWSC.Capture"/> を返します。座標は画像の左上が基準です。</summary>
	/// <param name="imagePath">画像ファイルのパスです (PNG・BMP など)。</param>
	/// <example>
	/// <code>
	/// var c = Capture(@"C:\log\shot.png");
	/// Print(c.CheckImage(@"C:\img\ok.png"));
	/// </code>
	/// </example>
	public Capture Capture( string imagePath ) {
		Check();
		return CsWSC.Capture.FromFile( imagePath );
	}

	/// <summary>
	/// 画面上で指定の色を探し、最初に見つかった位置 (上から順、同じ行は左から順) を返します。
	/// </summary>
	/// <param name="color">探す色です。アルファ値は無視します。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>見つかった位置です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	/// <remarks>呼ぶたびに画面をキャプチャします。同じ画面で何度も調べるときは <see cref="Capture(Rectangle?)"/> を使います。</remarks>
	/// <example>
	/// <code>
	/// if (CheckColor(Color.Red, new Rectangle(0, 0, 400, 300), 10) is Point p) Mouse(MouseButton.Left, KeyAction.Click, p);
	/// </code>
	/// </example>
	public Point? CheckColor( Color color, Rectangle? area = null, int tolerance = 0 ) =>
		CheckColors( color, area, tolerance, 1 ) is [var p, ..] ? p : null;

	/// <summary>
	/// 画面上の指定位置の色が指定の色と一致するかを調べます。
	/// </summary>
	/// <param name="color">比べる色です。アルファ値は無視します。</param>
	/// <param name="point">調べる位置です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>一致すれば true です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	/// <example>
	/// <code>
	/// if (CheckColor(Color.Red, new Point(100, 200), 10)) Print("赤い");
	/// </code>
	/// </example>
	public bool CheckColor( Color color, Point point, int tolerance = 0 ) =>
		CheckColors( color, new Rectangle( point, new Size( 1, 1 ) ), tolerance, 1 ).Length > 0;

	/// <summary>
	/// 画面上で指定の色のピクセルを全て探し、位置を上から順 (同じ行は左から順) に返します。
	/// </summary>
	/// <param name="color">探す色です。アルファ値は無視します。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった位置 の配列です。無ければ空の配列です。</returns>
	public Point[] CheckColors( Color color, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) {
		Check();
		var found = ScreenImage.FindColor( color, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen, tolerance, max );
		return Array.ConvertAll( found, FromScreen );
	}

	/// <summary>
	/// 画面上でいずれかの色を探し、最初に見つかったピクセル (上から順、同じ行は左から順) の色と位置を返します。
	/// </summary>
	/// <param name="colors">探す色です。アルファ値は無視します。1 つのピクセルが複数の色に一致するときは先に並ぶ色を返します。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>一致した色 (<paramref name="colors"/> の要素) と位置 です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	/// <example>
	/// <code>
	/// if (CheckColor([Color.Red, Color.Lime], new Rectangle(0, 0, 400, 300), 10) is { } f) Print($"{f.Color} が {f.Point} にある");
	/// </code>
	/// </example>
	public (Color Color, Point Point)? CheckColor( Color[] colors, Rectangle? area = null, int tolerance = 0 ) =>
		CheckColors( colors, area, tolerance, 1 ) is [var f, ..] ? f : null;

	/// <summary>
	/// 画面上でいずれかの色のピクセルを全て探し、色と位置を上から順 (同じ行は左から順) に返します。
	/// </summary>
	/// <param name="colors">探す色です。アルファ値は無視します。1 つのピクセルが複数の色に一致するときは先に並ぶ色を返します。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>一致した色 (<paramref name="colors"/> の要素) と位置 の配列です。無ければ空の配列です。</returns>
	public (Color Color, Point Point)[] CheckColors( Color[] colors, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) {
		Check();
		var found = ScreenImage.FindColors( colors, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen, tolerance, max );
		return Array.ConvertAll( found, f => (f.Color, FromScreen( f.Point )) );
	}

	/// <inheritdoc cref="CheckImage(string, TransColor, Rectangle?, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point? CheckImage( string imagePath, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImage( imagePath, TransColor.None, area, tolerance );

	/// <summary>
	/// 画面上で画像を色で探し、見つかった位置を返します。UWSC の CHKIMG に相当します。
	/// </summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	/// <returns>見つかった画像の左上の座標です。見つからなければ null です。</returns>
	/// <remarks>
	/// <c>CheckImage(path, transColor, area, ImageMatch.Color(tolerance))</c> と同じです。色を無視するなど別の照合方法は <see cref="ImageMatch"/> を渡す版を使います。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// 画像の 1 ピクセル (画面上で少ない色を選ぶ) で候補を絞ってから照合しますが、範囲が広いと時間がかかります。<paramref name="area"/> で範囲を絞ると速くなります。
	/// </remarks>
	/// <example>
	/// <code>
	/// if (CheckImage(@"C:\img\ok.png", tolerance: 10) is Point p) Mouse(MouseButton.Left, KeyAction.Click, p);
	/// </code>
	/// </example>
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

	/// <summary>
	/// 画面上で画像を指定した照合方法で探し、見つかった位置を返します。UWSC の CHKIMG に相当します。
	/// </summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/> (色)・<see cref="ImageMatch.Shape"/> (色無視。UWSC の透過色 -1)・<see cref="ImageMatch.Gray"/> (明るさの相関)・<see cref="ImageMatch.Edge"/> (輪郭の相関)。</param>
	/// <returns>見つかった画像の左上の座標です。見つからなければ null です。</returns>
	/// <remarks>
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	/// <example>
	/// <code>
	/// if (CheckImage(@"C:\img\icon.png", ImageMatch.Shape()) is Point p) Mouse(MouseButton.Left, KeyAction.Click, p);
	/// </code>
	/// </example>
	public Point? CheckImage( string imagePath, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImageCore( imagePath, transColor, area, match );

	private Point? CheckImageCore( string imagePath, TransColor transColor, Rectangle? area, ImageMatch match ) {
		Check();
		var found = ScreenImage.Find( imagePath, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen, match, transColor );
		return found is { } p ? FromScreen( p ) : null;
	}

	/// <inheritdoc cref="CheckImages(string, TransColor, Rectangle?, int, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point[] CheckImages( string imagePath, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImages( imagePath, TransColor.None, area, tolerance, max );

	/// <summary>
	/// 画面上で画像を色で全て探し、見つかった位置を返します。UWSC の CHKIMG (番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るもの) に相当します。
	/// </summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった画像の左上の座標の配列です。上から順、同じ行は左から順です。見つからなければ空の配列です。</returns>
	/// <remarks>
	/// 既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// </remarks>
	/// <example>
	/// <code>
	/// foreach (var p in CheckImages(@"C:\img\check.png", tolerance: 10)) Mouse(MouseButton.Left, KeyAction.Click, p);
	/// </code>
	/// </example>
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

	/// <summary>
	/// 画面上で画像を指定した照合方法で全て探し、見つかった位置を返します。UWSC の CHKIMG (番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るもの) に相当します。
	/// </summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="match">照合方法です。<see cref="CheckImage(string, TransColor, Rectangle, ImageMatch)"/> と同じです。</param>
	/// <returns>見つかった画像の左上の座標の配列です。上から順、同じ行は左から順です。見つからなければ空の配列です。</returns>
	/// <remarks>
	/// 既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	public Point[] CheckImages( string imagePath, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( imagePath, transColor, area, match );

	private Point[] CheckImagesCore( string imagePath, TransColor transColor, Rectangle? area, ImageMatch match, int max = int.MaxValue ) {
		Check();
		var found = ScreenImage.FindAll( imagePath, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen, match, transColor, max );
		return Array.ConvertAll( found, FromScreen );
	}

	/// <inheritdoc cref="CheckImage(Capture, TransColor, Rectangle?, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point? CheckImage( Capture capture, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImage( capture, TransColor.None, area, tolerance );

	/// <summary>
	/// 画面上で画像を色で探し、見つかった位置を返します。UWSC の CHKIMG に相当します。
	/// </summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	/// <returns>見つかった画像の左上の座標です。見つからなければ null です。</returns>
	/// <remarks>
	/// <c>CheckImage(capture, transColor, area, ImageMatch.Color(tolerance))</c> と同じです。色を無視するなど別の照合方法は <see cref="ImageMatch"/> を渡す版を使います。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// 画像の 1 ピクセル (画面上で少ない色を選ぶ) で候補を絞ってから照合しますが、範囲が広いと時間がかかります。<paramref name="area"/> で範囲を絞ると速くなります。
	/// </remarks>
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

	/// <summary>
	/// 画面上で画像を指定した照合方法で探し、見つかった位置を返します。UWSC の CHKIMG に相当します。
	/// </summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/> (色)・<see cref="ImageMatch.Shape"/> (色無視。UWSC の透過色 -1)・<see cref="ImageMatch.Gray"/> (明るさの相関)・<see cref="ImageMatch.Edge"/> (輪郭の相関)。</param>
	/// <returns>見つかった画像の左上の座標です。見つからなければ null です。</returns>
	/// <remarks>
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	public Point? CheckImage( Capture capture, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImageCore( capture, transColor, area, match );

	private Point? CheckImageCore( Capture capture, TransColor transColor, Rectangle? area, ImageMatch match ) {
		Check();
		var found = ScreenImage.Find( capture, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen, match, transColor );
		return found is { } p ? FromScreen( p ) : null;
	}

	/// <inheritdoc cref="CheckImages(Capture, TransColor, Rectangle?, int, int)"/>
	/// <remarks>透過色を使わない版です。</remarks>
	public Point[] CheckImages( Capture capture, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImages( capture, TransColor.None, area, tolerance, max );

	/// <summary>
	/// 画面上で画像を色で全て探し、見つかった位置を返します。UWSC の CHKIMG (番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るもの) に相当します。
	/// </summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった画像の左上の座標の配列です。上から順、同じ行は左から順です。見つからなければ空の配列です。</returns>
	/// <remarks>
	/// 既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// </remarks>
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

	/// <summary>
	/// 画面上で画像を指定した照合方法で全て探し、見つかった位置を返します。UWSC の CHKIMG (番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るもの) に相当します。
	/// </summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です。省略すると全画面です。</param>
	/// <param name="match">照合方法です。<see cref="CheckImage(Capture, TransColor, Rectangle, ImageMatch)"/> と同じです。</param>
	/// <returns>見つかった画像の左上の座標の配列です。上から順、同じ行は左から順です。見つからなければ空の配列です。</returns>
	/// <remarks>
	/// 既に見つかった位置と重なる位置は返しません。
	/// 画像の透明な部分 (アルファ値 128 未満) は比較しません。
	/// <see cref="ImageMatch.Gray"/> / <see cref="ImageMatch.Edge"/> は遅いので、繰り返し探すときは <paramref name="area"/> で範囲を絞ります。
	/// </remarks>
	public Point[] CheckImages( Capture capture, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( capture, transColor, area, match );

	private Point[] CheckImagesCore( Capture capture, TransColor transColor, Rectangle? area, ImageMatch match, int max = int.MaxValue ) {
		Check();
		var found = ScreenImage.FindAll( capture, area is { } a ? ToScreen( a ) : SystemInformation.VirtualScreen, match, transColor, max );
		return Array.ConvertAll( found, FromScreen );
	}

	/// <summary>
	/// 画面上の指定位置にあるコントロールの文字 (ボタン名・ラベル・入力値など) を取得します。UWSC の POSACC に相当します。
	/// </summary>
	/// <param name="point">座標です。</param>
	/// <returns>コントロールの名前です。名前が無ければ値です。取得できなければ空文字です。</returns>
	/// <remarks>UI Automation で取得します。</remarks>
	public string TextAtPoint( Point point ) {
		Check();
		var p = ToScreen( point );
		return UiAutomation.TextAt( p.X, p.Y );
	}

	/// <inheritdoc cref="TextAtPoint(Point)"/>
	/// <param name="x">X 座標です。</param>
	/// <param name="y">Y 座標です。</param>
	public string TextAtPoint( int x, int y ) => TextAtPoint( new Point( x, y ) );

	// ---- キーボード (アクティブウィンドウへ送信) ----

	/// <summary>
	/// キーを 1 つ押す・離します。UWSC の KBD に相当します。
	/// </summary>
	/// <param name="key">押すキーです (<c>Keys.Enter</c>、<c>Keys.A</c>、<c>Keys.F5</c> など)。</param>
	/// <param name="action">
	/// <see cref="KeyAction.Click"/> (押して離す、既定)・<see cref="KeyAction.Down"/> (押す)・<see cref="KeyAction.Up"/> (離す)。
	/// </param>
	/// <param name="delayMs">実行までの待ち時間です (ミリ秒)。UWSC の KBD と同じく、待ってから操作します。</param>
	/// <remarks>
	/// アクティブなウィンドウに送られます。
	/// 修飾キーとの同時押しは <see cref="ShortcutKey"/>、文字列の入力は <see cref="SendText"/> が簡単です。
	/// </remarks>
	/// <example>
	/// <code>
	/// Keyboard(Keys.Enter);
	/// Keyboard(Keys.ShiftKey, KeyAction.Down); Keyboard(Keys.Tab); Keyboard(Keys.ShiftKey, KeyAction.Up);
	/// </code>
	/// </example>
	public void Keyboard( Keys key, KeyAction action = KeyAction.Click, int delayMs = 0 ) {
		Sleep( delayMs );
		Input.Key( key, action );
	}

	/// <summary>
	/// 複数のキーを同時押しします (ショートカットキー)。
	/// </summary>
	/// <param name="keys">
	/// 押すキーを順に並べます。前から順に押し、逆順に離します。
	/// 修飾キーは <c>Keys.ControlKey</c> (Ctrl)・<c>Keys.ShiftKey</c> (Shift)・<c>Keys.Menu</c> (Alt)・<c>Keys.LWin</c> (Windows) です。
	/// </param>
	/// <remarks>アクティブなウィンドウに送られます。</remarks>
	/// <example>
	/// <code>
	/// ShortcutKey(Keys.ControlKey, Keys.S);   // Ctrl+S
	/// ShortcutKey(Keys.Menu, Keys.F4);        // Alt+F4
	/// </code>
	/// </example>
	public void ShortcutKey( params IEnumerable<Keys> keys ) {
		Check();
		Input.ShortcutKey( keys );
	}

	/// <summary>
	/// 文字列をキー入力します。
	/// </summary>
	/// <param name="text">入力する文字列です。日本語も IME を通さずにそのまま入力されます。改行は Enter になります。</param>
	/// <remarks>
	/// アクティブなウィンドウに送られます。特定のウィンドウに送るには <see cref="Window.SendText"/> (前面に出してから入力) か
	/// <see cref="Window.PostText"/> (前面に出さずに送る) を使います。
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
	/// キーまたはマウスボタンが今押されているかを返します。UWSC の GETKEYSTATE に相当します。
	/// </summary>
	/// <param name="key">調べるキーです。マウスボタンは <see cref="Keys.LButton"/> / <see cref="Keys.RButton"/> / <see cref="Keys.MButton"/> です。</param>
	/// <returns>押されていれば true です。</returns>
	/// <example>
	/// <code>
	/// while (!GetKeyState(Keys.Escape)) { /* 処理 */ Sleep(100); }
	/// </code>
	/// </example>
	public bool GetKeyState( Keys key ) {
		Check();
		return ( NativeMethods.GetAsyncKeyState( Input.ToVirtualKey( key ) ) & 0x8000 ) != 0;
	}

	/// <summary>
	/// トグルキー (CapsLock / NumLock / ScrollLock など) がオンかを返します。UWSC の GETKEYSTATE(TGL_xxx) に相当します。
	/// </summary>
	/// <param name="key">調べるキーです。<see cref="Keys.CapsLock"/>、<see cref="Keys.NumLock"/>、<see cref="Keys.Scroll"/> などです。</param>
	/// <returns>オンなら true です。</returns>
	/// <example>
	/// <code>
	/// if (GetToggleState(Keys.CapsLock)) Keyboard(Keys.CapsLock);
	/// </code>
	/// </example>
	public bool GetToggleState( Keys key ) {
		Check();
		return ( NativeMethods.GetKeyState( (int)( key & Keys.KeyCode ) ) & 1 ) != 0;
	}

	// ---- ウィンドウ ----

	/// <summary>
	/// タイトルでウィンドウを探します。UWSC の GETID に相当します。
	/// </summary>
	/// <param name="title">ウィンドウタイトルの一部です (部分一致・大文字小文字を区別しない)。</param>
	/// <param name="className">クラス名の一部です (部分一致・大文字小文字を区別しない)。省略するとクラス名では絞り込みません。</param>
	/// <param name="timeoutMs">見つからない場合に待つミリ秒数です。既定は 0 です。0 で待たない、負の値で見つかるまで待ち続けます。</param>
	/// <returns>見つかったウィンドウです。見つからなければ null です。</returns>
	/// <remarks>表示されているウィンドウだけが対象です。複数ある場合は手前のものを返します。すべて取得するには <see cref="GetAllWindows"/> を使います。</remarks>
	/// <example>
	/// <code>
	/// var w = GetWindow("メモ帳") ?? throw new Exception("メモ帳がありません");
	/// w.Activate();
	/// </code>
	/// </example>
	public Window? GetWindow( string title, string? className = null, int timeoutMs = 0 ) {
		Check();
		var sw = Stopwatch.StartNew();
		while( true ) {
			var w = Window.Find( title, className );
			if( w != null ) {
				return w;
			}

			if( timeoutMs >= 0 && sw.ElapsedMilliseconds >= timeoutMs ) {
				return null;
			}

			Sleep( 100 );
		}
	}

	/// <summary>
	/// 現在アクティブなウィンドウです。UWSC の GETID(GET_ACTIVE_WIN) に相当します。
	/// </summary>
	/// <example>
	/// <code>
	/// Print(ActiveWindow().Title);
	/// </code>
	/// </example>
	public Window ActiveWindow() {
		Check();
		return new( GetForegroundWindow() );
	}

	/// <summary>
	/// 条件に合う表示中のウィンドウをすべて取得します。UWSC の GETALLWIN に相当します。
	/// </summary>
	/// <param name="title">ウィンドウタイトルの一部です (部分一致)。省略するとタイトルのあるすべてのウィンドウ (<paramref name="className"/> を指定したときはタイトルが空のウィンドウも含む) です。</param>
	/// <param name="className">クラス名の一部です (部分一致・大文字小文字を区別しない)。省略するとクラス名では絞り込みません。</param>
	/// <returns>見つかったウィンドウの配列です (手前から順)。無ければ空の配列です。</returns>
	/// <example>
	/// <code>
	/// foreach (var w in GetAllWindows()) Print(w.Title);
	/// foreach (var w in GetAllWindows("メモ帳")) w.Close();
	/// </code>
	/// </example>
	public Window[] GetAllWindows( string? title = null, string? className = null ) {
		Check();
		return [.. Window.FindAll( title, className )];
	}

	/// <summary>
	/// ウィンドウハンドルから <see cref="Window"/> を取得します。UWSC の HNDTOID に相当します。逆 (IDTOHND) は <see cref="ControlBase.Handle"/> です。
	/// </summary>
	/// <param name="handle">ウィンドウハンドルです (HWND)。</param>
	/// <returns>対応するウィンドウです。無効なハンドルなら null です。</returns>
	/// <example>
	/// <code>
	/// var w = WindowFromHandle(hwnd);
	/// IntPtr h = ActiveWindow().Handle;
	/// </code>
	/// </example>
	public Window? WindowFromHandle( IntPtr handle ) {
		Check();
		return Window.FromHandle( handle );
	}

	/// <summary>
	/// 画面上の指定位置にあるウィンドウを取得します。UWSC の GETID(GET_FROMPOINT_WIN) に相当します。
	/// </summary>
	/// <param name="point">座標です。</param>
	/// <returns>その位置にあるトップレベルウィンドウです。無ければ null です。</returns>
	public Window? WindowFromPoint( Point point ) {
		Check();
		var p = ToScreen( point );
		return Window.FromPoint( p.X, p.Y );
	}

	/// <inheritdoc cref="WindowFromPoint(Point)"/>
	/// <param name="x">X 座標です。</param>
	/// <param name="y">Y 座標です。</param>
	public Window? WindowFromPoint( int x, int y ) => WindowFromPoint( new Point( x, y ) );

	/// <summary>
	/// 画面上の指定位置にあるコントロール (ボタンなどの子ウィンドウ) を取得します。UWSC の GETID(GET_FROMPOINT_OBJ) を座標指定にしたものです。
	/// </summary>
	/// <param name="point">座標です。</param>
	/// <returns>その位置にあるコントロールです。コントロールの上でなければ null です。</returns>
	public Control? ControlFromPoint( Point point ) {
		Check();
		var p = ToScreen( point );
		return Control.FromPoint( p.X, p.Y );
	}

	/// <inheritdoc cref="ControlFromPoint(Point)"/>
	/// <param name="x">X 座標です。</param>
	/// <param name="y">Y 座標です。</param>
	public Control? ControlFromPoint( int x, int y ) => ControlFromPoint( new Point( x, y ) );

	/// <summary>
	/// マウスカーソルの下にあるウィンドウです (トップレベル)。
	/// </summary>
	/// <remarks>コントロール (ボタンなどの子ウィンドウ) を取得するには <see cref="ControlUnderMouse"/> を使います。</remarks>
	public Window? WindowUnderMouse() {
		Check();
		return Window.FromPoint( Cursor.Position.X, Cursor.Position.Y );
	}

	/// <summary>
	/// マウスカーソルの下にあるコントロールです (ボタンや入力欄などの子ウィンドウ)。UWSC の GETID(GET_FROMPOINT_OBJ) に相当します。
	/// </summary>
	/// <returns>マウス下のコントロールです。コントロールの上でなければ null です。</returns>
	/// <remarks>WPF・ブラウザーなど、コントロールが子ウィンドウになっていないアプリでは null です。</remarks>
	public Control? ControlUnderMouse() {
		Check();
		return Control.FromPoint( Cursor.Position.X, Cursor.Position.Y );
	}

	/// <summary>
	/// ウィンドウが現れるまで待ちます。<see cref="GetWindow"/> と同じで、待ち時間の既定だけ 2 秒です。
	/// </summary>
	/// <param name="title">ウィンドウタイトルの一部です (部分一致)。</param>
	/// <param name="className">クラス名の一部です (部分一致・大文字小文字を区別しない)。省略するとクラス名では絞り込みません。</param>
	/// <param name="timeoutMs">待つミリ秒数です。既定は 2000 (2 秒) です。負の値で見つかるまで待ち続けます。</param>
	/// <returns>見つかったウィンドウです。時間内に見つからなければ null です。</returns>
	/// <example>
	/// <code>
	/// Mouse(MouseButton.Left, KeyAction.Click, 100, 200); // ダイアログを開くボタン
	/// var dlg = WaitWindow("名前を付けて保存") ?? throw new Exception("ダイアログが開きません");
	/// </code>
	/// </example>
	public Window? WaitWindow( string title, string? className = null, int timeoutMs = 2_000 ) => GetWindow( title, className, timeoutMs );

	// ---- プロセス ----

	/// <summary>
	/// プログラムやファイルを起動し、そのメインウィンドウを返します。UWSC の EXEC に相当します。
	/// </summary>
	/// <param name="fileName">実行ファイルのパス、またはファイル・URL です (関連付けられたアプリで開く)。</param>
	/// <param name="arguments">コマンドライン引数です。</param>
	/// <returns>起動したプログラムのメインウィンドウです。取得できなければ null です。</returns>
	/// <remarks>
	/// ウィンドウが入力待ちになるまで最大 3 秒待ちます。
	/// ストアアプリ版のメモ帳などは、起動したプロセスとウィンドウのプロセスが別になるため null になることがあります。
	/// その場合は <see cref="GetWindow"/> と組み合わせます。
	/// </remarks>
	/// <example>
	/// <code>
	/// var notepad = Exec("notepad.exe") ?? GetWindow("メモ帳");
	/// </code>
	/// </example>
	public Window? Exec( string fileName, string arguments = "" ) {
		Check();
		using var p = Process.Start( new ProcessStartInfo( fileName, arguments ) { UseShellExecute = true } );
		if( p is null ) {
			return null;
		}

		try {
			// 停止要求を確認しながら、入力待ちになるまで最大 3 秒待つ
			for( var i = 0; i < 30 && !p.WaitForInputIdle( 100 ); i++ ) {
				Check();
			}

			p.Refresh();
			return p.MainWindowHandle != IntPtr.Zero ? new Window( p.MainWindowHandle ) : null;
		} catch( InvalidOperationException ) {
			return null;
		}
	}

	/// <summary>
	/// プログラムを起動し、終了するまで待ちます。UWSC の EXEC(..., TRUE) に相当します。
	/// </summary>
	/// <param name="fileName">実行ファイルのパス、またはファイルです (関連付けられたアプリで開く)。</param>
	/// <param name="arguments">コマンドライン引数です。</param>
	/// <returns>プログラムの終了コードです。</returns>
	/// <exception cref="InvalidOperationException">プログラムを起動できませんでした。</exception>
	/// <exception cref="System.ComponentModel.Win32Exception">ファイルが見つからないなど、起動に失敗しました。</exception>
	/// <remarks>待っている間に停止が要求されると、待つのをやめてスクリプトを止めます (起動したプログラムは終了させない)。</remarks>
	/// <example>
	/// <code>
	/// var code = ExecWait("cmd.exe", "/c copy a.txt b.txt");
	/// if (code != 0) Print("失敗しました");
	/// </code>
	/// </example>
	public int ExecWait( string fileName, string arguments = "" ) {
		Check();
		using var p = Process.Start( new ProcessStartInfo( fileName, arguments ) { UseShellExecute = true } )
			?? throw new InvalidOperationException( $"{fileName} を起動できませんでした" );
		p.WaitForExitAsync( Cancellation ).GetAwaiter().GetResult();
		return p.ExitCode;
	}

	// ---- 選択ダイアログ・メニュー ----

	/// <summary>
	/// メッセージボックスを出し、押されたボタンを返します。UWSC の MSGBOX に相当します。
	/// </summary>
	/// <param name="message">表示するメッセージです。</param>
	/// <param name="title">タイトルです。省略すると "CsWSC" です。</param>
	/// <param name="buttons">ボタンの組み合わせです。既定は OK のみです。</param>
	/// <param name="icon">アイコンです。<see cref="MsgIcon.Information"/>・<see cref="MsgIcon.Warning"/>・<see cref="MsgIcon.Error"/>・<see cref="MsgIcon.Question"/>。既定はなしです。</param>
	/// <param name="timeoutMs">このミリ秒数で自動的に閉じます (<see cref="MsgResult.Timeout"/> を返す)。0 以下で無制限です。</param>
	/// <returns>
	/// 押されたボタンです。× / Esc で閉じたときは、キャンセルボタンがあれば <see cref="MsgResult.Cancel"/>、OK のみなら <see cref="MsgResult.OK"/> です。
	/// タイムアウト時は <see cref="MsgResult.Timeout"/> です。
	/// </returns>
	/// <remarks>
	/// 常に最前面に表示します。
	/// <see cref="MsgButtons.YesNo"/> / <see cref="MsgButtons.AbortRetryIgnore"/> は × / Esc で閉じられません (Win32 の MessageBox と同じ)。
	/// 表示中に停止が要求されるとダイアログを閉じ、<see cref="OperationCanceledException"/> でスクリプトを止めます。
	/// </remarks>
	/// <example>
	/// <code>
	/// MessageBox("完了しました", icon: MsgIcon.Information);
	/// if (MessageBox("続行しますか？", "確認", MsgButtons.YesNo, MsgIcon.Question) != MsgResult.Yes) return;
	/// MessageBox("10 秒後に閉じます", timeoutMs: 10_000);
	/// </code>
	/// </example>
	public MsgResult MessageBox( string message, string? title = null, MsgButtons buttons = MsgButtons.OK, MsgIcon icon = MsgIcon.None, int timeoutMs = 0 ) {
		Check();
		return RunSta( () => {
			using var f = new MessageBoxForm( message, title, buttons, icon );
			return f.Run( timeoutMs, Cancellation );
		} );
	}

	/// <summary>
	/// 項目ごとのボタンを並べたダイアログを出し、押されたボタンの番号を返します。UWSC の SLCTBOX(SLCT_BTN, ...) に相当します。
	/// </summary>
	/// <param name="message">ダイアログに表示するメッセージです。</param>
	/// <param name="items">選択肢です。</param>
	/// <returns>選ばれた項目の番号です (0 から)。閉じられたときは -1 です。</returns>
	/// <example>
	/// <code>
	/// var i = SelectBox("どれを実行しますか？", "集計", "印刷", "終了");
	/// if (i == 2 || i == -1) return;
	/// </code>
	/// </example>
	public int SelectBox( string message, params IReadOnlyList<string> items ) => SelectBox( SelectKind.Button, message, items );

	/// <summary>
	/// 選択ダイアログを出し、選ばれた項目の番号を返します。UWSC の SLCTBOX に相当します。
	/// </summary>
	/// <param name="kind">表示形式です。<see cref="SelectKind.Button"/>・<see cref="SelectKind.Radio"/>・<see cref="SelectKind.Combo"/>・<see cref="SelectKind.List"/>。</param>
	/// <param name="message">ダイアログに表示するメッセージです。</param>
	/// <param name="items">選択肢です。</param>
	/// <param name="timeoutMs">このミリ秒数で自動的に閉じます (-1 を返す)。0 以下で無制限です。</param>
	/// <returns>選ばれた項目の番号です (0 から)。キャンセル・タイムアウト時は -1 です。</returns>
	/// <remarks>選ばれた文字列が欲しいときは <c>items[i]</c> で取り出します。複数選択は <see cref="SelectBoxMulti"/> を使います。</remarks>
	/// <example>
	/// <code>
	/// string[] files = ["a.csv", "b.csv", "c.csv"];
	/// var i = SelectBox(SelectKind.List, "ファイルを選択", files, timeoutMs: 30_000);
	/// if (i >= 0) Print(files[i]);
	/// </code>
	/// </example>
	public int SelectBox( SelectKind kind, string message, IReadOnlyList<string> items, int timeoutMs = 0 ) {
		Check();
		var r = RunSta( () => {
			using var f = new SelectBoxForm( message, items, kind, false );
			return f.Run( timeoutMs, Cancellation );
		} );
		return r.Length > 0 ? r[0] : -1;
	}

	/// <summary>
	/// 複数選択できる選択ダイアログを出し、選ばれた項目の番号の配列を返します。UWSC の SLCTBOX(SLCT_CHK / SLCT_LST, ...) に相当します。
	/// </summary>
	/// <param name="message">ダイアログに表示するメッセージです。</param>
	/// <param name="items">選択肢です。</param>
	/// <param name="asList">true でリストボックス (Ctrl / Shift で複数選択)、false (既定) でチェックボックスです。</param>
	/// <param name="timeoutMs">このミリ秒数で自動的に閉じます。0 以下で無制限です。</param>
	/// <returns>選ばれた項目の番号 (0 から) の配列です。キャンセル・タイムアウト時は空の配列です。</returns>
	/// <example>
	/// <code>
	/// string[] items = ["売上", "在庫", "顧客"];
	/// foreach (var i in SelectBoxMulti("出力する帳票", items)) Print(items[i]);
	/// </code>
	/// </example>
	public int[] SelectBoxMulti( string message, IReadOnlyList<string> items, bool asList = false, int timeoutMs = 0 ) {
		Check();
		return RunSta( () => {
			using var f = new SelectBoxForm( message, items, null, asList );
			return f.Run( timeoutMs, Cancellation );
		} );
	}

	/// <summary>
	/// ポップアップメニューを表示し、選ばれた項目を返します。UWSC の POPUPMENU に相当します。
	/// </summary>
	/// <param name="items">
	/// メニュー項目です。"-" は区切り線です。先頭に "&lt;&lt;" を付けると直前の項目のサブメニューになります ("&lt;&lt;&lt;&lt;" で 2 段下)。
	/// </param>
	/// <param name="point">表示位置です。省略するとマウスの位置です。<see cref="MouseOrigin"/> の影響を受けます。</param>
	/// <returns>選ばれた項目の文字列です (先頭の "&lt;&lt;" を除く)。キャンセル時は null です。</returns>
	/// <example>
	/// <code>
	/// var s = PopupMenu(["開く", "保存", "-", "エクスポート", "&lt;&lt;CSV", "&lt;&lt;Excel", "-", "終了"]);
	/// if (s == "CSV") { /* ... */ }
	/// </code>
	/// </example>
	public string? PopupMenu( IReadOnlyList<string> items, Point? point = null ) {
		Check();
		var p = point is { } pt ? ToScreen( pt ) : Cursor.Position;
		return RunSta( () => CsWSC.PopupMenu.Show( items, p, Cancellation ) );
	}

	/// <summary>
	/// ポップアップメニューを表示します。<see cref="PopupMenu(IReadOnlyList{string}, Point?)"/> の座標を x, y で指定する版です。
	/// </summary>
	public string? PopupMenu( IReadOnlyList<string> items, int x, int y ) => PopupMenu( items, new Point( x, y ) );

	// ---- 入力ロック ----

	private readonly InputLock _inputLock = new();

	/// <summary>
	/// ユーザーのキーボード・マウス入力をロック/解除します。UWSC の LOCKHARD に相当します。
	/// </summary>
	/// <param name="lockInput">true でロック、false で解除です。</param>
	/// <remarks>
	/// スクリプトからの入力 (<see cref="Mouse(MouseButton, KeyAction, Point?, int)"/>・<see cref="Keyboard"/> など) は通ります。
	/// 非常停止のため Pause キーはロック中も効きます。スクリプトが終了すると自動で解除されます。
	/// 管理者権限で動いているウィンドウへの入力はロックできません。
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
	/// 入力の種類や対象ウィンドウを指定して入力をロックします。UWSC の LOCKHARDEX に相当します。
	/// </summary>
	/// <param name="window">ロックする対象のウィンドウです。null ですべてのウィンドウです。</param>
	/// <param name="mode">
	/// <see cref="LockMode.All"/> (既定)・<see cref="LockMode.Keyboard"/>・<see cref="LockMode.Mouse"/>。
	/// <see cref="LockMode.None"/> で解除です。
	/// </param>
	/// <remarks>
	/// ウィンドウを指定すると、キーボードはそのウィンドウがアクティブなとき、マウスはカーソルがそのウィンドウ上にあるときだけ捨てます。
	/// 呼ぶたびに設定を置き換えます (重ねがけはしない)。スクリプトからの入力と Pause キーは通ります。
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
		// 解除は後片付け (finally) にも使うので確認しない
		if( mode != LockMode.None ) {
			Check();
		}
		_inputLock.Set( mode, window?.Handle ?? IntPtr.Zero );
	}

	// ---- COM ----

	[System.Runtime.InteropServices.DllImport( "ole32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false )]
	private static extern void CLSIDFromProgID( string progId, out Guid clsid );

	[System.Runtime.InteropServices.DllImport( "oleaut32.dll", PreserveSig = false )]
	private static extern void GetActiveObject( ref Guid clsid, IntPtr reserved, [System.Runtime.InteropServices.MarshalAs( System.Runtime.InteropServices.UnmanagedType.IUnknown )] out object obj );

	/// <summary>
	/// COM オブジェクトを作成します。UWSC の CREATEOLEOBJ に相当します。
	/// </summary>
	/// <param name="progId">ProgID です ("Excel.Application"・"Scripting.FileSystemObject" など)。</param>
	/// <returns>作成したオブジェクトです (dynamic なのでメソッド・プロパティをそのまま呼べる)。</returns>
	/// <exception cref="ArgumentException"><paramref name="progId"/> が見つかりません。</exception>
	/// <exception cref="InvalidOperationException">オブジェクトを作成できませんでした。</exception>
	/// <example>
	/// <code>
	/// var excel = CreateOleObject("Excel.Application");
	/// excel.Visible = true;
	/// var book = excel.Workbooks.Add();
	/// </code>
	/// </example>
	public dynamic CreateOleObject( string progId ) {
		Check();
		var type = Type.GetTypeFromProgID( progId ) ?? throw new ArgumentException( $"ProgID \"{progId}\" が見つかりません" );
		return Activator.CreateInstance( type ) ?? throw new InvalidOperationException( $"{progId} を作成できませんでした" );
	}

	/// <summary>
	/// 起動中のアプリケーションの COM オブジェクトを取得します。UWSC の GETACTIVEOLEOBJ に相当します。
	/// </summary>
	/// <param name="progId">ProgID です ("Excel.Application"・"Word.Application" など)。</param>
	/// <returns>起動中のオブジェクトです。起動していなければ null です。</returns>
	/// <remarks>
	/// .NET 5 以降で削除された <c>Marshal.GetActiveObject</c> の代わりです。
	/// 同じアプリが複数起動しているときは、先に起動したものが返ることが多いです。
	/// </remarks>
	/// <example>
	/// <code>
	/// var excel = GetActiveOleObject("Excel.Application") ?? throw new Exception("Excel が起動していません");
	/// Print(excel.ActiveSheet.Range("A1").Value);
	/// </code>
	/// </example>
	public dynamic? GetActiveOleObject( string progId ) {
		Check();
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
	private readonly ThreadLocal<object?> _voice = new( trackAllValues: true ); // 終了時にまとめて解放するため

	/// <summary>
	/// 文字列を音声で読み上げます。UWSC の SPEAK に相当します。
	/// </summary>
	/// <param name="text">読み上げる文字列です。</param>
	/// <param name="async">true で読み上げの終了を待たずに戻ります。既定は終わるまで待ちます。</param>
	/// <param name="interrupt">true で読み上げ中の音声を止めてから読み上げます。</param>
	/// <param name="rate">速さです (-10〜10、0 が標準)。範囲外は丸めます。</param>
	/// <param name="volume">音量です (0〜100)。範囲外は丸めます。</param>
	/// <remarks>Windows の音声合成 (SAPI) を使います。声は Windows の設定 (既定の音声) になります。</remarks>
	/// <example>
	/// <code>
	/// Speak("処理が完了しました");
	/// Speak("", interrupt: true); // 読み上げを止める
	/// </code>
	/// </example>
	public void Speak( string text, bool async = false, bool interrupt = false, int rate = 0, int volume = 100 ) {
		Check();
		const int SVSFlagsAsync = 1, SVSFPurgeBeforeSpeak = 2;
		dynamic voice = _voice.Value ??= (object)CreateOleObject( "SAPI.SpVoice" );
		voice.Rate = Math.Clamp( rate, -10, 10 );
		voice.Volume = Math.Clamp( volume, 0, 100 );
		voice.Speak( text, SVSFlagsAsync | ( interrupt ? SVSFPurgeBeforeSpeak : 0 ) );
		if( async ) {
			return;
		}
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
	/// コマンドプロンプト (cmd.exe) のコマンドを実行し、出力を返します。UWSC の DOSCMD に相当します。
	/// </summary>
	/// <param name="command">実行するコマンドです。パイプ・リダイレクト・<c>&amp;&amp;</c> なども使えます。</param>
	/// <param name="async">true で終了を待たずに戻ります (戻り値は空文字列)。</param>
	/// <param name="showWindow">true でコンソール画面を表示します (出力は取得できず、戻り値は空文字列)。</param>
	/// <returns>標準出力と標準エラー出力をつなげた文字列です。</returns>
	/// <remarks>出力はコンソールのコードページ (日本語環境では Shift-JIS) で読むので文字化けしません。待っている間に停止が要求されると、コマンドを強制終了します。</remarks>
	/// <example>
	/// <code>
	/// Print(Cmd("dir /b C:\\"));
	/// var ip = Cmd("ipconfig | findstr IPv4");
	/// </code>
	/// </example>
	public string Cmd( string command, bool async = false, bool showWindow = false ) {
		var oem = System.Text.Encoding.GetEncoding( System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage );
		// /s: 先頭と末尾の引用符だけを外し、command はそのまま解釈させる
		var psi = new ProcessStartInfo( "cmd.exe" ) { Arguments = $"/d /s /c \"{command}\"" };
		return RunConsole( psi, oem, async, showWindow );
	}

	/// <summary>
	/// PowerShell のコマンドを実行し、出力を返します。UWSC の POWERSHELL に相当します。
	/// </summary>
	/// <param name="command">実行するコマンドです (複数行のスクリプトも可)。</param>
	/// <param name="async">true で終了を待たずに戻ります (戻り値は空文字列)。</param>
	/// <param name="showWindow">true で PowerShell の画面を表示します (出力は取得できず、戻り値は空文字列)。コマンドが終わると画面は閉じます。</param>
	/// <param name="useCore">true で PowerShell 7 (pwsh.exe) を使います。既定は Windows PowerShell (powershell.exe) です。</param>
	/// <returns>標準出力と標準エラー出力をつなげた文字列です。</returns>
	/// <remarks>実行ポリシーは無視 (Bypass)、プロファイルは読み込みません。待っている間に停止が要求されると、コマンドを強制終了します。</remarks>
	/// <example>
	/// <code>
	/// Print(PowerShell("Get-Process | Sort-Object CPU -Descending | Select-Object -First 5"));
	/// </code>
	/// </example>
	public string PowerShell( string command, bool async = false, bool showWindow = false, bool useCore = false ) {
		// 出力を UTF-8 にしてから実行する。引用符の問題を避けるため Base64 (UTF-16LE) で渡す
		var script = showWindow ? command : "[Console]::OutputEncoding = [Text.Encoding]::UTF8; $OutputEncoding = [Text.Encoding]::UTF8; $ProgressPreference = 'SilentlyContinue'\n" + command;
		var encoded = Convert.ToBase64String( System.Text.Encoding.Unicode.GetBytes( script ) );
		var psi = new ProcessStartInfo( useCore ? "pwsh.exe" : "powershell.exe" ) {
			Arguments = showWindow
				? $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}"
				: $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand {encoded}",
		};
		return RunConsole( psi, new System.Text.UTF8Encoding( false ), async, showWindow );
	}

	private string RunConsole( ProcessStartInfo psi, System.Text.Encoding encoding, bool async, bool showWindow ) {
		Check();
		psi.UseShellExecute = false;
		psi.CreateNoWindow = !showWindow;
		if( !showWindow ) {
			psi.RedirectStandardOutput = psi.RedirectStandardError = true;
			psi.RedirectStandardInput = true; // 入力待ちで止まらないよう、すぐ閉じる
			psi.StandardOutputEncoding = psi.StandardErrorEncoding = encoding;
		}
		using var p = Process.Start( psi ) ?? throw new InvalidOperationException( $"{psi.FileName} を起動できませんでした" );
		if( !showWindow ) {
			p.StandardInput.Close();
		}

		if( async ) {
			return "";
		}

		if( showWindow ) {
			WaitOrKill( p );
			return "";
		}
		// 両方を同時に読まないと、バッファが埋まってデッドロックする
		var stdout = new System.Text.StringBuilder();
		var stderr = new System.Text.StringBuilder();
		var reading = Task.WhenAll( ReadAllAsync( p.StandardOutput, stdout ), ReadAllAsync( p.StandardError, stderr ) );
		WaitOrKill( p );
		// 起動した子プロセス (start notepad など) がパイプを持ったまま残ると EOF が来ないので、少しだけ待って打ち切る
		reading.Wait( TimeSpan.FromSeconds( 1 ) );
		lock( stdout ) lock( stderr ) {
			return stdout.ToString() + stderr;
		}
	}

	private static async Task ReadAllAsync( System.IO.StreamReader reader, System.Text.StringBuilder sb ) {
		var buffer = new char[4096];
		int n;
		while( ( n = await reader.ReadAsync( buffer ) ) > 0 ) {
			lock( sb ) {
				sb.Append( buffer, 0, n );
			}
		}
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
	/// クリップボードの文字列を取得します。UWSC の GETSTR(0) に相当します。
	/// </summary>
	/// <returns>クリップボードの文字列です。文字列が入っていなければ空文字です。</returns>
	/// <example>
	/// <code>
	/// ShortcutKey(Keys.ControlKey, Keys.C);
	/// Sleep(200);
	/// Print(GetClipboard());
	/// </code>
	/// </example>
	public static string GetClipboard() {
		ScriptStop.Check();
		return RunSta( () => Clipboard.ContainsText() ? Clipboard.GetText() : "" );
	}

	/// <summary>
	/// クリップボードに文字列を設定します。UWSC の SENDSTR(0, ...) に相当します。
	/// </summary>
	/// <param name="text">設定する文字列です。空文字は指定できません。</param>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> が null または空文字です。</exception>
	/// <example>
	/// <code>
	/// SetClipboard("貼り付ける文字");
	/// ShortcutKey(Keys.ControlKey, Keys.V);
	/// </code>
	/// </example>
	public static void SetClipboard( string text ) {
		ScriptStop.Check();
		RunSta( () => {
			Clipboard.SetText( text );
			return 0;
		} );
	}

	private void Check() => Cancellation.ThrowIfCancellationRequested();

	private static T RunSta<T>( Func<T> fn ) {
		if( System.Threading.Thread.CurrentThread.GetApartmentState() == ApartmentState.STA ) {
			return fn();
		}

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
		if( error != null ) {
			throw error;
		}

		return result;
	}
}
