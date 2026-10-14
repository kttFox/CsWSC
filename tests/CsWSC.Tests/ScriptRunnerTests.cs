namespace CsWSC.Tests;

public class ScriptRunnerTests {
	private static List<string> Run( string code ) {
		var output = new List<string>();
		ScriptRunner.Run( code, output.Add, CancellationToken.None );
		return output;
	}

	[Fact]
	public void Print_WritesToOutput() {
		Assert.Equal( ["hello", "3", ""], Run( "Print(\"hello\");\nPrint(1 + 2);\nPrint(null);" ) );
	}

	[Fact]
	public void DefaultImports_AreAvailable() {
		Assert.Equal( ["6"], Run( "var sb = new StringBuilder();\nsb.Append(new List<int> { 1, 2, 3 }.Sum());\nPrint(sb);" ) );
	}

	[Fact]
	public void UpperAlias_IsAvailable() {
		Assert.Equal( ["x"], Run( "PRINT(\"x\");" ) );
	}

	[Fact]
	public void BalloonDefault_IsAvailable() {
		// 関数 Balloon と同名のクラスがあっても BalloonDefault 経由で初期値を設定できる (コンパイルのみ)
		ScriptRunner.Compile( """
			BalloonDefault.FontSize = 14;
			BalloonDefault.FontName = "Meiryo";
			BalloonDefault.ForeColor = Color.White;
			BalloonDefault.BackColor = Color.Navy;
			BalloonDefault.Opacity = 0.8;
			Balloon b = Balloon("x", fontSize: 20);
			BALLOONDEFAULT.FONTSIZE = 12;
			BALLOONDEFAULT.FONTNAME = null;
			BALLOONDEFAULT.FORECOLOR = Color.Black;
			BALLOONDEFAULT.BACKCOLOR = Color.White;
			BALLOONDEFAULT.OPACITY = 1.0;
			""" );
	}

	[Fact]
	public void UpperAlias_StaticMembers_Available() {
		// static の GetClipboard / SetClipboard にも別名がある (コンパイルのみ。実行するとクリップボードを書き換える)
		ScriptRunner.Compile( "SETCLIPBOARD(\"a\");\nstring s = GETCLIPBOARD();" );
	}

	[Fact]
	public void UpperAlias_WindowAndCaptureMembers_Available() {
		// Window・Capture のメンバー (static・プロパティ・既定値付きの引数を含む) にも別名がある (コンパイルのみ)
		ScriptRunner.Compile( """
			Window? w = Window.FROMHANDLE(IntPtr.Zero) ?? ACTIVEWINDOW();
			string t = w.TITLE;
			w.ACTIVATE().MOVEWINDOW(0, 0, 800, 600).MOUSE(MouseButton.Left, KeyAction.Click, 10, 20);
			w.SHORTCUTKEY(Keys.ControlKey, Keys.S);
			Point[] a = w.CHECKIMAGES("a.png", max: 1);
			Capture s = w.CAPTURE();
			Point? p = s.CHECKCOLOR(Color.Red, tolerance: 5);
			Color c = s.PEEKCOLOR(0, 0);
			Rectangle r = s.BOUNDS;
			"""
		);
	}

	[Fact]
	public void UpperAlias_Capture_RunsSameAsOriginal() {
		var path = Path.Combine( Path.GetTempPath(), $"CsWSC_Test_{Guid.NewGuid():N}.png" );
		using( var bmp = new Bitmap( 4, 4 ) ) {
			bmp.SetPixel( 2, 1, Color.Red );
			bmp.Save( path, System.Drawing.Imaging.ImageFormat.Png );
		}

		try {
			Assert.Equal( ["4x4 True"], Run( $$"""
				var s = CAPTURE(@"{{path}}");
				PRINT($"{s.WIDTH}x{s.HEIGHT} {s.CHECKCOLOR(Color.Red) == new Point(2, 1)}");
				"""
			) );
		} finally {
			File.Delete( path );
		}
	}

	[Fact]
	public void Capture_IsCallableFromScript() {
		// 型名 Capture とメソッド名 Capture が衝突しないことを確かめる (コンパイルのみ)
		ScriptRunner.Compile( "var s = Capture(@\"C:\\x.png\");\nvar t = Capture();\nPrint(s.CheckColor(Color.Red) ?? t.CheckImage(\"a.png\"));" );
	}

