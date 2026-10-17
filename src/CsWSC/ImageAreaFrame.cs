using System.Runtime.InteropServices;

namespace CsWSC;

/// <summary>
/// キャプチャ・検索 (SaveImage / Capture / CheckImage / CheckColor / PeekColor など) の対象範囲を示すデバッグ用の枠です。
/// 枠窓はレイヤードウィンドウにして、画面には表示したままキャプチャ (CAPTUREBLT なしの BitBlt・GetPixel) には写らないようにします。
/// キャプチャのたびに枠を隠して出し直すと、対象のウィンドウの操作 (持つ・置くなど) を邪魔するため隠しません。
/// 枠はキャプチャごとに出し、それぞれ指定時間のあいだ残します (複数の枠が同時に出ます)。
/// キャプチャを遅くしないよう、表示は枠のスレッドに頼むだけで待たず (BeginInvoke)、同じ範囲の枠は作り直さずに表示時間だけ延ばし、
/// 消えた枠窓は捨てずに使い回します (枠窓を毎回作って Invoke で待つと、CheckColor 1 回が約 8 ms から約 24 ms になった)。
/// </summary>
internal sealed class ImageAreaFrame : IDisposable {
	private System.Windows.Forms.Control? _invoker;
	// 枠の UI スレッドだけで触る
	private readonly List<FrameForm> _forms = [];
	private readonly Stack<FrameForm> _pool = new();
	private const int PoolMax = 32;
	private readonly object _lock = new();
	// 枠のスレッドがまだ処理していない表示の頼み (_lock で守る)。同じ頼みは 1 つにまとめる
	private readonly List<Request> _pending = [];

	private sealed record Request( Rectangle Area, Rectangle[] Hits, Color Color, int DurationMs, int Thickness ) {
		public bool SameFrame( Request o ) => Area == o.Area && Color == o.Color && Thickness == o.Thickness && Hits.SequenceEqual( o.Hits );
	}

	/// <summary>枠の色です。</summary>
	public Color Color { get; set; } = Color.Red;

	/// <summary>枠を表示し続けるミリ秒数です。0 以下なら次の表示まで消しません。</summary>
	public int DurationMs { get; set; } = 1000;

	/// <summary>枠の太さです (ピクセル)。</summary>
	public int Thickness { get; set; } = 3;

	/// <summary>
	/// キャプチャした範囲 (画面座標) に枠を表示します。hits を渡すと見つかった範囲にも枠を表示します。
	/// 表示を頼むだけで、表示し終わるのは待ちません。
	/// </summary>
	public void Show( Rectangle area, IReadOnlyList<Rectangle>? hits = null ) {
		EnsureThread();
		var request = new Request( area, hits?.ToArray() ?? [], Color, DurationMs, Thickness );
		bool post;
		lock( _lock ) {
			var i = _pending.FindIndex( r => r.SameFrame( request ) );
			if( i >= 0 ) {
				_pending[i] = request;   // 同じ枠がまだ表示待ちなら、新しい設定で 1 つにまとめる
				return;
			}

			_pending.Add( request );
			post = _pending.Count == 1;   // 頼みが空だったときだけ枠のスレッドを起こす
		}

		if( post ) Post( ShowPending );
	}

	public void Hide() {
		lock( _lock ) {
			_pending.Clear();
		}

		Run( RemoveAll );
	}

	public void Dispose() {
		lock( _lock ) {
			_pending.Clear();
			if( _invoker is { IsDisposed: false } inv ) {
				inv.BeginInvoke( () => {
					RemoveAll();
					while( _pool.TryPop( out var f ) ) f.Dispose();
					Application.ExitThread();
				} );
			}

			_invoker = null;
		}
	}

	private void ShowPending() {
		Request[] requests;
		lock( _lock ) {
			requests = [.. _pending];
			_pending.Clear();
		}

		foreach( var request in requests ) {
			// 時間指定のない枠は次の表示まで。時間指定のある枠は残り時間のあいだ戻す
			foreach( var f in _forms.Where( f => f.DurationMs <= 0 ).ToList() ) {
				Remove( f );
			}

			// 同じ範囲の枠が出ていれば、作り直さずに表示時間だけ延ばす
			var same = _forms.FirstOrDefault( f => f.Shown is { } shown && shown.SameFrame( request ) );
			if( same != null ) {
				same.Restart( request.DurationMs );
				continue;
			}

			var form = _pool.TryPop( out var pooled ) ? pooled : NewForm();
			_forms.Add( form );
			form.ShowFrame( request );
		}
	}

