using System.Drawing.Drawing2D;

namespace CsWSC;

internal sealed partial class BalloonForm : Form {
	// DPI 100% のときの大きさ。表示時に DPI に合わせて拡大する
	private const int TextMargin = 8, TailSize = 12, Radius = 8;
	private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

	private string _message = "";
	private Point _textAt;
	private GraphicsPath? _outer, _ring;
	private Color _back, _border;

	/// <summary>透過色です。吹き出しの外側をこの色で塗って抜きます。</summary>
	private static readonly Color Key = Color.FromArgb( 1, 2, 3 );

	public BalloonForm() {
		InitializeComponent();
		_back = BackColor;
		BackColor = TransparencyKey = Key;
		DoubleBuffered = true;
	}

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams {
		get {
			var cp = base.CreateParams;
			cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
			return cp;
		}
	}

	public void Display( string message, Point target, BalloonTail tail, BalloonAnchor anchor, float fontSize, Color back, Color fore,
		Color border, Thickness borderThickness, double opacity, string? fontName ) {
		_message = message;
		// Font の setter は古いフォントを解放しないので、差し替えた後に自分で解放する
		// (Update をループで呼ぶと GDI ハンドルが溜まる)
		var oldFont = Font;
		Font = string.IsNullOrWhiteSpace( fontName )
			? new Font( SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif, fontSize )
			: new Font( fontName, fontSize );
		if( !ReferenceEquals( oldFont, Font ) ) {
			oldFont.Dispose();
		}

		_back = back;
		_border = border;
		ForeColor = fore;
		Opacity = opacity;

		// 余白・しっぽ・枠線は DPI 100% のときの大きさなので、文字 (ポイント指定) と同じ比率で拡大する
		var bs = Scale( borderThickness );
		int margin = LogicalToDeviceUnits( TextMargin ), tailSize = LogicalToDeviceUnits( TailSize ), radius = LogicalToDeviceUnits( Radius );
		var hasTail = tail != BalloonTail.None;
		var top = tail is BalloonTail.TopLeft or BalloonTail.TopRight;
		var left = tail is BalloonTail.TopLeft or BalloonTail.BottomLeft;

		// 太い枠線が文字にかからないよう、1px より太くした分だけ余白を広げる
		int Pad( int b ) => margin + Math.Max( 0, b - 1 );
		var text = TextRenderer.MeasureText( message, Font, Size.Empty, TextFormatFlags.NoPrefix );
		var body = new Rectangle( 0, 0, text.Width + Pad( bs.Left ) + Pad( bs.Right ), text.Height + Pad( bs.Top ) + Pad( bs.Bottom ) );
		if( hasTail && top ) {
			body.Offset( 0, tailSize );
		}

		_textAt = new Point( body.Left + Pad( bs.Left ), body.Top + Pad( bs.Top ) );
		var size = new Size( body.Width, body.Height + ( hasTail ? tailSize : 0 ) );

		// しっぽの先 (フォーム内座標)
		var tip = new Point( left ? radius : size.Width - radius, top ? 0 : size.Height );

		var (outer, edges) = BuildOutline( body, hasTail ? tip : null, left, top, bs, tailSize, radius );
		_outer?.Dispose();
		_ring?.Dispose();
		_outer = new GraphicsPath();
		_outer.AddPolygon( outer );
		_ring = null;
		if( !bs.IsZero ) {
			var inner = Inset( outer, edges );
			// 太すぎてしっぽの内側が潰れたら、しっぽは枠線色で塗りつぶす
			if( hasTail && ( top ? inner[2].Y >= body.Top + bs.Top : inner[^3].Y <= body.Bottom - bs.Bottom ) ) {
				var (box, boxEdges) = BuildOutline( body, null, left, top, bs, tailSize, radius );
				inner = Inset( box, boxEdges );
			}

			_ring = new GraphicsPath( FillMode.Alternate );
			_ring.AddPolygon( outer );
			_ring.AddPolygon( inner );
		}

		var location = hasTail ? new Point( target.X - tip.X, target.Y - tip.Y ) : anchor switch {
			BalloonAnchor.TopRight => new Point( target.X - size.Width, target.Y ),
			BalloonAnchor.BottomLeft => new Point( target.X, target.Y - size.Height ),
			BalloonAnchor.BottomRight => new Point( target.X - size.Width, target.Y - size.Height ),
			BalloonAnchor.Center => new Point( target.X - size.Width / 2, target.Y - size.Height / 2 ),
			_ => target,
		};
		Bounds = new Rectangle( KeepOnScreen( location, size ), size );
		Invalidate();
		Visible = true;
	}

