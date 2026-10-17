using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CsWSC;

/// <summary>画面キャプチャと画像検索です (UWSC の SAVEIMG / CHKIMG)。照合方法は <see cref="ImageMatch"/> が受け持ちます。</summary>
internal static class ScreenImage {
	/// <summary>設定するとキャプチャした範囲に枠を表示します (デバッグ用)。</summary>
	internal static ImageAreaFrame? Frame { get; set; }

	/// <summary>画面の 1 ピクセルの色を取得します (PEEKCOLOR)。枠表示が有効ならそのピクセルを枠で囲みます。色を取れない位置 (画面の外など) なら例外です。</summary>
	/// <param name="point">位置です (画面座標)。</param>
	public static Color Peek( Point point ) {
		var hdc = NativeMethods.GetDC( IntPtr.Zero );
		try {
			var bgr = NativeMethods.GetPixel( hdc, point.X, point.Y );
			if( bgr == 0xFFFFFFFF ) { // CLR_INVALID: 画面の外など、色を取れない位置
				throw new ArgumentOutOfRangeException( nameof( point ), point, $"画面 (スクリーン座標 {point.X}, {point.Y}) の色を取得できません。画面の外を指定していないか確認してください。" );
			}

			Frame?.Show( new Rectangle( point, new Size( 1, 1 ) ) );
			return Color.FromArgb( (int)( bgr & 0xFF ), (int)( ( bgr >> 8 ) & 0xFF ), (int)( ( bgr >> 16 ) & 0xFF ) );
		} finally {
			NativeMethods.ReleaseDC( IntPtr.Zero, hdc );
		}
	}

	/// <summary>画面の範囲をキャプチャします。枠は取り込みに写りません。枠表示が有効なら範囲に枠を出します。</summary>
	/// <param name="area">キャプチャする範囲です (画面座標)。</param>
	public static Bitmap Capture( Rectangle area ) {
		var bmp = new Bitmap( area.Width, area.Height, PixelFormat.Format32bppArgb );
		using var g = Graphics.FromImage( bmp );
		g.CopyFromScreen( area.Location, Point.Empty, area.Size );
		Frame?.Show( area );
		return bmp;
	}

	/// <summary>画面の範囲をキャプチャしてファイルに保存します。範囲の大きさが 0 なら例外です。</summary>
	public static void Save( string path, Rectangle area ) {
		if( area.Width <= 0 || area.Height <= 0 ) {
			throw new ArgumentException( $"保存する範囲の大きさが 0 です ({area.Width}x{area.Height})。ウィンドウが最小化されていないか確認してください。", nameof( area ) );
		}

		using var bmp = Capture( area );
		SaveBitmap( bmp, path );
	}

	/// <summary>画像をファイルに保存します。形式は拡張子で決まります。</summary>
	internal static void SaveBitmap( Bitmap bmp, string path ) {
		var format = System.IO.Path.GetExtension( path ).ToLowerInvariant() switch {
			".jpg" or ".jpeg" => ImageFormat.Jpeg,
			".gif" => ImageFormat.Gif,
			".png" => ImageFormat.Png,
			_ => ImageFormat.Bmp,
		};
		bmp.Save( path, format );
	}

	/// <summary>
	/// area 内で画像を探し、見つかれば左上のスクリーン座標を返します。
	/// transColor を指定すると、画像のその隅と同じ色の部分を比較しません。
	/// area が画像より小さい (最小化中のウィンドウなど) 場合は null を返します。
	/// </summary>
	public static Point? Find( string imagePath, Rectangle area, ImageMatch match, TransColor transColor = TransColor.None ) =>
		FindAll( imagePath, area, match, transColor, 1 ) is [var p, ..] ? p : null;

	/// <summary><see cref="Find(string, Rectangle, ImageMatch, TransColor)"/> の Capture 版です。</summary>
	public static Point? Find( Capture capture, Rectangle area, ImageMatch match, TransColor transColor = TransColor.None ) =>
		FindAll( capture, area, match, transColor, 1 ) is [var p, ..] ? p : null;

