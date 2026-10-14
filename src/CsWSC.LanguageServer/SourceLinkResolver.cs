using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsWSC.LanguageServer;

/// <summary>ソースの無い定義の情報です。トークンは CLR のメタデータトークンです。</summary>
/// <param name="AssemblyPath">定義のある dll です。</param>
/// <param name="TypeToken">定義そのもの (型のとき) か、定義を含む型です。</param>
/// <param name="MethodTokens">位置の手がかりになるメソッド (メソッド自身、プロパティ・イベントのアクセサー) です。</param>
/// <param name="Name">定義の名前です。コンストラクターは .ctor、インデクサーは this[] です。</param>
/// <param name="TypeName">TypeToken の型の名前です。</param>
/// <param name="IsType">定義が型かどうかです。</param>
internal sealed record MetadataDefinition( string AssemblyPath, int TypeToken, int[] MethodTokens, string Name, string TypeName, bool IsType );

/// <summary>
/// dll の PDB からソースの場所を調べ、ソースファイルを用意します。
/// PDB は dll に埋め込まれていればそれを、無ければ Microsoft のシンボルサーバーから取得します。
/// ソースは PDB に記録されたパスが実在すればそれ (開発中の CsWSC)、無ければ Source Link の URL (GitHub) から取得します。
/// 取得したものは cacheDir に保存し、2 回目からは使い回します。
/// </summary>
internal sealed class SourceLinkResolver( string cacheDir ) {
	private const string SymbolServer = "https://msdl.microsoft.com/download/symbols/";
	private static readonly Guid SourceLinkKind = new( "CC110556-A091-4D38-9FEC-25AB9A351A6A" );
	private static readonly Guid EmbeddedSourceKind = new( "0E8A571B-6926-466E-B4AD-8AB04611F5FE" );
	private static readonly Guid TypeDocumentsKind = new( "932E74BC-DBA9-4478-8D46-0F32A7BAB3D3" );
	// partial で多数のファイルに分かれた型 (string など) でダウンロードしすぎないよう、調べるファイル数を絞る
	private const int MaxDocuments = 8;

	private static readonly HttpClient Http = CreateHttp();
	private readonly Dictionary<string, Module> _modules = new( StringComparer.OrdinalIgnoreCase );
	private readonly SemaphoreSlim _lock = new( 1, 1 );

	/// <param name="download">false ならダウンロードせず、dll に埋め込まれたものとキャッシュだけを使います。</param>
	/// <returns>定義の場所です。見つからなければ空です。</returns>
	public async Task<DefinitionEntry[]> ResolveAsync( MetadataDefinition def, bool download ) {
		// 同じ PDB を何度もダウンロードしないよう、1 つずつ処理する
		await _lock.WaitAsync();
		try {
			var module = await LoadAsync( def.AssemblyPath, download );
			if( module == null ) return [];
			(string Path, int Line)? fallback = null;
			foreach( var (document, line) in module.Candidates( def ).Take( MaxDocuments ) ) {
				var path = await module.GetSourceAsync( document, download );
				if( path == null ) continue;
				if( FindDeclaration( path, def, line ) is { } found ) return [found];
				fallback ??= (path, Math.Max( line, 0 ));
			}
			// 宣言を見つけられなくても、手がかりの行 (無ければ先頭) を開く
			return fallback is var (p, l) ? [new DefinitionEntry( p, l, 0, l, 0 )] : [];
		} finally {
			_lock.Release();
		}
	}

