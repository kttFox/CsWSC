namespace CsWSC.Tests;

public class CaptureTests {
	// 10x6 の白い画像の (2,1) と (7,4) に 2x2 の赤い四角を置く
	private static Bitmap Sample() {
		var bmp = new Bitmap( 10, 6 );
		using( var g = Graphics.FromImage( bmp ) ) {
			g.Clear( Color.White );
		}

		foreach( var (x, y) in new[] { (2, 1), (7, 4) } ) {
			for( var dy = 0; dy < 2; dy++ ) {
				for( var dx = 0; dx < 2; dx++ ) {
					bmp.SetPixel( x + dx, y + dy, Color.FromArgb( 250, 0, 0 ) );
				}
			}
		}

		return bmp;
	}

	private static Capture RedSquare() {
		using var bmp = new Bitmap( 2, 2 );
		using( var g = Graphics.FromImage( bmp ) ) {
			g.Clear( Color.FromArgb( 250, 0, 0 ) );
		}

		return Capture.FromBitmap( bmp );
	}

	[Fact]
	public void PeekColor_ReadsPixel() {
		using var bmp = Sample();
		var s = Capture.FromBitmap( bmp );
		Assert.Equal( Color.White.ToArgb(), s.PeekColor( 0, 0 ).ToArgb() );
		Assert.Equal( Color.FromArgb( 250, 0, 0 ).ToArgb(), s.PeekColor( 2, 1 ).ToArgb() );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.PeekColor( 10, 0 ) );
	}

	[Fact]
	public void CheckColor_FindsFirstAndAll() {
		using var bmp = Sample();
		var s = Capture.FromBitmap( bmp );
		Assert.Null( s.CheckColor( Color.Red ) );
		Assert.Equal( new Point( 2, 1 ), s.CheckColor( Color.Red, tolerance: 5 ) );
		Assert.Equal( 8, s.CheckColors( Color.Red, tolerance: 5 ).Length );
		Assert.Equal( new Point( 7, 4 ), s.CheckColor( Color.Red, new Rectangle( 5, 0, 5, 6 ), 5 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColor( Color.Red, tolerance: 256 ) );
	}

	[Fact]
	public void CheckColor_WideImage_FindsAcrossVectorLanes() {
		// SIMD でまとめて比べる部分と、行末の 1 ピクセルずつ比べる部分の両方に置く
		using var bmp = new Bitmap( 67, 4 );
		using( var g = Graphics.FromImage( bmp ) ) {
			g.Clear( Color.White );
		}

		Point[] expected = [new( 0, 0 ), new( 17, 1 ), new( 31, 1 ), new( 66, 2 ), new( 40, 3 )];
		foreach( var p in expected ) {
			bmp.SetPixel( p.X, p.Y, Color.FromArgb( 250, 5, 0 ) );
		}

		var s = Capture.FromBitmap( bmp );
		Assert.Empty( s.CheckColors( Color.Red ) );
		Assert.Equal( expected.OrderBy( p => p.Y ).ThenBy( p => p.X ), s.CheckColors( Color.Red, tolerance: 5 ) );
		Assert.Equal( new Point( 66, 2 ), s.CheckColor( Color.Red, new Rectangle( 41, 2, 26, 2 ), 5 ) );
		Assert.Equal( new Point( 40, 3 ), s.CheckColor( Color.Red, new Rectangle( 33, 3, 34, 1 ), 5 ) );
	}

	[Fact]
	public void CheckColor_MultipleColors_ReturnsColorAndPoint() {
		using var bmp = new Bitmap( 67, 3 );
		using( var g = Graphics.FromImage( bmp ) ) {
			g.Clear( Color.White );
		}

		bmp.SetPixel( 40, 0, Color.FromArgb( 0, 0, 250 ) );   // 青
		bmp.SetPixel( 9, 1, Color.FromArgb( 250, 0, 0 ) );    // 赤
		bmp.SetPixel( 66, 2, Color.FromArgb( 250, 0, 0 ) );   // 赤 (行末)
		var s = Capture.FromBitmap( bmp );
		Color[] colors = [Color.Red, Color.Blue];

		Assert.Equal( (Color.Blue, new Point( 40, 0 )), s.CheckColor( colors, tolerance: 5 ) );
		Assert.Equal( [(Color.Blue, new Point( 40, 0 )), (Color.Red, new Point( 9, 1 )), (Color.Red, new Point( 66, 2 ))], s.CheckColors( colors, tolerance: 5 ) );
		Assert.Equal( (Color.Red, new Point( 9, 1 )), s.CheckColor( colors, new Rectangle( 0, 1, 67, 2 ), 5 ) );
		Assert.Null( s.CheckColor( colors ) );          // 許容差 0 では一致しない
		Assert.Null( s.CheckColor( Array.Empty<Color>(), tolerance: 5 ) );
		// 両方に一致するピクセルは先に並ぶ色で返す
		Assert.Equal( Color.White, s.CheckColor( [Color.White, Color.FromArgb( 254, 254, 254 )], tolerance: 5 )?.Color );
	}

	[Fact]
	public void Max_LimitsResults() {
		using var bmp = Sample();
		var needle = RedSquare();
		var s = Capture.FromBitmap( bmp );
		Assert.Equal( [new Point( 2, 1 )], s.CheckImages( needle, max: 1 ) );
		Assert.Equal( [new Point( 2, 1 )], s.CheckImages( needle, ImageMatch.Color( 5, 1 ) ) );
		Assert.Equal( 2, s.CheckColors( Color.Red, tolerance: 5, max: 2 ).Length );
		Assert.Single( s.CheckColors( [Color.Red], tolerance: 5, max: 1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckImages( needle, max: 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColors( Color.Red, max: 0 ) );
	}

	[Fact]
	public void CheckImage_FindsInsideCaptureAndArea() {
		using var bmp = Sample();
		var needle = RedSquare();
		var s = Capture.FromBitmap( bmp );
		Assert.Equal( new Point( 2, 1 ), s.CheckImage( needle ) );
		Assert.Equal( [new Point( 2, 1 ), new Point( 7, 4 )], s.CheckImages( needle ) );
		Assert.Equal( new Point( 7, 4 ), s.CheckImage( needle, new Rectangle( 5, 0, 5, 6 ) ) );
		Assert.Null( s.CheckImage( needle, new Rectangle( 0, 0, 3, 3 ) ) );
		Assert.Equal( new Point( 2, 1 ), s.CheckImage( needle, ImageMatch.Color( 5 ) ) );
	}

	[Fact]
	public void CheckImage_TransColorDoesNotChangeNeedle() {
		using var bmp = Sample();
		var needle = RedSquare();
		var s = Capture.FromBitmap( bmp );
		s.CheckImage( needle, TransColor.TopLeft ); // 単色なので全て透明になり、どこでも一致する
		Assert.Equal( 255, needle.PeekColor( 0, 0 ).A ); // 透過色にしても元の Capture は透明にならない
	}

	[Fact]
	public void Origin_OffsetsInputAndOutput() {
		using var bmp = Sample();
		var needle = RedSquare();
		var b = Capture.FromBitmap( bmp );
		var s = new Capture( Enumerable.Range( 0, b.Width * b.Height ).Select( i => b.PeekColor( i % b.Width, i / b.Width ).ToArgb() ).ToArray(), b.Width, b.Height, new Point( 100, 50 ) );
		Assert.Equal( new Point( 102, 51 ), s.CheckImage( needle ) );
		Assert.Equal( new Point( 107, 54 ), s.CheckImage( needle, new Rectangle( 105, 50, 5, 6 ) ) );
		Assert.Equal( Color.White.ToArgb(), s.PeekColor( 100, 50 ).ToArgb() );
	}

	// 12x8 の画面。(2,1) と (7,4) に 2x2 の赤い四角を置く
	private static Bitmap Squares( Color background ) {
		var bmp = new Bitmap( 12, 8 );
		using( var g = Graphics.FromImage( bmp ) ) {
			g.Clear( background );
		}

		foreach( var (x, y) in new[] { (2, 1), (7, 4) } ) {
			for( var dy = 0; dy < 2; dy++ ) {
				for( var dx = 0; dx < 2; dx++ ) {
					bmp.SetPixel( x + dx, y + dy, Color.FromArgb( 250, 0, 0 ) );
				}
			}
		}

		return bmp;
	}

	// 4x4 の枠の内側に 2x2 の赤い四角。枠は白 (alpha で透明にもできる)
	private static Bitmap Bordered( int borderAlpha = 255 ) {
		var bmp = new Bitmap( 4, 4 );
		for( var y = 0; y < 4; y++ ) {
			for( var x = 0; x < 4; x++ ) {
				var inside = x is 1 or 2 && y is 1 or 2;
				bmp.SetPixel( x, y, inside ? Color.FromArgb( 250, 0, 0 ) : Color.FromArgb( borderAlpha, 255, 255, 255 ) );
			}
		}

		return bmp;
	}

	private static Capture WithOrigin( Bitmap bmp, Point origin ) => new( ScreenImage.Pixels( bmp ), bmp.Width, bmp.Height, origin );

	public static TheoryData<TransColor> Corners => [TransColor.TopLeft, TransColor.TopRight, TransColor.BottomLeft, TransColor.BottomRight];

	[Theory]
	[MemberData( nameof( Corners ) )]
	public void CheckImage_CaptureNeedleWithTransColor_IgnoresCornerColor( TransColor corner ) {
		// 背景が青の画面から、白い枠付きの画像を探す。枠を透過色にしたときだけ見つかる
		using var bmp = Squares( Color.Blue );
		using var needleBmp = Bordered();
		var s = Capture.FromBitmap( bmp );
		var needle = Capture.FromBitmap( needleBmp );
		Assert.Null( s.CheckImage( needle ) );
		Assert.Equal( new Point( 1, 0 ), s.CheckImage( needle, corner ) );
		Assert.Equal( [new Point( 1, 0 ), new Point( 6, 3 )], s.CheckImages( needle, corner ) );
		Assert.Equal( new Point( 1, 0 ), s.CheckImage( needle, corner, ImageMatch.Color() ) );
		Assert.Equal( [new Point( 6, 3 )], s.CheckImages( needle, corner, new Rectangle( 5, 2, 7, 6 ), ImageMatch.Color() ) );
		Assert.Null( s.CheckImage( needle ) ); // 透過色を使った後も、元の画像は透過色なしのまま
	}

	[Fact]
	public void CheckImage_ImagePath_FoundInsideCapture() {
		using var dir = new TempDir();
		using var needleBmp = Bordered();
		var path = dir.Save( needleBmp );
		using var bmp = Squares( Color.White );
		var s = Capture.FromBitmap( bmp );
		Assert.Equal( new Point( 1, 0 ), s.CheckImage( path ) );
		Assert.Equal( [new Point( 1, 0 ), new Point( 6, 3 )], s.CheckImages( path ) );
		Assert.Equal( [new Point( 1, 0 )], s.CheckImages( path, max: 1 ) );
		Assert.Equal( new Point( 6, 3 ), s.CheckImage( path, new Rectangle( 5, 0, 7, 8 ) ) );
		Assert.Equal( new Point( 1, 0 ), s.CheckImage( path, ImageMatch.Color( 5 ) ) );
		Assert.Equal( [new Point( 6, 3 )], s.CheckImages( path, new Rectangle( 5, 0, 7, 8 ), ImageMatch.Color( 5 ) ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckImages( path, max: 0 ) );
	}

	[Theory]
	[MemberData( nameof( Corners ) )]
	public void CheckImage_ImagePathWithTransColor_IgnoresCornerColor( TransColor corner ) {
		using var dir = new TempDir();
		using var needleBmp = Bordered();
		var path = dir.Save( needleBmp );
		using var bmp = Squares( Color.Blue );
		var s = Capture.FromBitmap( bmp );
		Assert.Null( s.CheckImage( path ) );
		Assert.Equal( new Point( 1, 0 ), s.CheckImage( path, corner ) );
		Assert.Equal( [new Point( 1, 0 ), new Point( 6, 3 )], s.CheckImages( path, corner ) );
		Assert.Equal( new Point( 1, 0 ), s.CheckImage( path, corner, ImageMatch.Color() ) );
		Assert.Equal( [new Point( 6, 3 )], s.CheckImages( path, corner, new Rectangle( 5, 2, 7, 6 ), ImageMatch.Color() ) );
	}

	[Fact]
	public void CheckImage_TransparentPngPixelsIgnored() {
		// 画像ファイルの透明な部分 (アルファ値 128 未満) は比較しない
		using var dir = new TempDir();
		using var needleBmp = Bordered( borderAlpha: 0 );
		var path = dir.Save( needleBmp );
		using var bmp = Squares( Color.Blue );
		var s = Capture.FromBitmap( bmp );
		Assert.Equal( [new Point( 1, 0 ), new Point( 6, 3 )], s.CheckImages( path ) );
		Assert.Equal( new Point( 1, 0 ), s.CheckImage( Capture.FromFile( path ) ) );
	}

	[Fact]
	public void Capture_FromImageFile_OriginIsZero() {
		using var dir = new TempDir();
		using var bmp = Squares( Color.White );
		var path = dir.Save( bmp );
		var s = new ScriptGlobals( _ => { }, CancellationToken.None ).Capture( path );
		Assert.Equal( Point.Empty, s.Origin );
		Assert.Equal( new Rectangle( 0, 0, 12, 8 ), s.Bounds );
		Assert.Equal( Color.FromArgb( 250, 0, 0 ).ToArgb(), s.PeekColor( 2, 1 ).ToArgb() );
		Assert.Equal( new Point( 2, 1 ), s.CheckColor( Color.Red, tolerance: 5 ) );
	}

	public static TheoryData<int, int> Origins => new() { { 100, 50 }, { -1920, -30 } };

	[Theory]
	[MemberData( nameof( Origins ) )]
	public void Origin_OffsetsColorSearch( int ox, int oy ) {
		using var bmp = Sample();
		var s = WithOrigin( bmp, new Point( ox, oy ) );
		Assert.Equal( new Point( ox + 2, oy + 1 ), s.CheckColor( Color.Red, tolerance: 5 ) );
		var all = s.CheckColors( Color.Red, tolerance: 5 );
		Assert.Equal( 8, all.Length );
		Assert.Equal( new Point( ox + 8, oy + 5 ), all[^1] );
		Assert.Equal( (Color.Red, new Point( ox + 2, oy + 1 )), s.CheckColor( [Color.Blue, Color.Red], tolerance: 5 ) );
		Assert.Equal( 8, s.CheckColors( [Color.Blue, Color.Red], tolerance: 5 ).Length );
		// 範囲指定も同じ座標系で受け取る
		Assert.Equal( new Point( ox + 7, oy + 4 ), s.CheckColor( Color.Red, new Rectangle( ox + 5, oy, 5, 6 ), 5 ) );
	}

	[Theory]
	[MemberData( nameof( Origins ) )]
	public void Origin_AreaOutsideCapture_Clipped( int ox, int oy ) {
		using var bmp = Sample();
		var needle = RedSquare();
		var s = WithOrigin( bmp, new Point( ox, oy ) );
		// はみ出した部分は無視する
		var partial = new Rectangle( ox + 5, oy - 10, 100, 100 );
		Assert.Equal( new Point( ox + 7, oy + 4 ), s.CheckColor( Color.Red, partial, 5 ) );
		Assert.Equal( (Color.Red, new Point( ox + 7, oy + 4 )), s.CheckColor( [Color.Red], partial, 5 ) );
		Assert.Equal( new Point( ox + 7, oy + 4 ), s.CheckImage( needle, partial ) );
		// 完全に外れた範囲では見つからない
		var outside = new Rectangle( ox + 20, oy, 5, 5 );
		Assert.Null( s.CheckColor( Color.Red, outside, 5 ) );
		Assert.Empty( s.CheckColors( [Color.Red], outside, 5 ) );
		Assert.Null( s.CheckImage( needle, outside ) );
		Assert.Empty( s.CheckImages( needle, outside ) );
	}

	[Theory]
	[MemberData( nameof( Origins ) )]
	public void Origin_PeekColorOutsideCapture_Throws( int ox, int oy ) {
		using var bmp = Sample();
		var s = WithOrigin( bmp, new Point( ox, oy ) );
		Assert.Equal( Color.White.ToArgb(), s.PeekColor( ox + 9, oy + 5 ).ToArgb() );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.PeekColor( ox - 1, oy ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.PeekColor( ox, oy - 1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.PeekColor( ox + 10, oy ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.PeekColor( ox, oy + 6 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.PeekColor( 0, 0 ) );
	}

	[Fact]
	public void CheckColor_MultipleColors_InvalidArguments() {
		using var bmp = Sample();
		var s = Capture.FromBitmap( bmp );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColors( [Color.Red], max: 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColor( [Color.Red], tolerance: 256 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColors( [Color.Red], tolerance: -1 ) );
		Assert.Throws<ArgumentNullException>( () => s.CheckColor( (Color[])null! ) );
	}

	[Fact]
	public void CheckColor_MultipleColors_PriorityAtRowEnd() {
		// 行末の 1 ピクセルずつ比べる部分でも、両方に一致するピクセルは先に並ぶ色で返す
		using var bmp = new Bitmap( 67, 1 );
		using( var g = Graphics.FromImage( bmp ) ) {
			g.Clear( Color.White );
		}

		for( var x = 64; x < 67; x++ ) {
			bmp.SetPixel( x, 0, Color.FromArgb( 250, 0, 0 ) );
		}

		var s = Capture.FromBitmap( bmp );
		var dark = Color.FromArgb( 240, 0, 0 );
		Assert.Equal( [(Color.Red, new Point( 64, 0 )), (Color.Red, new Point( 65, 0 )), (Color.Red, new Point( 66, 0 ))], s.CheckColors( [Color.Red, dark], tolerance: 10 ) );
		Assert.All( s.CheckColors( [dark, Color.Red], tolerance: 10 ), f => Assert.Equal( dark, f.Color ) );
	}

	// テスト用の一時フォルダ。破棄すると中身ごと消す
	private sealed class TempDir : IDisposable {
		private readonly string _path = Directory.CreateTempSubdirectory( "CsWSC_Test_" ).FullName;

		public string Save( Bitmap bmp ) {
			var path = Path.Combine( _path, $"{Guid.NewGuid():N}.png" );
			bmp.Save( path, System.Drawing.Imaging.ImageFormat.Png );
			return path;
		}

		public void Dispose() {
			try { Directory.Delete( _path, true ); } catch( IOException ) { }
		}
	}

	[Fact]
	public void ToBitmap_RoundTrips() {
		using var bmp = Sample();
		using var copy = Capture.FromBitmap( bmp ).ToBitmap();
		Assert.Equal( bmp.GetPixel( 3, 2 ).ToArgb(), copy.GetPixel( 3, 2 ).ToArgb() );
	}
}