	/// <summary>DPI 100% のときの太さを、この窓の DPI での太さにします。0 でなければ 1 ピクセル以上にします。</summary>
	private Thickness Scale( Thickness t ) {
		int S( int v ) => v == 0 ? 0 : Math.Max( 1, LogicalToDeviceUnits( v ) );
		return new Thickness( S( t.Left ), S( t.Top ), S( t.Right ), S( t.Bottom ) );
	}

	private static Point KeepOnScreen( Point p, Size size ) {
		var area = Screen.FromPoint( p ).WorkingArea;
		return new Point(
			Math.Clamp( p.X, area.Left, Math.Max( area.Left, area.Right - size.Width ) ),
			Math.Clamp( p.Y, area.Top, Math.Max( area.Top, area.Bottom - size.Height ) ) );
	}

	/// <summary>
	/// しっぽ込みの外周を 1 本の多角形 (時計回り) にし、各辺の枠線の太さと一緒に返します。
	/// しっぽの辺は、しっぽが付いている側 (上か下) の太さです。
	/// </summary>
	/// <remarks>しっぽ付きのとき、しっぽの先は上なら 3 番目、下なら後ろから 3 番目の点です。</remarks>
	private static (PointF[] Points, int[] Edges) BuildOutline( Rectangle r, Point? tip, bool left, bool top, Thickness bs, int tailSize, int radius ) {
		// 重ねて描くと境目に線が出るので、しっぽと本体を分けずに 1 本にする
		var points = new List<PointF>();
		var edges = new List<int>(); // edges[i] は points[i] から次の点への辺の太さ
		void Add( float x, float y, int edge ) {
			points.Add( new PointF( x, y ) );
			edges.Add( edge );
		}

		var x1 = left ? r.Left + radius : r.Right - radius - tailSize;
		Add( r.Left, r.Top, bs.Top );
		if( tip is { } p && top ) {
			Add( x1, r.Top, bs.Top );
			Add( p.X, p.Y, bs.Top );
			Add( x1 + tailSize, r.Top, bs.Top );
		}

		Add( r.Right, r.Top, bs.Right );
		Add( r.Right, r.Bottom, bs.Bottom );
		if( tip is { } q && !top ) {
			Add( x1 + tailSize, r.Bottom, bs.Bottom );
			Add( q.X, q.Y, bs.Bottom );
			Add( x1, r.Bottom, bs.Bottom );
		}

		Add( r.Left, r.Bottom, bs.Left );
		return (points.ToArray(), edges.ToArray());
	}

	/// <summary>時計回りの多角形の各辺を、それぞれの太さだけ内側にずらした多角形を返します。</summary>
	private static PointF[] Inset( PointF[] p, int[] edges ) {
		var n = p.Length;
		var result = new PointF[n];
		for( var i = 0; i < n; i++ ) {
			var prev = ( i + n - 1 ) % n;
			var (a, u1) = OffsetLine( p[prev], p[i], edges[prev] );
			var (b, u2) = OffsetLine( p[i], p[( i + 1 ) % n], edges[i] );
			// ずらした前後の辺の交点が新しい頂点
			var cross = u1.X * u2.Y - u1.Y * u2.X;
			if( Math.Abs( cross ) < 1e-6f ) {
				result[i] = b;
				continue;
			}

			var s = ( ( b.X - a.X ) * u2.Y - ( b.Y - a.Y ) * u2.X ) / cross;
			result[i] = new PointF( a.X + s * u1.X, a.Y + s * u1.Y );
		}

		return result;
	}

	/// <summary>辺 from→to を内側 (進行方向の右。画面座標で時計回りのとき内側) に d ずらした線の 1 点と向きです。</summary>
	private static (PointF Point, PointF Dir) OffsetLine( PointF from, PointF to, int d ) {
		float dx = to.X - from.X, dy = to.Y - from.Y;
		var len = MathF.Sqrt( dx * dx + dy * dy );
		if( len == 0 ) {
			return (from, new PointF( 1, 0 ));
		}

		dx /= len;
		dy /= len;
		return (new PointF( from.X - dy * d, from.Y + dx * d ), new PointF( dx, dy ));
	}

	protected override void OnPaint( PaintEventArgs e ) {
		if( _outer == null ) {
			return;
		}

		// 画素の中心を (x+0.5, y+0.5) にして、多角形の 0～幅 がちょうど窓の画素に収まるようにする
		e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
		using( var fill = new SolidBrush( _back ) ) {
			e.Graphics.FillPath( fill, _outer );
		}

		// 枠線色が半透明でも背景色と混ざるよう、背景を塗った上に重ねる
		if( _ring != null ) {
			using var border = new SolidBrush( _border );
			e.Graphics.FillPath( border, _ring );
		}

		e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;
		TextRenderer.DrawText( e.Graphics, _message, Font, _textAt, ForeColor, TextFormatFlags.NoPrefix );
	}
}
