namespace CsWSC.Tests;

// 期待値は UWSC 5.3.0.2 の CHKIMG (透過色 -1) で実際に確かめた結果に合わせている
public class ScreenImageTests {
	private static readonly string[] Shape2 = [
		"........",
		".##.....",
		".##.....",
		".######.",
		".######.",
		"....##..",
		"....##..",
		"........",
	];

	private static readonly string[] Shape3 = [
		"........",
		".##.....",
		".##.....",
		".###rrr.",
		".###rrr.",
		"....rr..",
		"....rr..",
		"........",
	];

	private const int White = 0xFFFFFF, Black = 0x000000, Red = 0xFF0000, Blue = 0x0000FF, Yellow = 0xFFFF00, Green = 0x008000, Gray = 0x808080;

	private static Dictionary<char, int> Colors( int bg, int fg, int fg2 = Red ) => new() { ['.'] = bg, ['#'] = fg, ['r'] = fg2 };

	private static Bitmap ToBitmap( string[] rows, Dictionary<char, int> colors ) {
		var bmp = new Bitmap( rows[0].Length, rows.Length );
		for( var y = 0; y < rows.Length; y++ ) {
			for( var x = 0; x < rows[y].Length; x++ ) {
				bmp.SetPixel( x, y, rows[y][x] == ' ' ? Color.Transparent : Color.FromArgb( unchecked((int)0xFF000000) | colors[rows[y][x]] ) );
			}
		}

		return bmp;
	}

	// 画像を背景色で 4 ピクセルずつ囲んだ画面を作って探す
	private static List<Point> Find( string[] needle, Dictionary<char, int> needleColors, string[] screen, Dictionary<char, int> screenColors,
		ImageMatch? match = null, TransColor transColor = TransColor.None ) {
		var pad = new string( '.', screen[0].Length + 8 );
		string[] rows = [.. Enumerable.Repeat( pad, 4 ), .. screen.Select( r => "...." + r + "...." ), .. Enumerable.Repeat( pad, 4 )];
		using var n = ToBitmap( needle, needleColors );
		using var h = ToBitmap( rows, screenColors );
		return ScreenImage.Search( ScreenImage.CreateNeedle( n, transColor ), ScreenImage.Pixels( h ), h.Width, h.Height, match ?? ImageMatch.Shape() );
	}

	[Fact]
	public void Shape_SameImage_Found() {
		Assert.Equal( [new Point( 4, 4 )], Find( Shape2, Colors( White, Black ), Shape2, Colors( White, Black ) ) );
	}

	[Theory]
	[InlineData( Blue, Yellow )] // 別の 2 色に置き換え
	[InlineData( Black, White )] // 前景と背景を反転
	public void Shape_Recolored_Found( int bg, int fg ) {
		Assert.Single( Find( Shape2, Colors( White, Black ), Shape2, Colors( bg, fg ) ) );
		Assert.Empty( Find( Shape2, Colors( White, Black ), Shape2, Colors( bg, fg ), ImageMatch.Color() ) );
	}

	[Fact]
	public void Shape_ThreeColorsPermuted_Found() {
		Assert.Single( Find( Shape3, Colors( White, Black, Red ), Shape3, Colors( Green, Red, Blue ) ) );
	}

	[Fact]
	public void Shape_SameColorSplitOnScreen_NotFound() {
		// 画像では前景が 1 色だが、画面では 2 色に分かれている
		Assert.Empty( Find( Shape2, Colors( White, Black ), Shape3, Colors( White, Black, Red ) ) );
	}

	[Fact]
	public void Shape_DifferentColorsMergedOnScreen_NotFound() {
		// 画像では違う 2 色が、画面では同じ色
		Assert.Empty( Find( Shape3, Colors( White, Black, Red ), Shape3, Colors( White, Black, Black ) ) );
		Assert.Empty( Find( Shape3, Colors( White, Black, Red ), Shape3, Colors( White, Black, White ) ) );
	}

