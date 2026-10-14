using System.Drawing.Drawing2D;

namespace CsWSC;

internal sealed partial class BalloonForm : Form {
	private const int TextMargin = 8, TailSize = 12, Radius = 8;
	private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

	private string _message = "";
	private BalloonTail _tail;
	private GraphicsPath? _path;
	private Color _back;

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

	public void Display( string message, Point target, BalloonTail tail, float fontSize, Color back, Color fore, double opacity, string? fontName ) {
		_message = message;
		_tail = tail;
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
		ForeColor = fore;
		Opacity = opacity;

		var text = TextRenderer.MeasureText( message, Font, Size.Empty, TextFormatFlags.NoPrefix );
		var body = new Rectangle( 0, 0, text.Width + TextMargin * 2, text.Height + TextMargin * 2 );
		var hasTail = tail != BalloonTail.None;
		var top = tail is BalloonTail.TopLeft or BalloonTail.TopRight;
		var left = tail is BalloonTail.TopLeft or BalloonTail.BottomLeft;
		if( hasTail && top ) {
			body.Offset( 0, TailSize );
		}

		var size = new Size( body.Width, body.Height + ( hasTail ? TailSize : 0 ) );

		// しっぽの先 (フォーム内座標)
		var tipX = left ? Radius : size.Width - Radius;
		var tip = new Point( tipX, top ? 0 : size.Height );

		_path?.Dispose();
		_path = BuildPath( body, hasTail ? tip : null, left, top );

		var location = hasTail ? new Point( target.X - tip.X, target.Y - tip.Y ) : target;
		Bounds = new Rectangle( KeepOnScreen( location, size ), size );
		Invalidate();
		Visible = true;
	}

	private static Point KeepOnScreen( Point p, Size size ) {
		var area = Screen.FromPoint( p ).WorkingArea;
		return new Point(
			Math.Clamp( p.X, area.Left, Math.Max( area.Left, area.Right - size.Width ) ),
			Math.Clamp( p.Y, area.Top, Math.Max( area.Top, area.Bottom - size.Height ) ) );
	}

	private static GraphicsPath BuildPath( Rectangle r, Point? tip, bool left, bool top, int inset = 1 ) {
		// しっぽ込みの外周を 1 本の多角形にする (重ねると境目に線が出る)
		int l = r.Left, t = r.Top, rt = r.Right - inset, b = r.Bottom - inset;
		var points = new List<Point> { new( l, t ) };
		if( tip is { } p ) {
			var x1 = left ? l + Radius : r.Right - Radius - TailSize;
			var tp = new Point( p.X, top ? p.Y : p.Y - inset );
			if( top ) {
				points.AddRange( [new( x1, t ), tp, new( x1 + TailSize, t )] );
			}

			points.AddRange( [new( rt, t ), new( rt, b )] );
			if( !top ) {
				points.AddRange( [new( x1 + TailSize, b ), tp, new( x1, b )] );
			}
		} else {
			points.AddRange( [new( rt, t ), new( rt, b )] );
		}
		points.Add( new( l, b ) );
		var path = new GraphicsPath();
		path.AddPolygon( points.ToArray() );
		return path;
	}

	protected override void OnPaint( PaintEventArgs e ) {
		if( _path == null ) {
			return;
		}

		using var border = new Pen( Color.FromArgb( 160, ForeColor ) );
		using( var fill = new SolidBrush( _back ) ) {
			e.Graphics.FillPath( fill, _path );
		}

		e.Graphics.DrawPath( border, _path );
		var bodyTop = _tail is BalloonTail.TopLeft or BalloonTail.TopRight ? TailSize : 0;
		TextRenderer.DrawText( e.Graphics, _message, Font, new Point( TextMargin, bodyTop + TextMargin ), ForeColor, TextFormatFlags.NoPrefix );
	}
}