	private async Task<Module?> LoadAsync( string assemblyPath, bool download ) {
		if( _modules.TryGetValue( assemblyPath, out var cached ) ) return cached;
		// CsWSC の dll はビルドで上書きされるので、メモリに読み込んでファイルを開いたままにしない
		var pe = new PEReader( File.ReadAllBytes( assemblyPath ).ToImmutableArray() );
		var entries = pe.ReadDebugDirectory();
		MetadataReaderProvider pdb;
		string pdbId;
		if( entries.FirstOrDefault( e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb ) is { Type: DebugDirectoryEntryType.EmbeddedPortablePdb } embedded ) {
			pdb = pe.ReadEmbeddedPortablePdbDebugDirectoryData( embedded );
			pdbId = $"{Path.GetFileNameWithoutExtension( assemblyPath )}-{pe.PEHeaders.CoffHeader.TimeDateStamp:x8}";
		} else {
			// ReadyToRun の dll には R2R 用の CodeView もあるので、Portable PDB のものを選ぶ
			var codeView = entries.FirstOrDefault( e => e.Type == DebugDirectoryEntryType.CodeView && e.IsPortableCodeView );
			if( codeView.Type != DebugDirectoryEntryType.CodeView ) return null;
			var data = pe.ReadCodeViewDebugDirectoryData( codeView );
			var name = Path.GetFileName( data.Path.Replace( '/', '\\' ) ).ToLowerInvariant();
			// シンボルサーバーのキー: Portable PDB は GUID + FFFFFFFF
			var key = $"{name}/{data.Guid:N}FFFFFFFF/{name}";
			var local = Path.Combine( cacheDir, "pdb", key.Replace( '/', Path.DirectorySeparatorChar ) );
			if( !File.Exists( local ) ) {
				if( !download ) return null;
				await DownloadAsync( SymbolServer + key, local );
			}
			pdb = MetadataReaderProvider.FromPortablePdbImage( File.ReadAllBytes( local ).ToImmutableArray() );
			pdbId = $"{data.Guid:N}";
		}
		var module = new Module( this, pe, pdb, pdbId );
		_modules[assemblyPath] = module;
		return module;
	}

	private sealed class Module( SourceLinkResolver owner, PEReader pe, MetadataReaderProvider pdbProvider, string pdbId ) {
		private readonly MetadataReader _metadata = pe.GetMetadataReader();
		private readonly MetadataReader _pdb = pdbProvider.GetMetadataReader();
		private readonly (string Pattern, string Url)[] _sourceLink = ReadSourceLink( pdbProvider.GetMetadataReader() );

		/// <summary>定義がありそうなソースファイルと、手がかりの行 (0 始まり、無ければ -1) です。可能性の高い順です。</summary>
		public IEnumerable<(DocumentHandle Document, int Line)> Candidates( MetadataDefinition def ) {
			var seen = new HashSet<DocumentHandle>();
			// メソッドの位置 (シーケンスポイント) が分かればそのファイル
			foreach( var token in def.MethodTokens ) {
				if( SequencePoints( token ).FirstOrDefault() is { Document.IsNil: false } sp && seen.Add( sp.Document ) )
					yield return (sp.Document, sp.StartLine - 1);
			}
			// 型のメソッドがあるファイルを、メソッドの多い順に
			if( MetadataTokens.EntityHandle( def.TypeToken ) is not { Kind: HandleKind.TypeDefinition } handle ) yield break;
			var typeHandle = (TypeDefinitionHandle)handle;
			var counts = _metadata.GetTypeDefinition( typeHandle ).GetMethods()
				.SelectMany( m => SequencePoints( MetadataTokens.GetToken( m ) ).Select( sp => sp.Document ).Distinct().Take( 1 ) )
				.GroupBy( d => d ).OrderByDescending( g => g.Count() ).Select( g => g.Key );
			// メソッドの無い型 (enum など) は PDB に型のファイルが記録されている
			foreach( var document in counts.Concat( TypeDocuments( typeHandle ) ) ) {
				if( seen.Add( document ) ) yield return (document, -1);
			}
		}

		private IEnumerable<SequencePoint> SequencePoints( int methodToken ) {
			if( MetadataTokens.EntityHandle( methodToken ) is not { Kind: HandleKind.MethodDefinition } handle ) return [];
			var row = MetadataTokens.GetRowNumber( handle );
			if( row > _pdb.MethodDebugInformation.Count ) return [];
			return _pdb.GetMethodDebugInformation( ( (MethodDefinitionHandle)handle ).ToDebugInformationHandle() )
				.GetSequencePoints().Where( sp => !sp.IsHidden );
		}