	[Fact]
	public void Shape_BackgroundSplitOnScreen_NotFound() {
		var screen = Shape2.Select( ( r, y ) => y >= 5 ? r.Replace( '.', 'r' ) : r ).ToArray();
		Assert.Empty( Find( Shape2, Colors( White, Black ), screen, Colors( White, Black, Gray ) ) );
	}

	[Fact]
	public void Shape_OnePixelDifferent_NotFound() {
		var screen = (string[])Shape2.Clone();
		screen[1] = "..#.....";
		Assert.Empty( Find( Shape2, Colors( White, Black ), screen, Colors( White, Black ) ) );
	}

	[Theory]
	[InlineData( 255, 248, false )] // 下位 3 ビットだけの違いは同じ色とみなす
	[InlineData( 247, 240, false )]
	[InlineData( 255, 247, true )]
	[InlineData( 248, 247, true )]
	[InlineData( 8, 7, true )]
	public void Shape_NearColors_TreatedAsSameColor( int bg, int fg, bool found ) {
		int gray( int v ) => v << 16 | v << 8 | v;
		Assert.Equal( found, Find( Shape2, Colors( White, Black ), Shape2, Colors( gray( bg ), gray( fg ) ) ).Count == 1 );
	}

	[Fact]
	public void Shape_TransparentPixelsIgnored() {
		// 画像の上の 2 隅を透明にすると、画面のその位置は何色でもよい
		var needle = (string[])Shape2.Clone();
		needle[0] = " ...... ";
		var screen = (string[])Shape2.Clone();
		screen[0] = "r......r";
		Assert.Single( Find( needle, Colors( White, Black ), screen, Colors( Blue, Yellow, Red ) ) );
	}

	[Theory]
	[InlineData( TransColor.TopLeft )]
	[InlineData( TransColor.TopRight )]
	[InlineData( TransColor.BottomLeft )]
	[InlineData( TransColor.BottomRight )]
	public void Transparent_CornerColorIgnored( TransColor corner ) {
		// 背景 (白) が透過色になるので、画面の背景が何色でも形の部分だけで一致する
		Assert.Single( Find( Shape2, Colors( White, Black ), Shape2, Colors( Blue, Black ), ImageMatch.Color(), transColor: corner ) );
		Assert.Empty( Find( Shape2, Colors( White, Black ), Shape2, Colors( Blue, Black ), ImageMatch.Color() ) );
	}

	[Fact]
	public void Transparent_OnlyCornerColorIgnored() {
		// 左下だけ赤にして左下を透過色にすると、白い背景は比較される
		var needle = (string[])Shape2.Clone();
		needle[7] = "r.......";
		Assert.Empty( Find( needle, Colors( White, Black ), Shape2, Colors( Blue, Black ), ImageMatch.Color(), transColor: TransColor.BottomLeft ) );
		Assert.Single( Find( needle, Colors( White, Black ), Shape2, Colors( White, Black ), ImageMatch.Color(), transColor: TransColor.BottomLeft ) );
	}

	[Fact]
	public void Transparent_CombinedWithShape() {
		// 背景を透過にすると、形の色が変わっても背景が何色でも見つかる
		Assert.Single( Find( Shape3, Colors( White, Black, Red ), Shape3, Colors( Gray, Green, Blue ), transColor: TransColor.TopLeft ) );
	}

	// 明るさに勾配のある 12x12 の画像 (中央に暗い四角)
	private static Bitmap Gradient( Func<int, int> tone ) {
		var bmp = new Bitmap( 12, 12 );
		for( var y = 0; y < 12; y++ ) {
			for( var x = 0; x < 12; x++ ) {
				var v = x >= 4 && x < 8 && y >= 3 && y < 9 ? 40 + x * 3 : 120 + x * 8 + y * 2;
				v = Math.Clamp( tone( v ), 0, 255 );
				bmp.SetPixel( x, y, Color.FromArgb( v, v, v ) );
			}
		}

		return bmp;
	}