	/// <summary>
	/// area 内で画像を全て探し、左上のスクリーン座標を上から順 (同じ行は左から順) に返します。
	/// 既に見つかった位置と重なる位置は返しません。max 件 (<see cref="PixelMatch.Max"/> の方が少なければその件数) に切り詰めます。
	/// Color・Shape は max 件見つかった時点で探すのをやめ、Gray・Edge は全体を探してから上から順に切り詰めます。
	/// area が画像より小さい (最小化中のウィンドウなど) 場合は空の配列を返します。
	/// </summary>
	public static Point[] FindAll( string imagePath, Rectangle area, ImageMatch match, TransColor transColor = TransColor.None, int max = int.MaxValue ) {
		return FindAll( LoadNeedle( imagePath, transColor ), area, match, max );
	}

	/// <summary><see cref="FindAll(string, Rectangle, ImageMatch, TransColor, int)"/> の Capture 版です。パスのキャッシュは使わず、検索用画像は Capture が持ちます。</summary>
	public static Point[] FindAll( Capture capture, Rectangle area, ImageMatch match, TransColor transColor = TransColor.None, int max = int.MaxValue ) =>
		FindAll( capture.ToNeedle( transColor ), area, match, max );

	private static Point[] FindAll( NeedleImage needle, Rectangle area, ImageMatch match, int max ) {
		ValidateMax( max );
		// 範囲が画像より小さい (最小化中のウィンドウで空など) なら見つからない扱い
		if( area.Width < needle.Width || area.Height < needle.Height ) {
			return [];
		}

		var found = Search( needle, CapturePixels( area ), area.Width, area.Height, match, max );
		return Result( found, area, needle.Width, needle.Height );
	}

	/// <summary>
	/// area 内で指定の色を探し、見つかったスクリーン座標を上から順 (同じ行は左から順) に返します。max 個見つかった時点で打ち切ります。
	/// area が空 (最小化中のウィンドウなど) なら空の配列を返します。
	/// </summary>
	public static Point[] FindColor( Color color, Rectangle area, int tolerance, int max = int.MaxValue ) {
		ValidateTolerance( tolerance );
		ValidateMax( max );
		if( area.Width <= 0 || area.Height <= 0 ) {
			return [];
		}

		var found = SearchColor( CapturePixels( area ), area.Width, new Rectangle( 0, 0, area.Width, area.Height ), color, tolerance, max );
		return Result( found, area, 1, 1 );
	}

	/// <summary>
	/// ピクセル h (幅 hw) の範囲 r の中で指定の色を探し、見つかった位置 (h 内の座標) を上から順 (同じ行は左から順) に返します。
	/// アルファ値は見ません。1 行ずつ SIMD でまとめて比べます。
	/// </summary>
	internal static List<Point> SearchColor( int[] h, int hw, Rectangle r, Color color, int tolerance, int max = int.MaxValue ) {
		ValidateTolerance( tolerance );
		ValidateMax( max );
		var found = new List<Point>();
		var target = color.ToArgb();
		var lanes = System.Numerics.Vector<int>.Count;
		var probe = System.Numerics.Vector.AsVectorByte( new System.Numerics.Vector<int>( target ) );
		var tol = new System.Numerics.Vector<byte>( (byte)tolerance );
		// アルファ値の差は見ない
		var alpha = new System.Numerics.Vector<int>( unchecked((int)0xFF000000) );
		var all = new System.Numerics.Vector<int>( -1 );
		for( var y = r.Top; y < r.Bottom; y++ ) {
			var row = y * hw;
			var x = r.Left;
			for( ; x <= r.Right - lanes; x += lanes ) {
				// 各バイト (B・G・R) の差が許容差以下か。アルファは OR で常に一致扱いにし、B・G・R の 3 バイト全てが以下なら一致
				var v = System.Numerics.Vector.AsVectorByte( new System.Numerics.Vector<int>( h, row + x ) );
				var near = System.Numerics.Vector.LessThanOrEqual( System.Numerics.Vector.Max( v, probe ) - System.Numerics.Vector.Min( v, probe ), tol );
				var hit = System.Numerics.Vector.Equals( System.Numerics.Vector.AsVectorInt32( near ) | alpha, all );
				if( hit == System.Numerics.Vector<int>.Zero ) {
					continue;
				}

				for( var j = 0; j < lanes; j++ ) {
					if( hit[j] != 0 ) {
						found.Add( new Point( x + j, y ) );
						if( found.Count >= max ) {
							return found;
						}
					}
				}
			}

			for( ; x < r.Right; x++ ) {
				var c = h[row + x];
				if( Math.Abs( ( ( c >> 16 ) & 0xFF ) - color.R ) <= tolerance
					&& Math.Abs( ( ( c >> 8 ) & 0xFF ) - color.G ) <= tolerance
					&& Math.Abs( ( c & 0xFF ) - color.B ) <= tolerance ) {
					found.Add( new Point( x, y ) );
					if( found.Count >= max ) {
						return found;
					}
				}
			}
		}

		return found;
	}

