using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tags;

namespace CsWSC.LanguageServer;

// ---- 拡張機能とやりとりするデータ (位置はすべて UTF-16 のオフセット) ----

internal sealed record CompletionEntry( int Index, string Label, string Kind, string? Detail, string? SortText, string? FilterText, string InsertText );
internal sealed record CompletionResult( int Start, int End, CompletionEntry[] Items );
internal sealed record HoverResult( string Signature, string? Documentation, int Start, int End );
internal sealed record SignatureEntry( string Label, string? Documentation, ParameterEntry[] Parameters );
internal sealed record ParameterEntry( string Label, string? Documentation );
internal sealed record SignatureResult( SignatureEntry[] Signatures, int ActiveSignature, int ActiveParameter );
internal sealed record DiagnosticEntry( int Start, int End, string Severity, string Code, string Message );
internal sealed record TextEditEntry( int Start, int End, string NewText );
/// <summary>定義の場所です。#load 先など別ファイルのこともあるので行・列 (0 始まり) で表します。Path が null なら要求したドキュメントです。</summary>
internal sealed record DefinitionEntry( string? Path, int StartLine, int StartCharacter, int EndLine, int EndCharacter );

/// <summary>名前の変更の準備です。変更できなければ Error に理由が入ります。</summary>
internal sealed record PrepareRenameResult( int Start, int End, string? Placeholder, string? Error );
/// <summary>名前の変更です。変更できなければ Edits は null で Error に理由が入ります。</summary>
internal sealed record RenameResult( TextEditEntry[]? Edits, string? Error );

/// <summary>補完・ホバー・引数ヒント・診断・定義へ移動・名前の変更・書式設定です。</summary>
internal sealed partial class LanguageService( ScriptWorkspace workspace ) {
	// 主コンストラクターの引数はほかの partial 宣言から見えないので、コードアクション (LanguageService.CodeActions.cs) 用に出す
	private ScriptWorkspace Workspace => workspace;

	// 詳細説明 (resolve) 用に直前の補完結果を覚えておく
	private (Document Document, CompletionList List)? _lastCompletion;

	public async Task<CompletionResult?> CompleteAsync( string uri, string text, int offset ) {
		var doc = workspace.GetDocument( uri, text );
		var service = CompletionService.GetService( doc );
		if( service == null ) return null;
		var list = await service.GetCompletionsAsync( doc, offset );
		_lastCompletion = (doc, list);
		var items = list.ItemsList.Select( ( item, i ) => new CompletionEntry(
			i,
			item.DisplayTextPrefix + item.DisplayText + item.DisplayTextSuffix,
			KindOf( item ),
			string.IsNullOrEmpty( item.InlineDescription ) ? null : item.InlineDescription,
			// ラベルと同じなら省略して応答を小さくする (候補が数千件になるため)
			item.SortText == item.DisplayText ? null : item.SortText,
			item.FilterText == item.DisplayText ? null : item.FilterText,
			item.DisplayText ) ).ToArray();
		return new CompletionResult( list.Span.Start, list.Span.End, items );
	}

	/// <summary>補完候補の説明 (シグネチャと XML ドキュメント) です。</summary>
	/// <param name="index">直前の補完結果での候補の番号です。</param>
	/// <returns>説明の文字列です。直前の補完結果が無いか、番号が範囲外か、説明が無ければ null です。</returns>
	public async Task<string?> ResolveCompletionAsync( int index ) {
		if( _lastCompletion is not var (doc, list) || index < 0 || index >= list.ItemsList.Count ) return null;
		var item = list.ItemsList[index];
		var service = CompletionService.GetService( doc );
		var description = service == null ? null : await service.GetDescriptionAsync( doc, item );
		if( description == null ) return null;

		// 1 行目 (シグネチャ) は Roslyn の説明を使い、本文は XML ドキュメントの remarks・returns・example まで含めたものに差し替える
		var lines = description.Text.Split( '\n', 2 );
		var symbol = await FindCompletionSymbolAsync( doc, list.Span.Start, item.DisplayText );
		var body = symbol == null ? null : Summary( symbol );
		return body == null ? description.Text : $"{lines[0].TrimEnd()}\n{body}";
	}

