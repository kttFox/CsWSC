using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;

namespace CsWSC;

/// <summary>
/// #r にライブラリ (.cswsl) を指定できるようにする MetadataReferenceResolver です。
/// .cswsl はスクリプトではなく通常の C# としてライブラリ (DLL) にコンパイルして参照します。
/// スクリプトでは書けないもの (C# 14 の extension ブロックによる拡張演算子など) を書くのに使います。
/// .cs ではなく専用の拡張子にしているのは、VS Code で C# 拡張ではなく CsWSC の言語サーバーに解析させるためです。
/// それ以外は ScriptMetadataResolver にそのまま任せます。
/// </summary>
/// <remarks>
/// DLL は内容のハッシュを名前にしてキャッシュフォルダの Lib に保存します。
/// 同じ名前の DLL を書き換えないので、実行中のプロセスが読み込んでいても衝突しません。
/// </remarks>
public sealed class ScriptLibraryResolver : MetadataReferenceResolver {
	private readonly ScriptMetadataResolver _inner;
	private readonly string _baseDirectory;
	private readonly ImmutableArray<MetadataReference> _references;

	/// <summary>コンパイルしたライブラリのパス (キャッシュの依存ファイル用) です。</summary>
	private readonly ConcurrentDictionary<string, byte> _sources = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>ライブラリのコンパイルエラーです (「ファイル名(行): メッセージ (ID)」の形式)。</summary>
	private readonly ConcurrentQueue<string> _errors = new();

	/// <summary>ライブラリの拡張子です。</summary>
	public const string Extension = ".cswsl";

	/// <summary>ライブラリ (.cswsl) かどうかを返します。</summary>
	public static bool IsLibrary( string path ) => path.EndsWith( Extension, StringComparison.OrdinalIgnoreCase );

	/// <summary>ライブラリでスクリプトと同じ名前空間を最初から使えるようにする global using です (言語サーバーと共通)。</summary>
	public static string GlobalUsings { get; } = string.Concat( ScriptRunner.Imports.Select( i => $"global using {i};\n" ) );

	/// <summary>ライブラリの構文解析オプションです (言語サーバーと共通)。</summary>
	public static CSharpParseOptions ParseOptions { get; } = new( LanguageVersion.Latest );

	/// <summary>ライブラリのコンパイルオプションです (言語サーバーと共通)。</summary>
	public static CSharpCompilationOptions CompilationOptions { get; } =
		new( OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: ScriptRunner.NullableContext );

	/// <summary>DLL を保存するフォルダです。</summary>
	public static string LibraryDirectory { get; } = Path.Combine( ScriptRunner.DefaultCacheDirectory, "Lib" );

	/// <summary>baseDirectory は相対パスの基準、references はライブラリのコンパイルで参照するアセンブリです。</summary>
	public ScriptLibraryResolver( string baseDirectory, ImmutableArray<MetadataReference> references ) {
		_inner = ScriptMetadataResolver.Default.WithBaseDirectory( baseDirectory );
		_baseDirectory = baseDirectory;
		_references = references;
	}

	/// <summary>コンパイルしたライブラリのフルパスです。</summary>
	public IReadOnlyCollection<string> Sources => [.. _sources.Keys];

	/// <summary>ライブラリのコンパイルエラーです。</summary>
	public IReadOnlyCollection<string> Errors => [.. _errors];

	public override ImmutableArray<PortableExecutableReference> ResolveReference( string reference, string? baseFilePath, MetadataReferenceProperties properties ) {
		if( !IsLibrary( reference ) ) {
			return _inner.ResolveReference( reference, baseFilePath, properties );
		}

		var baseDir = baseFilePath != null ? Path.GetDirectoryName( baseFilePath )! : _baseDirectory;
		var source = Path.GetFullPath( Path.Combine( baseDir, reference ) );
		if( !File.Exists( source ) ) {
			return [];
		}

		_sources.TryAdd( source, 0 );
		var dll = Compile( source );
		return dll != null ? [MetadataReference.CreateFromFile( dll, properties )] : [];
	}

	/// <summary>ライブラリをコンパイルして DLL のパスを返します。保存済みならコンパイルしません。エラーなら null です。</summary>
	private string? Compile( string source ) {
		var text = File.ReadAllText( source );
		var key = string.Join( "\0", text, source.ToUpperInvariant(), typeof( ScriptLibraryResolver ).Module.ModuleVersionId );
		var name = "csws_" + Convert.ToHexString( SHA256.HashData( Encoding.UTF8.GetBytes( key ) ) )[..32];
		var dll = Path.Combine( LibraryDirectory, name + ".dll" );
		if( File.Exists( dll ) ) {
			return dll;
		}

		var compilation = CSharpCompilation.Create( name,
			[
				CSharpSyntaxTree.ParseText( GlobalUsings, ParseOptions ),
				CSharpSyntaxTree.ParseText( text, ParseOptions, source, Encoding.UTF8 ),
			],
			_references,
			CompilationOptions );
		using var stream = new MemoryStream();
		var result = compilation.Emit( stream,
			options: new Microsoft.CodeAnalysis.Emit.EmitOptions( debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.Embedded ) );
		if( !result.Success ) {
			foreach( var e in result.Diagnostics.Where( d => d.Severity == DiagnosticSeverity.Error ) ) {
				var line = e.Location.GetLineSpan().StartLinePosition.Line + 1;
				_errors.Enqueue( $"{Path.GetFileName( source )}({line}): {e.GetMessage()} ({e.Id})" );
			}
			return null;
		}

		try {
			Directory.CreateDirectory( LibraryDirectory );
			var temp = dll + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";
			File.WriteAllBytes( temp, stream.ToArray() );
			File.Move( temp, dll, overwrite: true );
		} catch( IOException ) when( File.Exists( dll ) ) {
			// 別のプロセスが同時に同じ DLL を置いた
		}
		return dll;
	}

	public override bool Equals( object? other ) =>
		other is ScriptLibraryResolver o && _inner.Equals( o._inner ) && _references == o._references;

	public override int GetHashCode() => _inner.GetHashCode();
}
