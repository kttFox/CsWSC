using System.Runtime.InteropServices;

namespace CsWSC;

/// <summary>
/// キャプチャ・検索 (SaveImage / Capture / CheckImage / CheckColor / PeekColor など) の対象範囲を示すデバッグ用の枠です。
/// 枠窓は WDA_EXCLUDEFROMCAPTURE でキャプチャから除外します (Windows 10 2004 以降)。
/// ただし除外された窓は下に別ウィンドウがあると黒く写るため、表示中の枠はキャプチャ前に必ず隠します。
/// </summary>
internal sealed class ImageAreaFrame : IDisposable {
	private const int Thickness = 3;
	private System.Windows.Forms.Control? _invoker;
	private FrameForm? _form;
	private readonly object _lock = new();

	/// <summary>枠の色です。</summary>
	public Color Color { get; set; } = Color.Red;

	/// <summary>枠を表示し続ける時間です (ミリ秒)。0 以下なら次の表示まで消しません。</summary>
	public int DurationMs { get; set; } = 1000;

	/// <summary>キャプチャの前に呼びます。表示中の枠を隠し、画面への反映を待ちます。</summary>
	public void BeforeCapture() {
		if( _form is not { } f ) {
			return;
		}

		var hidden = false;
		Run( () => hidden = f.HideFrame() );
		if( hidden ) {
			DwmFlush();
		}
	}

	/// <summary>キャプチャした範囲 (画面座標) に枠を表示します。hits を渡すと見つかった範囲にも枠を表示します。</summary>
	public void Show( Rectangle area, IReadOnlyList<Rectangle>? hits = null ) {
		var form = EnsureForm();
		var (color, duration) = (Color, DurationMs);
		Run( () => form.ShowFrame( area, hits ?? [], color, duration ) );
	}

	public void Hide() {
		if( _form is { } f ) {
			Run( () => f.HideFrame() );
		}
	}

	public void Dispose() {
		lock( _lock ) {
			if( _invoker is { IsDisposed: false } inv ) {
				inv.BeginInvoke( () => { _form?.Close(); Application.ExitThread(); } );
			}

			_invoker = null;
			_form = null;
		}
	}

	private void Run( Action action ) {
		var inv = _invoker;
		if( inv is not { IsDisposed: false } ) {
			return;
		}

		try { inv.Invoke( action ); } catch( ObjectDisposedException ) { } catch( InvalidOperationException ) { }
	}

	private FrameForm EnsureForm() {
		lock( _lock ) {
			if( _form != null ) {
				return _form;
			}

			using var ready = new ManualResetEventSlim();
			var thread = new Thread( () => {
				var invoker = new System.Windows.Forms.Control();
				_ = invoker.Handle;
				var form = new FrameForm();
				_ = form.Handle;
				SetWindowDisplayAffinity( form.Handle, WDA_EXCLUDEFROMCAPTURE );
				(_invoker, _form) = (invoker, form);
				ready.Set();
				Application.Run();
				invoker.Dispose();
			} ) { IsBackground = true, Name = "CsWSC IMGFRAME" };
			thread.SetApartmentState( ApartmentState.STA );
			thread.Start();
			ready.Wait();
			return _form!;
		}
	}

	private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
	[DllImport( "user32.dll" )] private static extern bool SetWindowDisplayAffinity( IntPtr hWnd, uint affinity );
	[DllImport( "dwmapi.dll" )] private static extern int DwmFlush();

	private sealed class FrameForm : Form {
		private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
		private readonly System.Windows.Forms.Timer _timer = new();

		public FrameForm() {
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			TopMost = true;
			_timer.Tick += ( _, _ ) => HideFrame();
		}

		protected override bool ShowWithoutActivation => true;

		protected override CreateParams CreateParams {
			get {
				var cp = base.CreateParams;
				cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
				return cp;
			}
		}

		public void ShowFrame( Rectangle area, IReadOnlyList<Rectangle> hits, Color color, int durationMs ) {
			_timer.Stop();
			// 枠は範囲の外側に描く (範囲内の画素を隠さない)
			var outer = Rectangle.Inflate( area, Thickness, Thickness );
			foreach( var hit in hits ) {
				outer = Rectangle.Union( outer, Rectangle.Inflate( hit, Thickness, Thickness ) );
			}

			Bounds = outer;
			var region = new Region( Rectangle.Empty );
			foreach( var r in hits.Prepend( area ) ) {
				var ring = new Rectangle( r.X - Thickness - outer.X, r.Y - Thickness - outer.Y, r.Width + Thickness * 2, r.Height + Thickness * 2 );
				using var inner = new Region( ring );
				inner.Exclude( Rectangle.Inflate( ring, -Thickness, -Thickness ) );
				region.Union( inner );
			}

			Region = region;
			BackColor = color;
			Visible = true;
			Update();
			if( durationMs > 0 ) {
				_timer.Interval = durationMs;
				_timer.Start();
			}
		}

		/// <summary>枠を隠します。表示中だった場合は true です。</summary>
		public bool HideFrame() {
			_timer.Stop();
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
