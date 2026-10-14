namespace CsWSC.Recording;

/// <summary>記録方法です (設定画面の「記録方法」に対応)。</summary>
public sealed record RecordOptions(
	bool HighLevel,
	bool SkipIdle,
	bool RelativeMouse,
	bool Background );

/// <summary>操作対象のトップレベルウィンドウです (イベント発生時点の情報)。Left・Top はクライアント領域の左上です (画面座標)。</summary>
internal sealed record WindowInfo( IntPtr Handle, string Title, string ClassName, int Left, int Top );

internal abstract class RecordedEvent( long time ) {
	/// <summary>記録開始からの経過ミリ秒です。</summary>
	public long Time { get; } = time;
}

internal sealed class MouseMoveEvent( long time, int x, int y ) : RecordedEvent( time ) {
	public int X { get; } = x;
	public int Y { get; } = y;
}

internal sealed class MouseButtonEvent( long time, MouseButton button, bool down, int x, int y, WindowInfo? window ) : RecordedEvent( time ) {
	public MouseButton Button { get; } = button;
	public bool Down { get; } = down;
	public int X { get; } = x;
	public int Y { get; } = y;
	public WindowInfo? Window { get; } = window;

	/// <summary>高レベル記録時、UI Automation で取得したコントロールです (別スレッドで後から設定される)。</summary>
	public (string Name, string ControlType)? Element { get; set; }
}

internal sealed class WheelEvent( long time, int notches, int x, int y, WindowInfo? window ) : RecordedEvent( time ) {
	/// <summary>正で下方向です (Wheel() と同じ向き)。</summary>
	public int Notches { get; } = notches;
	public int X { get; } = x;
	public int Y { get; } = y;
	public WindowInfo? Window { get; } = window;
}

internal sealed class KeyEvent( long time, int vk, bool down, WindowInfo? window ) : RecordedEvent( time ) {
	public int VirtualKey { get; } = vk;
	public bool Down { get; } = down;
	public WindowInfo? Window { get; } = window;
}
