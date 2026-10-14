using System.Numerics;

namespace CsWSC;

/// <summary>
/// <see cref="ScriptGlobals.CheckImage(string, TransColor, Rectangle, ImageMatch)"/> の照合方法です。
/// <see cref="Color"/> / <see cref="Shape"/> / <see cref="Gray"/> / <see cref="Edge"/> で作ります。
/// </summary>
/// <example>
/// <code>
/// CheckImage(@"C:\img\icon.png", ImageMatch.Shape());
/// CheckImage(@"C:\img\btn.png", new Rectangle(0, 0, 800, 600), ImageMatch.Gray(0.9));
/// </code>
/// </example>
/// <remarks>外部から派生したクラスは作れません (検索処理が internal のため)。</remarks>
public abstract record ImageMatch {
	/// <summary>色で照合します。RGB 各成分の差が <paramref name="tolerance"/> 以下なら一致です (UWSC の CHKIMG)。</summary>
	/// <param name="tolerance">RGB 各成分 (0〜255) の許容差です。0 で完全一致です。</param>
	/// <param name="max"><c>CheckImages</c> で返す件数の上限です。上から順に探し、見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	public static ColorMatch Color( int tolerance = 0, int max = int.MaxValue ) => new( tolerance, max );

	/// <summary>
	/// 色を無視して形で照合します (UWSC の CHKIMG の透過色 -1)。
	/// 画像の各色が画面上の色と 1 対 1 に対応していれば一致です (同じ色の部分は同じ色、違う色の部分は違う色)。
	/// 色は各成分の下位 3 ビットを無視して比べます。
	/// </summary>
	/// <param name="max"><c>CheckImages</c> で返す件数の上限です。上から順に探し、見つかった時点で探すのをやめます。省略すると全てです。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> が 1 未満です。</exception>
	public static ShapeMatch Shape( int max = int.MaxValue ) => new( max );

	/// <summary>
	/// 明るさ (グレースケール) の相関で照合します。画像全体が明るく・暗くなったり、コントラストが変わったりしても一致します。
	/// 縮小した画像で候補を探しますが、<see cref="Color"/> より遅いです。
	/// </summary>
	/// <param name="threshold">一致とみなす相関係数です (0〜1)。1 に近いほど厳しくなります。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> が 0〜1 の範囲外です。</exception>
	public static GrayMatch Gray( double threshold = CorrelationMatch.DefaultThreshold ) => new( threshold );

	/// <summary>
	/// 輪郭 (明るさの変化の強さ) の相関で照合します。色や明るさが変わったり反転したりしても、輪郭が同じなら一致します。
	/// 画像の外周 1 ピクセルは比較しません。<see cref="Gray"/> よりさらに遅いです。
	/// </summary>
	/// <param name="threshold">一致とみなす相関係数です (0〜1)。1 に近いほど厳しくなります。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> が 0〜1 の範囲外です。</exception>
	public static EdgeMatch Edge( double threshold = CorrelationMatch.DefaultThreshold ) => new( threshold );

	/// <summary>画面の中で画像を探し、見つかった左上の位置 (画面内の座標) を上から順 (同じ行は左から順) に返します。</summary>
	internal abstract List<Point> Search( NeedleImage needle, HayImage hay, int max );

	/// <summary>既に見つかった位置と重なるかどうかです。</summary>
	private protected static bool Overlaps( List<Point> found, int x, int y, int w, int h ) {
		foreach( var p in found ) {
			if( Math.Abs( p.X - x ) < w && Math.Abs( p.Y - y ) < h ) {
				return true;
			}
		}

		return false;
	}
}

/// <summary>ピクセルを一つずつ比べて一致・不一致を決める照合方法です。一部のピクセルを先に比べて候補を絞ります。</summary>
public abstract record PixelMatch : ImageMatch {
	private protected PixelMatch( int max ) => Max = max;

	/// <summary><c>CheckImages</c> で返す件数の上限です。見つかった時点で探すのをやめます。</summary>
	/// <exception cref="ArgumentOutOfRangeException">1 未満を設定しました。</exception>
	public int Max {
		get;
		init {
			ScreenImage.ValidateMax( value );
			field = value;
		}
	}

	/// <summary>1 回の検索で使う、画像ごとに準備した照合処理です。</summary>
	/// <remarks>画面の全位置で呼ばれるので、構造体にしてジェネリクスで呼び出しをインライン化させます。</remarks>
	private protected interface IMatcher {
		/// <summary>位置 (x, y) が候補になりうるかどうかです (安く調べる)。</summary>
		bool IsCandidate( int[] h, int hw, int x, int y );

		/// <summary>位置 (x, y) で画像全体が一致するかどうかです。</summary>
		bool Matches( int[] h, int hw, int x, int y );
	}