	/// <summary>補完候補の名前から、その位置で参照できるシンボルを探します (a.b の b なら a の型のメンバーから)。</summary>
	private static async Task<ISymbol?> FindCompletionSymbolAsync( Document doc, int position, string name ) {
		var root = await doc.GetSyntaxRootAsync();
		var model = await doc.GetSemanticModelAsync();
		if( root == null || model == null ) return null;
		INamespaceOrTypeSymbol? container = null;
		var token = root.FindToken( Math.Max( 0, position - 1 ) );
		if( token.IsKind( Microsoft.CodeAnalysis.CSharp.SyntaxKind.DotToken ) && token.Parent is MemberAccessExpressionSyntax access ) {
			var info = model.GetSymbolInfo( access.Expression ).Symbol;
			container = info as INamespaceOrTypeSymbol ?? model.GetTypeInfo( access.Expression ).Type;
			if( container == null ) return null;
		}
		return model.LookupSymbols( position, container, name ).FirstOrDefault();
	}

	public async Task<HoverResult?> HoverAsync( string uri, string text, int offset ) {
		var doc = workspace.GetDocument( uri, text );
		var symbol = await SymbolFinder.FindSymbolAtPositionAsync( doc, offset );
		if( symbol == null ) return null;
		var root = await doc.GetSyntaxRootAsync();
		var token = root!.FindToken( offset );
		var display = symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor
			? $"new {ctor.ContainingType.ToDisplayString( Format )}({string.Join( ", ", ctor.Parameters.Select( p => p.ToDisplayString( Format ) ) )})"
			: symbol.ToDisplayString( Format );
		return new HoverResult( display, Summary( symbol ), token.Span.Start, token.Span.End );
	}

	public async Task<SignatureResult?> SignatureHelpAsync( string uri, string text, int offset ) {
		var doc = workspace.GetDocument( uri, text );
		var root = await doc.GetSyntaxRootAsync();
		var model = await doc.GetSemanticModelAsync();
		if( root == null || model == null ) return null;

		// カーソルを含む最も内側の呼び出し (引数リストの括弧の内側)
		var node = root.FindToken( Math.Max( 0, offset - 1 ) ).Parent;
		ArgumentListSyntax? args = null;
		IMethodSymbol[] methods = [];
		for( ; node != null; node = node.Parent ) {
			if( node is InvocationExpressionSyntax inv && Inside( inv.ArgumentList, offset ) ) {
				args = inv.ArgumentList;
				methods = model.GetMemberGroup( inv.Expression ).OfType<IMethodSymbol>().ToArray();
				break;
			}
			if( node is BaseObjectCreationExpressionSyntax create && create.ArgumentList is { } al && Inside( al, offset ) ) {
				args = al;
				methods = model.GetSymbolInfo( create ).CandidateSymbols.Prepend( model.GetSymbolInfo( create ).Symbol )
					.OfType<IMethodSymbol>().SelectMany( m => m.ContainingType.InstanceConstructors ).Distinct<IMethodSymbol>( SymbolEqualityComparer.Default ).ToArray();
				if( methods.Length == 0 && model.GetTypeInfo( create ).Type is INamedTypeSymbol t ) methods = [.. t.InstanceConstructors];
				break;
			}
		}
		if( args == null || methods.Length == 0 ) return null;

		var activeParam = args.Arguments.GetSeparators().Count( s => s.SpanStart < offset );
		var argCount = args.Arguments.Count;
		var chosen = model.GetSymbolInfo( args.Parent! ).Symbol as IMethodSymbol;
		var signatures = methods.Select( ToSignature ).ToArray();
		var active = chosen != null ? Array.FindIndex( methods, m => SymbolEqualityComparer.Default.Equals( m, chosen ) ) : -1;
		if( active < 0 ) active = Array.FindIndex( methods, m => m.Parameters.Length >= Math.Max( argCount, activeParam + 1 ) || m.Parameters.LastOrDefault()?.IsParams == true );
		return new SignatureResult( signatures, Math.Max( 0, active ), activeParam );
	}