	/// <summary>
	/// area 内でいずれかの色を探し、一致した色 (colors のうち先に並ぶもの) とスクリーン座標を上から順 (同じ行は左から順) に返します。
	/// max 個見つかった時点で打ち切ります。area が空なら空の配列を返します。
	/// </summary>
	public static (Color Color, Point Point)[] FindColors( Color[] colors, Rectangle area, int tolerance, int max = int.MaxValue ) {
		ArgumentNullException.ThrowIfNull( colors );
		ValidateTolerance( tolerance );
		ValidateMax( max );
		if( area.Width <= 0 || area.Height <= 0 || colors.Length == 0 ) {
			return [];
		}

		var found = SearchColors( CapturePixels( area ), area.Width, new Rectangle( 0, 0, area.Width, area.Height ), colors, tolerance, max );
		var points = Result( found.ConvertAll( f => f.Point ), area, 1, 1 );
		return found.Select( ( f, i ) => (f.Color, points[i]) ).ToArray();
	}

	/// <summary>
	/// ピクセル h (幅 hw) の範囲 r の中でいずれかの色を探し、一致した色 (colors のうち先に並ぶもの) と位置 (h 内の座標) を
	/// 上から順 (同じ行は左から順) に返します。アルファ値は見ません。1 行ずつ SIMD でまとめて比べます。
	/// </summary>
	internal static List<(Color Color, Point Point)> SearchColors( int[] h, int hw, Rectangle r, Color[] colors, int tolerance, int max = int.MaxValue ) {
		ArgumentNullException.ThrowIfNull( colors );
		ValidateTolerance( tolerance );
		ValidateMax( max );
		var found = new List<(Color, Point)>();
		if( colors.Length == 0 ) {
			return found;
		}

		var lanes = System.Numerics.Vector<int>.Count;
		var probes = Array.ConvertAll( colors, c => System.Numerics.Vector.AsVectorByte( new System.Numerics.Vector<int>( c.ToArgb() ) ) );

		var tol = new System.Numerics.Vector<byte>( (byte)tolerance );
		// アルファ値の差は見ない
		var alpha = new System.Numerics.Vector<int>( unchecked((int)0xFF000000) );
		var all = new System.Numerics.Vector<int>( -1 );
		for( var y = r.Top; y < r.Bottom; y++ ) {
			var row = y * hw;
			var x = r.Left;
			for( ; x <= r.Right - lanes; x += lanes ) {
				// どれか 1 色にでも近いピクセルがあるかをまとめて調べ、あった列だけどの色かを調べる
				var v = System.Numerics.Vector.AsVectorByte( new System.Numerics.Vector<int>( h, row + x ) );
				var any = System.Numerics.Vector<int>.Zero;
				foreach( var probe in probes ) {
					var near = System.Numerics.Vector.LessThanOrEqual( System.Numerics.Vector.Max( v, probe ) - System.Numerics.Vector.Min( v, probe ), tol );
					any |= System.Numerics.Vector.Equals( System.Numerics.Vector.AsVectorInt32( near ) | alpha, all );
				}

				if( any == System.Numerics.Vector<int>.Zero ) {
					continue;
				}

				for( var j = 0; j < lanes; j++ ) {
					if( any[j] != 0 && Add( h[row + x + j], x + j, y ) ) {
						return found;
					}
				}
			}

			for( ; x < r.Right; x++ ) {
				if( Add( h[row + x], x, y ) ) {
					return found;
				}
			}
		}

		return found;

		// 一致する最初の色で加える。max 個に達したら true
		bool Add( int c, int px, int py ) {
			foreach( var color in colors ) {
				if( Math.Abs( ( ( c >> 16 ) & 0xFF ) - color.R ) <= tolerance
					&& Math.Abs( ( ( c >> 8 ) & 0xFF ) - color.G ) <= tolerance
					&& Math.Abs( ( c & 0xFF ) - color.B ) <= tolerance ) {
					found.Add( (color, new Point( px, py )) );
					return found.Count >= max;
				}
			}

			return false;
		}
	}

