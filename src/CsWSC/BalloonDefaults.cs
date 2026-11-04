namespace CsWSC;

/// <summary>
/// <c>Balloon(...)</c> で引数を省略したときに使う初期値です。<c>BalloonDefault</c> から設定します。
/// </summary>
/// <remarks>
/// スクリプトの実行ごとに初期状態に戻ります。設定後に表示した吹き出しから反映されます (表示中の吹き出しは変わらない)。
/// </remarks>
/// <example>
/// <code>
/// BalloonDefault.FontSize = 14;
/// BalloonDefault.BackColor = Color.LightBlue;
/// Balloon("こんにちは");                 // 14pt・水色
/// Balloon("個別指定", fontSize: 20);     // 引数が優先
/// </code>
/// </example>
public sealed partial class BalloonDefaults {
	/// <summary>文字の大きさです (ポイント)。既定は 10 です。</summary>
	public float FontSize { get; set; } = 10;

	/// <summary>フォント名です。null ならシステムのメッセージ用フォントです。</summary>
	public string? FontName { get; set; }

	/// <summary>文字色です。既定は黒です。</summary>
	public Color ForeColor { get; set; } = Color.Black;

	/// <summary>背景色です。既定は薄い黄色です。</summary>
	public Color BackColor { get; set; } = Color.FromArgb( 255, 255, 225 );

	/// <summary>枠線の色です。null なら文字色を半透明にした色です。既定は null です。</summary>
	public Color? BorderColor { get; set; }

	/// <summary>
	/// 枠線の太さです (DPI 100% のときのピクセル数で、拡大率に合わせて太くなります)。<c>new Thickness(左, 上, 右, 下)</c> で上下左右を別々に指定できます。
	/// int を入れると上下左右が同じ太さです。0 で枠線なしです。既定は 1 です。負の値は Balloon を出すときに 0 にします。
	/// </summary>
	public Thickness BorderThickness { get; set; } = 1;

	/// <summary>不透明度です (0.0 = 透明 ～ 1.0 = 不透明)。既定は 1.0 です。範囲外は Balloon を出すときに丸めます。</summary>
	public double Opacity { get; set; } = 1.0;
}