	[Fact]
	public void CheckColor_MultipleColors_CompilesFromScript() {
		// 1 色版と複数色版のオーバーロードがあいまいにならないことを確かめる (コンパイルのみ)
		ScriptRunner.Compile( "Point? a = CheckColor(Color.Red);\n(Color Color, Point Point)? b = CheckColor([Color.Red, Color.Lime], new Rectangle(0, 0, 400, 300), 10);\nif (b is { } f) Print($\"{f.Color} {f.Point}\");\nforeach (var (c, p) in CheckColors([Color.Red], tolerance: 5)) Print(c);" );
	}

	[Fact]
	public void EnumerableParameters_AcceptArraysListsAndParams() {
		// 配列から IEnumerable / IReadOnlyList に変えた引数が、可変長引数・配列・List のどれでも渡せることを確かめる (コンパイルのみ)
		ScriptRunner.Compile( """
			ShortcutKey(Keys.ControlKey, Keys.S);
			ShortcutKey(new List<Keys> { Keys.A });
			var i = SelectBox("選択", "はい", "いいえ");
			var j = SelectBox(SelectKind.List, "選択", new[] { "a", "b" });
			var k = SelectBoxMulti("選択", Enumerable.Range(1, 3).Select(n => $"項目{n}").ToList());
			var m = PopupMenu(new List<string> { "a", "-", "b" });
			var c = CheckColor(new[] { Color.Red, Color.Lime });
			var w = GetWindow("メモ帳");
			w?.ShortcutKey(Keys.ControlKey, Keys.A);
			"""
		);
	}

	[Fact]
	public void CompileError_ReportsLine() {
		var ex = Assert.Throws<ScriptException>( () => ScriptRunner.Compile( "var a = 1;\nvar b = ;\n" ) );
		Assert.Equal( 2, ex.Line );
		Assert.StartsWith( "2行目: ", ex.Message );
	}

	[Fact]
	public void RuntimeError_ReportsLineAndInnerException() {
		var ex = Assert.Throws<ScriptException>( () => Run( "Print(\"a\");\n\nthrow new InvalidOperationException(\"boom\");" ) );
		Assert.Equal( 3, ex.Line );
		Assert.IsType<InvalidOperationException>( ex.InnerException );
		Assert.Equal( "3行目: InvalidOperationException: boom", ex.Message );
	}

	[Fact]
	public void Cache_RunsCompiledScriptWithLineNumbers() {
		var directory = Path.Combine( Path.GetTempPath(), "CsWSC.Tests." + Guid.NewGuid().ToString( "N" ) );
		var path = Path.Combine( directory, "cache.csws" );
		const string Code = "Print(\"a\");\nthrow new InvalidOperationException(\"boom\");";
		try {
			for( var i = 0; i < 2; i++ ) // 1 回目はコンパイルして保存、2 回目はキャッシュから実行
			{
				var output = new List<string>();
				var ex = Assert.Throws<ScriptException>( () =>
					ScriptRunner.Run( Code, output.Add, CancellationToken.None, path: path, cacheDirectory: directory ) );
				Assert.Equal( ["a"], output );
				Assert.Equal( 2, ex.Line );
				Assert.Single( Directory.GetFiles( directory, "*.dll" ) );
			}
		} finally { Directory.Delete( directory, true ); }
	}

	[Fact]
	public void Cache_DetectsLoadedScriptChanges() {
		var directory = Path.Combine( Path.GetTempPath(), "CsWSC.Tests." + Guid.NewGuid().ToString( "N" ) );
		var cache = Path.Combine( directory, "cache" );
		var path = Path.Combine( directory, "main.csws" );
		var lib = Path.Combine( directory, "lib.csx" );
		const string Code = "#load \"lib.csx\"\nPrint(Greet());";
		List<string> RunMain() {
			var output = new List<string>();
			ScriptRunner.Run( Code, output.Add, CancellationToken.None, path: path, cacheDirectory: cache );
			return output;
		}
		Directory.CreateDirectory( directory );
		try {
			File.WriteAllText( lib, "string Greet() => \"a\";" );
			Assert.Equal( ["a"], RunMain() );
			Assert.Equal( ["a"], RunMain() ); // キャッシュから
			File.WriteAllText( lib, "string Greet() => \"bb\";" );
			File.SetLastWriteTimeUtc( lib, DateTime.UtcNow.AddMinutes( 1 ) );
			Assert.Equal( ["bb"], RunMain() ); // 変更を検知してコンパイルし直す
		} finally { Directory.Delete( directory, true ); }
	}