		private IEnumerable<DocumentHandle> TypeDocuments( TypeDefinitionHandle type ) {
			foreach( var h in _pdb.GetCustomDebugInformation( type ) ) {
				var cdi = _pdb.GetCustomDebugInformation( h );
				if( _pdb.GetGuid( cdi.Kind ) != TypeDocumentsKind ) continue;
				var reader = _pdb.GetBlobReader( cdi.Value );
				while( reader.RemainingBytes > 0 ) yield return MetadataTokens.DocumentHandle( reader.ReadCompressedInteger() );
			}
		}

		/// <summary>ソースファイルを用意してパスを返します。用意できなければ null です。</summary>
		public async Task<string?> GetSourceAsync( DocumentHandle handle, bool download ) {
			var name = _pdb.GetString( _pdb.GetDocument( handle ).Name );
			// 開発中の CsWSC はビルドしたフォルダにソースがある
			if( Path.IsPathFullyQualified( name ) && File.Exists( name ) ) return name;

			var fileName = Path.GetFileName( name.Replace( '/', '\\' ) );
			if( Embedded( handle ) is { } bytes ) {
				var local = Path.Combine( owner.CacheDir, "src", pdbId, Hash( name ), fileName );
				if( !File.Exists( local ) ) WriteReadOnly( local, bytes );
				return local;
			}
			if( MapUrl( name ) is not { } url ) return null;
			var path = Path.Combine( owner.CacheDir, "src", Hash( url ), fileName );
			if( !File.Exists( path ) ) {
				if( !download ) return null;
				await DownloadAsync( url, path );
			}
			return path;
		}

		private byte[]? Embedded( DocumentHandle handle ) {
			foreach( var h in _pdb.GetCustomDebugInformation( handle ) ) {
				var cdi = _pdb.GetCustomDebugInformation( h );
				if( _pdb.GetGuid( cdi.Kind ) != EmbeddedSourceKind ) continue;
				// 先頭 4 バイトが 0 なら無圧縮、正なら deflate 前のサイズ
				var blob = _pdb.GetBlobBytes( cdi.Value );
				var size = BitConverter.ToInt32( blob, 0 );
				if( size == 0 ) return blob[4..];
				using var deflate = new DeflateStream( new MemoryStream( blob, 4, blob.Length - 4 ), CompressionMode.Decompress );
				var result = new byte[size];
				deflate.ReadExactly( result );
				return result;
			}
			return null;
		}

		// Source Link の "documents": { "/_/*": "https://raw.githubusercontent.com/.../*" } で最も長く一致するもの
		private string? MapUrl( string path ) {
			string? best = null;
			var bestLength = -1;
			foreach( var (pattern, url) in _sourceLink ) {
				if( pattern.EndsWith( '*' ) ) {
					var prefix = pattern[..^1];
					if( prefix.Length > bestLength && path.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) ) {
						best = url.Replace( "*", path[prefix.Length..].Replace( '\\', '/' ) );
						bestLength = prefix.Length;
					}
				} else if( string.Equals( pattern, path, StringComparison.OrdinalIgnoreCase ) ) {
					return url;
				}
			}
			return best;
		}

