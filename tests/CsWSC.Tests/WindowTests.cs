namespace CsWSC.Tests;

public class WindowTests {
	[Fact]
	public void Equality_SameHandle_AreEqual() {
		// 取得し直した Window どうしでも、同じハンドルなら等しい
		using var form = new System.Windows.Forms.Form();
		using var other = new System.Windows.Forms.Form();
		var a = Window.FromHandle( form.Handle )!;
		var b = Window.FromHandle( form.Handle )!;
		var c = Window.FromHandle( other.Handle )!;
		Assert.NotSame( a, b );
		Assert.True( a == b );
		Assert.False( a != b );
		Assert.True( a.Equals( b ) );
		Assert.Equal( a.GetHashCode(), b.GetHashCode() );
		Assert.Single( new HashSet<Window> { a, b } );
		Assert.False( a == c );
		Assert.True( a != c );
	}

	[Fact]
	public void Equality_Null() {
		using var form = new System.Windows.Forms.Form();
		var a = Window.FromHandle( form.Handle );
		Window? none = null;
		Assert.False( a == null );
		Assert.False( null == a );
		Assert.True( a != null );
		Assert.True( none == null );
		Assert.False( none != null );
		Assert.Null( Window.FromHandle( IntPtr.Zero ) );
	}

	[Fact]
	public void Equality_FromScript() {
		// スクリプトから == で比べられる (コンパイルのみ)
		ScriptRunner.Compile( "var w = GetWindow(\"メモ帳\");\nif (ActiveWindow() == w || WindowFromPoint(0, 0) != w) Print(\"ok\");" );
	}

	[Fact]
	public void Control_ChildrenAndText() {
		using var form = new System.Windows.Forms.Form();
		var panel = new System.Windows.Forms.Panel();
		var box = new System.Windows.Forms.TextBox { Text = "abc" };
		panel.Controls.Add( box );
		form.Controls.Add( panel );
		// 表示していないフォームでは子のハンドルが作られないので、親から順に作っておく
		var w = Window.FromHandle( form.Handle )!;
		_ = panel.Handle;
		_ = box.Handle;

		var edit = w.FindChild( "abc", "Edit" );
		Assert.NotNull( edit );
		Assert.Equal( box.Handle, edit.Handle );
		Assert.Contains( w.GetChildren(), c => c.Handle == panel.Handle );

		// 親はパネル (Control)、その親はフォーム (Window)
		var parent = Assert.IsType<Control>( edit.Parent );
		Assert.Equal( panel.Handle, parent.Handle );
		Assert.IsType<Window>( parent.Parent );
		Assert.True( edit.TopWindow == w );

		Assert.True( edit.SetText( "xyz" ) );
		Assert.Equal( "xyz", edit.Text );
		Assert.True( edit.SetText( "!", append: true ) );
		Assert.Equal( "xyz!", box.Text );
	}

	[Fact]
	public void Control_FromScript() {
		// スクリプトでは WinForms の Control と名前が重なるので var で受ける (コンパイルのみ)
		ScriptRunner.Compile( "var c = ControlUnderMouse();\nif (c != null && c.TopWindow == WindowUnderMouse()) c.SetText(\"a\");\nCsWSC.Control? d = GetWindow(\"メモ帳\")?.FindChild(\"\") ?? ControlFromPoint(0, 0) ?? ControlFromPoint(new Point(0, 0));" );
	}
}
