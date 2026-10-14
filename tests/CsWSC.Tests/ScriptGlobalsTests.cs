namespace CsWSC.Tests;

// 画面をキャプチャする前に引数を確かめる (または空を返す) ので、画面に依存しない
public class ScriptGlobalsTests {
	private static ScriptGlobals Globals() => new( _ => { }, CancellationToken.None );

	private static Capture RedSquare() {
		using var bmp = new Bitmap( 2, 2 );
		using( var g = Graphics.FromImage( bmp ) ) {
			g.Clear( Color.Red );
		}

		return Capture.FromBitmap( bmp );
	}

	[Fact]
	public void CheckColor_InvalidArguments_Throw() {
		var s = Globals();
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColor( Color.Red, tolerance: 256 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColors( Color.Red, tolerance: -1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColors( Color.Red, max: 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColor( [Color.Red], tolerance: 256 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckColors( [Color.Red], max: 0 ) );
		Assert.Throws<ArgumentNullException>( () => s.CheckColors( (Color[])null! ) );
	}

	[Fact]
	public void CheckColor_NothingToSearch_ReturnsEmpty() {
		var s = Globals();
		Assert.Null( s.CheckColor( Array.Empty<Color>() ) );
		Assert.Empty( s.CheckColors( Array.Empty<Color>() ) );
		Assert.Null( s.CheckColor( Color.Red, new Rectangle( 10, 10, 0, 10 ) ) );
		Assert.Empty( s.CheckColors( [Color.Red], new Rectangle( 10, 10, 10, 0 ) ) );
	}

	[Fact]
	public void CheckImage_InvalidArguments_Throw() {
		var s = Globals();
		var needle = RedSquare();
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckImages( needle, max: 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => ImageMatch.Shape( 0 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => s.CheckImage( needle, tolerance: 256 ) );
	}

	[Fact]
	public void ImageMatch_Initializer_SetsAndValidates() {
		var m = new ColorMatch { Tolerance = 10, Max = 1 };
		Assert.Equal( (10, 1), (m.Tolerance, m.Max) );
		Assert.Equal( 0.8, ( ImageMatch.Gray() with { Threshold = 0.8 } ).Threshold );
		Assert.Throws<ArgumentOutOfRangeException>( () => new ColorMatch { Tolerance = 256 } );
		Assert.Throws<ArgumentOutOfRangeException>( () => ImageMatch.Color() with { Max = 0 } );
		Assert.Throws<ArgumentOutOfRangeException>( () => new EdgeMatch { Threshold = 1.5 } );
	}

	[Fact]
	public void CheckImage_AreaSmallerThanImage_NotFound() {
		var s = Globals();
		var needle = RedSquare();
		Assert.Null( s.CheckImage( needle, new Rectangle( 0, 0, 1, 10 ) ) );
		Assert.Empty( s.CheckImages( needle, TransColor.TopLeft, new Rectangle( 0, 0, 10, 1 ) ) );
	}

	[Fact]
	public void Capture_EmptyArea_Throws() {
		var s = Globals();
		Assert.Throws<ArgumentException>( () => s.Capture( new Rectangle( 0, 0, 0, 5 ) ) );
		Assert.Throws<ArgumentException>( () => s.Capture( new Rectangle( 0, 0, 5, 0 ) ) );
	}
}
