using System.Text.Json;
using CsWSC.UI;

namespace CsWSC.Tests;

public class HotkeyTests {
	[Theory]
	[InlineData( "", 0u )]
	[InlineData( "ALT", Hotkey.MOD_ALT )]
	[InlineData( "WIN", Hotkey.MOD_WIN )]
	[InlineData( "CTRL+ALT", Hotkey.MOD_CONTROL | Hotkey.MOD_ALT )]
	[InlineData( "CTRL+SHIFT", Hotkey.MOD_CONTROL | Hotkey.MOD_SHIFT )]
	[InlineData( "FOO", 0u )] // 知らない修飾キーは無視する
	public void ModifierFlags_ParsesPlusSeparated( string modifier, uint expected ) {
		Assert.Equal( expected, new Hotkey( modifier, "F1" ).ModifierFlags );
	}

	[Theory]
	[InlineData( "F1", 0x70u )]
	[InlineData( "F12", 0x7Bu )]
	[InlineData( "A", 0x41u )]
	[InlineData( "0", 0x30u )]
	[InlineData( "9", 0x39u )]
	[InlineData( "PAUSE", 0x13u )]
	[InlineData( "SCROLL", 0x91u )]
	[InlineData( "DELETE", 0x2Eu )]
	public void TryGetVirtualKey_KnownKeys( string key, uint vk ) {
		var h = new Hotkey( "ALT", key );
		Assert.True( h.TryGetVirtualKey( out var actual ) );
		Assert.Equal( vk, actual );
		Assert.True( h.Enabled );
	}

	[Theory]
	[InlineData( "なし" )]
	[InlineData( "" )]
	[InlineData( "NOSUCHKEY" )]
	public void Disabled_WhenKeyIsNoneOrUnknown( string key ) {
		var h = new Hotkey( "ALT", key );
		Assert.False( h.Enabled );
		Assert.Equal( "なし", h.ToString() );
	}

	[Fact]
	public void AllChoices_AreEnabledExceptNone() {
		foreach( var key in Hotkey.Keys ) {
			Assert.Equal( key != "なし", new Hotkey( "CTRL", key ).Enabled );
		}
	}

	[Fact]
	public void ToString_OmitsEmptyModifier() {
		Assert.Equal( "ALT + F1", new Hotkey( "ALT", "F1" ).ToString() );
		Assert.Equal( "CTRL+ALT + A", new Hotkey( "CTRL+ALT", "A" ).ToString() );
		Assert.Equal( "F5", new Hotkey( "", "F5" ).ToString() );
	}

	[Fact]
	public void Equality_IgnoresPassThrough() {
		// 重複チェックはキーの組み合わせだけで判定する
		var a = new Hotkey( "ALT", "F1" );
		var b = new Hotkey( "ALT", "F1" ) { PassThrough = true };
		Assert.Equal( a, b );
		Assert.Equal( a.GetHashCode(), b.GetHashCode() );
		Assert.Single( new HashSet<Hotkey> { a, b } );
		Assert.NotEqual( a, new Hotkey( "CTRL", "F1" ) );
		Assert.NotEqual( a, new Hotkey( "ALT", "F2" ) );
	}

	[Fact]
	public void Json_RoundTripKeepsPassThrough() {
		var h = new Hotkey( "CTRL+SHIFT", "F9" ) { PassThrough = true };
		var back = JsonSerializer.Deserialize<Hotkey>( JsonSerializer.Serialize( h ) )!;
		Assert.Equal( "CTRL+SHIFT", back.Modifier );
		Assert.Equal( "F9", back.Key );
		Assert.True( back.PassThrough );
		Assert.False( JsonSerializer.Deserialize<Hotkey>( JsonSerializer.Serialize( new Hotkey( "ALT", "F1" ) ) )!.PassThrough );
	}