	internal static void ValidateMax( int max ) {
		if( max < 1 ) {
			throw new ArgumentOutOfRangeException( nameof( max ), max, "件数の上限は 1 以上で指定してください。" );
		}
	}

	private static void ValidateTolerance( int tolerance ) {
		if( tolerance is < 0 or > 255 ) {
			throw new ArgumentOutOfRangeException( nameof( tolerance ), tolerance, "許容差は 0〜255 で指定してください。" );
		}
	}

	/// <summary>
	/// 画面の範囲をキャプチャしてピクセル (ARGB。アルファ値は不定) を返します。枠は表示しません。
	/// Bitmap を介さず、DIB セクションに BitBlt して直接配列にコピーします (CopyFromScreen + LockBits より速い)。
	/// </summary>
	internal static int[] CapturePixels( Rectangle area ) {
		var screen = NativeMethods.GetDC( IntPtr.Zero );
		var dc = NativeMethods.CreateCompatibleDC( screen );
		// biHeight を負にすると上から下の順に並ぶ
		var header = new NativeMethods.BITMAPINFOHEADER {
			biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
			biWidth = area.Width,
			biHeight = -area.Height,
			biPlanes = 1,
			biBitCount = 32,
		};
		var bitmap = NativeMethods.CreateDIBSection( dc, ref header, NativeMethods.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0 );
		try {
			if( bitmap == IntPtr.Zero ) {
				throw new InvalidOperationException( $"画面キャプチャ用のビットマップを作れませんでした ({area.Width}x{area.Height})。" );
			}

			var old = NativeMethods.SelectObject( dc, bitmap );
			// CopyFromScreen と同じく SRCCOPY のみ (CAPTUREBLT を付けるとカーソルがちらつくことがある)
			if( !NativeMethods.BitBlt( dc, 0, 0, area.Width, area.Height, screen, area.X, area.Y, NativeMethods.SRCCOPY ) ) {
				throw new InvalidOperationException( "画面をキャプチャできませんでした。" );
			}

			NativeMethods.SelectObject( dc, old );
			var result = new int[area.Width * area.Height];
			Marshal.Copy( bits, result, 0, result.Length );
			return result;
		} finally {
			if( bitmap != IntPtr.Zero ) {
				NativeMethods.DeleteObject( bitmap );
			}

			NativeMethods.DeleteDC( dc );
			NativeMethods.ReleaseDC( IntPtr.Zero, screen );
		}
	}

	/// <summary>画面のピクセル h (幅 hw・高さ hh) の中で画像を探し、見つかった左上の位置 (h 内の座標) を返します。</summary>
	internal static List<Point> Search( NeedleImage needle, int[] h, int hw, int hh, ImageMatch match, int max = int.MaxValue ) {
		ValidateMax( max );
		return match.Search( needle, new HayImage( h, hw, hh ), max );
	}