	public async Task<DiagnosticEntry[]> DiagnoseAsync( string uri, string text ) {
		var doc = workspace.GetDocument( uri, text );
		var model = await doc.GetSemanticModelAsync();
		if( model == null ) return [];
		return model.GetDiagnostics()
			.Where( d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning && d.Location.IsInSource )
			.Select( d => new DiagnosticEntry( d.Location.SourceSpan.Start, d.Location.SourceSpan.End,
				d.Severity == DiagnosticSeverity.Error ? "error" : "warning", d.Id, d.GetMessage() ) )
			.ToArray();
	}

	/// <summary>
	/// 識別子の色分け (セマンティックトークン)。[開始, 長さ, 種類, 開始, 長さ, 種類, ...] の並びです。
	/// 種類は <see cref="SemanticTokenTypes"/> の添字です (拡張機能の凡例と同じ順)。
	/// キーワード・文字列・コメントは TextMate 文法で色が付くので、ここでは識別子だけを返します。
	/// </summary>
	public async Task<int[]> SemanticTokensAsync( string uri, string text ) {
		var doc = workspace.GetDocument( uri, text );
		var spans = await Classifier.GetClassifiedSpansAsync( doc, new TextSpan( 0, text.Length ) );
		var result = new List<int>();
		foreach( var s in spans ) {
			if( !TokenTypeIndex.TryGetValue( s.ClassificationType, out var type ) ) continue;
			result.AddRange( [s.TextSpan.Start, s.TextSpan.Length, type] );
		}
		return [.. result];
	}

	/// <summary>セマンティックトークンの種類 (VS Code の標準の名前) です。順番は extension.js の凡例と合わせます。</summary>
	public static readonly string[] SemanticTokenTypes =
		["namespace", "class", "struct", "interface", "enum", "enumMember", "typeParameter", "method", "property", "event", "parameter", "variable"];

	private static readonly Dictionary<string, int> TokenTypeIndex = new Dictionary<string, string> {
		[ClassificationTypeNames.NamespaceName] = "namespace",
		[ClassificationTypeNames.ClassName] = "class",
		[ClassificationTypeNames.RecordClassName] = "class",
		[ClassificationTypeNames.DelegateName] = "class",
		[ClassificationTypeNames.StructName] = "struct",
		[ClassificationTypeNames.RecordStructName] = "struct",
		[ClassificationTypeNames.InterfaceName] = "interface",
		[ClassificationTypeNames.EnumName] = "enum",
		[ClassificationTypeNames.EnumMemberName] = "enumMember",
		[ClassificationTypeNames.TypeParameterName] = "typeParameter",
		[ClassificationTypeNames.MethodName] = "method",
		[ClassificationTypeNames.ExtensionMethodName] = "method",
		[ClassificationTypeNames.PropertyName] = "property",
		[ClassificationTypeNames.FieldName] = "property",
		[ClassificationTypeNames.ConstantName] = "property",
		[ClassificationTypeNames.EventName] = "event",
		[ClassificationTypeNames.ParameterName] = "parameter",
		[ClassificationTypeNames.LocalName] = "variable",
	}.ToDictionary( p => p.Key, p => Array.IndexOf( SemanticTokenTypes, p.Value ) );

	/// <summary>
	/// 定義の場所です。スクリプト内と #load 先のソースにある定義だけを返します
	/// (CsWSC の API や .NET の型はソースが無いので返さない)。
	/// </summary>
	public async Task<DefinitionEntry[]> DefinitionAsync( string uri, string text, int offset ) {
		var doc = workspace.GetDocument( uri, text );
		var symbol = await SymbolFinder.FindSymbolAtPositionAsync( doc, offset );
		if( symbol == null ) return [];
		// ジェネリックの具体化 (List<int> の Add など) は元の定義へ
		symbol = symbol.OriginalDefinition;
		var tree = await doc.GetSyntaxTreeAsync();
		return symbol.Locations.Where( l => l.IsInSource )
			.Select( l => {
				var span = l.GetLineSpan();
				return new DefinitionEntry( l.SourceTree == tree ? null : span.Path,
					span.StartLinePosition.Line, span.StartLinePosition.Character,
					span.EndLinePosition.Line, span.EndLinePosition.Character );
			} )
			.ToArray();
	}

