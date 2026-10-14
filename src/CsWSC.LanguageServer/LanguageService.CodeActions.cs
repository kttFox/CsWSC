using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CsWSC.LanguageServer;

/// <summary>コードアクションです。Kind は "quickfix" (エラー・警告の修正) か "refactor" です。</summary>
internal sealed record CodeActionEntry( int Index, string Title, string Kind, int[]? Diagnostics );
/// <summary>コードアクションの適用結果です。適用できなければ Edits は null で Error に理由が入ります。</summary>
internal sealed record CodeActionEditResult( TextEditEntry[]? Edits, string? Error );

/// <summary>C# と同じく Roslyn 組み込みのコード修正・リファクタリングを使うコードアクションです。</summary>
internal sealed partial class LanguageService {
	// 適用 (resolveCodeAction) 用に直前の一覧を覚えておく
	// (トップレベルのリファクタリングは影のドキュメントで計算するので、アクションごとに元のドキュメントを持つ)
	private (Document Document, CodeAction Action)[]? _lastCodeActions;

	/// <summary>
	/// start～end で使えるコードアクションの一覧です。変更内容は重いので、選ばれてから <see cref="ResolveCodeActionAsync"/> で計算します。
	/// </summary>
	public async Task<CodeActionEntry[]> CodeActionsAsync( string uri, string text, int start, int end ) {
		// 修正後のコードは .editorconfig の書式で整形されるので、書式設定と同じく読み直す
		var doc = Workspace.GetDocumentWithEditorConfig( uri, text );
		var span = TextSpan.FromBounds( Math.Clamp( start, 0, text.Length ), Math.Clamp( Math.Max( start, end ), 0, text.Length ) );
		var model = await doc.GetSemanticModelAsync();
		var actions = new List<(Document, CodeAction)>();
		var entries = new List<CodeActionEntry>();
		var titles = new HashSet<string>();

		void Add( CodeAction action, string kind, int[]? diagnostics, string prefix = "", Document? from = null ) {
			// ダイアログで設定を選ぶもの (コンストラクターの生成など) は VS Code では出せない
			if( action is CodeActionWithOptions ) return;
			if( !action.NestedActions.IsEmpty ) {
				foreach( var nested in action.NestedActions ) Add( nested, kind, diagnostics, $"{prefix}{action.Title}: ", from );
				return;
			}
			var title = prefix + action.Title;
			if( !titles.Add( title ) ) return;
			entries.Add( new CodeActionEntry( actions.Count, title, kind, diagnostics ) );
			actions.Add( (from ?? doc, action) );
		}

		// エラー・警告の修正 (診断は DiagnoseAsync と同じもの。Diagnostics は修正する診断の開始位置)
		var diagnosticsInSpan = model!.GetDiagnostics()
			.Where( d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning && d.Location.IsInSource && d.Location.SourceSpan.IntersectsWith( span ) )
			.Select( d => (Diagnostic: d, Linked: true) )
			// IDE アナライザーの提案 (IDExxxx。既定では非表示の重大度なので、エディターには出さずコードアクションだけにする)
			.Concat( ( await AnalyzeAsync( doc, model, span ) ).Select( d => (Diagnostic: d, Linked: false) ) )
			.ToArray();
		foreach( var (diagnostic, linked) in diagnosticsInSpan ) {
			foreach( var provider in Providers.Value.Fixes ) {
				if( !provider.FixableDiagnosticIds.Contains( diagnostic.Id ) ) continue;
				var found = new List<CodeAction>();
				try {
					await provider.RegisterCodeFixesAsync( new CodeFixContext( doc, diagnostic, ( a, _ ) => found.Add( a ), CancellationToken.None ) );
				} catch( Exception e ) {
					// 組み込みのプロバイダーには VS の機能を前提にしたものがあるので、失敗したものは飛ばす
					Console.Error.WriteLine( $"{provider.GetType().Name}: {e.Message}" );
				}
				foreach( var a in found ) Add( a, "quickfix", linked ? [diagnostic.Location.SourceSpan.Start] : null );
			}
		}

		// リファクタリング
		// Roslyn のリファクタリングの多くはスクリプトのトップレベルの文に対応していないので、
		// トップレベルでは同じテキストを通常の C# のトップレベル文として解析した影のドキュメントでも計算する (位置はそのまま対応する)
		var targets = new List<Document> { doc };
		var root = await doc.GetSyntaxRootAsync();
		// (スクリプトのトップレベルの変数宣言は文ではなくフィールドになる)
		if( root!.FindToken( span.Start ).Parent?.AncestorsAndSelf().LastOrDefault( n => n.Parent is CompilationUnitSyntax ) is GlobalStatementSyntax or FieldDeclarationSyntax )
			targets.Add( await CreateShadowDocumentAsync( doc ) );
		foreach( var target in targets ) {
			foreach( var provider in Providers.Value.Refactorings ) {
				// 'Program.Main' スタイルへの変換は影のドキュメント (トップレベル文) だから出るもので、スクリプトでは意味がない
				if( target != doc && provider.GetType().Name == "ConvertToProgramMainCodeRefactoringProvider" ) continue;
				var found = new List<CodeAction>();
				try {
					await provider.ComputeRefactoringsAsync( new CodeRefactoringContext( target, span, found.Add, CancellationToken.None ) );
				} catch( Exception e ) {
					Console.Error.WriteLine( $"{provider.GetType().Name}: {e.Message}" );
				}
				foreach( var a in found ) Add( a, "refactor", null, from: target );
			}
		}

		_lastCodeActions = actions.ToArray();
		return entries.ToArray();
	}

