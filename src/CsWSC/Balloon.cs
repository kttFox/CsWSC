using System.Drawing.Drawing2D;

namespace CsWSC;

/// <summary>吹き出しの「しっぽ」の位置です。指定座標をしっぽの先が指します。</summary>
public enum BalloonTail { None, TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// 表示中の吹き出しです。<c>Balloon(...)</c> が返します。複数同時に表示できます。
/// </summary>
/// <remarks>
/// <see cref="Close"/> (または using) で消します。スクリプトが終わると自動で消えます。
/// </remarks>
public sealed partial class Balloon : IDisposable {
	private readonly BalloonHost _host;
	private readonly BalloonForm _form;
	private string _message;
	private Point _target; // 画面座標
						   // MouseOrigin の基準と画面座標の変換。呼ぶたびにその時点の基準を使う
	private readonly Func<Point, Point> _toScreen, _fromScreen;
	private readonly BalloonTail _tail;
	private readonly float _fontSize;
	private readonly Color _back, _fore;
	private readonly double _opacity;
	private readonly string? _fontName;

	internal Balloon( BalloonHost host, string message, Point target, BalloonTail tail, float fontSize, Color back, Color fore, double opacity, string? fontName,
		Func<Point, Point> toScreen, Func<Point, Point> fromScreen ) {
		_host = host;
		_toScreen = toScreen;
		_fromScreen = fromScreen;
		_message = message;
		_target = target;
		_tail = tail;
		_fontSize = fontSize;
		_back = back;
		_fore = fore;
		_opacity = opacity;
		_fontName = fontName;
		_form = host.CreateForm();
		Refresh();
	}

	/// <summary>表示中の文字列です。</summary>
	public string Message => _message;

	/// <summary>しっぽの先が指す位置です (画面座標。MouseOrigin 指定時はその基準)。</summary>
	public Point Target => _fromScreen( _target );

	/// <summary><see cref="Close"/> 済み、またはスクリプト終了で消えていれば true です。</summary>
	public bool IsClosed => !_host.IsOpen( _form );

	/// <summary>
	/// 文字列や位置を変えて表示し直します。省略した値はそのままです。
	/// </summary>
	/// <param name="message">新しい文字列です。省略すると変えません。</param>
	/// <param name="point">しっぽの先が指す座標です (画面座標。MouseOrigin 指定時はその基準)。省略すると変えません。</param>
	/// <returns>この吹き出し自身です。</returns>
	public Balloon Update( string? message = null, Point? point = null ) {
		ScriptStop.Check();
		if( message != null ) {
			_message = BalloonHost.Normalize( message );
		}

		if( point is { } p ) {
			_target = _toScreen( p );
		}

		Refresh();
		return this;
	}

	/// <summary><see cref="Update(string?, Point?)"/> の座標を x, y で指定する版です。</summary>
	public Balloon Update( string? message, int x, int y ) => Update( message, new Point( x, y ) );

	/// <summary>吹き出しを消します。消した後の <see cref="Update(string?, Point?)"/> は表示に反映されません。</summary>
	public void Close() => _host.CloseForm( _form );

	/// <summary><see cref="Close"/> と同じです。using で一時的に表示するときに使います。</summary>
	public void Dispose() => Close();

	private void Refresh() {
		var (message, target) = (_message, _target);
		_host.Run( _form, () => _form.Display( message, target, _tail, _fontSize, _back, _fore, _opacity, _fontName ) );
	}
}

/// <summary>
/// 吹き出し窓をまとめて持つ UI スレッドです。スクリプトのスレッドとは別に 1 本だけ作ります。
/// 窓は前面に出ず、クリックも透過します。
/// </summary>
internal sealed class BalloonHost : IDisposable {
	private System.Windows.Forms.Control? _invoker;
	private readonly List<BalloonForm> _forms = [];
	private readonly object _lock = new();

	internal static string Normalize( string message ) => message.ReplaceLineEndings( "\r\n" );

	internal BalloonForm CreateForm() {
		var invoker = EnsureThread();
		var form = (BalloonForm)invoker.Invoke( () => {
			var f = new BalloonForm();
			_ = f.Handle;
			return f;
		} );
		lock( _lock ) {
			_forms.Add( form );
		}

		return form;
	}

	internal bool IsOpen( BalloonForm form ) {
		lock( _lock ) {
			return _forms.Contains( form );
		}
	}

	internal void Run( BalloonForm form, Action action ) {
		if( !IsOpen( form ) ) {
			return;
		}

		try { form.Invoke( action ); } catch( ObjectDisposedException ) { } catch( InvalidOperationException ) when( form.IsDisposed ) { }
	}

	internal void CloseForm( BalloonForm form ) {
		lock( _lock ) {
			_forms.Remove( form );
		}

		if( form is { IsDisposed: false, IsHandleCreated: true } ) {
			form.BeginInvoke( form.Close );
		}
	}

	/// <summary>表示中の吹き出しをすべて消します。</summary>
	public void CloseAll() {
		BalloonForm[] forms;
		lock( _lock ) {
			forms = [.. _forms];
		}

		foreach( var f in forms ) {
			CloseForm( f );
		}
	}

	public void Dispose() {
		CloseAll();
		lock( _lock ) {
			if( _invoker is { IsDisposed: false } inv ) {
				inv.BeginInvoke( Application.ExitThread );
			}

			_invoker = null;
		}
	}

	private System.Windows.Forms.Control EnsureThread() {
		lock( _lock ) {
			if( _invoker is { IsDisposed: false } ) {
				return _invoker;
			}

			using var ready = new ManualResetEventSlim();
			var thread = new System.Threading.Thread( () => {
				var invoker = new System.Windows.Forms.Control();
				_ = invoker.Handle; // Invoke できるようハンドルを作る
				_invoker = invoker;
				ready.Set();
				Application.Run();
				invoker.Dispose();
			} ) { IsBackground = true, Name = "CsWSC BALLOON" };
			thread.SetApartmentState( ApartmentState.STA );
			thread.Start();
			ready.Wait();
			return _invoker!;
		}
	}
}
