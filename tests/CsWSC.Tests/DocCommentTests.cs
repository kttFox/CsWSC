using System.Xml.Linq;
using CsWSC.LanguageServer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsWSC.Tests;

public class DocCommentTests {
	private static string? Param( XElement doc, string name ) =>
		doc.Elements( "param" ).FirstOrDefault( e => (string?)e.Attribute( "name" ) == name )?.Value;

	// 言語サーバーと同じく、CsWSC.Core.dll を XML ドキュメント付きのメタデータとして読み込む
	private static INamedTypeSymbol CoreType( string metadataName ) {
		var dll = typeof( ScriptGlobals ).Assembly.Location;
		var reference = MetadataReference.CreateFromFile( dll, documentation: XmlDocumentationProvider.CreateFromFile( Path.ChangeExtension( dll, ".xml" ) ) );
		// 引数の型 (Rectangle・Color・Keys など) を解決できるよう、使っているアセンブリも参照する
		var others = new[] { typeof( object ), typeof( Rectangle ), typeof( Bitmap ), typeof( System.Windows.Forms.Keys ) }
			.Select( t => t.Assembly.Location ).Distinct().Select( p => MetadataReference.CreateFromFile( p ) );
		var compilation = CSharpCompilation.Create( "Test", references: [reference, .. others] );
		return compilation.GetTypeByMetadataName( metadataName )!;
	}

	// オーバーロードは引数名の並びで選ぶ
	private static IMethodSymbol Method( INamedTypeSymbol type, string name, params string[] parameterNames ) =>
		type.GetMembers( name ).OfType<IMethodSymbol>().Single( m => m.Parameters.Select( p => p.Name ).SequenceEqual( parameterNames ) );

	[Fact]
	public void InheritDoc_ScriptGlobalsOverload_Expanded() {
		// 透過色を使わない版は inheritdoc で透過色版の説明を引き継ぎ、remarks だけ自分のものを使う
		var type = CoreType( "CsWSC.ScriptGlobals" );
		var doc = DocComment.Parse( Method( type, "CheckImage", "imagePath", "area", "tolerance" ) )!;
		Assert.Empty( doc.Elements( "inheritdoc" ) );
		Assert.Contains( "CHKIMG", doc.Element( "summary" )!.Value );
		Assert.NotNull( Param( doc, "imagePath" ) );
		Assert.NotNull( Param( doc, "tolerance" ) );
		Assert.NotNull( doc.Element( "returns" ) );
		Assert.Equal( "透過色を使わない版です。", doc.Element( "remarks" )!.Value );
	}

	[Fact]
	public void InheritDoc_XYOverload_KeepsOwnParams() {
		// x, y 版は Point 版の説明を引き継ぎ、x・y の param は自分のものを使う
		var type = CoreType( "CsWSC.ScriptGlobals" );
		var doc = DocComment.Parse( Method( type, "Move", "x", "y", "delayMs" ) )!;
		Assert.Contains( "MMV", doc.Element( "summary" )!.Value );
		Assert.NotNull( Param( doc, "x" ) );
		Assert.NotNull( Param( doc, "y" ) );
		Assert.Single( doc.Elements( "param" ), e => (string?)e.Attribute( "name" ) == "delayMs" );
	}

	[Fact]
	public void InheritDoc_WindowAndCapture_Expanded() {
		var window = DocComment.Parse( Method( CoreType( "CsWSC.ControlBase" ), "PeekColor", "x", "y" ) )!;
		Assert.Contains( "PEEKCOLOR", window.Element( "summary" )!.Value );
		var capture = DocComment.Parse( Method( CoreType( "CsWSC.Capture" ), "PeekColor", "x", "y" ) )!;
		Assert.NotNull( capture.Element( "summary" ) );
		Assert.NotNull( Param( capture, "x" ) );
	}

	[Fact]
	public void UpperAlias_InheritsOriginalDoc() {
		// 大文字の別名は元のメンバーの説明 (summary・param・example) をそのまま表示する
		var globals = CoreType( "CsWSC.ScriptGlobals" );
		Assert.Equal( DocComment.Parse( Method( globals, "Print", "value" ) )!.Element( "summary" )!.Value,
			DocComment.Parse( Method( globals, "PRINT", "value" ) )!.Element( "summary" )!.Value );
		var move = DocComment.Parse( Method( globals, "MOVE", "x", "y", "delayMs" ) )!;
		Assert.Contains( "MMV", move.Element( "summary" )!.Value );
		Assert.NotNull( Param( move, "x" ) );
		Assert.NotNull( DocComment.Parse( globals.GetMembers( "MOUSEPOSITION" ).Single() )!.Element( "summary" ) );
		Assert.NotNull( DocComment.Parse( globals.GetMembers( "GETCLIPBOARD" ).Single() )!.Element( "summary" ) );
		Assert.Contains( "PEEKCOLOR", DocComment.Parse( Method( CoreType( "CsWSC.ControlBase" ), "PEEKCOLOR", "x", "y" ) )!.Element( "summary" )!.Value );
		Assert.NotNull( DocComment.Parse( CoreType( "CsWSC.Capture" ).GetMembers( "WIDTH" ).Single() )!.Element( "summary" ) );
	}

	[Fact]
	public void AllPublicMembers_HaveNoUnresolvedInheritDoc() {
		// inheritdoc の参照先が見つからないと説明が消えるので、全メンバーで summary まで展開できることを確かめる
		foreach( var name in new[] { "CsWSC.ScriptGlobals", "CsWSC.ControlBase", "CsWSC.Window", "CsWSC.Control", "CsWSC.Capture" } ) {
			foreach( var member in CoreType( name ).GetMembers().Where( m => m.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public && !m.IsImplicitlyDeclared ) ) {
				var xml = member.GetDocumentationCommentXml();
				if( xml == null || !xml.Contains( "<inheritdoc" ) ) continue;
				var doc = DocComment.Parse( member );
				Assert.True( doc?.Element( "summary" ) != null, $"{member.ToDisplayString()} の inheritdoc を展開できない" );
			}
		}
	}

	[Fact]
	public void InheritDoc_SourceWithoutCref_UsesOverriddenMember() {
		const string source = """
			public class Base {
				/// <summary>元の説明。</summary>
				/// <param name="a">引数 a。</param>
				public virtual void M( int a ) { }
			}
			public class Derived : Base {
				/// <inheritdoc/>
				/// <remarks>追加の説明。</remarks>
				public override void M( int a ) { }
			}
			public class Loop {
				/// <inheritdoc cref="N"/>
				public void N() { }
			}
			""";
		var tree = CSharpSyntaxTree.ParseText( source, new CSharpParseOptions( documentationMode: DocumentationMode.Parse ) );
		var compilation = CSharpCompilation.Create( "Test", [tree], [MetadataReference.CreateFromFile( typeof( object ).Assembly.Location )] );
		var doc = DocComment.Parse( compilation.GetTypeByMetadataName( "Derived" )!.GetMembers( "M" ).Single() )!;
		Assert.Equal( "元の説明。", doc.Element( "summary" )!.Value );
		Assert.Equal( "引数 a。", Param( doc, "a" ) );
		Assert.Equal( "追加の説明。", doc.Element( "remarks" )!.Value );
		// 自分自身を指す inheritdoc でも止まる
		Assert.NotNull( DocComment.Parse( compilation.GetTypeByMetadataName( "Loop" )!.GetMembers( "N" ).Single() ) );
	}
}
