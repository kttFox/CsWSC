using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ScintillaNET;

namespace CsWSC.UI;

/// <summary>
/// Roslyn の構文木と意味解析 (実行時と同じ条件でコンパイル) で Scintilla に色を付けます。
/// 配布サイズを抑えるため Microsoft.CodeAnalysis.Workspaces の Classifier は使わず、コンパイラ API だけで分類します。
/// 入力が止まってから少し待って、バックグラウンドで解析します。
/// </summary>
internal sealed class SyntaxHighlighter : IDisposable {
	private enum Style {
		Default = ScintillaNET.Style.Default,
		Keyword = 1, ControlKeyword, String, Number, Comment, DocComment, Type, Method, Preprocessor, Parameter,
	}

	// Visual Studio で制御キーワード (紫) として色分けされるもの
	private static readonly HashSet<SyntaxKind> ControlKeywords =
	[
		SyntaxKind.IfKeyword, SyntaxKind.ElseKeyword, SyntaxKind.WhileKeyword, SyntaxKind.ForKeyword, SyntaxKind.ForEachKeyword,
		SyntaxKind.DoKeyword, SyntaxKind.SwitchKeyword, SyntaxKind.CaseKeyword, SyntaxKind.TryKeyword, SyntaxKind.CatchKeyword,
		SyntaxKind.FinallyKeyword, SyntaxKind.GotoKeyword, SyntaxKind.BreakKeyword, SyntaxKind.ContinueKeyword,
		SyntaxKind.ReturnKeyword, SyntaxKind.ThrowKeyword, SyntaxKind.YieldKeyword, SyntaxKind.AwaitKeyword,
	];

	private readonly Scintilla _editor;
	private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
	private int _version;

	public SyntaxHighlighter( Scintilla editor ) {
		_editor = editor;
		// Visual Studio の既定 (ライト) に近い配色
		SetStyle( Style.Keyword, Color.Blue );
		SetStyle( Style.ControlKeyword, Color.FromArgb( 0x8F, 0x08, 0xC4 ) );
		SetStyle( Style.String, Color.FromArgb( 0xA3, 0x15, 0x15 ) );
		SetStyle( Style.Number, Color.FromArgb( 0x09, 0x86, 0x58 ) );
		SetStyle( Style.Comment, Color.Green );
		SetStyle( Style.DocComment, Color.Gray );
		SetStyle( Style.Type, Color.FromArgb( 0x2B, 0x91, 0xAF ) );
		SetStyle( Style.Method, Color.FromArgb( 0x74, 0x53, 0x1F ) );
		SetStyle( Style.Preprocessor, Color.Gray );
		SetStyle( Style.Parameter, Color.FromArgb( 0x1F, 0x37, 0x7F ) );

		_timer.Tick += async ( _, _ ) => { _timer.Stop(); await HighlightAsync(); };
		_editor.TextChanged += ( _, _ ) => { _version++; _timer.Stop(); _timer.Start(); };
	}

	private void SetStyle( Style style, Color color ) {
		var s = _editor.Styles[(int)style];
		s.ForeColor = color;
	}

	/// <summary>すぐに色付けします (ファイルを開いた直後など)。</summary>
	public void Refresh() { _timer.Stop(); _ = HighlightAsync(); }

	private async Task HighlightAsync() {
		var version = _version;
		var text = _editor.Text;
		List<(int Start, int Length, Style Style)> spans;
		try { spans = await Task.Run( () => Classify( text ) ); } catch( Exception ) { return; } // 色付けに失敗しても編集は続けられるようにする
																								 // 解析中に編集されたら、次の解析に任せる
		if( version != _version || _editor.IsDisposed ) return;

		_editor.StartStyling( 0 );
		_editor.SetStyling( text.Length, (int)Style.Default );
		foreach( var (start, length, style) in spans ) {
			_editor.StartStyling( start );
			_editor.SetStyling( length, (int)style );
		}
	}

	private static List<(int, int, Style)> Classify( string text ) {
		var compilation = ScriptRunner.GetCompilation( text );
		var tree = compilation.SyntaxTrees.Single();
		var model = compilation.GetSemanticModel( tree );
		var root = tree.GetRoot();
		var result = new List<(int, int, Style)>();

		foreach( var trivia in root.DescendantTrivia() ) {
			var style = trivia.Kind() switch {
				SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia => Style.Comment,
				SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia => Style.DocComment,
				_ when trivia.IsDirective => Style.Preprocessor,
				_ => Style.Default,
			};
			if( style != Style.Default ) result.Add( (trivia.Span.Start, trivia.Span.Length, style) );
		}

		foreach( var token in root.DescendantTokens() ) {
			var style = ClassifyToken( token, model );
			if( style != Style.Default ) result.Add( (token.Span.Start, token.Span.Length, style) );
		}
		return result;
	}

	private static Style ClassifyToken( SyntaxToken token, SemanticModel model ) {
		var kind = token.Kind();
		if( ControlKeywords.Contains( kind ) ) return Style.ControlKeyword;
		if( SyntaxFacts.IsKeywordKind( kind ) || token.IsContextualKeyword() ) return Style.Keyword;
		switch( kind ) {
			case SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken or SyntaxKind.InterpolatedStringTextToken
				or SyntaxKind.InterpolatedStringStartToken or SyntaxKind.InterpolatedStringEndToken
				or SyntaxKind.InterpolatedVerbatimStringStartToken or SyntaxKind.InterpolatedRawStringEndToken
				or SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken
				or SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken
				or SyntaxKind.Utf8StringLiteralToken or SyntaxKind.Utf8SingleLineRawStringLiteralToken
				or SyntaxKind.Utf8MultiLineRawStringLiteralToken:
				return Style.String;
			case SyntaxKind.NumericLiteralToken:
				return Style.Number;
			case SyntaxKind.IdentifierToken:
				break;
			default:
				return Style.Default;
		}

		var node = token.Parent;
		if( node is null ) return Style.Default;
		// var は識別子トークンのままなので、var という名前の型でなく推論された型ならキーワードとして色付けする
		if( node is IdentifierNameSyntax { IsVar: true } && model.GetSymbolInfo( node ).Symbol is ITypeSymbol { Name: not "var" } )
			return Style.Keyword;

		var info = model.GetSymbolInfo( node );
		var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault() ?? model.GetDeclaredSymbol( node );
		if( symbol is IAliasSymbol alias ) symbol = alias.Target;
		return symbol switch {
			ITypeParameterSymbol or INamedTypeSymbol => Style.Type,
			IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor } => Style.Type,
			IMethodSymbol => Style.Method,
			IParameterSymbol => Style.Parameter,
			_ => Style.Default,
		};
	}

	public void Dispose() => _timer.Dispose();
}
