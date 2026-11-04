namespace CsWSC;

/// <summary>
/// 上下左右それぞれの太さです (ピクセル)。WPF の Thickness と同じように使えます。
/// </summary>
/// <remarks>
/// int から暗黙に変換できます。<c>2</c> と書くと上下左右すべて 2 です。
/// </remarks>
/// <example>
/// <code>
/// Balloon("全部 2", borderThickness: 2);
/// Balloon("下だけ太く", borderThickness: new Thickness(1, 1, 1, 4));
/// </code>
/// </example>
/// <param name="Left">左の太さです。</param>
/// <param name="Top">上の太さです。</param>
/// <param name="Right">右の太さです。</param>
/// <param name="Bottom">下の太さです。</param>
public readonly record struct Thickness( int Left, int Top, int Right, int Bottom ) {
	/// <summary>上下左右を同じ太さにします。</summary>
	/// <param name="uniform">上下左右の太さです。</param>
	public Thickness( int uniform ) : this( uniform, uniform, uniform, uniform ) { }

	/// <summary>int から上下左右が同じ太さの <see cref="Thickness"/> に変換します。</summary>
	public static implicit operator Thickness( int uniform ) => new( uniform );

	/// <summary>負の値を 0 にした値を返します。</summary>
	internal Thickness NonNegative() => new( Math.Max( 0, Left ), Math.Max( 0, Top ), Math.Max( 0, Right ), Math.Max( 0, Bottom ) );

	/// <summary>上下左右がすべて 0 なら true です。</summary>
	internal bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

	/// <summary>"Left,Top,Right,Bottom" の形の文字列を返します。</summary>
	public override string ToString() => $"{Left},{Top},{Right},{Bottom}";
}
