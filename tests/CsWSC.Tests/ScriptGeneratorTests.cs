using CsWSC.Recording;

namespace CsWSC.Tests;

public class ScriptGeneratorTests {
	private static readonly RecordOptions LowLevel = new( HighLevel: false, SkipIdle: false, RelativeMouse: false, Background: false );
	private static readonly WindowInfo Notepad = new( IntPtr.Zero, "メモ帳", "Notepad", 10, 20 );
	private const string NotepadVar = "var w1 = GetWindow(\"メモ帳\", \"Notepad\") ?? throw new Exception(\"ウィンドウが見つかりません: メモ帳\");";

	// 1 行目の日時入りヘッダーと空行を除いた本文を行ごとに返す
	private static string[] Body( RecordOptions options, params RecordedEvent[] events ) {
		var lines = ScriptGenerator.Generate( events, options ).Split( "\r\n" );
		Assert.StartsWith( "// CsWSC 記録 ", lines[0] );
		Assert.Equal( "", lines[1] );
		return lines[2..^1];
	}

	private static MouseButtonEvent Down( long t, int x, int y, MouseButton b = MouseButton.Left, WindowInfo? w = null ) => new( t, b, true, x, y, w );
	private static MouseButtonEvent Up( long t, int x, int y, MouseButton b = MouseButton.Left, WindowInfo? w = null ) => new( t, b, false, x, y, w );
	private static KeyEvent KeyDown( long t, int vk, WindowInfo? w = null ) => new( t, vk, true, w );
	private static KeyEvent KeyUp( long t, int vk, WindowInfo? w = null ) => new( t, vk, false, w );

	[Fact]
	public void Header_ShowsRecordMode() {
		var script = ScriptGenerator.Generate( [], LowLevel with { SkipIdle = true, Background = true } );
		Assert.Contains( "(低レベル記録, 余分な時間・移動を省略, バックグラウンド)", script );

		script = ScriptGenerator.Generate( [], LowLevel with { HighLevel = true, SkipIdle = true } );
		Assert.Contains( "(高レベル記録, 余分な時間・移動を省略)", script );
	}

	[Fact]
	public void ShortPress_IsClick_WaitGoesToMsArgument() {
		var body = Body( LowLevel, Down( 0, 1, 1 ), Up( 10, 1, 1 ), Down( 1000, 100, 200 ), Up( 1100, 101, 199 ) );
		Assert.Equal( ["Mouse(MouseButton.Left, KeyAction.Click, 1, 1);", "Mouse(MouseButton.Left, KeyAction.Click, 100, 200, 1000);"], body );
	}

	[Fact]
	public void FirstEvent_HasNoWait() {
		var body = Body( LowLevel, Down( 3000, 1, 1 ), Up( 3010, 1, 1 ) );
		Assert.Equal( ["Mouse(MouseButton.Left, KeyAction.Click, 1, 1);"], body );
	}

	[Fact]
	public void RightClick_HasButtonArgument() {
		var body = Body( LowLevel, Down( 0, 1, 2, MouseButton.Right ), Up( 10, 1, 2, MouseButton.Right ) );
		Assert.Equal( ["Mouse(MouseButton.Right, KeyAction.Click, 1, 2);"], body );
	}

	[Fact]
	public void LongPress_IsDrag_WaitGoesToMsArgument() {
		var body = Body( LowLevel, Down( 0, 0, 0 ), new MouseMoveEvent( 100, 50, 50 ), Up( 1000, 100, 100 ) );
		Assert.Equal(
		[
			"Mouse(MouseButton.Left, KeyAction.Down, 0, 0);",
			"Move(50, 50, 100);",
			"Mouse(MouseButton.Left, KeyAction.Up, 100, 100, 900);",
		], body );
	}

	[Fact]
	public void FarRelease_IsDrag() {
		var body = Body( LowLevel, Down( 0, 0, 0 ), Up( 100, 10, 0 ) );
		Assert.Contains( "Mouse(MouseButton.Left, KeyAction.Down, 0, 0);", body );
	}

	[Fact]
	public void Wheel() {
		var body = Body( LowLevel, new WheelEvent( 0, -3, 5, 6, null ) );
		Assert.Equal( ["Wheel(-3, 5, 6);"], body );
	}

	[Fact]
	public void SkipIdle_CapsWait() {
		var body = Body( LowLevel with { SkipIdle = true }, Down( 0, 1, 1 ), Up( 10, 1, 1 ), Down( 5000, 2, 2 ), Up( 5010, 2, 2 ) );
		Assert.Equal( ["Mouse(MouseButton.Left, KeyAction.Click, 1, 1);", "Mouse(MouseButton.Left, KeyAction.Click, 2, 2, 500);"], body );
	}