	/// <summary>
	/// ソースの無い定義 (CsWSC の API や .NET の型) について、Source Link でソースを探すための情報です。
	/// スクリプト内の定義や、ローカル変数・名前空間などは null です。
	/// </summary>
	public async Task<MetadataDefinition?> MetadataDefinitionAsync( string uri, string text, int offset ) {
		var doc = workspace.GetDocument( uri, text );
		var symbol = await SymbolFinder.FindSymbolAtPositionAsync( doc, offset );
		if( symbol is IMethodSymbol { ReducedFrom: { } reduced } ) symbol = reduced;
		symbol = symbol?.OriginalDefinition;
		if( symbol == null || symbol.Locations.Any( l => l.IsInSource ) ) return null;
		var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
		if( type == null || type.MetadataToken == 0 ) return null;
		var compilation = await doc.Project.GetCompilationAsync();
		if( compilation?.GetMetadataReference( symbol.ContainingAssembly ) is not PortableExecutableReference { FilePath: { } path } ) return null;

		// PDB で位置が分かるのはメソッドだけなので、プロパティ・イベントはアクセサーで探す
		IEnumerable<IMethodSymbol?> methods = symbol switch {
			IMethodSymbol m => [m],
			IPropertySymbol p => [p.GetMethod, p.SetMethod],
			IEventSymbol e => [e.AddMethod, e.RemoveMethod],
			_ => [],
		};
		var name = symbol switch {
			IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => ".ctor",
			IPropertySymbol { IsIndexer: true } => "this[]",
			_ => symbol.Name,
		};
		return new MetadataDefinition( path, type.MetadataToken,
			methods.Where( m => m is { MetadataToken: not 0 } ).Select( m => m!.MetadataToken ).ToArray(),
			name, type.Name, symbol is INamedTypeSymbol );
	}

	/// <summary>名前を変更できるか調べます。できれば変更する範囲と今の名前、できなければ理由を返します。</summary>
	public async Task<PrepareRenameResult> PrepareRenameAsync( string uri, string text, int offset ) {
		var doc = workspace.GetDocument( uri, text );
		var (symbol, token, error) = await FindRenameTargetAsync( doc, offset );
		if( symbol == null ) return new PrepareRenameResult( 0, 0, null, error );
		return new PrepareRenameResult( token.Span.Start, token.Span.End, token.ValueText, null );
	}

	/// <summary>名前を変更します。変更箇所 (このドキュメント内) か、できない理由を返します。</summary>
	public async Task<RenameResult> RenameAsync( string uri, string text, int offset, string newName ) {
		var doc = workspace.GetDocument( uri, text );
		var (symbol, _, error) = await FindRenameTargetAsync( doc, offset );
		if( symbol == null ) return new RenameResult( null, error );

		newName = newName.Trim();
		var bare = newName.StartsWith( '@' ) ? newName[1..] : newName;
		if( !SyntaxFacts.IsValidIdentifier( bare ) ) return new RenameResult( null, $"'{newName}' は名前として使えません。" );
		// キーワードと同じ名前は @ を付ける (class → @class)
		if( SyntaxFacts.GetKeywordKind( bare ) != SyntaxKind.None ) newName = "@" + bare;
		if( bare == symbol.Name ) return new RenameResult( [], null );

		var before = await ErrorCountAsync( doc );
		var conflict = new RenameResult( null, $"'{newName}' に変更すると、ほかの名前と衝突するなどでエラーになります。" );
		Solution solution;
		try {
			solution = await Renamer.RenameSymbolAsync( doc.Project.Solution, symbol, new SymbolRenameOptions(), newName );
		} catch( NullReferenceException ) {
			// スクリプトのトップレベルの変数で衝突すると、Roslyn の衝突検出が例外を出す
			return conflict;
		}
		var renamed = solution.GetDocument( doc.Id )!;
		// 既にある名前と衝突するなどでエラーが増えるなら変更しない
		if( await ErrorCountAsync( renamed ) > before ) return conflict;
		var changes = await renamed.GetTextChangesAsync( doc );
		return new RenameResult( changes.Select( c => new TextEditEntry( c.Span.Start, c.Span.End, c.NewText ?? "" ) ).ToArray(), null );
	}