	private protected static List<Point> Scan<TMatcher>( TMatcher matcher, NeedleImage needle, HayImage hay, int max ) where TMatcher : struct, IMatcher {
		var (h, hw, hh) = hay;
		int nw = needle.Width, nh = needle.Height;
		var found = new List<Point>();
		for( var y = 0; y <= hh - nh; y++ ) {
			for( var x = 0; x <= hw - nw; x++ ) {
				if( !matcher.IsCandidate( h, hw, x, y ) || Overlaps( found, x, y, nw, nh ) || !matcher.Matches( h, hw, x, y ) ) {
					continue;
				}

				found.Add( new Point( x, y ) );
				if( found.Count >= max ) {
					return found;
				}
			}
		}

		return found;
	}

}

/// <summary>色で照合します。作り方は <see cref="ImageMatch.Color"/> です。</summary>
public sealed record ColorMatch : PixelMatch {
	/// <param name="tolerance">RGB 各成分 (0〜255) の許容差です。0 で完全一致です。</param>
	/// <param name="max">返す件数の上限です。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="tolerance"/> が 0〜255 の範囲外、または <paramref name="max"/> が 1 未満です。</exception>
	public ColorMatch( int tolerance = 0, int max = int.MaxValue ) : base( max ) => Tolerance = tolerance;

	/// <summary>RGB 各成分 (0〜255) の許容差です。0 で完全一致です。</summary>
	/// <exception cref="ArgumentOutOfRangeException">0〜255 の範囲外を設定しました。</exception>
	public int Tolerance {
		get;
		init => field = value is >= 0 and <= 255 ? value
			: throw new ArgumentOutOfRangeException( nameof( Tolerance ), value, "許容差は 0〜255 で指定してください。" );
	}

	/// <summary>
	/// 比べるピクセルのうち画面上で少ない色の 1 つ (probe。<see cref="RarestPixel"/> で選ぶ) を先に比べて候補を絞ります。probe の比較は SIMD で 1 行ずつまとめて行い、
	/// 合った位置だけ画像全体を比べます。
	/// </summary>
	internal override List<Point> Search( NeedleImage needle, HayImage image, int max ) {
		max = Math.Min( max, Max );
		var matcher = new ColorMatcher( needle, Tolerance, image );
		if( matcher.Probe < 0 ) {
			return Scan( matcher, needle, image, max ); // 比べるピクセルが無い (全て透明・透過色)
		}

		var (h, hw, hh) = image;
		int nw = needle.Width, nh = needle.Height;
		var found = new List<Point>();
		var lanes = Vector<int>.Count;
		var probe = Vector.AsVectorByte( new Vector<int>( needle.Pixels[matcher.Probe] ) );
		var tolerance = new Vector<byte>( (byte)Tolerance );
		// アルファ値の差は見ない
		var alpha = new Vector<int>( unchecked((int)0xFF000000) );
		for( var y = 0; y <= hh - nh; y++ ) {
			var row = ( y + matcher.ProbeY ) * hw + matcher.ProbeX;
			var x = 0;
			for( ; x <= hw - nw - lanes + 1; x += lanes ) {
				// 各バイト (B・G・R) の差が許容差以下か。アルファは OR で常に一致扱いにし、B・G・R の 3 バイト全てが以下なら候補
				var v = Vector.AsVectorByte( new Vector<int>( h, row + x ) );
				var near = Vector.LessThanOrEqual( Vector.Max( v, probe ) - Vector.Min( v, probe ), tolerance );
				var hit = Vector.Equals( Vector.AsVectorInt32( near ) | alpha, new Vector<int>( -1 ) );
				if( hit == Vector<int>.Zero ) {
					continue;
				}

				for( var j = 0; j < lanes; j++ ) {
					if( hit[j] != 0 && Accept( matcher, h, hw, x + j, y, nw, nh, found ) && found.Count >= max ) {
						return found;
					}
				}
			}

			for( ; x <= hw - nw; x++ ) {
				if( matcher.IsCandidate( h, hw, x, y ) && Accept( matcher, h, hw, x, y, nw, nh, found ) && found.Count >= max ) {
					return found;
				}
			}
		}

		return found;
	}

	/// <summary>(x, y) で画像全体が一致し、既に見つかった位置と重ならなければ found に加えます。</summary>
	private static bool Accept( in ColorMatcher matcher, int[] h, int hw, int x, int y, int nw, int nh, List<Point> found ) {
		if( Overlaps( found, x, y, nw, nh ) || !matcher.Matches( h, hw, x, y ) ) {
			return false;
		}

		found.Add( new Point( x, y ) );
		return true;
	}

	// RarestPixel で色を数える表。検索のたびに確保しないよう、スレッドごとに使い回す
	[ThreadStatic] private static int[]? _counts;

