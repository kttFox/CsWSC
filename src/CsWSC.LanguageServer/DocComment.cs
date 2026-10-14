using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace CsWSC.LanguageServer;

/// <summary>シンボルの XML ドキュメントを読みます。&lt;inheritdoc&gt; は継承元の内容に展開します。</summary>
internal static class DocComment {
	// inheritdoc が循環していても止まるように、展開する深さを制限する
	private const int MaxDepth = 8;

	/// <summary>XML ドキュメントを &lt;member&gt; 要素として返します。無ければ null です。</summary>
	public static XElement? Parse( ISymbol symbol ) => Parse( symbol, 0 );

	private static XElement? Parse( ISymbol symbol, int depth ) {
		var xml = symbol.GetDocumentationCommentXml( expandIncludes: true );
		if( string.IsNullOrWhiteSpace( xml ) ) return null;
		XElement doc;
		try { doc = XElement.Parse( xml.Trim().StartsWith( "<member" ) ? xml : $"<member>{xml}</member>" ); } catch( System.Xml.XmlException ) { return null; }

		foreach( var inherit in doc.Elements( "inheritdoc" ).ToList() ) {
			inherit.Remove();
			if( depth >= MaxDepth || InheritFrom( symbol, (string?)inherit.Attribute( "cref" ) ) is not { } source ) continue;
			if( Parse( source, depth + 1 ) is { } inherited ) Merge( doc, inherited );
		}

		return doc;
	}

	/// <summary>
	/// 自分に無い要素だけを継承元から足します (自分に書いた要素が優先)。param・typeparam は名前ごとに判定します。
	/// </summary>
	private static void Merge( XElement doc, XElement inherited ) {
		foreach( var e in inherited.Elements() ) {
			var exists = e.Name.LocalName is "param" or "typeparam"
				? doc.Elements( e.Name ).Any( d => (string?)d.Attribute( "name" ) == (string?)e.Attribute( "name" ) )
				: doc.Element( e.Name ) != null;
			if( !exists ) doc.Add( new XElement( e ) );
		}
	}

	/// <summary>継承元のシンボルです。cref があればそれを、無ければ override・インターフェイスの元のメンバーを使います。</summary>
	private static ISymbol? InheritFrom( ISymbol symbol, string? cref ) {
		if( cref != null ) return FindById( symbol, cref );
		return symbol switch {
			IMethodSymbol { OverriddenMethod: { } m } => m,
			IPropertySymbol { OverriddenProperty: { } p } => p,
			_ => symbol.ContainingType?.AllInterfaces.SelectMany( i => i.GetMembers( symbol.Name ) )
				.FirstOrDefault( m => SymbolEqualityComparer.Default.Equals( symbol.ContainingType.FindImplementationForInterfaceMember( m ), symbol ) ),
		};
	}

	/// <summary>
	/// ドキュメント ID (M:名前空間.型.メンバー(引数) など) のシンボルを、同じアセンブリから探します。
	/// メタデータから読んだシンボルにはコンパイルが無いので、型名で引いてからメンバーの ID を比べます。
	/// </summary>
	private static ISymbol? FindById( ISymbol symbol, string id ) {
		if( id.Length < 3 || id[1] != ':' ) return null;
		var name = id[2..];
		var paren = name.IndexOf( '(' );
		if( paren >= 0 ) name = name[..paren];
		var assembly = symbol.ContainingAssembly;
		if( id[0] == 'T' ) return assembly?.GetTypeByMetadataName( name );

		var dot = name.LastIndexOf( '.' );
		if( dot < 0 ) return null;
		var type = SymbolEqualityComparer.Default.Equals( symbol.ContainingType?.ContainingAssembly, assembly ) && symbol.ContainingType?.ToDisplayString() == name[..dot]
			? symbol.ContainingType
			: assembly?.GetTypeByMetadataName( name[..dot] );
		return type?.GetMembers( name[( dot + 1 )..] ).FirstOrDefault( m => m.GetDocumentationCommentId() == id );
	}
}