	[Fact]
	public void Settings_JsonRoundTripKeepsHotkeysAndLaunchers() {
		var launchers = LauncherSlot.Normalize( [
			new() { Path = @"C:\scripts\a.csws", Hotkeys = [new Hotkey( "CTRL+ALT", "1" ) { PassThrough = true }] },
			new() { Title = "==== 見出し ====" },
			new() { Title = "階層", Children = LauncherSlot.Normalize( [new() { Title = "B", Path = @"C:\scripts\b.csws", SeparateProcess = true }] ) },
		] );
		var settings = new AppSettings {
			StopHotkeys = [new Hotkey( "", "F10" ) { PassThrough = true }, new Hotkey( "ALT", "F2" )],
			LauncherMenuHotkeys = [new Hotkey( "CTRL", "SPACE" )],
			Launchers = launchers,
		};
		var back = JsonSerializer.Deserialize<AppSettings>( JsonSerializer.Serialize( settings ) )!;
		Assert.Equal( [new Hotkey( "", "F10" ), new Hotkey( "ALT", "F2" )], back.StopHotkeys );
		Assert.True( back.StopHotkeys[0].PassThrough );
		Assert.False( back.StopHotkeys[1].PassThrough );
		Assert.Equal( [new Hotkey( "CTRL", "SPACE" )], back.LauncherMenuHotkeys );
		Assert.Equal( LauncherSlot.Count, back.Launchers.Count );
		Assert.True( back.Launchers[0].IsScript );
		Assert.Equal( "a.csws", back.Launchers[0].DisplayName );
		Assert.Equal( [new Hotkey( "CTRL+ALT", "1" )], back.Launchers[0].Hotkeys );
		Assert.True( back.Launchers[0].Hotkeys[0].PassThrough );
		// タイトルだけの枠は見出し行 (スクリプトでも階層でも空でもない)
		Assert.False( back.Launchers[1].IsScript || back.Launchers[1].IsFolder || back.Launchers[1].IsEmpty );
		Assert.True( back.Launchers[2].IsFolder );
		Assert.Equal( LauncherSlot.Count, back.Launchers[2].Children!.Count );
		Assert.True( back.Launchers[2].Children![0].SeparateProcess );
		Assert.True( back.Launchers[3].IsEmpty );
	}

	[Fact]
	public void Settings_ReadsOldLauncherFormat() {
		// 旧形式 (Path と Hotkey だけの一覧) もそのまま先頭の枠から読める
		const string json = """{"Launchers":[{"Path":"C:/a.csws","Hotkey":{"Modifier":"ALT","Key":"F5","PassThrough":false}}]}""";
		var back = JsonSerializer.Deserialize<AppSettings>( json )!;
		var slots = LauncherSlot.Normalize( back.Launchers );
		Assert.Equal( LauncherSlot.Count, slots.Count );
		Assert.True( slots[0].IsScript );
		Assert.Equal( [new Hotkey( "ALT", "F5" )], slots[0].Hotkeys );
		// 未設定 (なし) のメニューのキーは含めない
		Assert.Equal( [new Hotkey( "ALT", "F5" )], new AppSettings { Launchers = slots }.LauncherHotkeys() );
	}

	[Fact]
	public void Settings_ReadsSingleHotkeyFormat() {
		// 1 つだけ設定できた頃の形式 (配列でなくオブジェクト) も読める。「なし」は取り除く
		const string json = """{"PlayHotkey":{"Modifier":"CTRL","Key":"F6","PassThrough":true},"StopHotkey":{"Modifier":"","Key":"なし"},"LauncherMenuHotkey":null}""";
		var back = JsonSerializer.Deserialize<AppSettings>( json )!;
		Assert.Equal( [new Hotkey( "CTRL", "F6" )], back.PlayHotkeys );
		Assert.True( back.PlayHotkeys[0].PassThrough );
		Assert.Empty( back.StopHotkeys );
		Assert.Empty( back.LauncherMenuHotkeys );
		// 書いていないものは既定値のまま
		Assert.Equal( [new Hotkey( "ALT", "F3" )], back.RecordHotkeys );
	}

	[Fact]
	public void Hotkey_ToStringOfList() {
		Assert.Equal( "なし", Hotkey.ToString( [] ) );
		Assert.Equal( "ALT + F1 / F5", Hotkey.ToString( [new( "ALT", "F1" ), new( "", "なし" ), new( "", "F5" )] ) );
		Assert.Equal( "ALT + F1 (グローバル), F5 (フック)", Hotkey.ToDisplayString( [new( "ALT", "F1" ), new Hotkey( "", "F5" ) { PassThrough = true }] ) );
	}

