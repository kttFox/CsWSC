using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CsWSC;

/// <summary>コンパイル済みスクリプト (埋め込み PDB 付きアセンブリ) のファイルキャッシュです。Roslyn に依存しません。</summary>
/// <remarks>
/// &lt;ハッシュ&gt;.dll にアセンブリ、&lt;ハッシュ&gt;.deps に依存ファイル (#load / #r) の一覧を保存します。
/// 依存ファイルの更新日時かサイズが変わっていれば使いません。
/// .deps にはスクリプト本体のパスも記録し、依存ファイルが変わったものを裏でコンパイルし直すのに使います (<see cref="FindStale"/>)。
/// </remarks>
internal static class ScriptCache {
	/// <summary>コンパイル結果です。References は #r で参照した DLL のパスです (実行時の読み込みに使う)。</summary>
	public sealed record Entry( byte[] Image, IReadOnlyList<string> References );

	/// <summary>これを超えたら古いものから消します。</summary>
	private const int MaxFiles = 100;

	private const string MainKind = "main", LoadKind = "load", ReferenceKind = "r";

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

				if( kind == MainKind ) {
					continue; // 本体はキーに含まれているので判定に使わない
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

	/// <summary>保存します。main はスクリプト本体 (無ければ null)、loads は #load したスクリプトです。失敗しても実行には影響しないので無視します。</summary>
	public static void Write( string path, Entry entry, string? main, IReadOnlyList<string> loads ) {
		try {
			var directory = Path.GetDirectoryName( path )!;
			Directory.CreateDirectory( directory );
			IEnumerable<(string, string)> files = main != null ? [(MainKind, main)] : [];
			var deps = files.Concat( loads.Select( f => (LoadKind, f) ) ).Concat( entry.References.Select( f => (ReferenceKind, f) ) )
				.Select( d => (Kind: d.Item1, Info: new FileInfo( d.Item2 )) )
				.Select( d => $"{d.Kind}\t{d.Info.FullName}\t{d.Info.LastWriteTimeUtc.Ticks}\t{d.Info.Length}" );
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

	/// <summary>
	/// 依存ファイル (#load / #r) が変わって使えなくなったキャッシュを探し、スクリプト本体のパスと .dll のパスを返します。
	/// 本体を記録していないもの (古い形式や温め用) は対象外です。
	/// </summary>
	public static IReadOnlyList<(string Main, string CachePath)> FindStale( string directory ) {
		var stale = new List<(string, string)>();
		try {
			foreach( var deps in Directory.EnumerateFiles( directory, "*.deps" ) ) {
				var dll = Path.ChangeExtension( deps, ".dll" );
				string? main;
				try {
					main = File.ReadLines( deps ).Select( l => l.Split( '	' ) ).FirstOrDefault( f => f is [MainKind, _, _, _] )?[1];
				} catch( IOException ) { continue; } catch( UnauthorizedAccessException ) { continue; }
				if( main != null && File.Exists( dll ) && !IsFresh( dll ) ) {
					stale.Add( (main, dll) );
				}
			}
		} catch( IOException ) { } catch( UnauthorizedAccessException ) { }
		return stale;
	}

	/// <summary>本体を記録しているキャッシュの依存ファイル (#load / #r) のフルパスを返します (変更の監視用)。</summary>
	public static IReadOnlySet<string> Dependencies( string directory ) {
		var files = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		try {
			foreach( var deps in Directory.EnumerateFiles( directory, "*.deps" ) ) {
				try {
					var lines = File.ReadAllLines( deps ).Select( l => l.Split( '\t' ) ).ToList();
					if( lines.Any( f => f is [MainKind, _, _, _] ) ) {
						files.UnionWith( lines.Where( f => f is [LoadKind or ReferenceKind, _, _, _] ).Select( f => f[1] ) );
					}
				} catch( IOException ) { } catch( UnauthorizedAccessException ) { }
			}
		} catch( IOException ) { } catch( UnauthorizedAccessException ) { }
		return files;
	}

	/// <summary>キャッシュがあり、依存ファイルが記録どおりかを返します (TryRead と違い .dll の更新日時を変えない)。</summary>
	public static bool IsFresh( string path ) {
		try {
			if( !File.Exists( path ) ) {
				return false;
			}

			foreach( var line in File.ReadAllLines( Path.ChangeExtension( path, ".deps" ) ) ) {
				if( line.Split( '	' ) is not [var kind, var file, var ticks, var length] ) {
					return false;
				}

				if( kind == MainKind ) {
					continue;
				}

				var info = new FileInfo( file );
				if( !info.Exists || info.LastWriteTimeUtc.Ticks.ToString() != ticks || info.Length.ToString() != length ) {
					return false;
				}
			}
			return true;
		} catch( IOException ) { return false; } catch( UnauthorizedAccessException ) { return false; }
	}

	/// <summary>キャッシュ (.dll と .deps) を消します。</summary>
	public static void Delete( string path ) {
		try {
			File.Delete( path );
			File.Delete( Path.ChangeExtension( path, ".deps" ) );
		} catch( IOException ) { } catch( UnauthorizedAccessException ) { }
	}

	private static void WriteAtomic( string path, Action<string> write ) {
		var temp = path + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";
		write( temp );
		File.Move( temp, path, overwrite: true );
	}
}
