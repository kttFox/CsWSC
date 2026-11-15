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

	/// <summary>ライブラリが #r で参照した DLL のフルパスです (実行時の読み込みと、キャッシュの依存ファイル用)。</summary>
	public IReadOnlyCollection<string> Dependencies => [.. _dependencies.Keys];

	private readonly ConcurrentDictionary<string, PortableExecutableReference> _dependencies = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// ライブラリの先頭に書かれた #r "..." を取り出します。通常の C# では #r を書けないので、返す Code ではその行の #r を // に置き換えます
	/// (同じ長さなので行と列はずれない)。先頭の空行・コメント行・# で始まる行だけを見て、それ以外の行が出たら終わります。
	/// </summary>
	public static (string Code, IReadOnlyList<(string Path, int Line)> References) ParseReferences( string text ) {
		var references = new List<(string, int)>();
		var code = new StringBuilder( text );
		var line = 1;
		for( var start = 0; start < text.Length; line++ ) {
			var end = text.IndexOf( '\n', start );
			if( end < 0 ) end = text.Length;
			var content = text.AsSpan( start, end - start ).Trim();
			var indent = text.AsSpan( start, end - start ).Length - text.AsSpan( start, end - start ).TrimStart().Length;
			if( content.StartsWith( "#r" ) && content.Length > 2 && ( content[2] == ' ' || content[2] == '\t' || content[2] == '"' ) ) {
				var rest = content[2..].Trim();
				var close = rest.Length > 1 && rest[0] == '"' ? rest[1..].IndexOf( '"' ) : -1;
				if( close >= 0 ) {
					references.Add( (rest.Slice( 1, close ).ToString(), line) );
					code[start + indent] = '/';
					code[start + indent + 1] = '/';
				}
			} else if( content.Length > 0 && !content.StartsWith( "//" ) && !content.StartsWith( "#" ) ) {
				break;
			}
			start = end + 1;
		}
		return (code.ToString(), references);
	}

	/// <summary>ライブラリが #r で参照した DLL は、ライブラリの DLL の隣には無いので、ここで名前から探して渡します。</summary>
	public override bool ResolveMissingAssemblies => true;

	public override PortableExecutableReference? ResolveMissingAssembly( MetadataReference definition, AssemblyIdentity referenceIdentity ) {
		foreach( var (path, reference) in _dependencies ) {
			if( string.Equals( Path.GetFileNameWithoutExtension( path ), referenceIdentity.Name, StringComparison.OrdinalIgnoreCase ) ) {
				return reference;
			}
		}
		return _inner.ResolveMissingAssembly( definition, referenceIdentity );
	}

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
		var (text, directives) = ParseReferences( File.ReadAllText( source ) );

		// ライブラリの #r。相対パスはライブラリのフォルダを基準にする
		var dependencies = new List<PortableExecutableReference>();
		var failed = false;
		foreach( var (path, line) in directives ) {
			var resolved = IsLibrary( path ) ? [] : _inner.ResolveReference( path, source, MetadataReferenceProperties.Assembly );
			if( resolved.IsDefaultOrEmpty || resolved[0].FilePath is not { } file ) {
				_errors.Enqueue( IsLibrary( path )
					? $"{Path.GetFileName( source )}({line}): ライブラリ ({Extension}) から別のライブラリ '{path}' は参照できません"
					: $"{Path.GetFileName( source )}({line}): 参照 '{path}' が見つかりません (CS0006)" );
				failed = true;
				continue;
			}
			dependencies.Add( _dependencies.GetOrAdd( Path.GetFullPath( file ), resolved[0] ) );
		}
		if( failed ) {
			return null;
		}

		// 参照先の DLL が変わったらコンパイルし直す
		var stamps = dependencies.Select( d => $"{d.FilePath!.ToUpperInvariant()}|{File.GetLastWriteTimeUtc( d.FilePath! ).Ticks}" );
		var key = string.Join( "\0", [text, source.ToUpperInvariant(), typeof( ScriptLibraryResolver ).Module.ModuleVersionId.ToString(), .. stamps] );
		var name = "csws_" + Convert.ToHexString( SHA256.HashData( Encoding.UTF8.GetBytes( key ) ) )[..32];
		var dll = Path.Combine( LibraryDirectory, name + ".dll" );
		if( File.Exists( dll ) ) {
			try {
				File.SetLastWriteTimeUtc( dll, DateTime.UtcNow ); // 削除順の判定用
			} catch( IOException ) { } catch( UnauthorizedAccessException ) { }
			return dll;
		}

		var compilation = CSharpCompilation.Create( name,
			[
				CSharpSyntaxTree.ParseText( GlobalUsings, ParseOptions ),
				CSharpSyntaxTree.ParseText( text, ParseOptions, source, Encoding.UTF8 ),
			],
			_references.AddRange( dependencies ),
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
		DeleteOldFiles();
		return dll;
	}

	/// <summary>保存しておくライブラリの DLL の最大数です。</summary>
	private const int MaxFiles = 100;

	/// <summary>最近使っていない DLL を、最大数を超えた分だけ消します。実行中のスクリプトが読み込んでいて消せないものは残します。</summary>
	private static void DeleteOldFiles() {
		FileInfo[] files;
		try {
			files = new DirectoryInfo( LibraryDirectory ).GetFiles( "csws_*.dll" );
		} catch( IOException ) { return; } catch( UnauthorizedAccessException ) { return; }
		foreach( var old in files.OrderByDescending( f => f.LastWriteTimeUtc ).Skip( MaxFiles ) ) {
			try {
				old.Delete();
			} catch( IOException ) { } catch( UnauthorizedAccessException ) { }
		}
	}

	public override bool Equals( object? other ) =>
		other is ScriptLibraryResolver o && _inner.Equals( o._inner ) && _references == o._references;

	public override int GetHashCode() => _inner.GetHashCode();
}