	[Fact]
	public void Cache_RunsScriptReferencingDll() {
		var directory = Path.Combine( Path.GetTempPath(), "CsWSC.Tests." + Guid.NewGuid().ToString( "N" ) );
		var cache = Path.Combine( directory, "cache" );
		var path = Path.Combine( directory, "main.csws" );
		Directory.CreateDirectory( Path.Combine( directory, "lib" ) );
		try {
			// 既定の探索場所に無い DLL を作る
			var name = "CsWSCTestLib" + Guid.NewGuid().ToString( "N" );
			var library = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create( name,
				[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText( "public static class Lib { public static string Greet() => \"a\"; }" )],
				[Microsoft.CodeAnalysis.MetadataReference.CreateFromFile( typeof( object ).Assembly.Location )],
				new( Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary ) );
			using( var stream = File.Create( Path.Combine( directory, "lib", name + ".dll" ) ) ) {
				Assert.True( library.Emit( stream ).Success );
			}

			var code = $"#r \"lib/{name}.dll\"\nPrint(Lib.Greet());";
			for( var i = 0; i < 2; i++ ) // 1 回目はコンパイル、2 回目はキャッシュから
			{
				var output = new List<string>();
				ScriptRunner.Run( code, output.Add, CancellationToken.None, path: path, cacheDirectory: cache );
				Assert.Equal( ["a"], output );
			}
			Assert.Single( Directory.GetFiles( cache, "*.dll" ) );
		} finally { try { Directory.Delete( directory, true ); } catch( UnauthorizedAccessException ) { } catch( IOException ) { } } // 読み込んだ DLL はロックされている
	}

	[Fact]
	public void Cancellation_StopsScript() {
		using var cts = new CancellationTokenSource();
		cts.Cancel();
		Assert.ThrowsAny<OperationCanceledException>( () => ScriptRunner.Run( "Sleep(10_000);", _ => { }, cts.Token ) );
	}

	[Fact]
	public void WpfInput_AvailableWithAlias() {
		Assert.Equal( ["A"], Run( "using Input = System.Windows.Input;\nPrint(Input.Key.A.ToString());" ) );
	}

	[Fact]
	public void Reference_RelativeToScriptPath() {
		var dir = Path.GetDirectoryName( typeof( ScriptGlobals ).Assembly.Location )!;
		var path = Path.Combine( dir, "test.csws" );
		ScriptRunner.Compile( "#r \"CsWSC.Core.dll\"\nvar t = typeof(CsWSC.ScriptGlobals);", path );
	}


	[Theory]
	[InlineData( "var n = 0;\nwhile (true) { n++; }" )]
	[InlineData( "var n = 0;\nwhile (true) n++;" )]
	[InlineData( "var n = 0;\nfor (;;) n++;" )]
	[InlineData( "var n = 0;\ndo n++; while (true);" )]
	[InlineData( "var n = 0;\nvoid F() { while (true) n++; }\nF();" )]
	public void InfiniteLoopWithoutApi_CanBeStopped( string code ) {
		using var cts = new CancellationTokenSource( TimeSpan.FromMilliseconds( 300 ) );
		var task = Task.Run( () => ScriptRunner.Run( code, _ => { }, cts.Token ) );
		var ex = Assert.ThrowsAny<AggregateException>( () => task.Wait( TimeSpan.FromSeconds( 10 ) ) );
		Assert.IsAssignableFrom<OperationCanceledException>( ex.InnerException );
	}

