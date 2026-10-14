using CsWSC.Recording;

namespace CsWSC.Tests;

public class RecorderTests {
	[Theory]
	[InlineData( 0, 120, 0, 1 )]     // 1 ノッチ
	[InlineData( 0, 360, 0, 3 )]     // 一度に 3 ノッチ
	[InlineData( 0, -240, 0, -2 )]   // 逆向き
	[InlineData( 0, 40, 40, 0 )]     // 1 ノッチに満たない
	[InlineData( 80, 40, 0, 1 )]     // たまって 1 ノッチ
	[InlineData( 100, 50, 30, 1 )]   // 余りは持ち越す
	[InlineData( 80, -40, -40, 0 )]  // 向きが変わったら、たまった量を捨てる
	[InlineData( -100, 120, 0, 1 )]
	[InlineData( 80, 0, 0, 0 )]      // 量 0 でもたまった量を捨てる (現在の挙動)
	public void AccumulateWheel( int pending, int delta, int expectedPending, int expectedNotches ) {
		Assert.Equal( (expectedPending, expectedNotches), Recorder.AccumulateWheel( pending, delta ) );
	}

	[Fact]
	public void AccumulateWheel_TouchpadSteps() {
		var (pending, total) = (0, 0);
		foreach( var delta in new[] { 30, 30, 30, 30, 30, -30, -30, -30, -30 } ) {
			(pending, var notches) = Recorder.AccumulateWheel( pending, delta );
			total += notches;
		}

		// 30 x 5 で 1 ノッチ (余り 30)、逆向き 30 x 4 で -1 ノッチ
		Assert.Equal( (0, 0), (pending, total) );
	}

	private static KeyEvent Key( int vk, bool down ) => new( 0, vk, down, null );

	[Fact]
	public void TrimTrailing_RemovesHotkeyThenMouseMoves() {
		var click = new MouseButtonEvent( 0, MouseButton.Left, true, 0, 0, null );
		var move = new MouseMoveEvent( 0, 1, 1 );
		List<RecordedEvent> events = [click, new MouseMoveEvent( 0, 2, 2 ), move, Key( 0x12, true ), Key( 0x78, true ), Key( 0x78, false )];
		Recorder.TrimTrailing( events, [0x12, 0x78] );
		Assert.Equal( [click], events );
	}

	[Fact]
	public void TrimTrailing_KeepsOtherKeys() {
		var a = Key( 'A', true );
		List<RecordedEvent> events = [a, Key( 0x78, true )];
		Recorder.TrimTrailing( events, [0x78] );
		Assert.Equal( [a], events );

		// 末尾が対象外のキーなら何も取り除かない (途中の対象キーも残す)
		var f9 = Key( 0x78, true );
		events = [f9, a];
		Recorder.TrimTrailing( events, [0x78] );
		Assert.Equal( [f9, a], events );
	}

	[Fact]
	public void TrimTrailing_KeysBeforeMovesAreKept() {
		// マウス移動を取り除いた後に現れた対象キーは取り除かない (キー → 移動の順で 1 回ずつ)
		var f9 = Key( 0x78, true );
		List<RecordedEvent> events = [f9, new MouseMoveEvent( 0, 1, 1 )];
		Recorder.TrimTrailing( events, [0x78] );
		Assert.Equal( [f9], events );
	}

	[Fact]
	public void TrimTrailing_NullKeysAndEmpty() {
		List<RecordedEvent> events = [new MouseMoveEvent( 0, 1, 1 )];
		Recorder.TrimTrailing( events, null );
		Assert.Empty( events );
		Recorder.TrimTrailing( events, [1] );
		Assert.Empty( events );
	}
}