	/// <summary>
	/// 比べるピクセル (透明・透過色を除く) のうち、探す範囲で最も少ない色 (許容差の範囲を含む) のものの位置を返します (比べるピクセルが無ければ -1)。
	/// 候補を絞る probe に使います。よくある色 (背景など) を probe にすると候補が増えて遅くなるためです。
	/// </summary>
	/// <remarks>
	/// 範囲は間引いて読み、色は各成分の上位 5 ビット (32768 種類) で数えます。
	/// 許容差が大きく数える種類が多すぎるときや、範囲が狭い (画像の 64 倍未満の) ときは、最初の比べるピクセルを返します。
	/// </remarks>
	private static int RarestPixel( NeedleImage needle, int tol, HayImage hay ) {
		var targets = needle.CompareTargets;
		var first = targets.First;
		var reach = ( tol + 7 ) >> 3; // 許容差の範囲が何区分にまたがるか (片側)
		var span = 2 * reach + 1;
		var h = hay.Pixels;
		// 範囲が狭ければ候補も少ないので、色を数える手間のほうが大きい
		if( first < 0 || span * span * span > 512 || h.Length < 64 * needle.Pixels.Length ) {
			return first;
		}

		var counts = _counts ??= new int[1 << 15]; // 使い終わったら数えた区分だけ 0 に戻すので、常に全て 0
		var step = Math.Max( 1, h.Length / 65536 ) | 1; // 約 65536 ピクセル。奇数にして行の並びと揃いにくくする
		for( var i = 0; i < h.Length; i += step ) {
			counts[Bin( h[i] )]++;
		}

		var best = first;
		var bestScore = long.MaxValue;
		foreach( var i in targets.DistinctIndices ) {
			var c = needle.Pixels[i];
			int r = ( c >> 19 ) & 0x1F, g = ( c >> 11 ) & 0x1F, b = ( c >> 3 ) & 0x1F;
			long score = 0;
			for( var dr = Math.Max( 0, r - reach ); dr <= Math.Min( 31, r + reach ); dr++ ) {
				for( var dg = Math.Max( 0, g - reach ); dg <= Math.Min( 31, g + reach ); dg++ ) {
					for( var db = Math.Max( 0, b - reach ); db <= Math.Min( 31, b + reach ); db++ ) {
						score += counts[( dr << 10 ) | ( dg << 5 ) | db];
					}
				}
			}

			if( score < bestScore ) {
				(best, bestScore) = (i, score);
			}
		}

		for( var i = 0; i < h.Length; i += step ) {
			counts[Bin( h[i] )] = 0;
		}

		return best;

		static int Bin( int c ) => ( ( c >> 9 ) & 0x7C00 ) | ( ( c >> 6 ) & 0x3E0 ) | ( ( c >> 3 ) & 0x1F );
	}

	private readonly struct ColorMatcher : IMatcher {
		private readonly int[] n;
		private readonly int tol;
		// 画面上で少ない色の 1 ピクセルだけ先に比較して候補を絞る (比べるピクセルが無ければ probe は -1)
		private readonly int probe, px, py;
		// 比べるピクセル (透明・透過色を除く) の色と、画面上の左上からのずれ。透明かどうかの判定を比較のたびにしなくて済む
		private readonly int[] colors, offsets;

		public int Probe => probe;
		public int ProbeX => px;
		public int ProbeY => py;

		/// <param name="needle">探す画像です。</param>
		/// <param name="tol">許容差です。</param>
		/// <param name="hay">探す範囲です (幅をずれの計算に、色の分布を probe 選びに使う)。</param>
		public ColorMatcher( NeedleImage needle, int tol, HayImage hay ) {
			(n, this.tol) = (needle.Pixels, tol);
			probe = RarestPixel( needle, tol, hay );
			(px, py) = (probe % needle.Width, probe / needle.Width);
			// 一覧は探す画像ごとに 1 回だけ作られ、ずれも同じ幅なら使い回される
			(colors, offsets) = (needle.CompareTargets.Colors, needle.OffsetsFor( hay.Width ));
		}

		public bool IsCandidate( int[] h, int hw, int x, int y ) =>
			probe < 0 || Near( n[probe], h[( y + py ) * hw + x + px], tol );

		public bool Matches( int[] h, int hw, int ox, int oy ) {
			var origin = oy * hw + ox;
			for( var i = 0; i < colors.Length; i++ ) {
				if( !Near( colors[i], h[origin + offsets[i]], tol ) ) {
					return false;
				}
			}

			return true;
		}

		private static bool Near( int a, int b, int tol ) =>
			tol == 0 ? ( a & 0xFFFFFF ) == ( b & 0xFFFFFF ) :
			Math.Abs( ( ( a >> 16 ) & 0xFF ) - ( ( b >> 16 ) & 0xFF ) ) <= tol &&
			Math.Abs( ( ( a >> 8 ) & 0xFF ) - ( ( b >> 8 ) & 0xFF ) ) <= tol &&
			Math.Abs( ( a & 0xFF ) - ( b & 0xFF ) ) <= tol;
	}
}

