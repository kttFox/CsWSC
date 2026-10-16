using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsWSC.LanguageServer;

/// <summary>参照の場所です。</summary>
internal sealed record ReferenceEntry( int Start, int End );
/// <summary>定義の名前の範囲と、このドキュメント内でそれを参照している場所です。</summary>
internal sealed record ReferenceLens( int Start, int End, ReferenceEntry[] References );

/// <summary>参照数 (CodeLens) です。</summary>
internal sealed partial class LanguageService {
	/// <summary>
	/// このドキュメントにある定義ごとに、このドキュメント内の参照を返します。
	/// </summary>
	/// <param name="kinds">
	/// 対象にする定義の種類 (method・property・field・event・type) です。null ならすべてです。
	/// method はメソッドとローカル関数、field はフィールド・定数・列挙型のメンバー、type はクラス・構造体・インターフェース・列挙型・デリゲートです。
	/// </param>
	public async Task<ReferenceLens[]> ReferenceLensesAsync( string uri, string text, IReadOnlyCollection<string>? kinds = null ) {
		var doc = Workspace.GetDocument( uri, text );
		var root = await doc.GetSyntaxRootAsync();
		var model = await doc.GetSemanticModelAsync();
		if( root == null || model == null ) return [];

		var declared = new Dictionary<ISymbol, (SyntaxToken Name, List<ReferenceEntry> References)>( SymbolEqualityComparer.Default );
		foreach( var node in root.DescendantNodes() ) {
			var (kind, name) = node switch {
				MethodDeclarationSyntax m => ("method", m.Identifier),
				LocalFunctionStatementSyntax l => ("method", l.Identifier),
				PropertyDeclarationSyntax p => ("property", p.Identifier),
				// スクリプトのトップレベルの変数 (構文上はフィールド) は対象にしない
				VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax { Parent: not CompilationUnitSyntax } } v => ("field", v.Identifier),
				EnumMemberDeclarationSyntax e => ("field", e.Identifier),
				VariableDeclaratorSyntax { Parent.Parent: EventFieldDeclarationSyntax } v => ("event", v.Identifier),
				EventDeclarationSyntax e => ("event", e.Identifier),
				BaseTypeDeclarationSyntax t => ("type", t.Identifier),
				DelegateDeclarationSyntax d => ("type", d.Identifier),
				_ => (null, default),
			};
			if( kind == null || name.IsMissing || kinds != null && !kinds.Contains( kind ) ) continue;
			if( model.GetDeclaredSymbol( node ) is { } symbol ) declared.TryAdd( symbol, (name, []) );
		}
		if( declared.Count == 0 ) return [];

		// 名前が一致する識別子だけ意味を調べる
		var names = declared.Keys.Select( s => s.Name ).ToHashSet();
		foreach( var token in root.DescendantTokens() ) {
			if( !token.IsKind( SyntaxKind.IdentifierToken ) || !names.Contains( token.ValueText ) || token.Parent is not SimpleNameSyntax node ) continue;
			var info = model.GetSymbolInfo( node );
			// オーバーロードが決まらない呼び出し (引数の入力中など) も、候補が 1 つならその参照とみなす
			var symbol = info.Symbol ?? ( info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null );
			if( symbol is IMethodSymbol { ReducedFrom: { } reduced } ) symbol = reduced;
			symbol = symbol?.OriginalDefinition;
			if( symbol != null && declared.TryGetValue( symbol, out var entry ) ) entry.References.Add( new ReferenceEntry( token.Span.Start, token.Span.End ) );
		}
		return declared.Values
			.OrderBy( d => d.Name.SpanStart )
			.Select( d => new ReferenceLens( d.Name.Span.Start, d.Name.Span.End, d.References.ToArray() ) )
			.ToArray();
	}
}
