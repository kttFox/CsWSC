using System.Runtime.InteropServices;

namespace CsWSC;

/// <summary>
/// キャプチャ・検索 (SaveImage / Capture / CheckImage / CheckColor / PeekColor など) の対象範囲を示すデバッグ用の枠です。
/// 枠窓は WDA_EXCLUDEFROMCAPTURE でキャプチャから除外します (Windows 10 2004 以降)。
/// ただし除外された窓は下に別ウィンドウがあると黒く写るため、表示中の枠はキャプチャ前に必ず隠します。
/// 枠はキャプチャごとに作り、それぞれ指定時間のあいだ残します (複数の枠が同時に出ます)。
/// </summary>
internal sealed class ImageAreaFrame : IDisposable {
	private System.Windows.Forms.Control? _invoker;
	// 枠の UI スレッドだけで触る
	private readonly List<FrameForm> _forms = [];
	private readonly object _lock = new();

	/// <summary>枠の色です。</summary>
	public Color Color { get; set; } = Color.Red;

	/// <summary>枠を表示し続けるミリ秒数です。0 以下なら次の表示まで消しません。</summary>
	public int DurationMs { get; set; } = 1000;

	/// <summary>枠の太さです (ピクセル)。</summary>
	public int Thickness { get; set; } = 3;

	/// <summary>キャプチャの前に呼びます。表示中の枠を隠し、画面への反映を待ちます。</summary>
	public void BeforeCapture() {
		var hidden = false;
		Run( () => {
			foreach( var f in _forms ) {
				hidden |= f.HideFrame();
			}
		} );
		if( hidden ) {
			DwmFlush();
		}
	}

	/// <summary>
	/// キャプチャした範囲 (画面座標) に枠を表示します。hits を渡すと見つかった範囲にも枠を表示します。
	/// キャプチャ前に隠した枠のうち、表示時間が残っているものは表示し直します。
	/// </summary>
	public void Show( Rectangle area, IReadOnlyList<Rectangle>? hits = null ) {
		EnsureThread();
		var (color, duration, thickness) = (Color, DurationMs, Thickness);
		Run( () => {
			// 時間指定のない枠は次の表示まで。時間指定のある枠は残り時間のあいだ戻す
			foreach( var f in _forms.Where( f => f.DurationMs <= 0 ).ToList() ) {
				Remove( f );
			}

			foreach( var f in _forms ) {
				f.Restore();
			}

			var form = new FrameForm( duration );
			SetWindowDisplayAffinity( form.Handle, WDA_EXCLUDEFROMCAPTURE );
			form.Expired += () => Remove( form );
			_forms.Add( form );
			form.ShowFrame( area, hits ?? [], color, thickness );
		} );
	}

	public void Hide() => Run( RemoveAll );

	public void Dispose() {
		lock( _lock ) {
			if( _invoker is { IsDisposed: false } inv ) {
				inv.BeginInvoke( () => { RemoveAll(); Application.ExitThread(); } );
			}

			_invoker = null;
		}
	}

	private void Remove( FrameForm form ) {
		_forms.Remove( form );
		form.Close();
		form.Dispose();
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

	private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
	[DllImport( "user32.dll" )] private static extern bool SetWindowDisplayAffinity( IntPtr hWnd, uint affinity );
	[DllImport( "dwmapi.dll" )] private static extern int DwmFlush();

	private sealed class FrameForm : Form {
		private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
		private readonly System.Windows.Forms.Timer _timer = new();

		public FrameForm( int durationMs ) {
			DurationMs = durationMs;
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			TopMost = true;
			_timer.Tick += ( _, _ ) => { _timer.Stop(); Expired?.Invoke(); };
		}

		/// <summary>表示し続けるミリ秒数です。0 以下なら時間では消えません。</summary>
		public int DurationMs { get; }

		/// <summary>表示時間が過ぎたときに発生します。</summary>
		public event Action? Expired;

		protected override bool ShowWithoutActivation => true;

		protected override CreateParams CreateParams {
			get {
				var cp = base.CreateParams;
				cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
				return cp;
			}
		}

		public void ShowFrame( Rectangle area, IReadOnlyList<Rectangle> hits, Color color, int thickness ) {
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

			Region = region;
			BackColor = color;
			Visible = true;
			Update();
			if( DurationMs > 0 ) {
				_timer.Interval = DurationMs;
				_timer.Start();
			}
		}

		/// <summary>キャプチャのために隠した枠を表示し直します。表示時間のタイマーは隠している間も進みます。</summary>
		public void Restore() {
			Visible = true;
			Update();
		}

		/// <summary>枠を隠します。表示中だった場合は true です。</summary>
		public bool HideFrame() {
			var wasVisible = Visible;
			Visible = false;
			return wasVisible;
		}

		protected override void Dispose( bool disposing ) {
			if( disposing ) {
				_timer.Dispose();
			}

			base.Dispose( disposing );
		}
	}
}