/// <summary>色を無視して形で照合します。作り方は <see cref="ImageMatch.Shape"/> です。</summary>
public sealed record ShapeMatch : PixelMatch {
	/// <param name="max">返す件数の上限です。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> が 1 未満です。</exception>
	public ShapeMatch( int max = int.MaxValue ) : base( max ) { }

	internal override List<Point> Search( NeedleImage needle, HayImage hay, int max ) =>
		Scan( new ShapeMatcher( needle, hay ), needle, hay, Math.Min( max, Max ) );

	/// <summary>
	/// 比べる色を 15 ビット (各成分 5 ビット) にします。各成分の下位 3 ビットを捨てます
	/// (UWSC の CHKIMG の色無視もほぼ同じ粒度で比べている)。
	/// </summary>
	private static short Quantize( int c ) => (short)( ( c >> 9 & 0x7C00 ) | ( c >> 6 & 0x03E0 ) | ( c >> 3 & 0x001F ) );

	/// <summary>事前絞り込みに使う条件の最大数です。</summary>
	private const int MaxConstraints = 8;

	private readonly struct ShapeMatcher : IMatcher {
		private readonly int nw, nh;
		// 画面の色 (Quantize 済み)
		private readonly short[] hay;
		// ピクセルごとの色番号 (Quantize 後の色で数える)。比較しないピクセル (透明・透過色) なら -1
		private readonly int[] colors;
		// 事前絞り込みの条件: 画面上のずれ 2 つと、その 2 ピクセルが同じ色であるべきか
		private readonly int[] first, second;
		private readonly bool[] same;
		// 色番号 → 画面の色、画面の色 → 使用中か。stamp が今回の照合の番号と同じときだけ有効 (毎回の初期化を省く)
		private readonly short[] forward;
		private readonly int[] forwardStamp, reverseStamp;
		private readonly int[] generation = [0];

		public ShapeMatcher( NeedleImage needle, HayImage image ) {
			(nw, nh) = (needle.Width, needle.Height);
			hay = new short[image.Pixels.Length];
			for( var i = 0; i < hay.Length; i++ ) {
				hay[i] = Quantize( image.Pixels[i] );
			}

			var indices = new Dictionary<short, int>();
			colors = new int[needle.Pixels.Length];
			var mask = needle.CompareTargets.Mask;
			for( var i = 0; i < colors.Length; i++ ) {
				var p = needle.Pixels[i];
				if( !mask[i] ) {
					colors[i] = -1;
					continue;
				}

				if( !indices.TryGetValue( Quantize( p ), out var index ) ) {
					index = indices.Count;
					indices.Add( Quantize( p ), index );
				}

				colors[i] = index;
			}

			forward = new short[indices.Count];
			forwardStamp = new int[indices.Count];
			reverseStamp = new int[1 << 15];
			(first, second, same) = Constraints( colors, nw, nh, image.Width );
		}

		/// <summary>
		/// 事前絞り込みの条件を選びます。色の境目 (隣り合う違う色の 2 ピクセル) と、
		/// 離れた同じ色の 2 ピクセル (その色の最初と最後) を選び、交互に並べます。
		/// 境目は画像全体から等間隔に選び、同じ色の組は最初と最後の距離が大きい色から選びます。
		/// </summary>
		private static (int[], int[], bool[]) Constraints( int[] colors, int w, int h, int stride ) {
			var different = new List<(int, int)>();
			var first = new Dictionary<int, int>();
			var last = new Dictionary<int, int>();
			for( var y = 0; y < h; y++ ) {
				for( var x = 0; x < w; x++ ) {
					var i = y * w + x;
					var c = colors[i];
					if( c < 0 ) {
						continue;
					}

					first.TryAdd( c, i );
					last[c] = i;
					if( x + 1 < w && colors[i + 1] >= 0 && colors[i + 1] != c ) {
						different.Add( (i, i + 1) );
					} else if( y + 1 < h && colors[i + w] >= 0 && colors[i + w] != c ) {
						different.Add( (i, i + w) );
					}
				}
			}

			// 同じ色の 2 ピクセルは、その色の最初と最後 (離れているほど違う場所で外れやすい)。最初と最後が離れている色から
			var equal = first.Keys.Where( c => first[c] != last[c] )
				.OrderByDescending( c => last[c] - first[c] ).Select( c => (first[c], last[c]) ).ToList();

			// 色の境目は画像全体から等間隔に、最大で条件の半分まで選ぶ。残りを同じ色の 2 ピクセルで埋め、交互に並べる
			var d = Math.Min( different.Count, MaxConstraints / 2 );
			var edges = Enumerable.Range( 0, d ).Select( k => different[k * different.Count / d] ).ToList();
			var pairs = equal.Take( MaxConstraints - d ).ToList();
			var picked = new List<(int A, int B, bool Same)>();
			for( var k = 0; k < Math.Max( edges.Count, pairs.Count ); k++ ) {
				if( k < edges.Count ) {
					picked.Add( (edges[k].Item1, edges[k].Item2, false) );
				}

				if( k < pairs.Count ) {
					picked.Add( (pairs[k].Item1, pairs[k].Item2, true) );
				}
			}

			int Offset( int i ) => i / w * stride + i % w;
			return (picked.Select( p => Offset( p.A ) ).ToArray(), picked.Select( p => Offset( p.B ) ).ToArray(), picked.Select( p => p.Same ).ToArray());
		}

		public bool IsCandidate( int[] h, int hw, int x, int y ) {
			var o = y * hw + x;
			for( var k = 0; k < first.Length; k++ ) {
				if( ( hay[o + first[k]] == hay[o + second[k]] ) != same[k] ) {
					return false;
				}
			}

			return true;
		}

		// 画像の各色と画面の色が 1 対 1 に対応するか
		public bool Matches( int[] h, int hw, int ox, int oy ) {
			var gen = ++generation[0];
			for( var y = 0; y < nh; y++ ) {
				var hi = ( oy + y ) * hw + ox;
				var ni = y * nw;
				for( var x = 0; x < nw; x++ ) {
					var index = colors[ni + x];
					if( index < 0 ) {
						continue; // 比較しないピクセル (透明・透過色)
					}

					var c = hay[hi + x];
					if( forwardStamp[index] == gen ) {
						if( forward[index] != c ) {
							return false; // 画像で同じ色の部分が、画面では違う色
						}
					} else {
						if( reverseStamp[c] == gen ) {
							return false; // 画像で違う色の部分が、画面では同じ色
						}

						(forward[index], forwardStamp[index], reverseStamp[c]) = (c, gen, gen);
					}
				}
			}

			return true;
		}
	}
}