	[Theory]
	[InlineData( "", "", "notepad", "無題", true )]                 // 限定なし
	[InlineData( "notepad.exe", "", "notepad", "無題", true )]
	[InlineData( "NOTEPAD.EXE", "", "notepad", "無題", true )]      // 大文字小文字は問わない
	[InlineData( "notepad", "", "notepad", "無題", false )]         // .exe も含めて全体一致
	[InlineData( "notepad.exe; excel.exe", "", "EXCEL", "Book1", true )] // ; 区切り
	[InlineData( "note*", "", "notepad", "無題", true )]            // ワイルドカード
	[InlineData( "notepad.exe", "", "notepad++", "無題", false )]   // プロセス名は全体一致
	[InlineData( "", "メモ帳", "notepad", "無題 - メモ帳", true )]   // タイトルは部分一致
	[InlineData( "", "*.txt - メモ帳", "notepad", "a.txt - メモ帳", true )]
	[InlineData( "", "*.txt - メモ帳", "notepad", "a.csv - メモ帳", false )]
	[InlineData( "notepad.exe", "Excel", "notepad", "無題", false )] // 両方指定は両方満たす
	public void LauncherSlot_MatchesTarget( string apps, string title, string process, string windowTitle, bool expected ) {
		var slot = new LauncherSlot { Path = "a.csws", TargetApps = apps, TargetTitle = title };
		Assert.Equal( expected, slot.MatchesTarget( process, windowTitle ) );
	}

	[Fact]
	public void LauncherSlot_CheckHotkeys_SameLauncher() {
		var key = new Hotkey( "CTRL", "1" );
		LauncherSlot Script() => new() { Path = "a.csws", Hotkeys = [key] };
		Assert.Null( LauncherSlot.CheckHotkeys( [Script()], [] ) );
		// 同じランチャーの中で同じキーは NG (階層の中も同じランチャー)
		Assert.NotNull( LauncherSlot.CheckHotkeys( [Script(), Script()], [] ) );
		var folder = new LauncherSlot { Title = "F", Children = LauncherSlot.Normalize( [Script()] ) };
		Assert.NotNull( LauncherSlot.CheckHotkeys( [folder, Script()], [] ) );
		Assert.Equal( [0, 0], LauncherSlot.HotkeyScripts( [folder, Script()] ).Select( x => x.Conditions.Length ) );
		// 再生などのホットキーとは NG
		Assert.NotNull( LauncherSlot.CheckHotkeys( [Script()], [key] ) );
	}

	[Theory]
	[InlineData( "notepad.exe", "", "excel.exe", "", true )]          // プロセス名が違えば方式を混ぜられる
	[InlineData( "notepad.exe", "", "NOTEPAD.EXE", "", false )]   // 同じプロセス
	[InlineData( "note*", "", "excel.exe", "", true )]            // パターンに合わない
	[InlineData( "note*", "", "notepad.exe", "", false )]         // パターンに合う
	[InlineData( "note*", "", "ex*", "", false )]             // パターンどうしは重なるとみなす
	[InlineData( "notepad.exe", "", "", "Book1", false )]         // タイトルだけの条件は判定できない
	[InlineData( "notepad.exe", "", null, null, false )]          // 対象アプリなしとは混ぜられない
	public void LauncherSlot_CheckHotkeys_MixedRegistration( string apps1, string title1, string? apps2, string? title2, bool ok ) {
		var key = new Hotkey( "CTRL", "1" );
		LauncherHotkeyEntry Entry( string? apps, string? title, bool passThrough ) {
			var script = new LauncherSlot { Path = "a.csws", Hotkeys = [key with { PassThrough = passThrough }] };
			LauncherSlot[] conditions = apps == null ? [] : [new LauncherSlot { TargetApps = apps, TargetTitle = title! }];
			return new( script.Hotkeys[0], conditions, script, null );
		}
		var error = LauncherSlot.CheckHotkeys( [Entry( apps1, title1, false ), Entry( apps2, title2, true )], [] );
		Assert.Equal( ok, error == null );
	}