	[Fact]
	public void ShortWait_IsOmitted() {
		var body = Body( LowLevel, Down( 40, 1, 1 ), Up( 50, 1, 1 ) );
		Assert.Equal( ["Mouse(MouseButton.Left, KeyAction.Click, 1, 1);"], body );
	}

	[Fact]
	public void Characters_AreMergedIntoSendText() {
		var body = Body( LowLevel,
			KeyDown( 0, 0xA0 ), KeyDown( 10, 'H' ), KeyUp( 20, 'H' ), KeyUp( 30, 0xA0 ),
			KeyDown( 40, 'I' ), KeyUp( 50, 'I' ),
			KeyDown( 60, ' ' ), KeyDown( 70, '1' ) );
		Assert.Equal( ["SendText(\"Hi 1\");"], body );
	}

	[Fact]
	public void Characters_AreSplitAfterLongPause() {
		var body = Body( LowLevel, KeyDown( 0, 'A' ), KeyDown( 2000, 'B' ) );
		Assert.Equal( ["SendText(\"a\");", "Sleep(2000);", "SendText(\"b\");"], body );
	}

	[Fact]
	public void CtrlCombination_IsKeyboardDownUp() {
		var body = Body( LowLevel, KeyDown( 0, 0xA2 ), KeyDown( 10, 'C' ), KeyUp( 15, 'C' ), KeyUp( 20, 0xA2 ) );
		Assert.Equal( ["Keyboard(Keys.ControlKey, KeyAction.Down); Keyboard(Keys.C); Keyboard(Keys.ControlKey, KeyAction.Up);"], body );
	}

	[Fact]
	public void ShiftWithNonCharacterKey_IsKeyboardDownUp() {
		var body = Body( LowLevel, KeyDown( 0, 0xA1 ), KeyDown( 10, 0x09 ), KeyUp( 20, 0xA1 ) );
		Assert.Equal( ["Keyboard(Keys.ShiftKey, KeyAction.Down); Keyboard(Keys.Tab); Keyboard(Keys.ShiftKey, KeyAction.Up);"], body );
	}

	[Fact]
	public void AltAlone_IsKeyboardMenu() {
		var body = Body( LowLevel, KeyDown( 0, 0xA4 ), KeyUp( 10, 0xA4 ) );
		Assert.Equal( ["Keyboard(Keys.Menu);"], body );
	}

	[Fact]
	public void ShiftAlone_IsIgnored() {
		Assert.Empty( Body( LowLevel, KeyDown( 0, 0xA0 ), KeyUp( 10, 0xA0 ) ) );
	}

	[Fact]
	public void SpecialKey_WaitGoesToMsArgument() {
		var body = Body( LowLevel, KeyDown( 0, 0x0D ), KeyUp( 10, 0x0D ), KeyDown( 500, 0x0D ), KeyUp( 510, 0x0D ) );
		Assert.Equal( ["Keyboard(Keys.Enter);", "Keyboard(Keys.Enter, KeyAction.Click, 500);"], body );
	}

	[Fact]
	public void RelativeMouse_ClickIsWindowRelative() {
		var body = Body( LowLevel with { RelativeMouse = true },
			Down( 0, 110, 220, w: Notepad ), Up( 10, 110, 220, w: Notepad ),
			Down( 20, 15, 25, w: Notepad ), Up( 30, 15, 25, w: Notepad ) );
		Assert.Equal( [NotepadVar, "w1.Activate();", "w1.Mouse(MouseButton.Left, KeyAction.Click, 100, 200);", "w1.Mouse(MouseButton.Left, KeyAction.Click, 5, 5);"], body );
	}

	[Fact]
	public void Wait_BeforeWindowMouse_IsDelayArgument() {
		var body = Body( LowLevel with { RelativeMouse = true },
			Down( 0, 110, 220, w: Notepad ), Up( 10, 110, 220, w: Notepad ),
			Down( 500, 15, 25, w: Notepad ), Up( 510, 15, 25, w: Notepad ) );
		Assert.Equal( "w1.Mouse(MouseButton.Left, KeyAction.Click, 5, 5, 500);", body[^1] );
	}

	[Fact]
	public void RelativeMouse_WheelEmitsMouseOrigin() {
		var body = Body( LowLevel with { RelativeMouse = true }, new WheelEvent( 0, 1, 110, 220, Notepad ), new WheelEvent( 10, 1, 5, 6, null ) );
		Assert.Equal(
		[
			NotepadVar,
			"w1.Activate();",
			"MouseOrigin(w1);",
			"Wheel(1, 100, 200);",
			"MouseOrigin();",
			"Wheel(1, 5, 6);",
		], body );
	}