	/// <summary>見つかった位置をスクリーン座標にし、枠表示が有効なら検索範囲と見つかった範囲に枠を出します。</summary>
	private static Point[] Result( List<Point> found, Rectangle area, int w, int h ) {
		var points = found.Select( p => new Point( p.X + area.X, p.Y + area.Y ) ).ToArray();
		Frame?.Show( area, points.Select( p => new Rectangle( p, new Size( w, h ) ) ).ToArray() );
		return points;
	}

	private static readonly Dictionary<string, NeedleImage> NeedleCache = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// 検索用画像を読み込みます。パス・更新日時が同じならキャッシュを使います。
	/// キャッシュは透過色なしで持ち、透過色はピクセルを共有したまま指定します。
	/// </summary>
	internal static NeedleImage LoadNeedle( string imagePath, TransColor transColor = TransColor.None ) {
		var fullPath = System.IO.Path.GetFullPath( imagePath );
		var lastWrite = System.IO.File.GetLastWriteTimeUtc( fullPath );
		NeedleImage? needle;
		lock( NeedleCache ) {
			if( !NeedleCache.TryGetValue( fullPath, out needle ) || needle.LastWrite != lastWrite ) {
				needle = null;
			}
		}

		if( needle is null ) {
			using var file = new Bitmap( fullPath );
			needle = CreateNeedle( file, TransColor.None, lastWrite );
			lock( NeedleCache ) {
				NeedleCache[fullPath] = needle;
			}
		}

		return needle.WithTransColor( transColor );
	}

	/// <summary>Bitmap から検索用画像を作ります。transColor の隅と同じ色の部分は比較しません。</summary>
	internal static NeedleImage CreateNeedle( Bitmap file, TransColor transColor = TransColor.None, DateTime lastWrite = default ) {
		using var bmp = file.Clone( new Rectangle( Point.Empty, file.Size ), PixelFormat.Format32bppArgb );
		return CreateNeedle( Pixels( bmp ), bmp.Width, bmp.Height, transColor, lastWrite );
	}

	/// <summary>ピクセル (ARGB) から検索用画像を作ります。pixels は書き換えないので、複製せずに渡して構いません。</summary>
	internal static NeedleImage CreateNeedle( int[] pixels, int width, int height, TransColor transColor = TransColor.None, DateTime lastWrite = default ) =>
		new NeedleImage( width, height, pixels, lastWrite ).WithTransColor( transColor );

	/// <summary>Bitmap のピクセルを ARGB の配列 (上から下・左から右の順) で返します。</summary>
	internal static int[] Pixels( Bitmap bmp ) {
		var data = bmp.LockBits( new Rectangle( Point.Empty, bmp.Size ), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb );
		try {
			var result = new int[bmp.Width * bmp.Height];
			for( var y = 0; y < bmp.Height; y++ ) {
				Marshal.Copy( data.Scan0 + y * data.Stride, result, y * bmp.Width, bmp.Width );
			}

			return result;
		} finally { bmp.UnlockBits( data ); }
	}
}

/// <summary>
/// 検索用画像です (ARGB)。どの照合方法にも依存しません。ピクセルは書き換えません。
/// 透過色は <see cref="TransKey"/> に持ち、比べるピクセルの一覧 (<see cref="CompareTargets"/>) を作るときに除外します。
/// </summary>
internal sealed record NeedleImage( int Width, int Height, int[] Pixels, DateTime LastWrite ) {
	/// <summary>透明として扱う色です (RGB)。透過色なしなら -1 です (どの色とも一致しない)。</summary>
	public int TransKey { get; private init; } = -1;

	// 透過色ごとの検索用画像 (TransColor の値が添字)。同じものを返して、下の計算結果を使い回す
	private readonly NeedleImage?[] _variants = new NeedleImage?[5];
	private Targets? _targets;
	// 別のスレッドから読まれても幅とずれが食い違わないよう、参照 1 つで差し替える
	private sealed record OffsetCache( int Hw, int[] Offsets );
	private OffsetCache? _offsets;