	/// <summary>直前の一覧の index 番目のコードアクションを適用したときの変更箇所 (このドキュメント内) です。</summary>
	public async Task<CodeActionEditResult> ResolveCodeActionAsync( int index ) {
		if( _lastCodeActions is not { } last || index < 0 || index >= last.Length )
			return new CodeActionEditResult( null, "コードアクションの一覧が古くなっています。もう一度実行してください。" );
		var (doc, action) = last[index];
		ImmutableArray<CodeActionOperation> operations;
		try {
			operations = await action.GetOperationsAsync( CancellationToken.None );
		} catch( Exception e ) {
			return new CodeActionEditResult( null, $"'{action.Title}' を適用できません: {e.Message}" );
		}
		var changed = operations.OfType<ApplyChangesOperation>().LastOrDefault()?.ChangedSolution;
		if( changed == null ) return new CodeActionEditResult( [], null );
		// ほかのファイル (#load 先) を変えるものは、このドキュメントだけ変えると壊れるので適用しない
		var solutionChanges = changed.GetChanges( doc.Project.Solution );
		if( solutionChanges.GetAddedProjects().Any() || solutionChanges.GetProjectChanges().Any( p =>
			p.GetChangedDocuments().Any( id => id != doc.Id ) || p.GetAddedDocuments().Any() || p.GetRemovedDocuments().Any() ) )
			return new CodeActionEditResult( null, $"'{action.Title}' はほかのファイルも変更するので、適用できません。" );
		var newDoc = changed.GetDocument( doc.Id );
		if( newDoc == null ) return new CodeActionEditResult( [], null );
		var changes = await newDoc.GetTextChangesAsync( doc );
		return new CodeActionEditResult( changes.Select( c => new TextEditEntry( c.Span.Start, c.Span.End, c.NewText ?? "" ) ).ToArray(), null );
	}

