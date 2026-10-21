using System.IO;

namespace CsWSC.UI;

/// <summary>
/// キャッシュ済みのスクリプトが #load / #r しているファイルを監視し、変わったら --recompile を裏で起動します (常駐プロセス用)。
/// VS Code 以外のエディターで共通のスクリプトを書き換えても、ダブルクリックで実行したときにコンパイルを待たないようにするためのものです。
/// 監視するファイルはキャッシュフォルダの .deps から集め、.deps が変わるたびに集め直します。
/// </summary>
internal sealed class DependencyWatcher : IDisposable {
	/// <summary>保存は書き込みが何回かに分かれて通知されるので、落ち着くまで待ってからまとめて処理します。</summary>
	private static readonly TimeSpan Delay = TimeSpan.FromSeconds( 1 );

	private readonly string _cacheDirectory;
	private readonly object _gate = new();
	private readonly Dictionary<string, FileSystemWatcher> _watchers = new( StringComparer.OrdinalIgnoreCase );
	private readonly FileSystemWatcher? _cacheWatcher;
	private readonly System.Threading.Timer _recompileTimer, _refreshTimer;
	private IReadOnlySet<string> _files = new HashSet<string>();
	private bool _disposed;

	private DependencyWatcher( string cacheDirectory ) {
		_cacheDirectory = cacheDirectory;
		_recompileTimer = new( _ => Program.StartRecompile() );
		_refreshTimer = new( _ => Refresh() );
		try {
			Directory.CreateDirectory( cacheDirectory );
			_cacheWatcher = new FileSystemWatcher( cacheDirectory, "*.deps" ) {
				NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
			};
			_cacheWatcher.Created += ( _, _ ) => Schedule( _refreshTimer );
			_cacheWatcher.Renamed += ( _, _ ) => Schedule( _refreshTimer );
			_cacheWatcher.Deleted += ( _, _ ) => Schedule( _refreshTimer );
			_cacheWatcher.Changed += ( _, _ ) => Schedule( _refreshTimer );
			_cacheWatcher.EnableRaisingEvents = true;
		} catch( Exception e ) when( e is IOException or UnauthorizedAccessException or ArgumentException ) { }
	}

	/// <summary>監視を始めます。失敗しても例外は出しません (監視しないだけ)。</summary>
	public static DependencyWatcher Start() {
		var watcher = new DependencyWatcher( ScriptRunner.DefaultCacheDirectory );
		// .deps の読み込みは起動を遅らせないよう裏で行う
		watcher.Schedule( watcher._refreshTimer );
		return watcher;
	}

	private void Schedule( System.Threading.Timer timer ) {
		lock( _gate ) {
			if( !_disposed ) timer.Change( Delay, Timeout.InfiniteTimeSpan );
		}
	}

	/// <summary>.deps から監視するファイルを集め直し、フォルダごとの監視を作り直します。</summary>
	private void Refresh() {
		var files = ScriptRunner.CachedDependencies( _cacheDirectory );
		var directories = files.Select( Path.GetDirectoryName ).OfType<string>().ToHashSet( StringComparer.OrdinalIgnoreCase );
		lock( _gate ) {
			if( _disposed ) return;
			_files = files;
			foreach( var dir in _watchers.Keys.Where( d => !directories.Contains( d ) ).ToList() ) {
				_watchers[dir].Dispose();
				_watchers.Remove( dir );
			}
			foreach( var dir in directories.Where( d => !_watchers.ContainsKey( d ) ) ) {
				try {
					var watcher = new FileSystemWatcher( dir ) {
						NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
					};
					// エディターによっては一時ファイルに書いてから置き換えるので、作成・名前の変更も見る
					watcher.Changed += ( _, e ) => OnChanged( e.FullPath );
					watcher.Created += ( _, e ) => OnChanged( e.FullPath );
					watcher.Renamed += ( _, e ) => OnChanged( e.FullPath );
					watcher.EnableRaisingEvents = true;
					_watchers.Add( dir, watcher );
				} catch( Exception e ) when( e is IOException or UnauthorizedAccessException or ArgumentException ) { }
			}
		}
	}

	private void OnChanged( string path ) {
		bool watched;
		lock( _gate ) watched = _files.Contains( path );
		if( watched ) Schedule( _recompileTimer );
	}

	public void Dispose() {
		lock( _gate ) {
			if( _disposed ) return;
			_disposed = true;
			_cacheWatcher?.Dispose();
			foreach( var watcher in _watchers.Values ) watcher.Dispose();
			_watchers.Clear();
			_recompileTimer.Dispose();
			_refreshTimer.Dispose();
		}
	}
}