	/// <summary>
	/// 指定した隅の色を透過色にした検索用画像を返します。ピクセルは共有します。
	/// 同じ透過色なら同じものを返すので、比べるピクセルの一覧などは 1 回だけ作られます。
	/// </summary>
	public NeedleImage WithTransColor( TransColor corner ) {
		if( corner == TransColor.None || TransKey >= 0 ) {
			return corner == TransColor.None ? this : throw new InvalidOperationException( "透過色を指定済みの検索用画像には透過色を指定できません。" );
		}

		var index = (int)corner;
		if( index is < 0 or >= 5 ) {
			throw new ArgumentOutOfRangeException( nameof( corner ) );
		}

		return _variants[index] ??= new NeedleImage( Width, Height, Pixels, LastWrite ) {
			TransKey = Pixels[corner switch {
				TransColor.TopLeft => 0,
				TransColor.TopRight => Width - 1,
				TransColor.BottomLeft => ( Height - 1 ) * Width,
				TransColor.BottomRight => Height * Width - 1,
				_ => throw new ArgumentOutOfRangeException( nameof( corner ) ),
			}] & 0xFFFFFF,
		};
	}

	/// <summary>比べるピクセルです (透明・透過色を除く)。初めて使うときに 1 回だけ作ります。</summary>
	public Targets CompareTargets => _targets ??= Targets.Create( this );

	/// <summary>比べるピクセルの、幅 hw の画面上での左上からのずれです。直前と同じ幅なら作り直しません。</summary>
	public int[] OffsetsFor( int hw ) {
		if( _offsets is { } o && o.Hw == hw ) {
			return o.Offsets;
		}

		var t = CompareTargets;
		var offsets = new int[t.Colors.Length];
		for( var i = 0; i < offsets.Length; i++ ) {
			offsets[i] = t.Ys[i] * hw + t.Xs[i];
		}

		_offsets = new OffsetCache( hw, offsets );
		return offsets;
	}

	/// <summary>
	/// 比べるピクセルの色と位置、ピクセルごとに比べるかどうか (Mask)、最初の 1 つの位置、重複を除いた色 (とその最初の位置) です。
	/// 透過色はここでだけ判定します。照合方法は Mask や一覧を使い、透過色を意識しません。
	/// </summary>
	internal sealed record Targets( int[] Colors, int[] Xs, int[] Ys, bool[] Mask, int First, int[] DistinctIndices ) {
		public static Targets Create( NeedleImage n ) {
			var mask = new bool[n.Pixels.Length];
			var count = 0;
			for( var i = 0; i < mask.Length; i++ ) {
				var c = n.Pixels[i];
				if( Opaque( c ) && ( c & 0xFFFFFF ) != n.TransKey ) {
					mask[i] = true;
					count++;
				}
			}

			var (colors, xs, ys) = (new int[count], new int[count], new int[count]);
			var distinct = new List<int>();
			var seen = new HashSet<int>();
			var k = 0;
			for( var i = 0; i < n.Pixels.Length; i++ ) {
				if( !mask[i] ) {
					continue;
				}

				var c = n.Pixels[i];
				(colors[k], xs[k], ys[k]) = (c, i % n.Width, i / n.Width);
				k++;
				if( seen.Add( c & 0xFFFFFF ) ) {
					distinct.Add( i );
				}
			}

			return new Targets( colors, xs, ys, mask, count > 0 ? ys[0] * n.Width + xs[0] : -1, [.. distinct] );
		}
	}

	/// <summary>アルファ値 128 未満のピクセルは透明とみなし、比較しません。透過色は <see cref="Targets.Create"/> で判定します。</summary>
	public static bool Opaque( int c ) => ( c >>> 24 ) >= 128;
}

/// <summary>検索対象の画面のピクセルです (ARGB)。</summary>
internal readonly record struct HayImage( int[] Pixels, int Width, int Height );
