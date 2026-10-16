using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsWSC.LanguageServer;

/// <summary>/// と入力したときに展開するドキュメントコメントの雛形です。</summary>
internal sealed partial class LanguageService {
	/// <summary>
	/// offset の直前が、メンバーの前の空の /// なら、&lt;summary&gt; の後に続けるタグ (&lt;typeparam&gt;・&lt;param&gt;・&lt;returns&gt;) を返します。
	/// 展開しない場所なら null を返します。
	/// </summary>
	public async Task<string[]?> DocCommentSnippetAsync( string uri, string text, int offset ) {
		if( offset < 3 || offset > text.Length || text[( offset - 3 )..offset] != "///" ) return null;
		var doc = Workspace.GetDocument( uri, text );
		var root = await doc.GetSyntaxRootAsync();
		if( root == null ) return null;
		var trivia = root.FindTrivia( offset - 1 );
		// 中身のある /// や、複数行の /// の途中では展開しない
		if( !trivia.IsKind( SyntaxKind.SingleLineDocumentationCommentTrivia ) || trivia.ToFullString().Trim() != "///" ) return null;
		var member = trivia.Token.Parent?.AncestorsAndSelf().FirstOrDefault( n => n is MemberDeclarationSyntax or LocalFunctionStatementSyntax );
		if( member == null || member.GetFirstToken() != trivia.Token || member is GlobalStatementSyntax ) return null;

		var tags = new List<string>();
		var typeParameters = member switch {
			TypeDeclarationSyntax t => t.TypeParameterList,
			DelegateDeclarationSyntax d => d.TypeParameterList,
			MethodDeclarationSyntax m => m.TypeParameterList,
			LocalFunctionStatementSyntax l => l.TypeParameterList,
			_ => null,
		};
		if( typeParameters != null ) tags.AddRange( typeParameters.Parameters.Select( p => $"<typeparam name=\"{p.Identifier.ValueText}\"></typeparam>" ) );
		BaseParameterListSyntax? parameters = member switch {
			BaseMethodDeclarationSyntax m => m.ParameterList,
			DelegateDeclarationSyntax d => d.ParameterList,
			LocalFunctionStatementSyntax l => l.ParameterList,
			IndexerDeclarationSyntax i => i.ParameterList,
			TypeDeclarationSyntax t => t.ParameterList, // プライマリコンストラクター
			_ => null,
		};
		if( parameters != null ) tags.AddRange( parameters.Parameters.Select( p => $"<param name=\"{p.Identifier.ValueText}\"></param>" ) );
		var returnType = member switch {
			MethodDeclarationSyntax m => m.ReturnType,
			DelegateDeclarationSyntax d => d.ReturnType,
			LocalFunctionStatementSyntax l => l.ReturnType,
			OperatorDeclarationSyntax o => o.ReturnType,
			ConversionOperatorDeclarationSyntax c => c.Type,
			_ => null,
		};
		if( returnType != null && !( returnType is PredefinedTypeSyntax p && p.Keyword.IsKind( SyntaxKind.VoidKeyword ) ) ) tags.Add( "<returns></returns>" );
		return tags.ToArray();
	}
}