	/// <summary>
	/// offset の名前の変更対象です。変更できるのは、定義がすべてこのドキュメントにあるシンボルだけです
	/// (CsWSC の API や .NET の型、#load 先の定義を変えると、使っている側だけが変わって壊れるため)。
	/// </summary>
	private static async Task<(ISymbol? Symbol, SyntaxToken Token, string? Error)> FindRenameTargetAsync( Document doc, int offset ) {
		var root = await doc.GetSyntaxRootAsync();
		var token = root!.FindToken( offset );
		// 名前の直後 (Foo| ) でも変更できるようにする
		if( !token.IsKind( SyntaxKind.IdentifierToken ) && offset > 0 ) token = root.FindToken( offset - 1 );
		if( !token.IsKind( SyntaxKind.IdentifierToken ) ) return (null, token, "名前の上で実行してください。");

		var symbol = await SymbolFinder.FindSymbolAtPositionAsync( doc, token.SpanStart );
		// new Foo() のコンストラクターは型の名前を変える
		if( symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor } ctor ) symbol = ctor.ContainingType;
		if( symbol is IMethodSymbol { ReducedFrom: { } reduced } ) symbol = reduced;
		symbol = symbol?.OriginalDefinition;
		if( symbol == null || symbol.IsImplicitlyDeclared ) return (null, token, "この名前は変更できません。");

