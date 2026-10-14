using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CsWSC;

/// <summary>
/// 一度キャプチャした画面 (または画像) のピクセルです。同じ瞬間の画面に対して、画像や色を何度でも速く調べられます。
/// <see cref="ScriptGlobals.Capture(Rectangle?)"/>・<see cref="ControlBase.Capture(Rectangle?)"/>・<see cref="ScriptGlobals.Capture(string)"/> で作ります。
/// </summary>
/// <remarks>
/// 座標は作ったときの座標系 (画面座標・<see cref="ScriptGlobals.MouseOrigin"/> の基準・クライアント領域・画像の左上) で受け取り、同じ座標系で返します。
/// 画面から作った場合、結果は作ったときと同じ座標系のコマンドにそのまま渡せます
/// (<see cref="ScriptGlobals.Capture(Rectangle?)"/> なら <c>Mouse</c>、ウィンドウ・コントロールの Capture ならそのウィンドウ・コントロールの <c>Mouse</c> など)。キャプチャ後に画面が変わっても内容は変わりません。
/// </remarks>
/// <example>
/// <code>
/// var c = Capture(new Rectangle(0, 0, 800, 600));
/// if (c.CheckImage(@"C:\img\ok.png") is Point p) Mouse(MouseButton.Left, KeyAction.Click, p);
/// else if (c.CheckImage(@"C:\img\cancel.png") is Point q) Mouse(MouseButton.Left, KeyAction.Click, q);
/// </code>
/// </example>
public sealed partial class Capture {
	private readonly int[] _pixels;

	internal Capture( int[] pixels, int width, int height, Point origin ) {
		_pixels = pixels;
		Width = width;
		Height = height;
		Origin = origin;
	}

	/// <summary>幅です (ピクセル)。</summary>
	public int Width { get; }

	/// <summary>高さです (ピクセル)。</summary>
	public int Height { get; }

	/// <summary>左上の座標です (作ったときの座標系)。画像から作った場合は (0, 0) です。</summary>
	public Point Origin { get; }

	/// <summary>キャプチャした範囲です (作ったときの座標系)。</summary>
	public Rectangle Bounds => new( Origin, new Size( Width, Height ) );

	/// <summary>画面の範囲をキャプチャします。アルファ値は 255 にそろえます。</summary>
	internal static Capture FromScreen( Rectangle screenArea, Point origin ) {
		if( screenArea.Width <= 0 || screenArea.Height <= 0 ) {
			throw new ArgumentException( $"キャプチャする範囲の大きさが 0 です ({screenArea.Width}x{screenArea.Height})。ウィンドウが最小化されていないか確認してください。", "area" );
		}

		var pixels = ScreenImage.CapturePixels( screenArea );
		for( var i = 0; i < pixels.Length; i++ ) {
			pixels[i] |= unchecked((int)0xFF000000);
		}

		ScreenImage.Frame?.Show( screenArea );
		return new Capture( pixels, screenArea.Width, screenArea.Height, origin );
	}

	/// <summary>Bitmap から作ります。座標は画像の左上が基準です。</summary>
	internal static Capture FromBitmap( Bitmap image ) {
		using var bmp = image.Clone( new Rectangle( Point.Empty, image.Size ), PixelFormat.Format32bppArgb );
		return new Capture( ScreenImage.Pixels( bmp ), bmp.Width, bmp.Height, Point.Empty );
	}

	/// <summary>画像ファイルから作ります。座標は画像の左上が基準です。</summary>
	internal static Capture FromFile( string imagePath ) {
		using var file = new Bitmap( imagePath );
		return FromBitmap( file );
	}

	// 探す画像として使うときの検索用画像。初めて使うときに作り、以後は使い回す
	private NeedleImage? _needle;

	/// <summary>探す画像として使う検索用画像を返します。ピクセルは共有し、比べるピクセルの一覧なども使い回します。</summary>
	internal NeedleImage ToNeedle( TransColor transColor ) =>
		( _needle ??= ScreenImage.CreateNeedle( _pixels, Width, Height ) ).WithTransColor( transColor );

