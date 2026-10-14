namespace CsWSC.Tests;

public class MouseOriginTests {
	// MouseOrigin はウィンドウのタイトルを取るので、別スレッドから呼んでも応答できるよう
	// メッセージループを回す専用のスレッドでフォームを作る
	private sealed class TestWindow : IDisposable {
		private readonly System.Windows.Forms.Form _form = new();
		private readonly Thread _thread;
		public Window Window { get; }

		public TestWindow() {
			using var ready = new ManualResetEventSlim();
			IntPtr handle = 0;
			_thread = new Thread( () => {
				handle = _form.Handle;
				ready.Set();
				System.Windows.Forms.Application.Run();
			} ) { IsBackground = true };
			_thread.SetApartmentState( ApartmentState.STA );
			_thread.Start();
			ready.Wait();
			Window = CsWSC.Window.FromHandle( handle )!;
		}

		public void Dispose() {
			_form.BeginInvoke( () => { _form.Dispose(); System.Windows.Forms.Application.ExitThread(); } );
			_thread.Join( 5000 );
		}
	}

	[Fact]
	public async Task AsyncMethod_RestoredOnReturn() {
		// async メソッドの中で切り替えた基準は、中では効き、抜けると呼び出し前に戻る
		using var tw = new TestWindow();
		var g = new ScriptGlobals( _ => { }, CancellationToken.None );
		Window? inside = null;

		async Task Setup() {
			g.MouseOrigin( tw.Window );
			await Task.Yield();
			inside = g.OriginWindow;
		}

		await Setup();
		Assert.Equal( tw.Window, inside );
		Assert.Null( g.OriginWindow );
	}

	[Fact]
	public async Task AsyncMethod_ConfigureAwaitFalse_SameBehavior() {
		// ConfigureAwait(false) で別のスレッドで再開しても、基準は続き、抜けると戻る
		using var tw = new TestWindow();
		var g = new ScriptGlobals( _ => { }, CancellationToken.None );
		Window? inside = null;

		async Task Setup() {
			g.MouseOrigin( tw.Window );
			await Task.Delay( 10 ).ConfigureAwait( false );
			inside = g.OriginWindow;
		}

		await Setup().ConfigureAwait( false );
		Assert.Equal( tw.Window, inside );
		Assert.Null( g.OriginWindow );
	}

	[Fact]
	public void Thread_DoesNotInheritAndDoesNotLeak() {
		// Thread は親の基準を引き継がず、中で切り替えても親に影響しない
		using var tw1 = new TestWindow();
		using var tw2 = new TestWindow();
		var g = new ScriptGlobals( _ => { }, CancellationToken.None );
		g.MouseOrigin( tw1.Window );

		Window? atStart = tw1.Window, afterSet = null;
		using var done = new ManualResetEventSlim();
		g.Thread( () => {
			atStart = g.OriginWindow;
			g.MouseOrigin( tw2.Window );
			afterSet = g.OriginWindow;
			done.Set();
		} );
		Assert.True( done.Wait( 5000 ) );

		Assert.Null( atStart );
		Assert.Equal( tw2.Window, afterSet );
		Assert.Equal( tw1.Window, g.OriginWindow );
	}

	[Fact]
	public void Using_RestoresPrevious() {
		using var tw = new TestWindow();
		var g = new ScriptGlobals( _ => { }, CancellationToken.None );
		using( g.MouseOrigin( tw.Window ) ) {
			Assert.Equal( tw.Window, g.OriginWindow );
		}
		Assert.Null( g.OriginWindow );
	}
}
