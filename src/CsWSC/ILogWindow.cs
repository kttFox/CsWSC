namespace CsWSC;

/// <summary>PRINT 窓の操作です (LOGPRINT)。ホスト側が実装して <see cref="ScriptRunner.Run"/> に渡します。</summary>
public interface ILogWindow {
	/// <summary>表示/非表示を切り替えます。非表示にすると、以降 Print しても自動では表示されません。</summary>
	void SetVisible( bool visible );

	/// <summary>位置とサイズを変えます (画面座標)。null の項目は変えません。</summary>
	void SetBounds( int? x, int? y, int? width, int? height );

	/// <summary>内容を消去します。</summary>
	void Clear();
}