	/// <summary>指定位置の色を取得します (PEEKCOLOR)。</summary>
	/// <param name="point">位置です (作ったときの座標系)。</param>
	/// <returns>その位置の色です。画面から作った場合のアルファ値は 255 です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="point"/> がキャプチャした範囲の外です。</exception>
	public Color PeekColor( Point point ) {
		ScriptStop.Check();
		var x = point.X - Origin.X;
		var y = point.Y - Origin.Y;
		if( (uint)x >= (uint)Width || (uint)y >= (uint)Height ) {
			throw new ArgumentOutOfRangeException( nameof( point ), point, $"キャプチャした範囲 {Bounds} の外です。" );
		}

		return Color.FromArgb( _pixels[y * Width + x] );
	}

	/// <inheritdoc cref="PeekColor(Point)"/>
	/// <param name="x">X 座標です (作ったときの座標系)。</param>
	/// <param name="y">Y 座標です (作ったときの座標系)。</param>
	public Color PeekColor( int x, int y ) => PeekColor( new Point( x, y ) );

	/// <summary>指定の色を探し、最初に見つかった位置 (上から順、同じ行は左から順) を返します。</summary>
	/// <param name="color">探す色です。アルファ値は無視します。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>見つかった位置 (作ったときの座標系) です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public Point? CheckColor( Color color, Rectangle? area = null, int tolerance = 0 ) =>
		FindColor( color, area, tolerance, 1 ) is [var p, ..] ? p : null;

	/// <summary>指定の色のピクセルを全て探し、位置を上から順 (同じ行は左から順) に返します。</summary>
	/// <param name="color">探す色です。アルファ値は無視します。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった位置 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckColors( Color color, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		FindColor( color, area, tolerance, max );

	private Point[] FindColor( Color color, Rectangle? area, int tolerance, int max ) {
		ScriptStop.Check();
		var found = ScreenImage.SearchColor( _pixels, Width, ToLocal( area ), color, tolerance, max );
		return found.Select( p => new Point( p.X + Origin.X, p.Y + Origin.Y ) ).ToArray();
	}

	/// <summary>いずれかの色を探し、最初に見つかったピクセル (上から順、同じ行は左から順) の色と位置を返します。</summary>
	/// <param name="colors">探す色です。アルファ値は無視します。1 つのピクセルが複数の色に一致するときは先に並ぶ色を返します。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>一致した色 (<paramref name="colors"/> の要素) と位置 (作ったときの座標系) です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public (Color Color, Point Point)? CheckColor( Color[] colors, Rectangle? area = null, int tolerance = 0 ) =>
		FindColors( colors, area, tolerance, 1 ) is [var f, ..] ? f : null;

	/// <summary>いずれかの色のピクセルを全て探し、色と位置を上から順 (同じ行は左から順) に返します。</summary>
	/// <param name="colors">探す色です。アルファ値は無視します。1 つのピクセルが複数の色に一致するときは先に並ぶ色を返します。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>一致した色 (<paramref name="colors"/> の要素) と位置 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public (Color Color, Point Point)[] CheckColors( Color[] colors, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		FindColors( colors, area, tolerance, max );

	private (Color Color, Point Point)[] FindColors( Color[] colors, Rectangle? area, int tolerance, int max ) {
		ScriptStop.Check();
		var found = ScreenImage.SearchColors( _pixels, Width, ToLocal( area ), colors, tolerance, max );
		return found.Select( f => (f.Color, new Point( f.Point.X + Origin.X, f.Point.Y + Origin.Y )) ).ToArray();
	}

	/// <summary>画像を色(<see cref="ImageMatch.Color"/>)で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public Point? CheckImage( string imagePath, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImageCore( imagePath, TransColor.None, area, ImageMatch.Color( tolerance ) );

	/// <summary>画像を色(<see cref="ImageMatch.Color"/>)で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public Point? CheckImage( string imagePath, TransColor transColor, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImageCore( imagePath, transColor, area, ImageMatch.Color( tolerance ) );

	/// <inheritdoc cref="CheckImage(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point? CheckImage( string imagePath, ImageMatch match ) =>
		CheckImageCore( imagePath, TransColor.None, null, match );