	[Fact]
	public void LauncherProfiles_HotkeyEntries() {
		var menuKey = new Hotkey( "CTRL", "SPACE" );
		var key = new Hotkey( "CTRL", "1" );
		var notepad = new LauncherSlot {
			TargetApps = "notepad.exe",
			Hotkeys = [menuKey],
			Children = LauncherSlot.Normalize( [new() { Path = "n.csws", Hotkeys = [key] }] ),
		};
		var settings = new AppSettings {
			LauncherMenuHotkeys = [menuKey],
			Launchers = LauncherSlot.Normalize( [new() { Path = "a.csws", Hotkeys = [key, new( "CTRL", "2" )] }] ),
			LauncherProfiles = [notepad],
		};
		var entries = settings.LauncherHotkeyEntries().ToList();
		// 対象アプリなしのメニュー・枠、対象アプリのメニュー・枠の順。対象アプリのものはその条件が付く
		// 複数のキーを設定した枠はキーごとに並ぶ
		Assert.Equal( [0, 0, 0, 1, 1], entries.Select( e => e.Conditions.Length ) );
		Assert.NotNull( entries[0].Menu );
		Assert.Equal( [new Hotkey( "CTRL", "1" ), new Hotkey( "CTRL", "2" )], entries.Skip( 1 ).Take( 2 ).Select( e => e.Hotkey ) );
		Assert.Same( entries[1].Script, entries[2].Script );
		Assert.Same( notepad, entries[3].Menu );
		Assert.Equal( "n.csws", entries[4].Script!.Path );
		// 対象アプリが違えば同じキー (メニュー・枠とも) を使える
		Assert.Null( LauncherSlot.CheckHotkeys( entries, [] ) );
		// 同じランチャーの中で同じキーは NG
		notepad.Children[1] = new() { Path = "m.csws", Hotkeys = [key] };
		Assert.NotNull( LauncherSlot.CheckHotkeys( settings.LauncherHotkeyEntries(), [] ) );
	}

	[Fact]
	public void LauncherHotkeyEntry_Pick_PrefersMoreSpecific() {
		var key = new Hotkey( "CTRL", "1" );
		var notepad = new LauncherSlot { TargetApps = "notepad.exe" };
		var memo = new LauncherSlot { TargetApps = "notepad.exe", TargetTitle = "メモ" };
		LauncherSlot Script( string path ) => new() { Path = path, Hotkeys = [key] };
		var global = new LauncherHotkeyEntry( key, [], Script( "g.csws" ), null );
		var byApp = new LauncherHotkeyEntry( key, [notepad], Script( "n.csws" ), null );
		var byTitle = new LauncherHotkeyEntry( key, [notepad, memo], Script( "m.csws" ), null );
		LauncherHotkeyEntry[] all = [global, byApp, byTitle];
		// 合うもののうち条件の多いものを選ぶ (並び順に関係なく)
		Assert.Same( byTitle, LauncherHotkeyEntry.Pick( all, "notepad", "メモ - 無題" ) );
		Assert.Same( byTitle, LauncherHotkeyEntry.Pick( [byTitle, byApp, global], "notepad", "メモ - 無題" ) );
		Assert.Same( byApp, LauncherHotkeyEntry.Pick( all, "notepad", "無題" ) );
		// 対象アプリに合わなければ対象アプリなしのもの
		Assert.Same( global, LauncherHotkeyEntry.Pick( all, "excel", "Book1" ) );
		// 合うものが無ければ null
		Assert.Null( LauncherHotkeyEntry.Pick( [byApp], "excel", "Book1" ) );
	}

	[Fact]
	public void LauncherProfiles_JsonRoundTrip() {
		var settings = new AppSettings {
			LauncherProfiles = [new() { TargetApps = "excel", Hotkeys = [new( "ALT", "Q" ), new( "ALT", "W" )], Children = LauncherSlot.Normalize( [new() { Path = "x.csws" }] ) }],
		};
		var back = JsonSerializer.Deserialize<AppSettings>( JsonSerializer.Serialize( settings ) )!;
		Assert.Single( back.LauncherProfiles );
		Assert.Equal( "excel", back.LauncherProfiles[0].ProfileName );
		Assert.Equal( [new Hotkey( "ALT", "Q" ), new Hotkey( "ALT", "W" )], back.LauncherProfiles[0].Hotkeys );
		Assert.Equal( "x.csws", back.LauncherProfiles[0].Children![0].Path );
	}

	[Fact]
	public void LauncherSlot_KeyOf() {
		Assert.Equal( '0', LauncherSlot.KeyOf( 0 ) );
		Assert.Equal( '9', LauncherSlot.KeyOf( 9 ) );
		Assert.Equal( 'A', LauncherSlot.KeyOf( 10 ) );
		Assert.Equal( 'Z', LauncherSlot.KeyOf( 35 ) );
	}
}
