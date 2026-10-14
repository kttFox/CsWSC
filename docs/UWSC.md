# UWSC からの移行ガイド

UWSC のスクリプトを CsWSC (C#) に書き換えるときの対応表と注意点です。
各コマンドの詳しい引数は [コマンド説明書](Commands.md) を参照してください。

## 目次

1. [考え方の違い](#1-考え方の違い)
2. [構文](#2-構文)
3. [出力・ダイアログ・待機](#3-出力ダイアログ待機)
4. [マウス](#4-マウス)
5. [キーボード](#5-キーボード)
6. [ウィンドウの取得](#6-ウィンドウの取得)
7. [ウィンドウの状態 (STATUS)](#7-ウィンドウの状態-status)
8. [ウィンドウの制御 (ACW / CTRLWIN)](#8-ウィンドウの制御-acw--ctrlwin)
9. [コントロール操作](#9-コントロール操作)
10. [画像](#10-画像)
11. [スレッド・プロセス・COM・クリップボード](#11-スレッドプロセスcomクリップボード)
12. [組み込み関数の代わりに .NET を使うもの](#12-組み込み関数の代わりに-net-を使うもの)
13. [書き換え例](#13-書き換え例)
14. [対応していないもの](#14-対応していないもの)

---

## 1. 考え方の違い

| 項目 | UWSC | CsWSC |
|---|---|---|
| 言語 | UWSC 独自の BASIC 風言語 | C# (スクリプト形式。`class` や `Main` は不要) |
| 大文字・小文字 | 区別しない | **区別する** (`Print` と `print` は別物)。ただし組み込み関数には全部大文字の別名がある (`PRINT`・`w.ACTIVATE()` など。名前は C# 名を大文字にしたもの) |
| 文の区切り | 改行 | `;` |
| ウィンドウ | 数値の ID (`GETID` の戻り値) | `Window` オブジェクト。操作はメソッドとして呼ぶ (`w.Activate()`) |
| 見つからないとき | `GETID` は `-1` | `GetWindow` は `null` |
| 時間の単位 | 関数ごとに秒・ミリ秒が混在 | **すべてミリ秒** (int)。`SLEEP(1)` は `Sleep(1000)` になる |
| 座標 | 画面座標。`MOUSEORG` で切り替え | 画面座標。`MouseOrigin` で切り替え (基準はクライアント領域左上) |
| 文字列の添字 | 1 始まり (`COPY` / `POS`) | **0 始まり** (`Substring` / `IndexOf`) |
| 配列 | `DIM a[10]` で 11 要素 | `new int[10]` で 10 要素 |
| 真偽値 | 数値 (`0` が偽) | `bool` (`true` / `false`)。数値を条件に直接書けない |
| 文字列の結合 | `+` | `+` または `$"…{式}…"` (文字列補間) |
| 戻り値の受け取り | 関数の戻り値 + `G_IMG_X` などのシステム変数 | 戻り値のみ (`Point?` などで返す) |
| 型 | 実行時に決まる | コンパイル時に決まる (`var` で推論) |
| 編集支援 | なし | VS Code 拡張で補完・エラー表示・定義へ移動・名前の変更・整形 |
| デバッグ | `Print` で値を確認 | VS Code 拡張でブレークポイント・ステップ実行 (`F5`) |

---

## 2. 構文

### 変数・定数・配列

| UWSC | CsWSC (C#) | 備考 |
|---|---|---|
| `DIM a = 1` | `var a = 1;` | |
| `PUBLIC a = 1` | `var a = 1;` (スクリプトの先頭で宣言) | トップレベルの変数は関数からも参照できる |
| `CONST A = 1` | `const int A = 1;` | |
| `DIM arr[10]` | `var arr = new int[11];` | UWSC は添字 0〜10 の 11 要素 |
| `DIM arr[] = 1, 2, 3` | `int[] arr = [1, 2, 3];` | |
| `RESIZE(arr, n)` | `Array.Resize(ref arr, n + 1);` / `List<T>` を使う | 要素数が変わるなら `List<T>` が楽 |
| `LENGTH(arr)` | `arr.Length` / `list.Count` | |
| 連想配列 `HASHTBL h` | `var h = new Dictionary<string, object>();` | |
| `h[キー] = 値` / `h[キー, HASH_EXISTS]` | `h[キー] = 値;` / `h.ContainsKey(キー)` | |

### 制御構文

| UWSC | CsWSC (C#) |
|---|---|
| `IF 条件 THEN 文 ELSE 文` | `if (条件) 文; else 文;` |
| `IFB 条件` … `ELSEIF 条件` … `ELSE` … `ENDIF` | `if (条件) { … } else if (条件) { … } else { … }` |
| `SELECT 式` `CASE 1, 2` … `DEFAULT` … `SELEND` | `switch (式) { case 1: case 2: …; break; default: …; break; }` |
| `FOR i = 0 TO 9` … `NEXT` | `for (var i = 0; i <= 9; i++) { … }` |
| `FOR i = 10 TO 0 STEP -2` … `NEXT` | `for (var i = 10; i >= 0; i -= 2) { … }` |
| `FOR v IN arr` … `NEXT` | `foreach (var v in arr) { … }` |
| `WHILE 条件` … `WEND` | `while (条件) { … }` |
| `REPEAT` … `UNTIL 条件` | `do { … } while (!(条件));` |
| `BREAK` / `CONTINUE` | `break;` / `continue;` |
| `EXIT` (メイン) | `return;` |
| `EXIT` (関数内) | `return;` / `return 値;` |
| `EXITEXIT` | `ExitScript();` |
| `TRY` … `EXCEPT` … `ENDTRY` | `try { … } catch (Exception e) { … }` |
| `TRY` … `FINALLY` … `ENDTRY` | `try { … } finally { … }` |
| `TRY_ERRMSG` / `TRY_ERRLINE` | `e.Message` / `e.StackTrace` |
| `RAISE(番号, メッセージ)` | `throw new Exception("メッセージ");` |

### 演算子

| UWSC | CsWSC (C#) |
|---|---|
| `=` (比較) / `<>` | `==` / `!=` |
| `AND` / `OR` / `NOT` (論理) | `&&` / `\|\|` / `!` |
| `AND` / `OR` / `XOR` (ビット) | `&` / `\|` / `^` |
| `MOD` | `%` |
| `/` (整数同士でも小数) | `/` (**整数同士は切り捨て**)。小数にするなら `a / 2.0` |
| `a += 1` | `a += 1;` / `a++;` |
| `IIF(条件, a, b)` | `条件 ? a : b` |

### 関数

```
// UWSC
PRINT Add(1, 2)

FUNCTION Add(a, b)
    RESULT = a + b
FEND

PROCEDURE Hello(name)
    PRINT "Hello " + name
FEND
```

```csharp
// CsWSC
Print(Add(1, 2));

int Add(int a, int b) => a + b;

void Hello(string name)
{
    Print("Hello " + name);
}
```

| UWSC | CsWSC (C#) |
|---|---|
| `PROCEDURE 名前(引数)` … `FEND` | `void 名前(型 引数) { … }` |
| `FUNCTION 名前(引数)` … `RESULT = 値` … `FEND` | `型 名前(型 引数) { …; return 値; }` |
| `VAR` 引数 (参照渡し) | `ref` 引数 |
| 引数の既定値 `a = 1` | `int a = 1` |
| `MODULE` … `ENDMODULE` | `static class` / `class` |
| `CLASS` … `ENDCLASS` | `class` |

関数はスクリプトのどこに書いても呼べます。

### ファイル・その他

| UWSC | CsWSC (C#) |
|---|---|
| `CALL ファイル.uws` | `#load "ファイル.csws"` (相対パスはスクリプトのフォルダが基準) |
| `DEF_DLL 関数名(引数):戻り値:DLL名` | `[DllImport]` を付けた `static extern` メソッド ([例](#win32-api-def_dll)) |
| `OPTION EXPLICIT` | 不要 (C# は常に宣言が必要) |
| `OPTION LOGPATH` | 対応なし。ログは `File.AppendAllText` などで自前で書く |
| その他の OPTION | 一部のみ設定画面 (記録方法・ホットキー・タスクトレイ) |
| `//` コメント | `//` / `/* … */` |
| `TEXTBLOCK` … `ENDTEXTBLOCK` | `"""` で囲む生文字列リテラル |
| `<#CR>` / `<#TAB>` / `<#DBL>` | `"\r\n"` / `"\t"` / `"\""` |
| `"C:\temp"` (逆スラッシュそのまま) | `@"C:\temp"` (`@` を付けないと `\t` がタブになる) |

---

## 3. 出力・ダイアログ・待機

| UWSC | CsWSC | 備考 |
|---|---|---|
| `PRINT 値` | `Print(値)` | |
| `LOGPRINT(表示, x, y, 幅, 高さ)` | `LogPrint(表示, x, y, 幅, 高さ)` | |
| `LOGPRINT` で消去 | `LogClear()` | |
| `SLEEP(秒)` | `Sleep(ミリ秒)` | **単位が違う**。`SLEEP(1)` → `Sleep(1000)` |
| `MSGBOX(メッセージ, ボタン)` | `MessageBox(メッセージ, タイトル, MsgButtons.YesNo, MsgIcon.Question)` | 戻り値は `MsgResult` (`BTN_YES` → `MsgResult.Yes`)。アイコン・タイトル・タイムアウトも指定できる |
| `INPUT(メッセージ, 既定値)` | `InputBox(メッセージ, 既定値)` | キャンセル時、UWSC は `EMPTY`、CsWSC は `""` |
| `SLCTBOX(種類, 待ち, メッセージ, 項目…)` | `SelectBox(種類, メッセージ, 項目, 待ち)` | 戻り値は 0 始まりの番号 (UWSC はビット値) |
| `SLCTBOX` (複数選択) | `SelectBoxMulti` | 選ばれた番号の配列を返す |
| `POPUPMENU(配列, x, y)` | `PopupMenu(配列, x, y)` | 戻り値は選ばれた**文字列** (UWSC は番号)。キャンセルで `null` |
| `SPEAK(文字列, 非同期, 中断)` | `Speak(文字列, async, interrupt)` | |
| `FUKIDASI(メッセージ, x, y, 向き, サイズ, …)` | `Balloon(メッセージ, x, y, しっぽ, サイズ, …)` | 戻り値の `Balloon` で個別に更新・削除できる |
| `FUKIDASI()` (消す) | `b.Close()` / `BalloonClear()` | 引数なしで消す書き方は無い |
| `FUKIDASI` の再呼び出し (内容・位置の変更) | `b.Update(メッセージ, x, y)` | `b` は `Balloon` の戻り値 |
| `FUKIDASI` の色・フォント指定 | `BalloonDefault.BackColor` / `BalloonDefault.FontSize` など | 引数を省略したときの初期値を変える |

---

## 4. マウス

| UWSC | CsWSC | 備考 |
|---|---|---|
| `MMV(x, y, ms)` | `Move(x, y, ms)` | |
| `BTN(LEFT, CLICK, x, y)` | `Mouse(MouseButton.Left, KeyAction.Click, x, y);` | |
| `BTN(RIGHT, CLICK, x, y)` | `Mouse(MouseButton.Right, KeyAction.Click, x, y);` | |
| `BTN(MIDDLE, CLICK, x, y)` | `Mouse(MouseButton.Middle, KeyAction.Click, x, y);` | |
| `BTN(LEFT, DOWN, x, y)` / `BTN(LEFT, UP, x, y)` | `Mouse(MouseButton.Left, KeyAction.Down, x, y);` / `KeyAction.Up` | |
| `BTN(LEFT, DOWN)` (座標省略) | `Mouse(MouseButton.Left, KeyAction.Down);` | 現在のマウス位置で操作 |
| `BTN(…, x, y, ms)` | `Mouse(…, x, y, ms)` | UWSC と同じく実行**前**に待つ |
| `BTN(WHEEL, 量)` | `Wheel(量)` | 正で下、負で上 |
| `BTN(LEFT, CLICK)` の連続 (ダブルクリック) | `Mouse(MouseButton.Left, KeyAction.Click, x, y); Mouse(MouseButton.Left, KeyAction.Click, x, y);` | |
| `G_MOUSE_X` / `G_MOUSE_Y` | `MousePosition.X` / `MousePosition.Y` | |
| `MOUSEORG(id, MORG_CLIENT)` | `MouseOrigin(w)` | 基準は**クライアント領域**左上。UWSC 既定の `MOUSEORG(id)` (ウィンドウ左上が基準) には対応しない |
| `MOUSEORG(0)` | `MouseOrigin()` | 画面座標に戻す |
| `MOUSEORG(id, MORG_CLIENT)` + `MMV` / `BTN` | `w.Move(x, y)` / `w.Mouse(…, x, y)` / `w.Wheel(量, x, y)` | クライアント領域相対。ウィンドウは前面に出さない |
| `MOUSEORG(id, MORG_DIRECT)` + `BTN(...)` | `w.PostMouse(...)` / `PostMouse(...)` (`MouseOrigin(w)` 指定時) / `w.PostKey` / `w.PostText` | ウィンドウを前面に出さずに送る |
| `MUSCUR()` | `MouseCursor()` | 戻り値は `CursorKind` (`CUR_WAIT` → `CursorKind.Wait`) |
| `PEEKCOLOR(x, y)` | `PeekColor(x, y)` | 戻り値は `Color` (UWSC は BGR の数値) |
| `PEEKCOLOR` (ウィンドウ内) | `w.PeekColor(x, y)` | クライアント領域相対 |
| `POSACC(id, x, y)` | `w.TextAtPoint(x, y)` / `TextAtPoint(x, y)` | |

`MouseOrigin` は戻り値を `using` すると、ブロックを抜けたときに元の基準に戻ります。

```csharp
using (MouseOrigin(w))
{
    Mouse(MouseButton.Left, KeyAction.Click, 10, 20);   // w のクライアント領域基準
}
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);       // 画面座標
```

---

## 5. キーボード

| UWSC | CsWSC | 備考 |
|---|---|---|
| `KBD(VK_RETURN)` | `Keyboard(Keys.Enter)` | |
| `KBD(VK_SHIFT, DOWN)` / `KBD(VK_SHIFT, UP)` | `Keyboard(Keys.ShiftKey, KeyAction.Down)` / `KeyAction.Up` | |
| `KBD(キー, 状態, ms)` | `Keyboard(キー, 状態, ms)` | |
| `KBD(VK_CTRL, DOWN); KBD(VK_S); KBD(VK_CTRL, UP)` | `ShortcutKey(Keys.ControlKey, Keys.S)` | 同時押しを 1 行で書ける |
| `KBD` / `SENDSTR(0, …)` で文字入力 | `SendText("文字列")` | 日本語も IME を通さず入力 |
| `SCKEY(id, キー…)` | `w.ShortcutKey(キー…)` | |
| `GETKEYSTATE(VK_xxx)` | `GetKeyState(Keys.xxx)` | |
| `GETKEYSTATE(TGL_CAPSLOCK)` など | `GetToggleState(Keys.CapsLock)` | |
| `LOCKHARD(TRUE)` / `LOCKHARD(FALSE)` | `LockHard(true)` / `LockHard(false)` | |
| `LOCKHARDEX(id, モード)` | `LockHardEx(w, LockMode.xxx)` | |

### 仮想キーの対応

| UWSC | CsWSC | UWSC | CsWSC |
|---|---|---|---|
| `VK_RETURN` | `Keys.Enter` | `VK_ESC` | `Keys.Escape` |
| `VK_TAB` | `Keys.Tab` | `VK_SPACE` | `Keys.Space` |
| `VK_BACK` | `Keys.Back` | `VK_DELETE` | `Keys.Delete` |
| `VK_SHIFT` | `Keys.ShiftKey` | `VK_CTRL` | `Keys.ControlKey` |
| `VK_ALT` | `Keys.Menu` | `VK_WIN` | `Keys.LWin` |
| `VK_UP` / `VK_DOWN` | `Keys.Up` / `Keys.Down` | `VK_LEFT` / `VK_RIGHT` | `Keys.Left` / `Keys.Right` |
| `VK_HOME` / `VK_END` | `Keys.Home` / `Keys.End` | `VK_PGUP` / `VK_PGDN` | `Keys.PageUp` / `Keys.PageDown` |
| `VK_F1`〜 | `Keys.F1`〜 | `VK_A`〜`VK_Z` | `Keys.A`〜`Keys.Z` |
| `VK_0`〜`VK_9` | `Keys.D0`〜`Keys.D9` | `VK_NUMPAD0`〜 | `Keys.NumPad0`〜 |

---

## 6. ウィンドウの取得

| UWSC | CsWSC | 備考 |
|---|---|---|
| `GETID(タイトル)` | `GetWindow(タイトル)` | 見つからなければ `null` (UWSC は `-1`) |
| `GETID(タイトル, クラス, 待ち秒)` | `GetWindow(タイトル, クラス, 待ちミリ秒)` | 既定は待たない (0) |
| `GETID(タイトル, , -1)` | `GetWindow(タイトル, timeoutMs: -1)` | 見つかるまで待つ |
| — | `WaitWindow(タイトル)` | 既定で 2000 ミリ秒 (2 秒) 待つ `GetWindow` |
| `GETID(GET_ACTIVE_WIN)` | `ActiveWindow()` | |
| `GETID(GET_FROMPOINT_WIN)` | `WindowUnderMouse()` / `WindowFromPoint(x, y)` | |
| `GETID(GET_FROMPOINT_OBJ)` | `ControlUnderMouse()` / `ControlFromPoint(x, y)` | |
| `GETALLWIN()` + `ALL_WIN_ID[]` | `GetAllWindows()` | 配列で返す |
| `HNDTOID(hwnd)` | `WindowFromHandle(hwnd)` | |
| `IDTOHND(id)` | `w.Handle` | |

UWSC の `GETID` はタイトルの部分一致です。CsWSC の `GetWindow` も部分一致で探します。

---

## 7. ウィンドウの状態 (STATUS)

`STATUS(id, ST_xxx)` は `Window` のプロパティになります。

| UWSC | CsWSC |
|---|---|
| `ST_TITLE` | `w.Title` |
| `ST_CLASS` | `w.ClassName` |
| `ST_X` / `ST_Y` / `ST_WIDTH` / `ST_HEIGHT` | `w.X` / `w.Y` / `w.Width` / `w.Height` (まとめて `w.Bounds`) |
| `ST_CLX` / `ST_CLY` / `ST_CLWIDTH` / `ST_CLHEIGHT` | `w.ClientBounds` |
| `ST_PROCESS` | `w.ProcessId` |
| `ST_PATH` | `w.ProcessPath` |
| `ST_ISID` | `w.Exists` |
| `ST_VISIBLE` | `w.Visible` |
| `ST_ACTIVE` | `w.IsActive` |
| `ST_ICONIC` | `w.Minimized` |
| `ST_MAXIMIZED` | `w.Maximized` |
| `ST_BUSY` | `w.Busy` |
| `ST_TOPMOST` | `w.Topmost` |
| — | `w.Text` `w.ProcessName` `w.Enabled` |

---

## 8. ウィンドウの制御 (ACW / CTRLWIN)

| UWSC | CsWSC |
|---|---|
| `ACW(id)` | `w.Activate()` |
| `ACW(id, x, y)` | `w.MoveWindow(x, y)` |
| `ACW(id, x, y, 幅, 高さ)` | `w.MoveWindow(x, y, 幅, 高さ)` |
| — | `w.Resize(幅, 高さ)` |
| `CTRLWIN(id, CLOSE)` | `w.Close()` |
| `CTRLWIN(id, CLOSE2)` | `w.Kill()` |
| `CTRLWIN(id, MIN)` | `w.Minimize()` |
| `CTRLWIN(id, MAX)` | `w.Maximize()` |
| `CTRLWIN(id, NORMAL)` | `w.Restore()` |
| `CTRLWIN(id, HIDE)` / `CTRLWIN(id, SHOW)` | `w.Hide()` / `w.Show()` |
| `CTRLWIN(id, TOPMOST)` / `CTRLWIN(id, NOTOPMOST)` | `w.SetTopmost()` / `w.SetTopmost(false)` |
| — | `w.SetEnabled(bool)` |
| `REPEAT; SLEEP(0.1); UNTIL STATUS(id, ST_ISID) = FALSE` | `w.WaitClose()` |
| `REPEAT; SLEEP(0.1); UNTIL STATUS(id, ST_ACTIVE)` | `w.WaitActive()` |

制御メソッドは `Window` を返すので、続けて書けます (`Close` / `Kill` と `WaitClose` / `WaitActive` などの Wait 系を除く)。

```csharp
GetWindow("メモ帳")?.Activate().MoveWindow(0, 0, 800, 600);
```

---

## 9. コントロール操作

CsWSC は UI Automation で操作するので、Win32 以外 (WPF・UWP・ブラウザなど) でも同じ書き方で使えます。

| UWSC | CsWSC | 備考 |
|---|---|---|
| `CLKITEM(id, 名前)` | `w.ClickItem(名前)` | 見つからなければ待ってから `false` |
| `CLKITEM(id, 名前, CLK_BTN)` など種類指定 | `w.ClickItem(名前)` | 種類の指定は不要 |
| `CHKBTN(id, 名前)` | `w.CheckButton(名前)` | 戻り値も同じ (-1 は見つからないか無効) |
| `GETITEM(id, 種類)` + `ALL_ITEM_LIST[]` | `w.GetItem(ItemKind.xxx)` | 配列で返す |
| `GETSLCTLST(id, n)` | `w.GetSelectedItems(n)` | |
| `GETSLIDER(id, n, SLD_POS)` など | `w.GetSlider(n)` | `Value` `Minimum` `Maximum` などをまとめて返す |
| `SETSLIDER(id, 値, n)` | `w.SetSlider(値, n)` | |
| `GETSTR(id, n)` | `w.GetText(n)` | Win32 のエディットのみ |
| `SENDSTR(id, 文字列, n, 追記)` | `w.SetText(文字列, n, 追記)` | Win32 のエディットのみ |
| `GETCTLHND(id, 名前)` | `w.FindChild(名前)` | `Control` を返す |
| — | `w.GetChildren()` | 子孫コントロール (`Control`) の一覧 |
| `SENDSTR(コントロールの id, 文字列, 0, 追記)` | `c.SetText(文字列, 追記)` | `c` は `Control` |
| `STATUS(コントロールの id, ST_CLASS)` など | `c.ClassName` `c.Text` `c.Bounds` など | |
| `ACW(コントロールの id, x, y, 幅, 高さ)` | `c.MoveControl(x, y, 幅, 高さ)` / `c.Resize(幅, 高さ)` | |
| `CTRLWIN(コントロールの id, HIDE)` / `SHOW` | `c.Hide()` / `c.Show()` | |
| — | `c.SetEnabled(bool)` | |

コントロールの ID (`GETID(GET_FROMPOINT_OBJ)`)・ハンドル (`GETCTLHND` の戻り値) は `Control` オブジェクトになります。`Window` と同じく情報の取得・`ClickItem` などの UI Automation・画像検索が使え、座標はそのコントロールが基準です。

### ItemKind の対応

| UWSC | CsWSC |
|---|---|
| `ITM_BTN` | `ItemKind.Buttons` |
| `ITM_LIST` | `ItemKind.ListItem` |
| `ITM_TAB` | `ItemKind.Tab` |
| `ITM_MENU` | `ItemKind.Menu` |
| `ITM_TREEVIEW` | `ItemKind.TreeItem` |
| `ITM_LISTVIEW` | `ItemKind.DataItem` |
| `ITM_EDIT` | `ItemKind.Edit` |
| `ITM_STATIC` | `ItemKind.Text` |
| `ITM_LINK` | `ItemKind.Link` |

---

## 10. 画像

| UWSC | CsWSC | 備考 |
|---|---|---|
| `CHKIMG(ファイル, 透過, x1, y1, x2, y2)` | `CheckImage(ファイル, new Rectangle(x1, y1, x2 - x1, y2 - y1))` | 範囲は**幅・高さ**で指定。透過・色幅は下の行を参照 |
| 戻り値 + `G_IMG_X` / `G_IMG_Y` | 戻り値の `Point?` (左上)。無ければ `null` | |
| `CHKIMG(ファイル, 1〜4, …)` (透過色) | `CheckImage(ファイル, transColor: TransColor.TopLeft)` | 1=`TopLeft` 2=`TopRight` 3=`BottomLeft` 4=`BottomRight` |
| `CHKIMG(ファイル, -1, …)` (色無視) | `CheckImage(ファイル, ImageMatch.Shape())` | 色の対応が 1 対 1 なら一致 |
| `IMG_MSK_*` (色幅) | `CheckImage(ファイル, tolerance: 許容誤差)` | RGB 共通の許容差のみ |
| — | `CheckImage(ファイル, 範囲, ImageMatch.Gray())` / `ImageMatch.Edge()` | 明るさ・輪郭の相関で探す (Color より遅い) |
| `CHKIMG` (ウィンドウ内) | `w.CheckImage(…)` | クライアント領域相対で返す |
| `CHKIMG` の全件 (`ALL_IMG_X[]` / `ALL_IMG_Y[]`) | `CheckImages(…)` | 見つかった全ての左上を `Point[]` で返す |
| — | `CheckColor(色, 範囲, 許容誤差)` / `CheckColors(…)` | 指定の色のピクセルを探す |
| — | `Capture([範囲])` / `Capture(画像ファイル)` | 一度取り込んだ画面 (または画像) の中で画像や色を何度も調べる |
| `SAVEIMG(ファイル, id)` | `w.SaveImage(ファイル)` | |
| `SAVEIMG(ファイル, 0, x, y, 幅, 高さ)` | `SaveImage(ファイル, new Rectangle(x, y, 幅, 高さ))` | 形式は拡張子で決まる |
| — | `ShowImageArea()` | 探している範囲を枠で表示 |

```
// UWSC
IFB CHKIMG("ok.bmp") THEN
    BTN(LEFT, CLICK, G_IMG_X + 5, G_IMG_Y + 5)
ENDIF
```

```csharp
// CsWSC
if (CheckImage(@"ok.png") is Point p) Mouse(MouseButton.Left, KeyAction.Click, p.X + 5, p.Y + 5);
```

---

## 11. スレッド・プロセス・COM・クリップボード

| UWSC | CsWSC | 備考 |
|---|---|---|
| `THREAD 関数()` | `Thread(() => 関数())` | `MOUSEORG` (`MouseOrigin`) の設定は引き継がない (UWSC と同じ) |
| `EXEC(ファイル)` | `Exec(ファイル, 引数)` | 戻り値は `Window?` |
| `EXEC(ファイル, TRUE)` | `ExecWait(ファイル, 引数)` | 終了コードを返す |
| `DOSCMD(コマンド)` | `Cmd(コマンド)` | 出力を返す |
| `POWERSHELL(コマンド)` | `PowerShell(コマンド)` | `useCore: true` で pwsh |
| `CREATEOLEOBJ(ProgID)` | `CreateOleObject(ProgID)` | 戻り値は `dynamic` |
| `GETACTIVEOLEOBJ(ProgID)` | `GetActiveOleObject(ProgID)` | |
| `GETSTR(0)` | `GetClipboard()` | |
| `SENDSTR(0, 文字列)` | `SetClipboard(文字列)` | |

COM の例:

```
// UWSC
excel = CREATEOLEOBJ("Excel.Application")
excel.Visible = TRUE
excel.Workbooks.Add()
excel.Cells(1, 1).Value = "Hello"
```

```csharp
// CsWSC
var excel = CreateOleObject("Excel.Application");
excel.Visible = true;
excel.Workbooks.Add();
excel.Cells(1, 1).Value = "Hello";
```

---

## 12. 組み込み関数の代わりに .NET を使うもの

### 文字列

| UWSC | C# / .NET | 備考 |
|---|---|---|
| `LENGTH(s)` | `s.Length` | |
| `LENGTHB(s)` | `Encoding.GetEncoding("shift_jis").GetByteCount(s)` | |
| `COPY(s, 開始, 文字数)` | `s.Substring(開始 - 1, 文字数)` | 0 始まりに直す |
| `POS(探す, s)` | `s.IndexOf(探す) + 1` | 見つからないと `-1` (UWSC は `0`) |
| `REPLACE(s, 前, 後)` | `s.Replace(前, 後)` | |
| `TRIM(s)` | `s.Trim()` | |
| `CHGMOJ(s, STR_UPPER)` / `STR_LOWER` | `s.ToUpper()` / `s.ToLower()` | |
| `BETWEENSTR(s, 前, 後)` | `System.Text.RegularExpressions.Regex.Match(s, "前(.*?)後").Groups[1].Value` | |
| `SPLIT(s, 区切り)` | `s.Split(区切り)` | |
| `JOIN(配列, 区切り)` | `string.Join(区切り, 配列)` | |
| `STRCONV` | `Microsoft.VisualBasic.Strings.StrConv` | 必要なら `#r "Microsoft.VisualBasic"` |
| `ASC(s)` / `CHR(n)` | `(int)s[0]` / `((char)n).ToString()` | |
| `REGEXP` / `TESTREGEXP` | `Regex.Replace` / `Regex.IsMatch` | |

### 数値・変換

| UWSC | C# / .NET |
|---|---|
| `VAL(s)` | `double.Parse(s)` / `int.TryParse(s, out var n)` |
| `FORMAT(n, 桁)` | `n.ToString("D桁")` / `$"{n:D3}"` |
| `FORMAT(n, 0, 小数桁)` | `n.ToString("F2")` |
| `RANDOM(n)` | `Random.Shared.Next(n)` |
| `INT(x)` / `ROUND(x)` | `Math.Truncate(x)` / `Math.Round(x)` |
| `ABS` / `SQRT` / `POWER` | `Math.Abs` / `Math.Sqrt` / `Math.Pow` |

### 日時

| UWSC | C# / .NET |
|---|---|
| `GETTIME()` + `G_TIME_YY` など | `DateTime.Now` (`.Year` `.Month` `.Day` `.Hour` …) |
| `GETTIME(日数)` | `DateTime.Now.AddDays(日数)` |
| `G_TIME_WW` | `DateTime.Now.DayOfWeek` |
| `GETTICKCOUNT` / 経過時間の計測 | `Stopwatch.StartNew()` (`System.Diagnostics`) / `Environment.TickCount64` |

### ファイル

| UWSC | C# / .NET |
|---|---|
| `FOPEN` + `FGET` (全体) | `File.ReadAllText(パス)` |
| `FOPEN` + `FGET` (行ごと) | `File.ReadAllLines(パス)` |
| `FOPEN` + `FPUT` | `File.WriteAllText(パス, 内容)` / `File.AppendAllText` |
| CSV (`F_READ` + 行・列指定) | `File.ReadAllLines(パス).Select(l => l.Split(','))` |
| `FOPEN(パス, F_EXISTS)` | `File.Exists(パス)` |
| `DELETEFILE` / `COPYFILE` / `MOVEFILE` | `File.Delete` / `File.Copy` / `File.Move` |
| `CREATEFOLDER` / `DELETEFOLDER` | `Directory.CreateDirectory` / `Directory.Delete` |
| `GETDIR(フォルダ, パターン)` | `Directory.GetFiles(フォルダ, パターン)` |
| `READINI` / `WRITEINI` | — (API を `[DllImport]` するか、JSON などに置き換え) |

文字コードの既定は UTF-8 です。Shift_JIS のファイルは `Encoding.GetEncoding("shift_jis")` を渡してください。

### 配列

| UWSC | C# / .NET |
|---|---|
| `SORTARY(arr)` | `Array.Sort(arr)` / `arr.Order().ToArray()` |
| `QSORT(arr, QSRT_D)` | `Array.Sort(arr); Array.Reverse(arr);` / `arr.OrderDescending()` |
| `CALCARRAY(arr, CALC_ADD)` | `arr.Sum()` |
| `CALCARRAY(arr, CALC_MAX)` / `CALC_MIN` / `CALC_AVR` | `arr.Max()` / `arr.Min()` / `arr.Average()` |
| `SETCLEAR(arr, 値)` | `Array.Fill(arr, 値)` |
| `SLICE(arr, 開始, 終了)` | `arr[開始..(終了 + 1)]` |

### 環境

| UWSC | C# / .NET |
|---|---|
| `GET_CUR_DIR` | `Environment.CurrentDirectory` |
| `GET_UWSC_DIR` | `AppContext.BaseDirectory` |
| `GET_WIN_DIR` / `GET_SYS_DIR` | `Environment.GetFolderPath(Environment.SpecialFolder.Windows)` / `.System` |
| `GET_APPDATA_DIR` | `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` |
| `ENVIRON("PATH")` | `Environment.GetEnvironmentVariable("PATH")` |
| `G_SCREEN_W` / `G_SCREEN_H` | `Screen.PrimaryScreen.Bounds.Width` / `.Height` |
| `MONITOR` | `Screen.AllScreens` |
| `SOUND(ファイル)` | `new System.Media.SoundPlayer(ファイル).Play()` |
| `BEEP` | `System.Media.SystemSounds.Beep.Play()` |
| `SHUTDOWNWIN` | `Cmd("shutdown /s /t 0")` |

### Win32 API (DEF_DLL)

```
// UWSC
DEF_DLL MessageBeep(DWORD): BOOL: user32
MessageBeep(0)
```

```csharp
// CsWSC
using System.Runtime.InteropServices;

MessageBeep(0);

[DllImport("user32.dll")]
static extern bool MessageBeep(uint type);
```

---

## 13. 書き換え例

### メモ帳に入力して保存する

```
// UWSC
EXEC("notepad")
id = GETID("メモ帳", "Notepad", 5)
IFB id = -1 THEN
    MSGBOX("メモ帳が見つかりません")
    EXIT
ENDIF
ACW(id, 0, 0, 800, 600)
SENDSTR(id, "こんにちは")
KBD(VK_CTRL, DOWN); KBD(VK_S); KBD(VK_CTRL, UP)
```

```csharp
// CsWSC
var w = Exec("notepad");
if (w == null)
{
    MessageBox("メモ帳が見つかりません", icon: MsgIcon.Warning);
    return;
}
w.Activate().MoveWindow(0, 0, 800, 600);
w.SendText("こんにちは");
ShortcutKey(Keys.ControlKey, Keys.S);
```

### ダイアログが出るまで待ってボタンを押す

```
// UWSC
id = GETID("確認", "#32770", -1)
CLKITEM(id, "OK", CLK_BTN)
```

```csharp
// CsWSC
var dlg = GetWindow("確認", "#32770", timeoutMs: -1);
dlg.ClickItem("OK");
```

### 繰り返しと中断

```
// UWSC
FOR i = 1 TO 10
    BTN(LEFT, CLICK, 100, 200)
    SLEEP(0.5)
    IF GETKEYSTATE(VK_ESC) THEN BREAK
NEXT
```

```csharp
// CsWSC
for (var i = 1; i <= 10; i++)
{
    Mouse(MouseButton.Left, KeyAction.Click, 100, 200);
    Sleep(500);
    if (GetKeyState(Keys.Escape)) break;
}
```

### 監視スレッド

```
// UWSC
THREAD Watch()
// … メイン処理 …

PROCEDURE Watch()
    WHILE TRUE
        id = GETID("エラー", , 0)
        IF id <> -1 THEN CTRLWIN(id, CLOSE)
        SLEEP(0.5)
    WEND
FEND
```

```csharp
// CsWSC
Thread(() =>
{
    while (true)
    {
        GetWindow("エラー", timeoutMs: 0)?.Close();
        Sleep(500);
    }
});
// … メイン処理 …
```

---

## 14. 対応していないもの

次のものは CsWSC に専用のコマンドがありません。必要なら .NET や Win32 API で代用してください。

| UWSC | 代わりの方法 |
|---|---|
| `IE` 系関数 (`IEGETDATA` `IESETDATA` など) / `BROWSERCONTROL` | Selenium / Playwright などのライブラリを `#r` で参照する |
| `XLOPEN` / `XLGETDATA` / `XLSETDATA` など | `CreateOleObject("Excel.Application")` で COM 操作する |
| `READINI` / `WRITEINI` | `GetPrivateProfileString` などを `[DllImport]` する |
| `SETHOTKEY` | ランチャー (設定メニュー) でスクリプトにグローバルホットキーを割り当てる (スクリプト内からは登録できない) |
| `TASK` (タスクスケジューラ) | Windows のタスクスケジューラから CsWSC を起動する |
| `OPTION` による各種設定 | 一部のみ設定画面 (記録方法・ホットキー・タスクトレイ)。`OPTION LOGPATH` などは対応なし |
| `INPUT` のパスワード入力 | Windows Forms で独自のフォームを作る |
| `MSGBOX` の位置指定 | Windows Forms で独自のフォームを作る |