	private FrameForm NewForm() {
		var form = new FrameForm();
		SetLayeredWindowAttributes( form.Handle, 0, 255, LWA_ALPHA );
		form.Expired += () => Remove( form );
		return form;
	}

	/// <summary>
	/// 枠を消します。枠窓は捨てずに隠して使い回します。
	/// </summary>
	private void Remove( FrameForm form ) {
		if( !_forms.Remove( form ) ) return;
		form.HideFrame();
		if( _pool.Count < PoolMax ) {
			_pool.Push( form );
		} else {
			form.Close();
			form.Dispose();
		}
	}

	private void RemoveAll() {
		foreach( var f in _forms.ToList() ) {
			Remove( f );
		}
	}

	private void Run( Action action ) {
		var inv = _invoker;
		if( inv is not { IsDisposed: false } ) {
			return;
		}

		try { inv.Invoke( action ); } catch( ObjectDisposedException ) { } catch( InvalidOperationException ) { }
	}

	private void Post( Action action ) {
		var inv = _invoker;
		if( inv is not { IsDisposed: false } ) {
			return;
		}

		try { inv.BeginInvoke( action ); } catch( ObjectDisposedException ) { } catch( InvalidOperationException ) { }
	}

	private void EnsureThread() {
		lock( _lock ) {
			if( _invoker != null ) {
				return;
			}

			using var ready = new ManualResetEventSlim();
			var thread = new Thread( () => {
				var invoker = new System.Windows.Forms.Control();
				_ = invoker.Handle;
				_invoker = invoker;
				ready.Set();
				Application.Run();
				invoker.Dispose();
			} ) { IsBackground = true, Name = "CsWSC IMGFRAME" };
			thread.SetApartmentState( ApartmentState.STA );
			thread.Start();
			ready.Wait();
		}
	}

	private const uint LWA_ALPHA = 0x2;
	[DllImport( "user32.dll" )] private static extern bool SetLayeredWindowAttributes( IntPtr hWnd, uint crKey, byte alpha, uint flags );

	private sealed class FrameForm : Form {
		private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
		private readonly System.Windows.Forms.Timer _timer = new();

		public FrameForm() {
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			TopMost = true;
			_timer.Tick += ( _, _ ) => { _timer.Stop(); Expired?.Invoke(); };
		}

		/// <summary>表示し続けるミリ秒数です。0 以下なら時間では消えません。</summary>
		public int DurationMs => Shown?.DurationMs ?? 0;

		/// <summary>いま表示している枠です。隠しているときは null です。</summary>
		public Request? Shown { get; private set; }

		/// <summary>表示時間が過ぎたときに発生します。</summary>
		public event Action? Expired;

		protected override bool ShowWithoutActivation => true;

		protected override CreateParams CreateParams {
			get {
				var cp = base.CreateParams;
				cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
				return cp;
			}
		}

		public void ShowFrame( Request request ) {
			var (area, hits, color, thickness) = (request.Area, request.Hits, request.Color, request.Thickness);
			// 枠は範囲の外側に描く (範囲内の画素を隠さない)
			var outer = Rectangle.Inflate( area, thickness, thickness );
			foreach( var hit in hits ) {
				outer = Rectangle.Union( outer, Rectangle.Inflate( hit, thickness, thickness ) );
			}

			Bounds = outer;
			var region = new Region( Rectangle.Empty );
			foreach( var r in hits.Prepend( area ) ) {
				var ring = new Rectangle( r.X - thickness - outer.X, r.Y - thickness - outer.Y, r.Width + thickness * 2, r.Height + thickness * 2 );
				using var inner = new Region( ring );
				inner.Exclude( Rectangle.Inflate( ring, -thickness, -thickness ) );
				region.Union( inner );
			}

			var old = Region;
			Region = region;
			old?.Dispose();
			BackColor = color;
			Shown = request;
			Visible = true;
			Update();
			Restart( request.DurationMs );
		}

		/// <summary>
		/// 表示時間を durationMs から数え直します。0 以下なら時間では消えません。
		/// </summary>
		public void Restart( int durationMs ) {
			if( Shown != null ) Shown = Shown with { DurationMs = durationMs };
			_timer.Stop();
			if( durationMs > 0 ) {
				_timer.Interval = durationMs;
				_timer.Start();
			}
		}

		public void HideFrame() {
			_timer.Stop();
			Shown = null;
			Visible = false;
		}

		protected override void Dispose( bool disposing ) {
			if( disposing ) {
				_timer.Dispose();
			}

			base.Dispose( disposing );
		}
	}
}
