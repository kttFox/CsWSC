using System.Globalization;
using System.Text;

namespace CsWSC.Recording;

/// <summary>記録したイベント列から C# スクリプトを生成します。</summary>
internal sealed class ScriptGenerator {
	private const int ClickMaxMs = 200;        // 押してから離すまでがこれ以内ならクリック
	private const int ClickMaxDistance = 4;    // 押した位置と離した位置のずれ許容
	private const long MinWaitMs = 50;         // これ未満の待ちは省略
	private const long SkipIdleMaxWaitMs = 500; // 「余分な時間は記録しない」時の最大待ち
	private const long TextMergeMs = 1000;     // この間隔以内の文字入力はまとめて SendText にする

	private static readonly HashSet<int> ModifierKeys = [0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

	private readonly RecordOptions _options;
	private readonly StringBuilder _sb = new();
	private readonly Dictionary<(IntPtr Handle, string Title, string Class), string> _windowVars = [];
	private readonly Dictionary<(IntPtr Handle, string Title, string Class), string> _windowTitles = [];
	private string? _activeVar;
	private long _lastTime;
	private int _pendingMs; // 次の行の前に待つ時間 (Move / Mouse / Wheel / Keyboard なら delayMs 引数にする)
	private WindowInfo? _dragWindow;
	private string? _orgVar; // 現在の MouseOrigin の基準ウィンドウ変数 (null は画面座標)

	// 文字入力のまとめ
	private readonly StringBuilder _text = new();
	private string? _textWindowVar;
	private long _textLastTime;

	// 修飾キーの状態: 押されている修飾キーと、単独で押されたか (他のキーと組み合わせていないか)
	private readonly List<int> _heldModifiers = [];
	private readonly HashSet<int> _usedModifiers = [];

	private ScriptGenerator( RecordOptions options ) => _options = options;

	private bool UseWindows => _options.RelativeMouse || _options.Background || _options.HighLevel;

	public static string Generate( IReadOnlyList<RecordedEvent> events, RecordOptions options ) {
		var g = new ScriptGenerator( options );
		g.CollectTitles( events );
		g.Header();
		g.Body( events );
		return g._sb.ToString();
	}

	private void Header() {
		var mode = new List<string> { _options.HighLevel ? "高レベル記録" : "低レベル記録" };
		if( _options.SkipIdle ) {
			mode.Add( "余分な時間・移動を省略" );
		}

		if( !_options.HighLevel ) {
			if( _options.RelativeMouse ) {
				mode.Add( "相対座標" );
			}

			if( _options.Background ) {
				mode.Add( "バックグラウンド" );
			}
		}
		Line( $"// CsWSC 記録 {DateTime.Now:yyyy/MM/dd HH:mm:ss} ({string.Join( ", ", mode )})" );
		Line( "" );
	}

	private void Body( IReadOnlyList<RecordedEvent> events ) {
		// 記録開始から最初の操作までの時間は待たない
		if( events.Count > 0 ) {
			_lastTime = events[0].Time;
		}

		for( int i = 0; i < events.Count; i++ ) {
			switch( events[i] ) {
				case MouseMoveEvent m:
					FlushText();
					Wait( m.Time );
					// ドラッグ中はドラッグ元のウィンドウ基準 (相対座標時)
					Line( $"Move({Pos( m.X, m.Y, _dragWindow )});" );
					break;

				case MouseButtonEvent { Down: true } down:
					FlushText();
					// 対応する「離す」を探してクリックかドラッグか判定
					int upIndex = FindUp( events, i, down.Button );
					if( upIndex >= 0 && events[upIndex] is MouseButtonEvent release && IsClick( down, release, events, i, upIndex ) ) {
						Wait( down.Time );
						EmitClick( down );
						events = Without( events, upIndex );
					} else {
						Wait( down.Time );
						Line( $"Mouse({ButtonArg( down.Button )}, KeyAction.Down, {Pos( down.X, down.Y, down.Window )});" );
						_dragWindow = down.Window;
					}
					break;

				case MouseButtonEvent up:
					FlushText();
					Wait( up.Time );
					// ドロップ先が別ウィンドウでも、ドラッグ中に Activate しないようドラッグ元基準で出す
					Line( $"Mouse({ButtonArg( up.Button )}, KeyAction.Up, {Pos( up.X, up.Y, _dragWindow ?? up.Window )});" );
					_dragWindow = null;
					break;

				case WheelEvent w:
					FlushText();
					Wait( w.Time );
					Line( $"Wheel({w.Notches}, {Pos( w.X, w.Y, w.Window )});" );
					break;

				case KeyEvent k:
					OnKey( k );
					break;
			}
		}
		FlushText();
	}

	// ---- マウス ----

	private static int FindUp( IReadOnlyList<RecordedEvent> events, int from, MouseButton button ) {
		for( int j = from + 1; j < events.Count; j++ ) {
			if( events[j] is MouseButtonEvent { Down: false } u && u.Button == button ) {
				return j;
			}
		}

		return -1;
	}

	private static bool IsClick( MouseButtonEvent down, MouseButtonEvent up, IReadOnlyList<RecordedEvent> events, int i, int j ) {
		if( up.Time - down.Time > ClickMaxMs ) {
			return false;
		}

		if( Math.Abs( up.X - down.X ) > ClickMaxDistance || Math.Abs( up.Y - down.Y ) > ClickMaxDistance ) {
			return false;
		}
		// 間に別の操作 (キー入力など) が挟まる場合はクリック扱いしない
		for( int k = i + 1; k < j; k++ ) {
			if( events[k] is not MouseMoveEvent ) {
				return false;
			}
		}

		return true;
	}

	// up と、down〜up 間のマウス移動を取り除く
	private static List<RecordedEvent> Without( IReadOnlyList<RecordedEvent> events, int upIndex ) {
		var list = new List<RecordedEvent>( events.Count );
		for( int k = 0; k < events.Count; k++ ) {
			if( k == upIndex ) {
				continue;
			}

			if( k < upIndex && events[k] is MouseMoveEvent && IsBetweenDownAndUp( events, k, upIndex ) ) {
				continue;
			}

			list.Add( events[k] );
		}
		return list;
	}

	private static bool IsBetweenDownAndUp( IReadOnlyList<RecordedEvent> events, int k, int upIndex ) {
		for( int d = k - 1; d >= 0; d-- ) {
			if( events[d] is MouseButtonEvent { Down: true } ) {
				return true;
			} else if( events[d] is not MouseMoveEvent ) {
				return false;
			}
		}

		return false;
	}

	private void EmitClick( MouseButtonEvent e ) {
		var btn = ButtonArg( e.Button );
		var win = e.Window;

		if( _options.HighLevel && win != null && e.Element is var (name, type) ) {
			var v = WindowVar( win );
			Activate( v );
			Line( $"{v}.ClickItem({Str( name )}); // {type}" );
			return;
		}
		if( win != null && UseWindows ) {
			var v = WindowVar( win );
			int dx = e.X - win.Left, dy = e.Y - win.Top;
			if( _options.Background && !_options.HighLevel ) {
				Line( $"{v}.PostMouse({btn}, KeyAction.Click, {dx}, {dy});" );
			} else {
				Activate( v );
				Line( $"{v}.Mouse({btn}, KeyAction.Click, {dx}, {dy});" );
			}
			return;
		}
		Org( null );
		Line( $"Mouse({btn}, KeyAction.Click, {e.X}, {e.Y});" );
	}

	// 座標をウィンドウ基準で出すときは、ウィンドウが変わるたびに MouseOrigin を出力する
	private string Pos( int x, int y, WindowInfo? win ) {
		if( win == null || !UseWindows ) {
			Org( null );
			return $"{x}, {y}";
		}
		var v = WindowVar( win );
		Activate( v );
		Org( v );
		return $"{x - win.Left}, {y - win.Top}";
	}

	private void Org( string? v ) {
		if( _orgVar == v ) {
			return;
		}

		Line( v == null ? "MouseOrigin();" : $"MouseOrigin({v});" );
		_orgVar = v;
	}

	private static string ButtonArg( MouseButton b ) => $"MouseButton.{b}";

	// ---- キーボード ----

	private void OnKey( KeyEvent k ) {
		if( ModifierKeys.Contains( k.VirtualKey ) ) {
			int mod = NormalizeModifier( k.VirtualKey );
			if( k.Down ) {
				if( !_heldModifiers.Contains( mod ) ) {
					_heldModifiers.Add( mod );
				}
			} else {
				// 記録開始前に押されていたキー (記録ホットキーの ALT など) の離しは無視する
				if( !_heldModifiers.Remove( mod ) ) {
					return;
				}
				// 修飾キーだけを押して離した (例: ALT 単独でメニュー)
				if( !_usedModifiers.Remove( mod ) && mod != 0x10 ) {
					FlushText();
					Wait( k.Time );
					KeyTarget( k.Window );
					Line( $"Keyboard({KeyName( mod )});" );
				}
			}
			return;
		}
		if( !k.Down ) {
			return;
		}

		foreach( var m in _heldModifiers ) {
			_usedModifiers.Add( m );
		}

		bool shift = _heldModifiers.Contains( 0x10 );
		bool combo = _heldModifiers.Any( m => m != 0x10 );

		if( !combo && ToChar( k.VirtualKey, shift ) is char ch ) {
			AppendText( ch, k );
			return;
		}

		FlushText();
		Wait( k.Time );
		var v = KeyTarget( k.Window );
		if( combo || shift ) {
			var modifiers = _heldModifiers.Select( KeyName ).ToList();
			Line( string.Join( " ", modifiers.Select( m => $"Keyboard({m}, KeyAction.Down);" )
				.Append( $"Keyboard({KeyName( k.VirtualKey )});" )
				.Concat( modifiers.AsEnumerable().Reverse().Select( m => $"Keyboard({m}, KeyAction.Up);" ) ) ) );
		} else if( _options.Background && !_options.HighLevel && v != null ) {
			Line( $"{v}.PostKey({KeyName( k.VirtualKey )});" );
		} else {
			Line( $"Keyboard({KeyName( k.VirtualKey )});" );
		}
	}

	private void AppendText( char ch, KeyEvent k ) {
		var v = UseWindows && k.Window != null ? WindowVar( k.Window ) : null;
		if( _text.Length > 0 && ( v != _textWindowVar || k.Time - _textLastTime > TextMergeMs ) ) {
			FlushText();
		}

		if( _text.Length == 0 ) {
			Wait( k.Time );
			_textWindowVar = v;
		}
		_text.Append( ch );
		_textLastTime = k.Time;
		_lastTime = k.Time;
	}

	private void FlushText() {
		if( _text.Length == 0 ) {
			return;
		}

		var s = Str( _text.ToString() );
		if( _textWindowVar != null && _options.Background && !_options.HighLevel ) {
			Line( $"{_textWindowVar}.PostText({s});" );
		} else {
			if( _textWindowVar != null ) {
				Activate( _textWindowVar );
			}

			Line( $"SendText({s});" );
		}
		_text.Clear();
		_textWindowVar = null;
	}

	private string? KeyTarget( WindowInfo? win ) {
		if( win == null || !UseWindows ) {
			return null;
		}

		var v = WindowVar( win );
		if( !( _options.Background && !_options.HighLevel ) ) {
			Activate( v );
		}

		return v;
	}

	private static int NormalizeModifier( int vk ) => vk switch {
		0xA0 or 0xA1 => 0x10, // Shift
		0xA2 or 0xA3 => 0x11, // Ctrl
		0xA4 or 0xA5 => 0x12, // Alt
		0x5C => 0x5B,         // Win
		_ => vk,
	};

	/// <summary>そのまま文字として送れるキーです (英数字・スペース)。</summary>
	private static char? ToChar( int vk, bool shift ) => vk switch {
		>= 'A' and <= 'Z' => shift ? (char)vk : char.ToLowerInvariant( (char)vk ),
		>= '0' and <= '9' when !shift => (char)vk,
		0x20 when !shift => ' ',
		_ => null,
	};

	private static string KeyName( int vk ) => vk switch {
		0x10 => "Keys.ShiftKey",
		0x11 => "Keys.ControlKey",
		0x12 => "Keys.Menu",
		0x5B => "Keys.LWin",
		0x0D => "Keys.Enter",
		0x08 => "Keys.Back",
		0x1B => "Keys.Escape",
		0x22 => "Keys.PageDown",
		0x21 => "Keys.PageUp",
		_ => Enum.IsDefined( typeof( Keys ), vk ) && !int.TryParse( ( (Keys)vk ).ToString(), out _ )
			? $"Keys.{(Keys)vk}"
			: $"(Keys){vk}",
	};

	// ---- ウィンドウ ----

	// 同じウィンドウ (ハンドルが同じ) は、記録中にタイトルが変わっても 1 つの変数にまとめる
	private static (IntPtr, string, string) WindowKey( WindowInfo win ) =>
		win.Handle != IntPtr.Zero ? (win.Handle, "", win.ClassName) : (IntPtr.Zero, win.Title, win.ClassName);

	// タイトルが変わったウィンドウは、記録中の全タイトルに共通する部分で探す (GetWindow は部分一致)
	private void CollectTitles( IReadOnlyList<RecordedEvent> events ) {
		foreach( var e in events ) {
			var win = e switch {
				MouseButtonEvent m => m.Window,
				WheelEvent w => w.Window,
				KeyEvent k => k.Window,
				_ => null,
			};
			if( win == null ) {
				continue;
			}

			var key = WindowKey( win );
			_windowTitles[key] = _windowTitles.TryGetValue( key, out var t ) ? CommonPart( t, win.Title ) : win.Title;
		}
	}

	/// <summary>2 つの文字列の最長共通部分文字列です (前後の空白・区切り記号を除く)。</summary>
	internal static string CommonPart( string a, string b ) {
		if( a == b ) {
			return a;
		}

		int best = 0, end = 0;
		var prev = new int[b.Length + 1];
		for( int i = 1; i <= a.Length; i++ ) {
			var cur = new int[b.Length + 1];
			for( int j = 1; j <= b.Length; j++ ) {
				if( a[i - 1] == b[j - 1] ) {
					cur[j] = prev[j - 1] + 1;
					if( cur[j] > best ) {
						best = cur[j];
						end = i;
					}
				}
			}
			prev = cur;
		}
		return a.Substring( end - best, best ).Trim( ' ', '-', '•', '●', '*' );
	}

	private string WindowVar( WindowInfo win ) {
		var key = WindowKey( win );
		if( _windowVars.TryGetValue( key, out var v ) ) {
			return v;
		}

		v = $"w{_windowVars.Count + 1}";
		_windowVars[key] = v;
		var title = _windowTitles.GetValueOrDefault( key, win.Title );
		var label = title.Length > 0 ? title : win.ClassName;
		Line( $"var {v} = GetWindow({Str( title )}, {Str( win.ClassName )}) ?? throw new Exception({Str( "ウィンドウが見つかりません: " + label )});" );
		return v;
	}

	private void Activate( string v ) {
		if( _activeVar == v ) {
			return;
		}

		Line( $"{v}.Activate();" );
		_activeVar = v;
	}

	// ---- 出力 ----

	private void Wait( long time ) {
		long ms = time - _lastTime;
		_lastTime = time;
		if( _options.SkipIdle ) {
			ms = Math.Min( ms, SkipIdleMaxWaitMs );
		}

		if( ms >= MinWaitMs ) {
			_pendingMs += (int)ms;
		}
	}

	// UWSC の BTN / KBD / MMV と同じく、待ち時間は可能なら delayMs 引数 (実行前の待ち) に入れる
	// wN.Mouse / wN.PostMouse / wN.PostKey / wN.PostText も delayMs 引数を持つ
	private static bool HasDelayArgument( string s ) {
		var dot = s.IndexOf( '.' );
		if( dot <= 0 || !s[..dot].All( char.IsLetterOrDigit ) ) return false;
		var call = s[( dot + 1 )..];
		return call.StartsWith( "Mouse(" ) || call.StartsWith( "PostMouse(" ) || call.StartsWith( "PostKey(" ) || call.StartsWith( "PostText(" );
	}

	private void Line( string s ) {
		if( _pendingMs > 0 ) {
			var ms = _pendingMs;
			_pendingMs = 0;
			// 1 行に複数の文があるとき (同時押し) は、先頭の文に待ち時間を入れる
			var rest = "";
			if( s.IndexOf( "; " ) is int split and >= 0 ) {
				rest = s[( split + 1 )..];
				s = s[..( split + 1 )];
			}
			if( s.StartsWith( "Keyboard(" ) && !s.Contains( "KeyAction." ) ) {
				s = s[..^2] + $", KeyAction.Click, {ms});";
			} else if( s.StartsWith( "Keyboard(" ) || s.StartsWith( "Mouse(" ) || s.StartsWith( "Move(" ) || s.StartsWith( "Wheel(" )
				|| HasDelayArgument( s ) ) {
				s = s[..^2] + $", {ms});";
			} else {
				Line( $"Sleep({ms});" );
			}
			s += rest;
		}
		_sb.Append( s ).Append( "\r\n" );
	}

	private static string Str( string s ) {
		var sb = new StringBuilder( "\"" );
		foreach( var c in s ) {
			sb.Append( c switch {
				'\\' => "\\\\",
				'"' => "\\\"",
				'\r' => "\\r",
				'\n' => "\\n",
				'\t' => "\\t",
				< ' ' => $"\\u{(int)c:X4}",
				_ => c.ToString(),
			} );
		}
		return sb.Append( '"' ).ToString();
	}
}