/// <summary>
/// ピクセルを数値 (明るさ・輪郭の強さ) に変換し、正規化相互相関で照合する方法です。
/// 縮小した画像で候補を探してから、倍率を半分ずつ戻しながら候補の周りだけを調べます。
/// </summary>
public abstract record CorrelationMatch : ImageMatch {
	internal const double DefaultThreshold = 0.95;

	private protected CorrelationMatch( double threshold ) => Threshold = threshold;

	/// <summary>一致とみなす相関係数です (0〜1)。1 に近いほど厳しくなります。</summary>
	/// <exception cref="ArgumentOutOfRangeException">0〜1 の範囲外を設定しました。</exception>
	public double Threshold {
		get;
		init => field = value is >= 0 and <= 1 ? value
			: throw new ArgumentOutOfRangeException( nameof( Threshold ), value, "相関係数は 0〜1 で指定してください。" );
	}

	/// <summary>明るさ (0〜255) を比べる値に変換します。</summary>
	private protected abstract float[] Transform( float[] gray, int w, int h );

	/// <summary>画像の (x, y) を比較に使うかどうかです。</summary>
	private protected abstract bool IsTarget( NeedleImage needle, int x, int y );

	/// <summary>縮小した画像の短い辺がこのピクセル数以上になる範囲で、できるだけ縮小して候補を探します。</summary>
	private const int MinCoarseSize = 8;

	/// <summary>縮小の最大倍率です。</summary>
	private const int MaxScale = 8;

	/// <summary>
	/// 縮小して探すときの相関の閾値です (元の閾値に掛ける)。
	/// 縮小すると位置のずれ (最大で倍率 - 1 ピクセル) で相関が下がるので、緩めて候補を多めに拾います。
	/// </summary>
	private const double CoarseThresholdRatio = 0.6;

	/// <summary>
	/// 一致とみなす相関の下限です。相関は float で計算するので、同じ画像でも 1 をわずかに下回ることがあります。
	/// その誤差の分だけ閾値を下げて比べ、Threshold = 1 でも完全に一致する位置が見つかるようにします。
	/// </summary>
	private double MinScore => Threshold - 1e-5;