	[Fact]
	public void UntitledWindow_UsesClassNameInMessage() {
		var w = new WindowInfo( IntPtr.Zero, "", "Foo", 0, 0 );
		var body = Body( LowLevel with { RelativeMouse = true }, Down( 0, 1, 1, w: w ), Up( 10, 1, 1, w: w ) );
		Assert.Equal( "var w1 = GetWindow(\"\", \"Foo\") ?? throw new Exception(\"ウィンドウが見つかりません: Foo\");", body[0] );
	}

	[Fact]
	public void TitleChangingWindow_IsOneVariable_WithCommonTitle() {
		WindowInfo W( string title ) => new( 123, title, "Chrome_WidgetWin_1", 0, 0 );
		var body = Body( LowLevel with { RelativeMouse = true },
			Down( 0, 5, 5, w: W( "Untitled-2 - Visual Studio Code" ) ), Up( 10, 5, 5, w: W( "Untitled-2 - Visual Studio Code" ) ),
			KeyDown( 20, 'A', W( "Untitled-2 - Visual Studio Code" ) ),
			KeyDown( 30, 'B', W( "● a • Untitled-2 - Visual Studio Code" ) ),
			KeyDown( 40, 'C', W( "● ab • Untitled-2 - Visual Studio Code" ) ) );
		Assert.Equal(
		[
			"var w1 = GetWindow(\"Untitled-2 - Visual Studio Code\", \"Chrome_WidgetWin_1\") ?? throw new Exception(\"ウィンドウが見つかりません: Untitled-2 - Visual Studio Code\");",
			"w1.Activate();",
			"w1.Mouse(MouseButton.Left, KeyAction.Click, 5, 5);",
			"SendText(\"abc\");",
		], body );
	}

	[Fact]
	public void Background_UsesPostMethodsWithoutActivate() {
		var body = Body( LowLevel with { Background = true },
			Down( 0, 110, 220, MouseButton.Right, Notepad ), Up( 10, 110, 220, MouseButton.Right, Notepad ),
			KeyDown( 20, 'A', Notepad ), KeyDown( 30, 0x0D, Notepad ) );
		Assert.Equal( [NotepadVar, "w1.PostMouse(MouseButton.Right, KeyAction.Click, 100, 200);", "w1.PostText(\"a\");", "w1.PostKey(Keys.Enter);"], body );
	}

	[Fact]
	public void Background_WaitIsDelayArgument() {
		var body = Body( LowLevel with { Background = true },
			Down( 0, 110, 220, MouseButton.Right, Notepad ), Up( 10, 110, 220, MouseButton.Right, Notepad ),
			Down( 500, 110, 220, MouseButton.Right, Notepad ), Up( 510, 110, 220, MouseButton.Right, Notepad ),
			KeyDown( 2000, 'A', Notepad ), KeyDown( 3000, 0x0D, Notepad ) );
		Assert.Equal( [
			NotepadVar,
			"w1.PostMouse(MouseButton.Right, KeyAction.Click, 100, 200);",
			"w1.PostMouse(MouseButton.Right, KeyAction.Click, 100, 200, 500);",
			"w1.PostText(\"a\", 1500);",
			"w1.PostKey(Keys.Enter, 1000);"], body );
	}

	[Fact]
	public void HighLevel_ClickItem_EscapesName() {
		var down = Down( 0, 50, 50, w: Notepad );
		down.Element = ("a\"b\\c\n", "Button");
		var body = Body( LowLevel with { HighLevel = true }, down, Up( 10, 50, 50, w: Notepad ) );
		Assert.Equal( [NotepadVar, "w1.Activate();", "w1.ClickItem(\"a\\\"b\\\\c\\n\"); // Button"], body );
	}

	[Fact]
	public void HighLevel_WithoutElement_FallsBackToWindowClick() {
		var body = Body( LowLevel with { HighLevel = true }, Down( 0, 50, 50, w: Notepad ), Up( 10, 50, 50, w: Notepad ) );
		Assert.Equal( "w1.Mouse(MouseButton.Left, KeyAction.Click, 40, 30);", body[^1] );
	}

	private static readonly WindowInfo Excel = new( IntPtr.Zero, "Book1", "XLMAIN", 300, 400 );
	private const string ExcelVar = "var w2 = GetWindow(\"Book1\", \"XLMAIN\") ?? throw new Exception(\"ウィンドウが見つかりません: Book1\");";