	// 画像を (20, 10) に置いた 60x40 の画面で探す
	private static List<Point> FindIn( Bitmap needle, Bitmap image, int background, ImageMatch match ) {
		using var hay = new Bitmap( 60, 40 );
		using( var g = Graphics.FromImage( hay ) ) {
			g.Clear( Color.FromArgb( background ) );
			g.DrawImageUnscaled( image, 20, 10 );
		}

		return ScreenImage.Search( ScreenImage.CreateNeedle( needle ), ScreenImage.Pixels( hay ), hay.Width, hay.Height, match );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void Correlation_BrightnessAndContrastChanged_Found( bool edge ) {
		ImageMatch match = edge ? ImageMatch.Edge() : ImageMatch.Gray();
		using var needle = Gradient( v => v );
		using var screen = Gradient( v => v * 3 / 4 + 30 ); // 暗く・コントラストを弱く
		Assert.Equal( [new Point( 20, 10 )], FindIn( needle, screen, unchecked((int)0xFF404040), match ) );
		Assert.Empty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Color( 10 ) ) );
	}

	[Fact]
	public void Gray_Inverted_NotFound() {
		using var needle = Gradient( v => v );
		using var screen = Gradient( v => 255 - v );
		Assert.Empty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Gray() ) );
	}

	[Fact]
	public void Edge_Inverted_Found() {
		// 輪郭の強さは明るさを反転しても同じ
		using var needle = Gradient( v => v );
		using var screen = Gradient( v => 255 - v );
		Assert.Equal( [new Point( 20, 10 )], FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Edge() ) );
	}

	[Fact]
	public void Correlation_DifferentImage_NotFound() {
		using var needle = Gradient( v => v );
		using var screen = new Bitmap( 12, 12 );
		for( var y = 0; y < 12; y++ ) {
			for( var x = 0; x < 12; x++ ) {
				screen.SetPixel( x, y, ( x + y ) % 3 == 0 ? Color.Black : Color.White );
			}
		}

		Assert.Empty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Gray() ) );
		Assert.Empty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Edge() ) );
	}

	[Fact]
	public void Correlation_ShapeWithColorChanged_FoundByEdge() {
		// 白地に黒の形を、青地に黄色の形の中から輪郭で探す
		Assert.Single( Find( Shape2, Colors( White, Black ), Shape2, Colors( Blue, Yellow ), ImageMatch.Edge() ) );
	}

	// UI に似せた画面 (平坦な背景・色付きの矩形・文字)
	private static Bitmap UiScreen( int w, int h ) {
		var bmp = new Bitmap( w, h );
		var random = new Random( 2 );
		using var g = Graphics.FromImage( bmp );
		g.Clear( Color.FromArgb( 240, 240, 240 ) );
		// 特定のフォントの有無に依存しないよう、どの環境にもある既定のフォントを使う
		using var font = new Font( FontFamily.GenericSansSerif, 9 );
		for( var i = 0; i < 40; i++ ) {
			using var brush = new SolidBrush( Color.FromArgb( random.Next( 256 ), random.Next( 256 ), random.Next( 256 ) ) );
			g.FillRectangle( brush, random.Next( w ), random.Next( h ), random.Next( 20, 120 ), random.Next( 10, 40 ) );
		}

		for( var i = 0; i < 60; i++ ) {
			g.DrawString( $"Item{random.Next( 1000 )}", font, Brushes.Black, random.Next( w ), random.Next( h ) );
		}

		return bmp;
	}

	[Theory]
	[InlineData( false, 200, 120 )]
	[InlineData( false, 201, 123 )] // 縮小のブロック境界からずれた位置
	[InlineData( false, 207, 125 )]
	[InlineData( true, 200, 120 )]
	[InlineData( true, 201, 123 )]
	[InlineData( true, 207, 125 )]
	public void Correlation_LargeNeedle_FoundAtExactPosition( bool edge, int x, int y ) {
		// 32x32 なら縮小して候補を探す経路を通る
		using var screen = UiScreen( 400, 300 );
		using var g = Graphics.FromImage( screen );
		using var font = new Font( FontFamily.GenericSansSerif, 12, FontStyle.Bold );
		g.FillRectangle( Brushes.SteelBlue, x, y, 32, 32 );
		g.DrawString( "OK", font, Brushes.White, x + 2, y + 5 );
		using var needle = screen.Clone( new Rectangle( x, y, 32, 32 ), screen.PixelFormat );

		// 画面全体を暗くしても見つかる
		var pixels = ScreenImage.Pixels( screen ).Select( c => unchecked((int)0xFF000000) | ( ( c >> 16 & 0xFF ) * 3 / 4 ) << 16 | ( ( c >> 8 & 0xFF ) * 3 / 4 ) << 8 | ( c & 0xFF ) * 3 / 4 ).ToArray();
		ImageMatch match = edge ? ImageMatch.Edge() : ImageMatch.Gray();
		Assert.Equal( [new Point( x, y )], ScreenImage.Search( ScreenImage.CreateNeedle( needle ), pixels, 400, 300, match ) );
	}

	[Theory]
	[InlineData( 200, 120 )]
	[InlineData( 201, 123 )]
	public void Shape_ManyColors_FoundAfterColorsInverted( int x, int y ) {
		// 文字のアンチエイリアスなどで色数の多い画像でも、色を反転 (1 対 1 の置き換え) した画面から見つかる
		using var screen = UiScreen( 400, 300 );
		using var needle = screen.Clone( new Rectangle( x, y, 32, 32 ), screen.PixelFormat );
		var pixels = ScreenImage.Pixels( screen );
		var inverted = pixels.Select( c => c ^ 0xFFFFFF ).ToArray();
		var n = ScreenImage.CreateNeedle( needle );
		Assert.Equal( [new Point( x, y )], ScreenImage.Search( n, pixels, 400, 300, ImageMatch.Shape() ) );
		Assert.Equal( [new Point( x, y )], ScreenImage.Search( n, inverted, 400, 300, ImageMatch.Shape() ) );
		Assert.Empty( ScreenImage.Search( n, inverted, 400, 300, ImageMatch.Color( 10 ) ) );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 16 )]
	[InlineData( 24 )]
	[InlineData( 40 )] // 許容差が大きいと色を数えず、最初のピクセルで候補を絞る
	public void Color_LargeScreen_FoundAtExactPosition( int tolerance ) {
		// 画面が画像の 64 倍以上あると、画面上で少ない色のピクセルを選んで候補を絞る
		using var screen = UiScreen( 400, 300 );
		var random = new Random( 7 );
		for( var y = 0; y < 8; y++ ) {
			for( var x = 0; x < 8; x++ ) {
				screen.SetPixel( 150 + x, 100 + y, Color.FromArgb( random.Next( 256 ), random.Next( 256 ), random.Next( 256 ) ) );
			}
		}

		using var needle = screen.Clone( new Rectangle( 150, 100, 8, 8 ), screen.PixelFormat );
		Assert.Equal( [new Point( 150, 100 )], ScreenImage.Search( ScreenImage.CreateNeedle( needle ), ScreenImage.Pixels( screen ), 400, 300, ImageMatch.Color( tolerance ) ) );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 16 )]
	public void Color_MostlyBackgroundNeedle_FoundByRarePixel( int tolerance ) {
		// 背景色ばかりの画像でも、珍しい色のピクセル (先頭ではない) で候補を絞って見つかる
		using var screen = UiScreen( 400, 300 );
		using( var g = Graphics.FromImage( screen ) )
		using( var background = new SolidBrush( Color.FromArgb( 240, 240, 240 ) ) ) {
			g.FillRectangle( background, 300, 250, 8, 8 );
		}

		screen.SetPixel( 306, 255, Color.Magenta );
		using var needle = screen.Clone( new Rectangle( 300, 250, 8, 8 ), screen.PixelFormat );
		var pixels = ScreenImage.Pixels( screen );
		Assert.Equal( [new Point( 300, 250 )], ScreenImage.Search( ScreenImage.CreateNeedle( needle ), pixels, 400, 300, ImageMatch.Color( tolerance ) ) );
		// 背景を透過色にすると珍しい色のピクセルだけを比べる
		Assert.Equal( [new Point( 300, 250 )], ScreenImage.Search( ScreenImage.CreateNeedle( needle, TransColor.TopLeft ), pixels, 400, 300, ImageMatch.Color( tolerance ) ) );
	}

	// 内側の画像を 3 ピクセルの枠で囲む
	private static Bitmap Framed( Bitmap inner, Func<int, int, Color> frame ) {
		var bmp = new Bitmap( inner.Width + 6, inner.Height + 6 );
		for( var y = 0; y < bmp.Height; y++ ) {
			for( var x = 0; x < bmp.Width; x++ ) {
				var inside = x >= 3 && y >= 3 && x < inner.Width + 3 && y < inner.Height + 3;
				bmp.SetPixel( x, y, inside ? inner.GetPixel( x - 3, y - 3 ) : frame( x, y ) );
			}
		}

		return bmp;
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void Correlation_TransColor_FrameIgnored( bool edge ) {
		// 枠の色を透過色にすると、画面の枠の部分が全く違っても内側の模様だけで見つかる
		ImageMatch match = edge ? ImageMatch.Edge() : ImageMatch.Gray();
		using var inner = Gradient( v => v );
		using var needle = Framed( inner, ( _, _ ) => Color.FromArgb( 10, 200, 30 ) );
		// 市松模様は Sobel の輪郭が 0 になるので、乱数のノイズにする
		var noise = new Random( 3 );
		using var screen = Framed( inner, ( _, _ ) => noise.Next( 2 ) == 0 ? Color.Black : Color.White );
		using var hay = new Bitmap( 60, 40 );
		using( var g = Graphics.FromImage( hay ) ) {
			g.Clear( Color.FromArgb( 0x40, 0x40, 0x40 ) );
			g.DrawImageUnscaled( screen, 20, 10 );
		}

		var pixels = ScreenImage.Pixels( hay );
		Assert.Equal( [new Point( 20, 10 )], ScreenImage.Search( ScreenImage.CreateNeedle( needle, TransColor.TopLeft ), pixels, 60, 40, match ) );
		Assert.Empty( ScreenImage.Search( ScreenImage.CreateNeedle( needle ), pixels, 60, 40, match ) );
	}

	[Fact]
	public void Arguments_OutOfRange_Throw() {
		Assert.Throws<ArgumentOutOfRangeException>( () => ImageMatch.Color( -1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => ImageMatch.Color( 256 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => ImageMatch.Gray( 1.1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => ImageMatch.Edge( -0.1 ) );
	}

	[Fact]
	public void Correlation_ThresholdChangesResult() {
		// 1 ピクセルだけ違う画像は、閾値を 1 にすると見つからない
		using var needle = Gradient( v => v );
		using var screen = Gradient( v => v );
		screen.SetPixel( 5, 5, Color.White );
		Assert.Single( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Gray( 0.9 ) ) );
		Assert.Empty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Gray( 1 ) ) );
	}

	[Fact]
	public void Matches_AreValueEqual() {
		Assert.Equal( ImageMatch.Color( 10 ), ImageMatch.Color( 10 ) );
		Assert.NotEqual<ImageMatch>( ImageMatch.Gray( 0.9 ), ImageMatch.Edge( 0.9 ) );
	}

	[Fact]
	public void Color_ToleranceStillWorks() {
		Assert.Single( Find( Shape2, Colors( White, Black ), Shape2, Colors( 0xF8F8F8, 0x080808 ), ImageMatch.Color( 8 ) ) );
		Assert.Empty( Find( Shape2, Colors( White, Black ), Shape2, Colors( 0xF8F8F8, 0x080808 ), ImageMatch.Color( 7 ) ) );
	}

	public static TheoryData<string> AllModes => ["Color", "Shape", "Gray", "Edge"];

	private static ImageMatch Mode( string name ) => name switch {
		"Color" => ImageMatch.Color(),
		"Shape" => ImageMatch.Shape(),
		"Gray" => ImageMatch.Gray(),
		"Edge" => ImageMatch.Edge(),
		_ => throw new ArgumentException( name ),
	};

	private static int Rgb( int v ) => unchecked((int)0xFF000000) | v << 16 | v << 8 | v;

	private static Bitmap ToBitmap( int w, int h, Func<int, int, int> argb ) {
		var bmp = new Bitmap( w, h );
		for( var y = 0; y < h; y++ ) {
			for( var x = 0; x < w; x++ ) {
				bmp.SetPixel( x, y, Color.FromArgb( argb( x, y ) ) );
			}
		}

		return bmp;
	}

	private static List<Point> Search( Bitmap needle, int[] hay, int hw, int hh, ImageMatch match, int max = int.MaxValue ) =>
		ScreenImage.Search( ScreenImage.CreateNeedle( needle ), hay, hw, hh, match, max );

	// 横に 4 ピクセル周期のランダムな模様 (12x12)。4 ピクセルずらしても同じ画像になる
	private static readonly int[] PeriodicTable = [.. Enumerable.Range( 0, 4 * 12 ).Select( i => new Random( 5 + i ).Next( 16, 256 ) )];

	private static int Periodic( int x, int y ) => Rgb( PeriodicTable[y * 4 + x % 4] );

	// 黒い 70x50 の画面に、(5,5) に横 16 ピクセルの模様 ((5,5) と重なる (9,5) でも一致する)、(40,5) と (5,30) に画像を置く
	private static int[] PeriodicScreen() {
		var hay = new int[70 * 50];
		Array.Fill( hay, Rgb( 0 ) );
		void Put( int ox, int oy, int w ) {
			for( var y = 0; y < 12; y++ ) {
				for( var x = 0; x < w; x++ ) {
					hay[( oy + y ) * 70 + ox + x] = Periodic( x, y );
				}
			}
		}

		Put( 40, 5, 12 );
		Put( 5, 30, 12 );
		Put( 5, 5, 16 );
		return hay;
	}

	[Theory]
	[MemberData( nameof( AllModes ) )]
	public void MultipleMatches_SortedAndOverlapsExcluded( string mode ) {
		using var needle = ToBitmap( 12, 12, Periodic );
		var hay = PeriodicScreen();
		// 上から順 (同じ行は左から順)。(9,5) は (5,5) と重なるので返さない
		Assert.Equal( [new Point( 5, 5 ), new Point( 40, 5 ), new Point( 5, 30 )], Search( needle, hay, 70, 50, Mode( mode ) ) );
	}

	[Theory]
	[MemberData( nameof( AllModes ) )]
	public void MultipleMatches_StopAtMax( string mode ) {
		using var needle = ToBitmap( 12, 12, Periodic );
		var hay = PeriodicScreen();
		Assert.Equal( [new Point( 5, 5 ), new Point( 40, 5 )], Search( needle, hay, 70, 50, Mode( mode ), max: 2 ) );
		Assert.Equal( [new Point( 5, 5 )], Search( needle, hay, 70, 50, Mode( mode ), max: 1 ) );
	}

	[Theory]
	[MemberData( nameof( AllModes ) )]
	public void NeedleLargerThanScreen_NotFound( string mode ) {
		using var needle = Gradient( v => v );
		Assert.Empty( Search( needle, new int[10 * 10], 10, 10, Mode( mode ) ) );
		Assert.Empty( Search( needle, new int[40 * 10], 40, 10, Mode( mode ) ) ); // 幅は足りるが高さが足りない
		Assert.Empty( Search( needle, new int[10 * 40], 10, 40, Mode( mode ) ) );
	}

	[Theory]
	[MemberData( nameof( AllModes ) )]
	public void BottomRightCorner_Found( string mode ) {
		// 余白なしで画面の右下隅にぴったり置いた画像
		using var hay = new Bitmap( 30, 20 );
		using( var g = Graphics.FromImage( hay ) ) {
			g.Clear( Color.Black );
			using var image = Gradient( v => v );
			g.DrawImageUnscaled( image, 18, 8 );
		}

		using var needle = Gradient( v => v );
		Assert.Equal( [new Point( 18, 8 )], Search( needle, ScreenImage.Pixels( hay ), 30, 20, Mode( mode ) ) );
	}

	[Fact]
	public void SameSize_Found() {
		// 画面と画像が同じ大きさ
		using var needle = Gradient( v => v );
		var hay = ScreenImage.Pixels( needle );
		foreach( var mode in new[] { "Color", "Shape", "Gray", "Edge" } ) {
			Assert.Equal( [Point.Empty], Search( needle, hay, 12, 12, Mode( mode ) ) );
		}
	}

	[Fact]
	public void TransparentNeedle_PixelMatchesEverywhere_CorrelationNothing() {
		// 全て透明な画像: Color / Shape は比較するピクセルが無いのでどこでも一致 (重ならない位置を返す)、
		// Gray / Edge は比較するピクセルが無いので見つからない (現在の挙動)
		using var needle = ToBitmap( 4, 4, ( _, _ ) => 0 );
		var hay = Enumerable.Range( 0, 64 ).Select( i => Rgb( i * 4 ) ).ToArray();
		Point[] tiles = [new( 0, 0 ), new( 4, 0 ), new( 0, 4 ), new( 4, 4 )];
		Assert.Equal( tiles, Search( needle, hay, 8, 8, ImageMatch.Color() ) );
		Assert.Equal( tiles, Search( needle, hay, 8, 8, ImageMatch.Shape() ) );
		Assert.Empty( Search( needle, hay, 8, 8, ImageMatch.Gray() ) );
		Assert.Empty( Search( needle, hay, 8, 8, ImageMatch.Edge() ) );
	}

	[Fact]
	public void OnePixelNeedle() {
		using var needle = ToBitmap( 1, 1, ( _, _ ) => unchecked((int)0xFF000000) | Red );
		var hay = new int[8 * 6];
		Array.Fill( hay, unchecked((int)0xFF000000) | White );
		hay[3 * 8 + 6] = unchecked((int)0xFF000000) | Red;
		Assert.Equal( [new Point( 6, 3 )], Search( needle, hay, 8, 6, ImageMatch.Color() ) );
		// 1 色だけの画像は形としてどの位置とも一致する
		Assert.Equal( 48, Search( needle, hay, 8, 6, ImageMatch.Shape() ).Count );
		// 1 ピクセルの画像は明るさが平坦とみなされ、画面の 1 ピクセルも平坦なので、ほぼどの位置とも一致する (現在の挙動)。
		// 赤の位置は float の誤差で平坦と判定されず外れる (白は誤差が出ない)
		var gray = Search( needle, hay, 8, 6, ImageMatch.Gray() );
		Assert.Equal( 47, gray.Count );
		Assert.DoesNotContain( new Point( 6, 3 ), gray );
		// 輪郭は外周 1 ピクセルを比較しないので、比較するピクセルが無く見つからない
		Assert.Empty( Search( needle, hay, 8, 6, ImageMatch.Edge() ) );
	}

	[Theory]
	[InlineData( false )]
	[InlineData( true )]
	public void FlatNeedle_MatchesOnlyFlatArea( bool edge ) {
		// 平坦な (分散 0 の) 画像は、0 除算にならず画面の平坦な部分とだけ一致する
		ImageMatch match = edge ? ImageMatch.Edge() : ImageMatch.Gray();
		using var needle = ToBitmap( 10, 10, ( _, _ ) => Rgb( 128 ) );
		var random = new Random( 3 );
		var hay = new int[30 * 20];
		for( var i = 0; i < hay.Length; i++ ) {
			hay[i] = i % 30 < 15 ? Rgb( 128 ) : Rgb( random.Next( 256 ) ); // 左半分が平坦、右半分はノイズ
		}

		Assert.Equal( [new Point( 0, 0 ), new Point( 0, 10 )], Search( needle, hay, 30, 20, match ) );

		var noise = hay.Select( _ => Rgb( random.Next( 256 ) ) ).ToArray();
		Assert.Empty( Search( needle, noise, 30, 20, match ) );
	}

	[Fact]
	public void Correlation_SameImage_FoundWithNearlyOne() {
		using var needle = Gradient( v => v );
		Assert.Equal( [new Point( 20, 10 )], FindIn( needle, needle, unchecked((int)0xFF404040), ImageMatch.Gray( 0.99999 ) ) );
		Assert.Equal( [new Point( 20, 10 )], FindIn( needle, needle, unchecked((int)0xFF404040), ImageMatch.Edge( 1 ) ) );
	}

	[Fact]
	public void Gray_SameImage_FoundWithThresholdOne() {
		using var needle = Gradient( v => v );
		Assert.Equal( [new Point( 20, 10 )], FindIn( needle, needle, unchecked((int)0xFF404040), ImageMatch.Gray( 1 ) ) );
	}

	[Fact]
	public void Arguments_Boundary_DoNotThrow() {
		using var needle = Gradient( v => v );
		using var screen = Gradient( v => v );
		Assert.Equal( [new Point( 20, 10 )], FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Color( 0 ) ) );
		Assert.NotEmpty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Color( 255 ) ) ); // どこでも一致
		Assert.NotEmpty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Gray( 0 ) ) );
		Assert.NotEmpty( FindIn( needle, screen, unchecked((int)0xFF404040), ImageMatch.Edge( 0 ) ) );
		Assert.Equal( 0, ImageMatch.Color( 0 ).Tolerance );
		Assert.Equal( 255, ImageMatch.Color( 255 ).Tolerance );
		Assert.Equal( 0, ImageMatch.Gray( 0 ).Threshold );
		Assert.Equal( 1, ImageMatch.Gray( 1 ).Threshold );
	}

	[Fact]
	public void LoadNeedle_CachedUntilFileChanges() {
		var directory = Path.Combine( Path.GetTempPath(), "CsWSC.Tests." + Guid.NewGuid().ToString( "N" ) );
		var path = Path.Combine( directory, "needle.png" );
		Directory.CreateDirectory( directory );
		try {
			using( var bmp = ToBitmap( 2, 2, ( _, _ ) => Rgb( 10 ) ) ) {
				bmp.Save( path, System.Drawing.Imaging.ImageFormat.Png );
			}

			var first = ScreenImage.LoadNeedle( path );
			Assert.Same( first, ScreenImage.LoadNeedle( path ) );
			Assert.NotSame( first, ScreenImage.LoadNeedle( path, TransColor.TopLeft ) ); // 透過色が違えば別

			using( var bmp = ToBitmap( 2, 2, ( _, _ ) => Rgb( 20 ) ) ) {
				bmp.Save( path, System.Drawing.Imaging.ImageFormat.Png );
			}

			File.SetLastWriteTimeUtc( path, DateTime.UtcNow.AddMinutes( 1 ) );
			var second = ScreenImage.LoadNeedle( path );
			Assert.NotSame( first, second );
			Assert.Equal( Rgb( 20 ), second.Pixels[0] );
			Assert.Same( second, ScreenImage.LoadNeedle( path ) );
		} finally { Directory.Delete( directory, true ); }
	}
}