	/// <summary>
	/// 縮小した画像で候補を探し、倍率を半分ずつ戻しながら候補の周りだけを調べて位置を絞り込みます (例: 4 倍 → 2 倍 → 元の大きさ)。
	/// 閾値以上の位置から、相関の高い順に重ならない位置を選び、上から順 (同じ行は左から順) に返します。
	/// </summary>
	internal sealed override List<Point> Search( NeedleImage needle, HayImage image, int max ) {
		var (h, hw, hh) = image;
		int nw = needle.Width, nh = needle.Height;
		if( nw > hw || nh > hh ) {
			return [];
		}

		var hay = Transform( ToGray( h ), hw, hh );
		var nv = Transform( ToGray( needle.Pixels ), nw, nh );
		var mask = new bool[nw * nh];
		for( var y = 0; y < nh; y++ ) {
			for( var x = 0; x < nw; x++ ) {
				mask[y * nw + x] = IsTarget( needle, x, y );
			}
		}

		// levels[0] が元の大きさ、levels[^1] が最も縮小した画像
		var levels = new List<Level> { new( 1, hay, hw, hh, Template.Create( nv, mask, nw, nh, hw ), nw, nh ) };
		if( levels[0].Template is null ) {
			return [];
		}

		// 1 つ前の段を半分に縮小して次の段を作る
		var (lv, lmask) = (nv, mask);
		for( var scale = 2; scale <= MaxScale && Math.Min( nw, nh ) / scale >= MinCoarseSize; scale *= 2 ) {
			var prev = levels[^1];
			lv = Shrink( lv, prev.NeedleWidth, prev.NeedleHeight, 2, lmask, out lmask );
			var t = Template.Create( lv, lmask, nw / scale, nh / scale, hw / scale );
			if( t is null ) {
				break; // 縮小すると比較するピクセルが無くなる (透明な部分が多い)
			}

			levels.Add( new( scale, Shrink( prev.Hay, prev.Width, prev.Height, 2, null, out _ ), hw / scale, hh / scale, t, nw / scale, nh / scale ) );
		}

		var hits = new List<(Point P, double Score)>();
		if( levels.Count == 1 ) {
			foreach( var p in AllPositions( hw - nw, hh - nh ) ) {
				var score = levels[0].Score( p );
				if( score >= MinScore ) {
					hits.Add( (p, score) );
				}
			}
		} else {
			// 最も縮小した画像で、緩めた閾値 (Threshold × CoarseThresholdRatio) 以上で周囲 8 か所より相関が低くない位置を候補にする
			var top = levels[^1];
			int mw = top.MaxX + 1, mh = top.MaxY + 1;
			var scores = new double[mw * mh];
			foreach( var p in AllPositions( top.MaxX, top.MaxY ) ) {
				scores[p.Y * mw + p.X] = top.Score( p );
			}

			var coarseThreshold = Threshold * CoarseThresholdRatio;
			foreach( var p in AllPositions( top.MaxX, top.MaxY ) ) {
				if( scores[p.Y * mw + p.X] < coarseThreshold || !IsLocalMax( scores, mw, mh, p.X, p.Y ) ) {
					continue;
				}

				// 1 段ずつ大きくしながら、前の段の位置の周りで最も相関が高い位置に絞り込む
				var (pos, score) = (p, 0.0);
				for( var i = levels.Count - 2; i >= 0 && score >= 0; i-- ) {
					(pos, score) = levels[i].BestAround( pos, levels[i + 1].Scale, i == 0 ? MinScore : coarseThreshold );
				}

				if( score >= MinScore ) {
					hits.Add( (pos, score) );
				}
			}
		}

		// 相関の高い順に、既に選んだ位置と重ならない位置を選ぶ
		hits.Sort( ( a, b ) => b.Score != a.Score ? b.Score.CompareTo( a.Score ) : a.P.Y != b.P.Y ? a.P.Y.CompareTo( b.P.Y ) : a.P.X.CompareTo( b.P.X ) );
		var found = new List<Point>();
		foreach( var (p, _) in hits ) {
			if( !Overlaps( found, p.X, p.Y, nw, nh ) ) {
				found.Add( p );
			}
		}

		found.Sort( ( a, b ) => a.Y != b.Y ? a.Y.CompareTo( b.Y ) : a.X.CompareTo( b.X ) );
		return found.Count > max ? found.GetRange( 0, max ) : found;
	}

	/// <summary>ある倍率に縮小した画面の値と検索用の画像です。</summary>
	/// <param name="Scale">元の大きさに対する縮小の倍率です。</param>
	/// <param name="Hay">縮小した画面の値です。</param>
	/// <param name="Width">縮小した画面の幅です。</param>
	/// <param name="Height">縮小した画面の高さです。</param>
	/// <param name="Template">縮小した検索用の画像です。</param>
	/// <param name="NeedleWidth">縮小した画像の幅です。</param>
	/// <param name="NeedleHeight">縮小した画像の高さです。</param>
	private sealed record Level( int Scale, float[] Hay, int Width, int Height, Template? Template, int NeedleWidth, int NeedleHeight ) {
		public int MaxX => Width - NeedleWidth;
		public int MaxY => Height - NeedleHeight;

		public double Score( Point p ) => Template!.Score( Hay, p.Y * Width + p.X );