	/// <summary>画像を指定した照合方法で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	public Point? CheckImage( string imagePath, Rectangle area, ImageMatch match ) =>
		CheckImageCore( imagePath, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImage(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point? CheckImage( string imagePath, TransColor transColor, ImageMatch match ) =>
		CheckImageCore( imagePath, transColor, null, match );

	/// <summary>画像を指定した照合方法で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	public Point? CheckImage( string imagePath, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImageCore( imagePath, transColor, area, match );

	private Point? CheckImageCore( string imagePath, TransColor transColor, Rectangle? area, ImageMatch match ) =>
		Find( ScreenImage.LoadNeedle( imagePath, transColor ), area, match, 1 ) is [var p, ..] ? p : null;

	/// <summary>画像を色で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public Point? CheckImage( Capture capture, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImageCore( capture, TransColor.None, area, ImageMatch.Color( tolerance ) );

	/// <summary>画像を色で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外です。</exception>
	public Point? CheckImage( Capture capture, TransColor transColor, Rectangle? area = null, int tolerance = 0 ) =>
		CheckImageCore( capture, transColor, area, ImageMatch.Color( tolerance ) );

	/// <inheritdoc cref="CheckImage(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point? CheckImage( Capture capture, ImageMatch match ) =>
		CheckImageCore( capture, TransColor.None, null, match );

	/// <summary>画像を指定した照合方法で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	public Point? CheckImage( Capture capture, Rectangle area, ImageMatch match ) =>
		CheckImageCore( capture, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImage(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point? CheckImage( Capture capture, TransColor transColor, ImageMatch match ) =>
		CheckImageCore( capture, transColor, null, match );

	/// <summary>画像を指定した照合方法で探し、見つかった左上の位置を返します (CHKIMG)。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) です。無ければ null です。</returns>
	public Point? CheckImage( Capture capture, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImageCore( capture, transColor, area, match );

	private Point? CheckImageCore( Capture capture, TransColor transColor, Rectangle? area, ImageMatch match ) =>
		Find( capture.ToNeedle( transColor ), area, match, 1 ) is [var p, ..] ? p : null;

	/// <summary>画像を色で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( string imagePath, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImagesCore( imagePath, TransColor.None, area, ImageMatch.Color( tolerance ), max );

	/// <summary>画像を色で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( string imagePath, TransColor transColor, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImagesCore( imagePath, transColor, area, ImageMatch.Color( tolerance ), max );

	/// <inheritdoc cref="CheckImages(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point[] CheckImages( string imagePath, ImageMatch match ) =>
		CheckImagesCore( imagePath, TransColor.None, null, match );

	/// <summary>画像を指定した照合方法で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( string imagePath, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( imagePath, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImages(string, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point[] CheckImages( string imagePath, TransColor transColor, ImageMatch match ) =>
		CheckImagesCore( imagePath, transColor, null, match );

	/// <summary>画像を指定した照合方法で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="imagePath">探す画像ファイルのパスです (PNG・BMP など)。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( string imagePath, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( imagePath, transColor, area, match );

	private Point[] CheckImagesCore( string imagePath, TransColor transColor, Rectangle? area, ImageMatch match, int max = int.MaxValue ) =>
		Find( ScreenImage.LoadNeedle( imagePath, transColor ), area, match, max );

	/// <summary>画像を色で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( Capture capture, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImagesCore( capture, TransColor.None, area, ImageMatch.Color( tolerance ), max );

	/// <summary>画像を色で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します (CHKIMG の番号 -1 で ALL_IMG_X / ALL_IMG_Y に全件を得るのに相当)。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="tolerance">色の許容差です。RGB 各成分 (0〜255) の差がこの値以下なら一致とみなします。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( Capture capture, TransColor transColor, Rectangle? area = null, int tolerance = 0, int max = int.MaxValue ) =>
		CheckImagesCore( capture, transColor, area, ImageMatch.Color( tolerance ), max );

	/// <inheritdoc cref="CheckImages(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>透過色と範囲を省略する版です。</remarks>
	public Point[] CheckImages( Capture capture, ImageMatch match ) =>
		CheckImagesCore( capture, TransColor.None, null, match );

	/// <summary>画像を指定した照合方法で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( Capture capture, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( capture, TransColor.None, area, match );

	/// <inheritdoc cref="CheckImages(Capture, TransColor, Rectangle, ImageMatch)"/>
	/// <remarks>範囲を省略する版です。</remarks>
	public Point[] CheckImages( Capture capture, TransColor transColor, ImageMatch match ) =>
		CheckImagesCore( capture, transColor, null, match );

	/// <summary>画像を指定した照合方法で全て探し、見つかった左上の位置を上から順 (同じ行は左から順) に返します。既に見つかった位置と重なる位置は返しません。</summary>
	/// <param name="capture">探す画像です。<see cref="ScriptGlobals.Capture(string)"/> や <see cref="ScriptGlobals.Capture(Rectangle?)"/> で作ります。</param>
	/// <param name="transColor">透過色にする画像の隅です (UWSC の透過 1〜4)。その隅と同じ色の部分は比較しません。</param>
	/// <param name="area">探す範囲です (作ったときの座標系)。省略するとキャプチャした範囲全体です。</param>
	/// <param name="match">照合方法です。<see cref="ImageMatch.Color"/>・<see cref="ImageMatch.Shape"/>・<see cref="ImageMatch.Gray"/>・<see cref="ImageMatch.Edge"/> です。</param>
	/// <returns>見つかった画像の左上 (作ったときの座標系) の配列です。無ければ空の配列です。</returns>
	public Point[] CheckImages( Capture capture, TransColor transColor, Rectangle area, ImageMatch match ) =>
		CheckImagesCore( capture, transColor, area, match );

	private Point[] CheckImagesCore( Capture capture, TransColor transColor, Rectangle? area, ImageMatch match, int max = int.MaxValue ) =>
		Find( capture.ToNeedle( transColor ), area, match, max );

	private Point[] Find( NeedleImage needle, Rectangle? area, ImageMatch match, int max ) {
		ScriptStop.Check();
		ScreenImage.ValidateMax( max );
		var r = ToLocal( area );
		if( r.Width < needle.Width || r.Height < needle.Height ) {
			return [];
		}

		var found = ScreenImage.Search( needle, Crop( r ), r.Width, r.Height, match, max );
		return found.Select( p => new Point( p.X + r.X + Origin.X, p.Y + r.Y + Origin.Y ) ).ToArray();
	}

	// 作ったときの座標系の範囲を、ピクセル配列内の範囲にする。はみ出した部分は切り捨てる
	private Rectangle ToLocal( Rectangle? area ) {
		var all = new Rectangle( 0, 0, Width, Height );
		return area is { } a ? Rectangle.Intersect( all, new Rectangle( a.X - Origin.X, a.Y - Origin.Y, a.Width, a.Height ) ) : all;
	}

	private int[] Crop( Rectangle r ) {
		if( r.X == 0 && r.Y == 0 && r.Width == Width && r.Height == Height ) {
			return _pixels;
		}

		var result = new int[r.Width * r.Height];
		for( var y = 0; y < r.Height; y++ ) {
			Array.Copy( _pixels, ( r.Y + y ) * Width + r.X, result, y * r.Width, r.Width );
		}

		return result;
	}

	/// <summary>Bitmap にして返します。</summary>
	/// <returns>新しい画像です。使い終わったら破棄します。</returns>
	public Bitmap ToBitmap() {
		ScriptStop.Check();
		var bmp = new Bitmap( Width, Height, PixelFormat.Format32bppArgb );
		var data = bmp.LockBits( new Rectangle( 0, 0, Width, Height ), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb );
		try {
			for( var y = 0; y < Height; y++ ) {
				Marshal.Copy( _pixels, y * Width, data.Scan0 + y * data.Stride, Width );
			}
		} finally { bmp.UnlockBits( data ); }

		return bmp;
	}

	/// <summary>画像ファイルに保存します (SAVEIMG)。形式は拡張子で決まります (.png .jpg .jpeg .gif、それ以外は .bmp)。</summary>
	/// <param name="path">保存先のパスです。</param>
	public void Save( string path ) {
		ScriptStop.Check();
		using var bmp = ToBitmap();
		ScreenImage.SaveBitmap( bmp, path );
	}
}
