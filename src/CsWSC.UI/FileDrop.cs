namespace CsWSC.UI;

/// <summary>コントロールへファイルをドラッグ＆ドロップで受け付けます。</summary>
internal static class FileDrop {
	/// <summary>
	/// ドロップされた最初の項目がファイルなら、そのパスを <paramref name="onDrop"/> に渡します。
	/// ライブラリ (.cswsl) は <paramref name="allowLibrary"/> が true のときだけ受け付けます。
	/// </summary>
	public static void Enable( System.Windows.Forms.Control control, Action<string> onDrop, bool allowLibrary = false ) {
		control.AllowDrop = true;
		control.DragEnter += ( _, e ) => {
			e.Effect = GetPath( e, allowLibrary ) != null ? DragDropEffects.Copy : DragDropEffects.None;
		};
		control.DragDrop += ( _, e ) => {
			if( GetPath( e, allowLibrary ) is not { } path ) return;
			// ドラッグ元 (エクスプローラー) を待たせないよう、ドロップ処理が終わってから開く
			control.BeginInvoke( () => {
				control.FindForm()?.Activate();
				onDrop( path );
			} );
		};
	}

	private static string? GetPath( DragEventArgs e, bool allowLibrary ) =>
		e.Data?.GetData( DataFormats.FileDrop ) is string[] { Length: > 0 } files && File.Exists( files[0] )
			&& ( allowLibrary || !ScriptLibraryResolver.IsLibrary( files[0] ) )
			? files[0]
			: null;
}