		/// <summary>
		/// 倍率 coarseScale の段で見つけた位置 coarse の周りを調べ、最も相関が高い位置を返します。threshold 未満なら score は -1 です。
		/// 倍率の比を r とすると本当の位置は coarse * r 〜 coarse * r + r - 1 です。前の段のずれも見込んで前後 r ピクセルを調べます。
		/// </summary>
		public (Point Position, double Score) BestAround( Point coarse, int coarseScale, double threshold ) {
			var r = coarseScale / Scale;
			var (best, score) = (Point.Empty, -1.0);
			for( var y = Math.Max( 0, ( coarse.Y - 1 ) * r ); y <= Math.Min( MaxY, ( coarse.Y + 1 ) * r ); y++ ) {
				for( var x = Math.Max( 0, ( coarse.X - 1 ) * r ); x <= Math.Min( MaxX, ( coarse.X + 1 ) * r ); x++ ) {
					var s = Score( new Point( x, y ) );
					if( s > score ) {
						(best, score) = (new Point( x, y ), s);
					}
				}
			}

			return score >= threshold ? (best, score) : (best, -1);
		}
	}

	private static bool IsLocalMax( double[] scores, int w, int h, int x, int y ) {
		var v = scores[y * w + x];
		for( var dy = -1; dy <= 1; dy++ ) {
			for( var dx = -1; dx <= 1; dx++ ) {
				int nx = x + dx, ny = y + dy;
				if( ( dx != 0 || dy != 0 ) && nx >= 0 && ny >= 0 && nx < w && ny < h && scores[ny * w + nx] > v ) {
					return false;
				}
			}
		}

		return true;
	}

	private static IEnumerable<Point> AllPositions( int maxX, int maxY ) {
		for( var y = 0; y <= maxY; y++ ) {
			for( var x = 0; x <= maxX; x++ ) {
				yield return new Point( x, y );
			}
		}
	}

	/// <summary>
	/// scale x scale ピクセルごとの平均で縮小します。端の余りは捨てます。
	/// mask を渡すと、ブロック内が全て比較対象のときだけ縮小後も比較対象にします。
	/// </summary>
	private static float[] Shrink( float[] v, int w, int h, int scale, bool[]? mask, out bool[] shrunkMask ) {
		int sw = w / scale, sh = h / scale;
		var result = new float[sw * sh];
		shrunkMask = new bool[sw * sh];
		for( var sy = 0; sy < sh; sy++ ) {
			for( var sx = 0; sx < sw; sx++ ) {
				float sum = 0;
				var all = true;
				for( var y = sy * scale; y < ( sy + 1 ) * scale; y++ ) {
					for( var x = sx * scale; x < ( sx + 1 ) * scale; x++ ) {
						sum += v[y * w + x];
						all &= mask is null || mask[y * w + x];
					}
				}

				result[sy * sw + sx] = sum / ( scale * scale );
				shrunkMask[sy * sw + sx] = all;
			}
		}

		return result;
	}

	/// <summary>比較するピクセルの、画面上でのずれと平均を引いた値です。</summary>
	private sealed class Template {
		// 比較するピクセルを行ごとの連続した区間にまとめたもの (画面上でのずれ・values の開始位置・長さ)
		private readonly (int Offset, int Start, int Length)[] runs;
		// 平均を引いた値
		private readonly float[] values;
		private readonly double norm;

		private Template( (int, int, int)[] runs, float[] values, double norm ) => (this.runs, this.values, this.norm) = (runs, values, norm);

		/// <param name="v">画像の値です。</param>
		/// <param name="mask">ピクセルごとに比較するかどうかです。</param>
		/// <param name="w">画像の幅です。</param>
		/// <param name="h">画像の高さです。</param>
		/// <param name="stride">画面の幅です (ずれの計算に使う)。</param>
		/// <returns>比較するピクセルが無ければ null です。</returns>
		public static Template? Create( float[] v, bool[] mask, int w, int h, int stride ) {
			var runs = new List<(int, int, int)>();
			var values = new List<float>();
			for( var y = 0; y < h; y++ ) {
				for( var x = 0; x < w; ) {
					if( !mask[y * w + x] ) {
						x++;
						continue;
					}

					var start = x;
					while( x < w && mask[y * w + x] ) {
						values.Add( v[y * w + x] );
						x++;
					}

					runs.Add( (y * stride + start, values.Count - ( x - start ), x - start) );
				}
			}

			if( values.Count == 0 ) {
				return null;
			}

			var mean = values.Average();
			var nz = values.Select( x => x - mean ).ToArray();
			return new Template( [.. runs], nz, Math.Sqrt( nz.Sum( x => (double)x * x ) ) );
		}