		private static (string, string)[] ReadSourceLink( MetadataReader pdb ) {
			foreach( var h in pdb.GetCustomDebugInformation( EntityHandle.ModuleDefinition ) ) {
				var cdi = pdb.GetCustomDebugInformation( h );
				if( pdb.GetGuid( cdi.Kind ) != SourceLinkKind ) continue;
				using var json = JsonDocument.Parse( pdb.GetBlobBytes( cdi.Value ) );
				if( !json.RootElement.TryGetProperty( "documents", out var documents ) ) return [];
				return documents.EnumerateObject().Select( p => (p.Name, p.Value.GetString() ?? "") ).ToArray();
			}
			return [];
		}
	}

	private string CacheDir => cacheDir;

	/// <summary>ソースから宣言を探します。line は手がかりの行で、同名が複数あればそれを含むものを選びます。</summary>
	internal static DefinitionEntry? FindDeclaration( string path, MetadataDefinition def, int line ) {
		var tree = CSharpSyntaxTree.ParseText( File.ReadAllText( path ), path: path );
		var matches = tree.GetRoot().DescendantNodes()
			.Select( n => (Node: n, Name: Declared( n, def )) )
			.Where( m => m.Name is not null )
			.Select( m => (m.Node, Name: m.Name!.Value, Span: tree.GetLineSpan( m.Node.Span )) )
			.ToList();
		if( matches.Count == 0 ) return null;
		var best = matches.FirstOrDefault( m => m.Span.StartLinePosition.Line <= line && line <= m.Span.EndLinePosition.Line );
		if( best.Node == null ) best = matches[0];
		var span = tree.GetLineSpan( best.Name.Span );
		return new DefinitionEntry( path, span.StartLinePosition.Line, span.StartLinePosition.Character,
			span.EndLinePosition.Line, span.EndLinePosition.Character );
	}

	/// <summary>n が def の宣言なら、その名前のトークンです。</summary>
	private static SyntaxToken? Declared( SyntaxNode n, MetadataDefinition def ) {
		if( def.IsType ) {
			return n switch {
				BaseTypeDeclarationSyntax t when t.Identifier.Text == def.Name => t.Identifier,
				DelegateDeclarationSyntax d when d.Identifier.Text == def.Name => d.Identifier,
				_ => null,
			};
		}
		var token = n switch {
			MethodDeclarationSyntax m when m.Identifier.Text == def.Name => m.Identifier,
			ConstructorDeclarationSyntax c when def.Name == ".ctor" => c.Identifier,
			PropertyDeclarationSyntax p when p.Identifier.Text == def.Name => p.Identifier,
			IndexerDeclarationSyntax i when def.Name == "this[]" => i.ThisKeyword,
			EventDeclarationSyntax e when e.Identifier.Text == def.Name => e.Identifier,
			VariableDeclaratorSyntax v when v.Identifier.Text == def.Name && v.Parent?.Parent is BaseFieldDeclarationSyntax => v.Identifier,
			EnumMemberDeclarationSyntax em when em.Identifier.Text == def.Name => em.Identifier,
			_ => (SyntaxToken?)null,
		};
		// 同名のメンバーがほかの型にもあるので、含む型の名前も確かめる
		if( token == null || n.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text != def.TypeName ) return null;
		return token;
	}

	private static async Task DownloadAsync( string url, string path ) {
		Console.Error.WriteLine( $"ダウンロード: {url}" );
		var bytes = await Http.GetByteArrayAsync( url );
		WriteReadOnly( path, bytes );
	}

	// 一時ファイルに書いてから移すので、途中で失敗しても壊れたファイルが残らない
	private static void WriteReadOnly( string path, byte[] bytes ) {
		Directory.CreateDirectory( Path.GetDirectoryName( path )! );
		var temp = path + ".tmp";
		File.WriteAllBytes( temp, bytes );
		try {
			File.Move( temp, path, overwrite: true );
			// キャッシュを開いたときに誤って編集しないよう読み取り専用にする
			File.SetAttributes( path, File.GetAttributes( path ) | FileAttributes.ReadOnly );
		} catch( IOException ) {
			File.Delete( temp );
		} catch( UnauthorizedAccessException ) {
			// 既に読み取り専用のファイルがある (ほかのプロセスが先に保存した)
			File.Delete( temp );
		}
	}

	private static string Hash( string s ) => Convert.ToHexStringLower( SHA256.HashData( Encoding.UTF8.GetBytes( s ) ) )[..16];

	private static HttpClient CreateHttp() {
		var http = new HttpClient { Timeout = TimeSpan.FromMinutes( 2 ) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd( "CsWSC-LanguageServer" );
		return http;
	}
}
