using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CsWSC;

/// <summary>コンパイル済みスクリプト (埋め込み PDB 付きアセンブリ) のファイルキャッシュです。Roslyn に依存しません。</summary>
/// <remarks>
/// &lt;ハッシュ&gt;.dll にアセンブリ、&lt;ハッシュ&gt;.deps に依存ファイル (#load / #r) の一覧を保存します。
/// 依存ファイルの更新日時かサイズが変わっていれば使いません。
/// </remarks>
internal static class ScriptCache {
	/// <summary>コンパイル結果です。References は #r で参照した DLL のパスです (実行時の読み込みに使う)。</summary>
	public sealed record Entry( byte[] Image, IReadOnlyList<string> References );

	/// <summary>これを超えたら古いものから消します。</summary>
	private const int MaxFiles = 100;

	private const string LoadKind = "load", ReferenceKind = "r";

	/// <summary>キャッシュファイル (.dll) のパスです。キーはコード・ファイルパス (行番号表示と #r の基準)・CsWSC のビルドです。</summary>
	public static string? GetPath( string? directory, string code, string filePath ) {
		if( directory == null ) {
			return null;
		}

		var key = string.Join( "\0", code, filePath.ToUpperInvariant(), typeof( ScriptCache ).Module.ModuleVersionId );
		var hash = Convert.ToHexString( SHA256.HashData( Encoding.UTF8.GetBytes( key ) ) );
		return Path.Combine( directory, hash + ".dll" );
	}

	/// <summary>キャッシュを読みます。無いか、依存ファイルが変わっていれば null です。</summary>
	public static Entry? TryRead( string path ) {
		try {
			var references = new List<string>();
			foreach( var line in File.ReadAllLines( Path.ChangeExtension( path, ".deps" ) ) ) {
				if( line.Split( '\t' ) is not [var kind, var file, var ticks, var length] ) {
					return null;
				}

				var info = new FileInfo( file );
				if( !info.Exists || info.LastWriteTimeUtc.Ticks.ToString() != ticks || info.Length.ToString() != length ) {
					return null;
				}

				if( kind == ReferenceKind ) {
					references.Add( file );
				}
			}
			var image = File.ReadAllBytes( path );
			File.SetLastWriteTimeUtc( path, DateTime.UtcNow ); // 削除順の判定用
			return new Entry( image, references );
		} catch( IOException ) { return null; } catch( UnauthorizedAccessException ) { return null; }
	}

	/// <summary>保存します。loads は #load したスクリプトです。失敗しても実行には影響しないので無視します。</summary>
	public static void Write( string path, Entry entry, IReadOnlyList<string> loads ) {
		try {
			var directory = Path.GetDirectoryName( path )!;
			Directory.CreateDirectory( directory );
			var deps = loads.Select( f => (LoadKind, f) ).Concat( entry.References.Select( f => (ReferenceKind, f) ) )
				.Select( d => (d.Item1, Info: new FileInfo( d.f )) )
				.Select( d => $"{d.Item1}\t{d.Info.FullName}\t{d.Info.LastWriteTimeUtc.Ticks}\t{d.Info.Length}" );
			// .deps が無ければキャッシュは使われないので、.dll を先に置き .deps を最後に置く
			WriteAtomic( path, temp => File.WriteAllBytes( temp, entry.Image ) );
			WriteAtomic( Path.ChangeExtension( path, ".deps" ), temp => File.WriteAllLines( temp, deps ) );
			foreach( var old in new DirectoryInfo( directory ).GetFiles( "*.dll" )
				.OrderByDescending( f => f.LastWriteTimeUtc ).Skip( MaxFiles ) ) {
				old.Delete();
				File.Delete( Path.ChangeExtension( old.FullName, ".deps" ) );
			}
		} catch( IOException ) { } catch( UnauthorizedAccessException ) { }
	}

	private static void WriteAtomic( string path, Action<string> write ) {
		var temp = path + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";
		write( temp );
		File.Move( temp, path, overwrite: true );
	}
}