		/// <summary>画面の値 hay の位置 o (左上のインデックス) での正規化相互相関です (-1〜1)。</summary>
		public double Score( float[] hay, int o ) {
			// 行の区間ごとに SIMD で足し、精度のため区間の合計は double で足す
			double s = 0, ss = 0, sn = 0;
			var n = Vector<float>.Count;
			foreach( var (offset, start, length) in runs ) {
				var hi = o + offset;
				var vs = Vector<float>.Zero;
				var vss = Vector<float>.Zero;
				var vsn = Vector<float>.Zero;
				var i = 0;
				for( ; i <= length - n; i += n ) {
					var hv = new Vector<float>( hay, hi + i );
					vs += hv;
					vss += hv * hv;
					vsn += hv * new Vector<float>( values, start + i );
				}

				float rs = Vector.Sum( vs ), rss = Vector.Sum( vss ), rsn = Vector.Sum( vsn );
				for( ; i < length; i++ ) {
					var hv = hay[hi + i];
					rs += hv;
					rss += hv * hv;
					rsn += hv * values[start + i];
				}

				(s, ss, sn) = (s + rs, ss + rss, sn + rsn);
			}

			var count = values.Length;
			var hNorm = Math.Sqrt( Math.Max( 0, ss - s * s / count ) );
			// 画像が平坦 (全て同じ値) なら、画面も平坦なときだけ一致とする (float の誤差を見込んで判定する)
			if( norm < 1e-3 || hNorm < 1e-3 * Math.Sqrt( count ) ) {
				return norm < 1e-3 && hNorm < 1e-3 * Math.Sqrt( count ) ? 1 : 0;
			}

			return sn / ( norm * hNorm );
		}
	}

	/// <summary>明るさです (0〜255)。ITU-R BT.601 の重みです。</summary>
	private static float[] ToGray( int[] pixels ) {
		var result = new float[pixels.Length];
		for( var i = 0; i < pixels.Length; i++ ) {
			var c = pixels[i];
			result[i] = 0.299f * ( ( c >> 16 ) & 0xFF ) + 0.587f * ( ( c >> 8 ) & 0xFF ) + 0.114f * ( c & 0xFF );
		}

		return result;
	}
}

/// <summary>明るさの相関で照合します。作り方は <see cref="ImageMatch.Gray"/> です。</summary>
public sealed record GrayMatch : CorrelationMatch {
	/// <param name="threshold">一致とみなす相関係数です (0〜1)。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> が 0〜1 の範囲外です。</exception>
	public GrayMatch( double threshold = DefaultThreshold ) : base( threshold ) { }

	private protected override float[] Transform( float[] gray, int w, int h ) => gray;

	private protected override bool IsTarget( NeedleImage needle, int x, int y ) =>
		needle.CompareTargets.Mask[y * needle.Width + x];
}

/// <summary>輪郭の相関で照合します。作り方は <see cref="ImageMatch.Edge"/> です。</summary>
public sealed record EdgeMatch : CorrelationMatch {
	/// <param name="threshold">一致とみなす相関係数です (0〜1)。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> が 0〜1 の範囲外です。</exception>
	public EdgeMatch( double threshold = DefaultThreshold ) : base( threshold ) { }

	/// <summary>Sobel フィルタで求めた輪郭の強さです。外周 1 ピクセルは 0 です。</summary>
	private protected override float[] Transform( float[] g, int w, int h ) {
		var result = new float[g.Length];
		for( var y = 1; y < h - 1; y++ ) {
			for( var x = 1; x < w - 1; x++ ) {
				var i = y * w + x;
				var gx = g[i - w + 1] + 2 * g[i + 1] + g[i + w + 1] - g[i - w - 1] - 2 * g[i - 1] - g[i + w - 1];
				var gy = g[i + w - 1] + 2 * g[i + w] + g[i + w + 1] - g[i - w - 1] - 2 * g[i - w] - g[i - w + 1];
				result[i] = MathF.Sqrt( gx * gx + gy * gy );
			}
		}

		return result;
	}

	/// <summary>周囲 3x3 が全て画像内の比べるピクセル (透明・透過色でない) なら、そのピクセルの輪郭を比較に使います。</summary>
	private protected override bool IsTarget( NeedleImage needle, int x, int y ) {
		int w = needle.Width, h = needle.Height;
		if( x < 1 || y < 1 || x > w - 2 || y > h - 2 ) {
			return false;
		}

		for( var dy = -1; dy <= 1; dy++ ) {
			for( var dx = -1; dx <= 1; dx++ ) {
				if( !needle.CompareTargets.Mask[( y + dy ) * w + x + dx] ) {
					return false;
				}
			}
		}

		return true;
	}
}

/// <summary>
/// <see cref="ScriptGlobals.CheckImage(string, TransColor, Rectangle, ImageMatch)"/> で透過色にする画像の隅です (UWSC の CHKIMG の透過 1〜4)。
/// </summary>
/// <remarks>指定した隅のピクセルと同じ色の部分を、画像全体で透明とみなして比較しません。</remarks>
public enum TransColor {
	/// <summary>透過色を使いません。</summary>
	None,
	/// <summary>左上の色を透過色にします (UWSC の 1)。</summary>
	TopLeft,
	/// <summary>右上の色を透過色にします (UWSC の 2)。</summary>
	TopRight,
	/// <summary>左下の色を透過色にします (UWSC の 3)。</summary>
	BottomLeft,
	/// <summary>右下の色を透過色にします (UWSC の 4)。</summary>
	BottomRight,
}