	[Fact]
	public void RelativeMouse_DropOnOtherWindow_UsesDragSourceWindow() {
		var body = Body( LowLevel with { RelativeMouse = true }, Down( 0, 110, 220, w: Notepad ), Up( 1000, 350, 460, w: Excel ) );
		Assert.Equal(
		[
			NotepadVar,
			"w1.Activate();",
			"MouseOrigin(w1);",
			"Mouse(MouseButton.Left, KeyAction.Down, 100, 200);",
			"Mouse(MouseButton.Left, KeyAction.Up, 340, 440, 1000);", // ドロップ先 (Book1) ではなくドラッグ元 (メモ帳) 基準。Activate もしない
		], body );

		// ドロップの後の操作はドロップ先のウィンドウ基準
		body = Body( LowLevel with { RelativeMouse = true },
			Down( 0, 110, 220, w: Notepad ), Up( 1000, 350, 460, w: Excel ), Down( 1010, 310, 410, w: Excel ), Up( 1020, 310, 410, w: Excel ) );
		Assert.Equal( [ExcelVar, "w2.Activate();", "w2.Mouse(MouseButton.Left, KeyAction.Click, 10, 10);"], body[^3..] );
	}

	[Fact]
	public void Wait_GoesToWheelArgument() {
		var body = Body( LowLevel, new WheelEvent( 0, 1, 5, 6, null ), new WheelEvent( 300, -1, 7, 8, null ) );
		Assert.Equal( ["Wheel(1, 5, 6);", "Wheel(-1, 7, 8, 300);"], body );
	}

	[Fact]
	public void Wait_BeforeWindowLines_IsSleep() {
		// 待ちの後に最初に出る行が GetWindow などなら、待ちは Sleep として前に出す
		var body = Body( LowLevel with { RelativeMouse = true }, Down( 0, 1, 1 ), Up( 10, 1, 1 ), new WheelEvent( 300, 1, 110, 220, Notepad ) );
		Assert.Equal( ["Mouse(MouseButton.Left, KeyAction.Click, 1, 1);", "Sleep(300);", NotepadVar, "w1.Activate();", "MouseOrigin(w1);", "Wheel(1, 100, 200);"], body );
	}

	[Fact]
	public void Wait_GoesToKeyboardArgumentWithKeyAction() {
		// 修飾キー単独の Keyboard も、待ちは KeyAction.Click を補って delayMs 引数にする
		var body = Body( LowLevel, KeyDown( 0, 0x0D ), KeyDown( 300, 0xA4 ), KeyUp( 400, 0xA4 ) );
		Assert.Equal( ["Keyboard(Keys.Enter);", "Keyboard(Keys.Menu, KeyAction.Click, 400);"], body );
	}

	[Fact]
	public void Wait_BeforeKeyCombination_IsDelayOfFirstKey() {
		var body = Body( LowLevel, KeyDown( 0, 0x0D ), KeyDown( 300, 0xA2 ), KeyDown( 400, 'C' ) );
		Assert.Equal( ["Keyboard(Keys.Enter);", "Keyboard(Keys.ControlKey, KeyAction.Down, 400); Keyboard(Keys.C); Keyboard(Keys.ControlKey, KeyAction.Up);"], body );
	}

	[Fact]
	public void Background_PostText_MergedAndSplitByWindow() {
		var body = Body( LowLevel with { Background = true },
			KeyDown( 0, 'A', Notepad ), KeyDown( 10, 'B', Notepad ), KeyDown( 20, 'C', Excel ), KeyDown( 30, 'D' ) );
		// 変数宣言は文字を受け取った時点で出るので、前の文字列より先に出る
		Assert.Equal( [NotepadVar, ExcelVar, "w1.PostText(\"ab\");", "w2.PostText(\"c\");", "SendText(\"d\");"], body );
	}

	[Fact]
	public void Background_IgnoredInHighLevel_SendTextWithActivate() {
		var body = Body( LowLevel with { HighLevel = true, Background = true }, KeyDown( 0, 'A', Notepad ) );
		Assert.Equal( [NotepadVar, "w1.Activate();", "SendText(\"a\");"], body );
	}

	[Fact]
	public void GeneratedScript_Compiles() {
		RecordedEvent[] events =
		[
			Down(0, 110, 220, w: Notepad), Up(10, 110, 220, w: Notepad),
			Down(1000, 0, 0, w: Notepad), new MouseMoveEvent(1100, 50, 50), Up(2000, 100, 100, w: Notepad),
			new WheelEvent(2100, 2, 5, 5, Notepad),
			KeyDown(2200, 'A', Notepad), KeyDown(2300, 0xA2), KeyDown(2400, 'S'), KeyUp(2500, 0xA2),
			KeyDown(3000, 0x0D), KeyDown(3100, 0xA4), KeyUp(3200, 0xA4),
		];
		foreach( var options in new[]
		{
			LowLevel,
			LowLevel with { RelativeMouse = true },
			LowLevel with { Background = true },
			LowLevel with { HighLevel = true },
		} ) {
			ScriptRunner.Compile( ScriptGenerator.Generate( events, options ) );
		}
	}
}
