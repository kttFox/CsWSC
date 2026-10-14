namespace CsWSC.UI;

/// <summary>コントロールへファイルをドラッグ＆ドロップで受け付けます。</summary>
internal static class FileDrop {
	/// <summary>ドロップされた最初の項目がファイルなら、そのパスを <paramref name="onDrop"/> に渡します。</summary>
	public static void Enable( System.Windows.Forms.Control control, Action<string> onDrop ) {
		control.AllowDrop = true;
		control.DragEnter += ( _, e ) => {
			e.Effect = GetPath( e ) != null ? DragDropEffects.Copy : DragDropEffects.None;
		};
		control.DragDrop += ( _, e ) => {
			if( GetPath( e ) is not { } path ) return;
			// ドラッグ元 (エクスプローラー) を待たせないよう、ドロップ処理が終わってから開く
			control.BeginInvoke( () => {
				control.FindForm()?.Activate();
				onDrop( path );
			} );
		};
	}

	private static string? GetPath( DragEventArgs e ) =>
		e.Data?.GetData( DataFormats.FileDrop ) is string[] { Length: > 0 } files && File.Exists( files[0] )
			? files[0]
			: null;
}