		var tree = await doc.GetSyntaxTreeAsync();
		if( symbol.Locations.Any( l => !l.IsInSource ) ) return (null, token, $"'{symbol.Name}' は CsWSC や .NET の定義なので、名前を変更できません。");
		if( symbol.Locations.Any( l => l.SourceTree != tree ) ) return (null, token, $"'{symbol.Name}' はほかのファイル (#load 先) の定義なので、名前を変更できません。");
		// ToString などの override やインターフェースの実装は、元の名前と合わなくなるので変えない
		if( symbol.IsOverride || symbol switch {
			IMethodSymbol m => m.ExplicitInterfaceImplementations.Length > 0,
			IPropertySymbol p => p.ExplicitInterfaceImplementations.Length > 0,
			IEventSymbol e => e.ExplicitInterfaceImplementations.Length > 0,
			_ => false,
		} ) return (null, token, $"'{symbol.Name}' は継承元の名前に合わせる必要があるので、変更できません。");
		return (symbol, token, null);
	}

	private static async Task<int> ErrorCountAsync( Document doc ) =>
		( await doc.GetSemanticModelAsync() )!.GetDiagnostics().Count( d => d.Severity == DiagnosticSeverity.Error );

	/// <summary>
	/// C# と同じく Roslyn の Formatter で整形し、変更箇所だけを返します。
	/// 設定は .editorconfig ([*.{cs,csws}] など .csws に当たるセクション) に従い、
	/// そこに無いインデントの設定だけエディターの設定 (useTabs, tabSize) を使います。
	/// </summary>
	/// <param name="start">整形する範囲の先頭です。</param>
	/// <param name="end">整形する範囲の末尾です。負ならドキュメントの末尾までです。</param>
	public async Task<TextEditEntry[]> FormatAsync( string uri, string text, int start, int end, bool useTabs, int tabSize ) {
		var doc = workspace.GetDocumentWithEditorConfig( uri, text );
		var tree = await doc.GetSyntaxTreeAsync();
		var config = doc.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions( tree! );
		var options = await doc.GetOptionsAsync();
		OptionSet set = options;
		if( !config.TryGetValue( "indent_style", out _ ) ) set = set.WithChangedOption( FormattingOptions.UseTabs, LanguageNames.CSharp, useTabs );
		if( !config.TryGetValue( "indent_size", out _ ) ) set = set.WithChangedOption( FormattingOptions.IndentationSize, LanguageNames.CSharp, tabSize );
		if( !config.TryGetValue( "tab_width", out _ ) ) set = set.WithChangedOption( FormattingOptions.TabSize, LanguageNames.CSharp, tabSize );
		// 改行の指定が無ければ元のテキストに合わせる (全行が変更扱いになるのを防ぐ)
		if( !config.TryGetValue( "end_of_line", out _ ) )
			set = set.WithChangedOption( FormattingOptions.NewLine, LanguageNames.CSharp, text.Contains( "\r\n" ) || !text.Contains( '\n' ) ? "\r\n" : "\n" );

		var span = TextSpan.FromBounds( Math.Clamp( start, 0, text.Length ), end < 0 ? text.Length : Math.Clamp( end, start, text.Length ) );
		var formatted = await Formatter.FormatAsync( doc, span, set );
		var changes = await formatted.GetTextChangesAsync( doc );
		return changes.Select( c => new TextEditEntry( c.Span.Start, c.Span.End, c.NewText ?? "" ) ).ToArray();
	}

	public void Close( string uri ) => workspace.Close( uri );

	// ---- ヘルパー ----

	private static readonly SymbolDisplayFormat Format = SymbolDisplayFormat.MinimallyQualifiedFormat
		.AddMemberOptions( SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType )
		.AddParameterOptions( SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut )
		.AddMiscellaneousOptions( SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier );

	private static readonly SymbolDisplayFormat ParameterFormat = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
						  SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeParamsRefOut,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier );

	private static bool Inside( ArgumentListSyntax list, int offset ) =>
		offset > list.OpenParenToken.SpanStart && ( list.CloseParenToken.IsMissing || offset <= list.CloseParenToken.SpanStart );

	private static SignatureEntry ToSignature( IMethodSymbol m ) {
		var docXml = ParseDoc( m );
		var parameters = m.Parameters.Select( p => new ParameterEntry( p.ToDisplayString( ParameterFormat ),
			docXml?.Elements( "param" ).FirstOrDefault( e => (string?)e.Attribute( "name" ) == p.Name ) is { } pe ? Clean( pe ) : null ) ).ToArray();
		var name = m.MethodKind == MethodKind.Constructor ? m.ContainingType.Name : m.Name;
		var ret = m.MethodKind == MethodKind.Constructor ? "" : m.ReturnType.ToDisplayString( ParameterFormat ) + " ";
		var label = $"{ret}{name}({string.Join( ", ", parameters.Select( p => p.Label ) )})";
		return new SignatureEntry( label, Summary( m ), parameters );
	}

	private static XElement? ParseDoc( ISymbol symbol ) => DocComment.Parse( symbol );

	/// <summary>summary・remarks・returns・example を Markdown にまとめます。</summary>
	private static string? Summary( ISymbol symbol ) {
		var doc = ParseDoc( symbol is IMethodSymbol { ReducedFrom: { } r } ? r : symbol );
		if( doc == null ) return null;
		var parts = new List<string>();
		if( doc.Element( "summary" ) is { } s ) parts.Add( Clean( s ) );
		if( doc.Element( "remarks" ) is { } rem ) parts.Add( Clean( rem ) );
		if( doc.Element( "returns" ) is { } ret ) parts.Add( $"**戻り値**: {Clean( ret )}" );
		foreach( var ex in doc.Elements( "example" ) ) parts.Add( $"**例**\n{Clean( ex )}" );
		parts.RemoveAll( string.IsNullOrWhiteSpace );
		return parts.Count == 0 ? null : string.Join( "\n\n", parts );
	}

	/// <summary>XML ドキュメントの要素を Markdown にします。code はコードブロック、see などはインラインコードにします。</summary>
	private static string Clean( XElement e ) {
		var sb = new StringBuilder();
		foreach( var n in e.Nodes() ) {
			switch( n ) {
				case XText t: AppendProse( sb, t.Value ); break;
				case XElement { Name.LocalName: "see" or "seealso" } see:
					var cref = (string?)see.Attribute( "cref" ) ?? (string?)see.Attribute( "langword" ) ?? (string?)see.Attribute( "href" ) ?? see.Value;
					sb.Append( '`' ).Append( cref.Contains( ':' ) ? ShortName( cref[( cref.IndexOf( ':' ) + 1 )..] ) : cref ).Append( '`' );
					break;
				case XElement { Name.LocalName: "paramref" or "typeparamref" } pr: sb.Append( '`' ).Append( (string?)pr.Attribute( "name" ) ).Append( '`' ); break;
				case XElement { Name.LocalName: "c" } c: sb.Append( '`' ).Append( c.Value ).Append( '`' ); break;
				case XElement { Name.LocalName: "code" } code: sb.Append( "\n```csharp\n" ).Append( Dedent( code.Value ) ).Append( "\n```\n" ); break;
				case XElement { Name.LocalName: "para" or "br" } p: sb.Append( "\n\n" ).Append( Clean( p ) ); break;
				case XElement other: sb.Append( Clean( other ) ); break;
			}
		}
		return sb.ToString().Trim();
	}

	/// <summary>
	/// 文中の改行・インデントは XML 上の折り返しなので 1 行に詰めます。
	/// 日本語同士をつなぐときは空白を入れません。
	/// </summary>
	private static void AppendProse( StringBuilder sb, string text ) {
		var lines = text.Replace( "\r", "" ).Split( '\n' );
		for( var i = 0; i < lines.Length; i++ ) {
			var line = lines[i];
			if( i > 0 ) line = line.TrimStart();
			if( i < lines.Length - 1 ) line = line.TrimEnd();
			line = string.Join( ' ', line.Split( [' ', '\t'] ).Where( ( w, j ) => w.Length > 0 || j == 0 ) ).Replace( "  ", " " );
			if( line.Trim().Length == 0 ) {
				if( lines.Length == 1 && line.Length > 0 && sb.Length > 0 && !char.IsWhiteSpace( sb[^1] ) ) sb.Append( ' ' );
				continue;
			}
			if( i > 0 && sb.Length > 0 && !char.IsWhiteSpace( sb[^1] ) && !( IsWide( sb[^1] ) && IsWide( line[0] ) ) ) sb.Append( ' ' );
			sb.Append( line );
		}
	}

	private static bool IsWide( char c ) => c >= 0x2E80;

	// CsWSC.Window.Mouse(CsWSC.MouseButton,...) → Window.Mouse
	private static string ShortName( string id ) {
		var name = id.Split( '(' )[0].Split( '.' );
		return name.Length >= 2 ? $"{name[^2]}.{name[^1]}" : name[^1];
	}

	private static string Dedent( string code ) {
		var lines = code.Replace( "\r", "" ).Split( '\n' )
			.SkipWhile( string.IsNullOrWhiteSpace ).Reverse().SkipWhile( string.IsNullOrWhiteSpace ).Reverse().ToArray();
		var indent = lines.Where( l => l.Trim().Length > 0 ).Select( l => l.Length - l.TrimStart().Length ).DefaultIfEmpty( 0 ).Min();
		return string.Join( "\n", lines.Select( l => l.Length >= indent ? l[indent..] : l.TrimStart() ) );
	}

	private static string KindOf( CompletionItem item ) {
		foreach( var tag in item.Tags ) {
			switch( tag ) {
				case WellKnownTags.Method or WellKnownTags.ExtensionMethod: return "method";
				case WellKnownTags.Property: return "property";
				case WellKnownTags.Field: return "field";
				case WellKnownTags.Event: return "event";
				case WellKnownTags.Class: return "class";
				case WellKnownTags.Structure: return "struct";
				case WellKnownTags.Interface: return "interface";
				case WellKnownTags.Enum: return "enum";
				case WellKnownTags.EnumMember: return "enumMember";
				case WellKnownTags.Delegate: return "class";
				case WellKnownTags.Namespace: return "module";
				case WellKnownTags.Keyword: return "keyword";
				case WellKnownTags.Local or WellKnownTags.Parameter or WellKnownTags.RangeVariable: return "variable";
				case WellKnownTags.Constant: return "constant";
				case WellKnownTags.TypeParameter: return "typeParameter";
				case WellKnownTags.Snippet: return "snippet";
			}
		}
		return "text";
	}
}