	/// <summary>
	/// スクリプトと同じテキストを、通常の C# のトップレベル文 (Program.cs の形) として解析するドキュメントです。ワークスペースには加えません。
	/// CsWSC の using は global using で補います。ScriptGlobals のメンバー (Print など) は解決できませんが、構文だけで済むリファクタリングには足ります。
	/// </summary>
	private static async Task<Document> CreateShadowDocumentAsync( Document doc ) {
		var project = doc.Project;
		var id = ProjectId.CreateNewId();
		var solution = project.Solution.AddProject( ProjectInfo.Create( id, VersionStamp.Create(), "shadow", "shadow", LanguageNames.CSharp,
			compilationOptions: new CSharpCompilationOptions( OutputKind.ConsoleApplication ),
			parseOptions: new CSharpParseOptions( LanguageVersion.Latest ),
			metadataReferences: project.MetadataReferences ) );
		// 書式は .editorconfig に合わせる
		foreach( var config in project.AnalyzerConfigDocuments )
			solution = solution.AddAnalyzerConfigDocument( DocumentId.CreateNewId( id ), config.Name, await config.GetTextAsync(), filePath: config.FilePath );
		solution = solution.AddDocument( DocumentId.CreateNewId( id ), "usings.cs",
			string.Concat( CsWSC.ScriptRunner.Imports.Select( u => $"global using {u};{Environment.NewLine}" ) ) );
		var shadowId = DocumentId.CreateNewId( id );
		solution = solution.AddDocument( shadowId, doc.Name, await doc.GetTextAsync(), filePath: doc.FilePath );
		return solution.GetDocument( shadowId )!;
	}

	/// <summary>span にかかる IDE アナライザーの診断 (非表示の重大度のものも含む) です。</summary>
	private static async Task<Diagnostic[]> AnalyzeAsync( Document doc, SemanticModel model, TextSpan span ) {
		try {
			var analyzers = model.Compilation.WithAnalyzers( Providers.Value.Analyzers, doc.Project.AnalyzerOptions );
			var syntax = await analyzers.GetAnalyzerSyntaxDiagnosticsAsync( model.SyntaxTree, span, CancellationToken.None );
			var semantic = await analyzers.GetAnalyzerSemanticDiagnosticsAsync( model, span, CancellationToken.None );
			return syntax.Concat( semantic )
				.Where( d => !d.IsSuppressed && d.Location.IsInSource && d.Location.SourceTree == model.SyntaxTree && d.Location.SourceSpan.IntersectsWith( span ) )
				.ToArray();
		} catch( Exception e ) {
			Console.Error.WriteLine( $"analyzers: {e.Message}" );
			return [];
		}
	}

	/// <summary>
	/// Roslyn の Features アセンブリにある C# 用のコード修正・リファクタリング・IDE アナライザーです。
	/// VS では MEF で集めるものですが、引数なしのコンストラクターで作れるので、属性を見て直接作ります。
	/// </summary>
	private static readonly Lazy<(CodeFixProvider[] Fixes, CodeRefactoringProvider[] Refactorings, ImmutableArray<DiagnosticAnalyzer> Analyzers)> Providers = new( () => {
		var types = new[] { "Microsoft.CodeAnalysis.Features", "Microsoft.CodeAnalysis.CSharp.Features" }
			.SelectMany( name => {
				try { return Assembly.Load( name ).GetTypes(); } catch( ReflectionTypeLoadException e ) { return e.Types.OfType<Type>().ToArray(); }
			} )
			.Where( t => t is { IsAbstract: false, IsGenericTypeDefinition: false } )
			.ToArray();
		return (
			Create<CodeFixProvider>( types, t => t.GetCustomAttribute<ExportCodeFixProviderAttribute>()?.Languages ),
			Create<CodeRefactoringProvider>( types, t => t.GetCustomAttribute<ExportCodeRefactoringProviderAttribute>()?.Languages ),
			[.. Create<DiagnosticAnalyzer>( types, t => t.GetCustomAttribute<DiagnosticAnalyzerAttribute>()?.Languages )]);
	} );

	private static T[] Create<T>( Type[] types, Func<Type, string[]?> languages ) where T : class =>
		types.Where( t => typeof( T ).IsAssignableFrom( t ) && languages( t )?.Contains( LanguageNames.CSharp ) == true )
			.Select( t => {
				try {
					var ctor = t.GetConstructor( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes );
					return ctor?.Invoke( null ) as T;
				} catch( Exception e ) {
					Console.Error.WriteLine( $"{t.Name}: {e.Message}" );
					return null;
				}
			} )
			.OfType<T>()
			.ToArray();
}