	[Fact]
	public void InfiniteLoopInLoadedScript_CanBeStopped() {
		var directory = Path.Combine( Path.GetTempPath(), "CsWSC.Tests." + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( directory );
		try {
			File.WriteAllText( Path.Combine( directory, "lib.csx" ), "void Loop() { var n = 0; while (true) n++; }" );
			using var cts = new CancellationTokenSource( TimeSpan.FromMilliseconds( 300 ) );
			var task = Task.Run( () => ScriptRunner.Run( "#load \"lib.csx\"\nLoop();", _ => { }, cts.Token, path: Path.Combine( directory, "main.csws" ) ) );
			var ex = Assert.ThrowsAny<AggregateException>( () => task.Wait( TimeSpan.FromSeconds( 10 ) ) );
			Assert.IsAssignableFrom<OperationCanceledException>( ex.InnerException );
		} finally { Directory.Delete( directory, true ); }
	}

	[Theory]
	[InlineData( "WindowFromPoint(0, 0)!.WaitClose();" )]
	[InlineData( "InputBox(\"x\");" )]
	[InlineData( "MessageBox(\"x\", \"t\", MsgButtons.YesNo, MsgIcon.Question);" )]
	public void BlockingApi_CanBeStopped( string code ) {
		Exception? error = null;
		var thread = new Thread( () => {
			using var cts = new CancellationTokenSource( TimeSpan.FromMilliseconds( 500 ) );
			try { ScriptRunner.Run( code, _ => { }, cts.Token ); } catch( Exception ex ) { error = ex; }
		} );
		thread.SetApartmentState( ApartmentState.STA );
		thread.Start();
		Assert.True( thread.Join( TimeSpan.FromSeconds( 10 ) ), "停止しなかった" );
		Assert.IsAssignableFrom<OperationCanceledException>( error );
	}

	[Fact]
	public void Sleep_NegativeOrZero_ReturnsImmediately() {
		var sw = System.Diagnostics.Stopwatch.StartNew();
		Assert.Equal( ["ok"], Run( "Sleep(-1);\nSleep(-100);\nSleep(0);\nPrint(\"ok\");" ) );
		Assert.True( sw.ElapsedMilliseconds < 5000, $"{sw.ElapsedMilliseconds} ms" );
	}

	[Theory]
	[InlineData( "Print(\"a\");\nExitScript();\nPrint(\"b\");" )]
	[InlineData( "void F() { ExitScript(); }\nPrint(\"a\");\nF();\nPrint(\"b\");" )]
	public void ExitScript_StopsScript( string code ) {
		var output = new List<string>();
		Assert.ThrowsAny<OperationCanceledException>( () => ScriptRunner.Run( code, output.Add, CancellationToken.None ) );
		Assert.Equal( ["a"], output );
	}

	// 存在しないウィンドウのタイトル
	private static readonly string NoSuchWindow = "CsWSC.Tests." + Guid.NewGuid().ToString( "N" );

	[Fact]
	public void GetWindow_NotFound_TimeoutZero_ReturnsNullImmediately() {
		var sw = System.Diagnostics.Stopwatch.StartNew();
		Assert.Equal( ["True"], Run( $"Print(GetWindow(\"{NoSuchWindow}\") == null);" ) );
		Assert.Equal( ["True"], Run( $"Print(GetWindow(\"{NoSuchWindow}\", timeoutMs: 0) == null);" ) );
		Assert.True( sw.ElapsedMilliseconds < 5000, $"{sw.ElapsedMilliseconds} ms" );
	}

	[Fact]
	public void GetWindow_NotFound_WaitsForTimeout() {
		Assert.Equal( ["True"], Run( $"var sw = System.Diagnostics.Stopwatch.StartNew();\nvar w = GetWindow(\"{NoSuchWindow}\", timeoutMs: 300);\nPrint(w == null && sw.ElapsedMilliseconds >= 300);" ) );
	}

	[Fact]
	public void GetWindow_NegativeTimeout_WaitsUntilStopped() {
		using var cts = new CancellationTokenSource( TimeSpan.FromMilliseconds( 500 ) );
		var task = Task.Run( () => ScriptRunner.Run( $"GetWindow(\"{NoSuchWindow}\", timeoutMs: -1);\nPrint(\"x\");", _ => { }, cts.Token ) );
		var ex = Assert.ThrowsAny<AggregateException>( () => task.Wait( TimeSpan.FromSeconds( 10 ) ) );
		Assert.IsAssignableFrom<OperationCanceledException>( ex.InnerException );
	}

	[Fact]
	public void StopChecks_KeepLineNumbers() {
		var ex = Assert.Throws<ScriptException>( () => Run( "var a = 0;\nwhile (a < 1)\n{\n    a++;\n    throw new Exception(\"x\");\n}" ) );
		Assert.Equal( 5, ex.Line );
	}
}
