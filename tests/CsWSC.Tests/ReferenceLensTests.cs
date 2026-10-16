using CsWSC.LanguageServer;

namespace CsWSC.Tests;

public class ReferenceLensTests {
	private readonly LanguageService _service = new( new ScriptWorkspace() );

	// 定義の名前と参照数の組
	private async Task<(string Name, int Count)[]> Lenses( string text, params string[] kinds ) =>
		( await _service.ReferenceLensesAsync( "untitled:test", text, kinds.Length == 0 ? null : kinds ) )
			.Select( l => (text[l.Start..l.End], l.References.Length) ).ToArray();

	[Fact]
	public async Task TopLevelMethod() {
		Assert.Equal( [("Add", 2)], await Lenses( "int Add( int a, int b ) => a + b;\r\nPrint( Add( 1, 2 ) );\r\nvar f = Add;" ) );
	}

	[Fact]
	public async Task Unused_Zero() {
		Assert.Equal( [("F", 0)], await Lenses( "void F() { }" ) );
	}

	[Fact]
	public async Task ClassMembers() {
		Assert.Equal( [("C", 1), ("Value", 2), ("Run", 1)], await Lenses(
			"class C {\r\n\tint Value { get; set; }\r\n\tvoid Run() { Value = Value + 1; }\r\n}\r\nnew C().Run();" ) );
	}

	[Fact]
	public async Task LocalFunction() {
		Assert.Equal( [("G", 1)], await Lenses( "{\r\n\tvoid G() { }\r\n\tG();\r\n}" ) );
	}

	[Fact]
	public async Task Overloads_CountedSeparately() {
		Assert.Equal( [("F", 1), ("F", 2)], await Lenses( "void F( int x ) { }\r\nvoid F( string s ) { }\r\nF( 1 );\r\nF( \"a\" );\r\nF( \"b\" );" ) );
	}

	[Fact]
	public async Task SameNameApi_NotCounted() {
		Assert.Equal( [("C", 0), ("Sleep", 0)], await Lenses( "class C { void Sleep() { } }\r\nSleep( 1 );" ) );
	}

	[Fact]
	public async Task FieldsAndConstants() {
		Assert.Equal( [("C", 0), ("count", 1), ("Max", 1), ("Min", 0)], await Lenses(
			"class C {\r\n\tint count;\r\n\tconst int Max = 10, Min = 0;\r\n\tbool Full => count >= Max;\r\n}", "type", "field" ) );
	}

	[Fact]
	public async Task TopLevelVariable_NotCounted() {
		Assert.Empty( await Lenses( "var x = 1;\r\nPrint( x );" ) );
	}

	[Fact]
	public async Task EnumAndMembers() {
		Assert.Equal( [("Color", 2), ("Red", 1), ("Blue", 0)], await Lenses( "enum Color { Red, Blue }\r\nColor c = Color.Red;" ) );
	}

	[Fact]
	public async Task Events() {
		Assert.Equal( [("Changed", 2)], await Lenses(
			"class C {\r\n\tevent System.Action? Changed;\r\n\tvoid F() { Changed += () => { }; Changed?.Invoke(); }\r\n}", "event" ) );
	}

	[Fact]
	public async Task GenericType() {
		Assert.Equal( [("Box", 2)], await Lenses( "class Box<T> { }\r\nvar a = new Box<int>();\r\nBox<string>? b = null;", "type" ) );
	}

	[Fact]
	public async Task Kinds_Filter() {
		Assert.Equal( [("Value", 0)], await Lenses( "class C {\r\n\tint Value { get; }\r\n\tvoid Run() { }\r\n}", "property" ) );
	}
}
