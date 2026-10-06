namespace CsWSC;

/// <summary>PRINT 窓の操作 (LOGPRINT)。ホスト側が実装して <see cref="ScriptRunner.Run"/> に渡す。</summary>
public interface ILogWindow
{
    /// <summary>表示/非表示を切り替える。非表示にすると、以降 Print しても自動では表示されない。</summary>
    void SetVisible(bool visible);

    /// <summary>位置とサイズを変える (画面座標)。null の項目は変えない。</summary>
    void SetBounds(int? x, int? y, int? width, int? height);

    /// <summary>内容を消去する。</summary>
    void Clear();
}
