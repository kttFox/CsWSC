# CsWSC コマンド説明書

CsWSC のスクリプト (`.csws`) で使える組み込みコマンドの一覧です。
スクリプトは C# で書き、ここに載っている関数はクラス名なしでそのまま呼べます。

- `[引数]` は省略可能な引数です。名前付きで渡すこともできます (例: `GetWindow("メモ帳", timeout: 5)`)。
- 座標はすべて**ピクセル**、時間はすべて**秒** (小数可) です。
- 「UWSC」欄は、UWSC で対応する関数・定数です。

## 目次

1. [基本](#1-基本)
2. [出力・ダイアログ・待機](#2-出力ダイアログ待機)
3. [マウス](#3-マウス)
4. [キーボード](#4-キーボード)
5. [ウィンドウの取得](#5-ウィンドウの取得)
6. [Window オブジェクト](#6-window-オブジェクト)
7. [コントロール操作](#7-コントロール操作)
8. [画像](#8-画像)
9. [スレッド](#9-スレッド)
10. [プロセス・COM](#10-プロセスcom)
11. [クリップボード](#11-クリップボード)
12. [列挙型](#12-列挙型)
13. [UWSC 対応表](#13-uwsc-対応表)

---

## 1. 基本

### 使える名前空間

次の名前空間は最初から `using` されています。

`System` `System.IO` `System.Linq` `System.Text` `System.Collections.Generic`
`System.Threading` `System.Threading.Tasks` `System.Drawing` `System.Windows.Forms` `CsWSC`

### 停止

再生中は、停止ボタン・停止ホットキー (既定 `ALT+F2`)・**Pause キー**で停止できます。
停止要求は、組み込みコマンド (`Sleep` `Click` `Kbd` など) を呼んだ時点で反映されます。

### Cancellation

```csharp
CancellationToken Cancellation
```

停止要求のトークンです。組み込みコマンドを呼ばない長い計算ループの中では、次のように書くと停止できるようになります。

```csharp
while (true)
{
    Cancellation.ThrowIfCancellationRequested();
    // 重い計算
}
```

### スクリプトの終了

途中で終了するには `return;` と書きます。

---

## 2. 出力・ダイアログ・待機

### Print

```csharp
void Print(object? value)
```

PRINT 窓に値を 1 行出力します。UWSC: `PRINT`

```csharp
Print("開始");
Print($"x = {x}, y = {y}");
```

### LogPrint / LogClear

```csharp
void LogPrint(bool show)
void LogPrint(bool show, int x, int y, [int? width], [int? height])
void LogClear()
```

PRINT 窓の表示/非表示を切り替え、位置やサイズを変えます。`false` で隠すと、そのあと `Print` しても窓は自動で表示されません (次のスクリプト実行時に元に戻ります)。`LogClear` は内容を消去します。UWSC: `LOGPRINT`

```csharp
LogPrint(false);                 // PRINT 窓を出さずに実行
LogPrint(true, 0, 0, 400, 300);  // 左上に 400x300 で表示
```

### Sleep

```csharp
void Sleep(double seconds)
```

指定秒数待ちます。待っている間に停止要求があると、すぐに中断します。UWSC: `SLEEP`

```csharp
Sleep(0.5);
```

### ExitExit

```csharp
void ExitExit()
```

スクリプト全体を終了します。関数の中や `Thread` の中から呼んでも、メインスクリプトと全スレッドを停止します。UWSC: `EXITEXIT`

```csharp
if (GetWindow("メモ帳", timeout: 0) == null) ExitExit();
```

### MsgBox

```csharp
DialogResult MsgBox(object? message, [MessageBoxButtons buttons = OK])
```

メッセージボックスを表示し、押されたボタンを返します。UWSC: `MSGBOX`

```csharp
if (MsgBox("続行しますか？", MessageBoxButtons.YesNo) == DialogResult.No) return;
```

### InputBox

```csharp
string InputBox(string prompt, [string defaultValue = ""])
```

入力ダイアログを表示し、入力された文字列を返します。キャンセルされた場合は空文字を返します。UWSC: `INPUT`

```csharp
var name = InputBox("名前を入力", "山田");
```

### SlctBox / SlctBoxMulti

```csharp
int SlctBox(string message, params string[] items)
int SlctBox(SlctKind kind, string message, string[] items, [double timeout = 0])
int[] SlctBoxMulti(string message, string[] items, [bool list = false], [double timeout = 0])
```

選択ダイアログを出し、選ばれた項目の番号 (0 から) を返します。キャンセルやタイムアウトのときは `-1` (`SlctBoxMulti` は空の配列) です。
`kind` は `Button` (項目ごとのボタン、既定)・`Radio`・`Combo`・`List` です。`SlctBoxMulti` は複数選択で、既定はチェックボックス、`list: true` で複数選択できるリストボックスになります。
`timeout` 秒たつと自動で閉じます (0 以下で無制限)。UWSC: `SLCTBOX`

```csharp
var i = SlctBox("どれを実行しますか？", "集計", "印刷", "終了");
string[] items = ["売上", "在庫", "顧客"];
foreach (var n in SlctBoxMulti("出力する帳票", items)) Print(items[n]);
```

### PopupMenu

```csharp
string? PopupMenu(string[] items, [Point? point])
```

ポップアップメニューを出し、選ばれた項目の文字列を返します。キャンセルのときは `null` です。`point` を省略するとマウスの位置に出します (`MouseOrg` の影響を受けます)。
`"-"` は区切り線になります。先頭に `"<<"` を付けた項目は直前の項目のサブメニューになり、`"<<<<"` で 2 段下になります。戻り値に `"<<"` は含みません。UWSC: `POPUPMENU`

```csharp
var s = PopupMenu(["開く", "保存", "-", "エクスポート", "<<CSV", "<<Excel", "-", "終了"]);
if (s == "CSV") { /* ... */ }
```

### Speak

```csharp
void Speak(string text, [bool async = false], [bool interrupt = false], [int rate = 0], [int volume = 100])
```

Windows の音声合成 (SAPI) で読み上げます。既定では読み終わるまで待ちます。`async: true` にすると待たずに戻り、`interrupt: true` にすると読み上げ中の音声を止めてから読みます。
`rate` は速さ (-10〜10)、`volume` は音量 (0〜100) です。UWSC: `SPEAK`

```csharp
Speak("処理が完了しました");
Speak("", interrupt: true); // 読み上げを止める
```

### Fukidasi

```csharp
Balloon Fukidasi(string message, [int? x], [int? y], [BalloonTail tail = TopLeft],
                 [float fontSize = 10], [Color? backColor], [Color? foreColor])
void FukidasiClear()
```

吹き出しを表示し、その吹き出し (`Balloon`) を返します。UWSC: `FUKIDASI`

- 呼ぶたびに新しい吹き出しが増えます。複数を同時に表示できます。
- `x`, `y` を省略すると、マウスの位置に表示します。しっぽの先が `(x, y)` を指します。
- 既定の色は、背景が薄い黄色、文字が黒です。
- 前面に出ずフォーカスを奪わないので、操作中のウィンドウに影響しません。
- 返り値の `Balloon` で操作します。
  - `Update([message], [x], [y])`: 文字列や位置を変えます。省略した値はそのままです。
  - `Close()` / `Dispose()`: 消します。`using` で囲むと、ブロックを抜けたときに消えます。
  - `IsClosed`, `Message`, `Target`: 状態を取得します。
- `FukidasiClear()` で、表示中の吹き出しをすべて消します。
- スクリプトが終わると自動で消えます。
- UWSC の「引数なしで呼ぶと消す」には対応していません。`Close()` か `FukidasiClear()` を使ってください。

```csharp
var b = Fukidasi("処理中です…");
b.Update("あと少し…");
b.Close();

var a = Fukidasi("ここ", 600, 300, BalloonTail.BottomRight, 14, Color.LightBlue);
var c = Fukidasi("そこ", 900, 300);
FukidasiClear();   // 全部消す

using (Fukidasi("この間だけ表示")) { Sleep(2); }
```

---

## 3. マウス

### MouseOrg

```csharp
IDisposable MouseOrg([Window? window = null], [MouseOrigin origin = Window])
```

以降のマウス操作 (`MouseMove` / `Click` / `MousePos` / `PeekColor`) の座標の基準を切り替えます。`ChkImg` / `SaveImg` / `Fukidasi` も対象です。`PosAcc` / `WindowFromPoint` / `Balloon.Update` は常に画面座標です。`MouseOrigin.Window` はウィンドウ左上、`MouseOrigin.Client` はクライアント領域左上が原点です。引数なしで画面座標に戻ります。基準は呼び出しのたびにウィンドウの現在位置から求めます。UWSC: `MOUSEORG`

```csharp
var w = GetWindow("メモ帳");
MouseOrg(w, MouseOrigin.Client);
Click(10, 20);
MouseOrg();

using (MouseOrg(w)) {   // ブロックを抜けると元の基準に戻る
    Click(10, 20);
}
```

### MouseMove

```csharp
void MouseMove(Point point, [int ms = 0])
void MouseMove(int x, int y, [int ms = 0])
```

マウスカーソルを移動します (画面座標。`MouseOrg` 指定時はその基準)。`ms` を指定すると、その時間 (ミリ秒) 待ってから移動します。UWSC: `MMV`

### Click

```csharp
void Click(Point point, [MouseButton button = Left])
void Click(int x, int y, [MouseButton button = Left])
```

指定座標をクリックします (画面座標。`MouseOrg` 指定時はその基準)。UWSC: `BTN(LEFT, CLICK, x, y)`

```csharp
Click(300, 250);
Click(300, 250, MouseButton.Right);
```

### Btn

```csharp
void Btn([MouseButton button = Left], [KeyAction action = Click], [int ms = 0])
```

現在のマウス位置でボタンを操作します。`ms` を指定すると、その時間 (ミリ秒) 待ってから実行します (UWSC と同じく実行**前**の待ち)。`KeyAction.Down` と `KeyAction.Up` を組み合わせるとドラッグできます。UWSC: `BTN`

```csharp
MouseMove(100, 100); Btn(MouseButton.Left, KeyAction.Down);
MouseMove(300, 200); Btn(MouseButton.Left, KeyAction.Up, 500);   // 500ms 待ってから離す
```

### Wheel

```csharp
void Wheel(int notches)
```

ホイールを回します。正の値で下方向、負の値で上方向です。UWSC: `BTN(WHEEL, ...)`

### MousePos

```csharp
Point MousePos
```

現在のマウス座標を返します。UWSC: `G_MOUSE_X` / `G_MOUSE_Y`

### MusCur

```csharp
CursorKind MusCur
```

現在のマウスカーソルの種類を返します ([CursorKind](#cursorkind))。UWSC: `MUSCUR`

```csharp
while (MusCur == CursorKind.Wait) Sleep(0.2);   // 砂時計の間は待つ
```

### PeekColor

```csharp
Color PeekColor(Point point)
Color PeekColor(int x, int y)
```

画面上の指定位置の色を返します。UWSC: `PEEKCOLOR`

```csharp
if (PeekColor(10, 10) == Color.FromArgb(255, 0, 0)) Print("赤");
```

### PosAcc

```csharp
string PosAcc(Point point)
string PosAcc(int x, int y)
```

画面座標 (`MouseOrg` の影響を受けない) にあるコントロールの文字 (名前。名前が無ければ値) を返します。UWSC: `POSACC`

---

## 4. キーボード

キー入力は、**アクティブなウィンドウ**に送られます。

### Kbd

```csharp
void Kbd(Keys key, [KeyAction action = Click], [int ms = 0])
```

キーを 1 つ押します。`ms` を指定すると、その時間 (ミリ秒) 待ってから実行します。UWSC: `KBD(キー, 状態, ms)`

```csharp
Kbd(Keys.Enter);
Kbd(Keys.ShiftKey, KeyAction.Down); Kbd(Keys.Tab); Kbd(Keys.ShiftKey, KeyAction.Up);
```

### Hotkey

```csharp
void Hotkey(params Keys[] keys)
```

複数のキーを同時押しします。修飾キーには `ControlKey` `ShiftKey` `Menu` (Alt) `LWin` を使います。

```csharp
Hotkey(Keys.ControlKey, Keys.S);
Hotkey(Keys.Menu, Keys.F4);
```

### SendText

```csharp
void SendText(string text)
```

文字列を入力します。日本語も IME を通さずに直接入力できます。UWSC: `SENDSTR(0, ...)` / `KBD` での文字入力

### GetKeyState / GetToggleState

```csharp
bool GetKeyState(Keys key)
bool GetToggleState(Keys key)
```

`GetKeyState` はキーやマウスボタンがいま押されているかを返します。マウスボタンは `Keys.LButton` `Keys.RButton` `Keys.MButton` で指定します。
`GetToggleState` は `CapsLock` `NumLock` `Scroll` などのトグルキーがオンかを返します。UWSC: `GETKEYSTATE`

```csharp
while (!GetKeyState(Keys.Escape)) { Click(100, 100); Sleep(1); }
if (GetToggleState(Keys.CapsLock)) Kbd(Keys.CapsLock);
```

### LockHard / LockHardEx

```csharp
void LockHard(bool lockInput)
void LockHardEx([Window? window], [LockMode mode = All])
```

ユーザーのキーボード・マウス入力をロックします。`LockHard(false)` または `LockHardEx(null, LockMode.None)` で解除します。
`LockHardEx` は `mode` (`All` / `Keyboard` / `Mouse`) でロックする入力を選べます。`window` を指定すると、キーボードはそのウィンドウがアクティブなときだけ、マウスはカーソルがそのウィンドウ上にあるときだけロックします。UWSC: `LOCKHARD` / `LOCKHARDEX`

- スクリプトからの入力 (`Click` `Kbd` など) は通ります。
- 非常停止のため、ロック中も **Pause キー** は効きます (設定画面の停止ホットキーは効きません)。
- スクリプトが終了すると自動で解除されます。
- 管理者権限で動いているウィンドウへの入力はロックできません。

```csharp
LockHard(true);
try { /* 邪魔されたくない操作 */ }
finally { LockHard(false); }
```

---

## 5. ウィンドウの取得

### GetWindow

```csharp
Window? GetWindow(string title, [string? className], [double timeout = 1])
```

タイトル (部分一致・大文字小文字を区別しない) でウィンドウを探します。`className` を指定すると、クラス名 (部分一致) でも絞り込みます。
見つからない場合は `timeout` 秒まで待ち、それでも無ければ `null` を返します。`timeout` に負の値を指定すると、見つかるまで待ち続けます。UWSC: `GETID`

```csharp
var w = GetWindow("メモ帳", timeout: 5) ?? throw new Exception("メモ帳がありません");
```

### WaitWindow

```csharp
Window? WaitWindow(string title, [string? className], [double timeout = 10])
```

`GetWindow` と同じですが、待ち時間の既定値が 10 秒です。

### ActiveWindow

```csharp
Window ActiveWindow
```

アクティブなウィンドウを返します。UWSC: `GETID(GET_ACTIVE_WIN)`

### GetAllWindows

```csharp
Window[] GetAllWindows([string? title], [string? className])
```

条件に合う表示中のウィンドウをすべて、手前から順に返します。`title` を省略すると、タイトルのあるすべてのウィンドウを返します。UWSC: `GETALLWIN`

```csharp
foreach (var w in GetAllWindows()) Print(w.Title);
```

### WindowFromPoint / WindowUnderMouse / ControlUnderMouse

```csharp
Window? WindowFromPoint(Point point)
Window? WindowFromPoint(int x, int y)
Window? WindowUnderMouse
Window? ControlUnderMouse
```

指定座標のウィンドウ、マウス下のウィンドウ、マウス下のコントロール (子ウィンドウ) を返します。
UWSC: `GETID(GET_FROMPOINT_WIN)` / `GETID(GET_FROMPOINT_OBJ)`

### WindowFromHandle

```csharp
Window? WindowFromHandle(IntPtr handle)
```

ウィンドウハンドル (HWND) から `Window` を取得します。無効なハンドルのときは `null` を返します。逆向きの変換は `w.Handle` です。UWSC: `HNDTOID` / `IDTOHND`

```csharp
var w = WindowFromHandle(hwnd);
IntPtr h = ActiveWindow.Handle;
```

### Monitors

```csharp
Screen[] Monitors
```

接続されているモニタの一覧です。`Bounds` は画面全体、`WorkingArea` はタスクバーを除いた領域 (どちらも画面座標)、`Primary` はメインモニタかどうかを表します。モニタの数は `Monitors.Length` で取れます。UWSC: `MONITOR`

```csharp
foreach (var m in Monitors) Print($"{m.DeviceName} {m.Bounds} primary={m.Primary}");
```

---

## 6. Window オブジェクト

`GetWindow` などで取得したウィンドウです。UWSC のウィンドウ ID に相当します。
操作系のメソッドは `Window` 自身を返すので、メソッドチェーンで続けて書けます。

```csharp
GetWindow("メモ帳")?.Activate().Move(0, 0, 800, 600).SendText("abc");
```

### 情報 (UWSC: `STATUS`)

| プロパティ | 型 | 内容 | UWSC |
|---|---|---|---|
| `Title` | string | タイトル | `ST_TITLE` |
| `ClassName` | string | クラス名 | `ST_CLASS` |
| `Text` | string | ウィンドウのテキスト (エディットなら内容、ボタンなら表示名) | — |
| `Bounds` | Rectangle | 位置とサイズ (画面座標) | — |
| `X` `Y` `Width` `Height` | int | 位置とサイズ | `ST_X` `ST_Y` `ST_WIDTH` `ST_HEIGHT` |
| `ClientBounds` | Rectangle | クライアント領域 (画面座標) | `ST_CLX` `ST_CLY` `ST_CLWIDTH` `ST_CLHEIGHT` |
| `ProcessId` | int | プロセス ID | `ST_PROCESS` |
| `ProcessName` | string | プロセス名 (拡張子なし) | — |
| `ProcessPath` | string | 実行ファイルのパス (取得できなければ空文字) | `ST_PATH` |
| `Parent` | Window? | 親ウィンドウ (トップレベルなら null) | — |
| `Handle` | IntPtr | ウィンドウハンドル | `IDTOHND` |

### 状態 (UWSC: `STATUS`)

| プロパティ | 内容 | UWSC |
|---|---|---|
| `Exists` | ウィンドウが存在するか | `ST_ISID` |
| `Visible` | 表示されているか | `ST_VISIBLE` |
| `IsActive` | アクティブか | `ST_ACTIVE` |
| `Minimized` | 最小化されているか | `ST_ICONIC` |
| `Maximized` | 最大化されているか | `ST_MAXIMIZED` |
| `Enabled` | 入力を受け付けるか | — |
| `Busy` | 応答なしか | `ST_BUSY` |
| `Topmost` | 常に手前か | `ST_TOPMOST` |

### 制御 (UWSC: `ACW` / `CTRLWIN`)

| メソッド | 内容 | UWSC |
|---|---|---|
| `Activate()` | 前面に出してアクティブにする (最小化されていれば元に戻す) | `ACW(id)` |
| `Move(x, y, [w], [h])` | 移動する。サイズを省略すると現在のサイズのまま | `ACW(id, x, y, w, h)` |
| `Resize(w, h)` | サイズだけ変更する | — |
| `Minimize()` `Maximize()` `Restore()` | 最小化・最大化・元のサイズに戻す | `CTRLWIN(id, ICON / MAX / NORMAL)` |
| `Hide()` `Show()` | 非表示にする・表示する | `CTRLWIN(id, HIDE / SHOW)` |
| `SetTopmost([bool])` | 常に手前に表示するかを切り替える | `CTRLWIN(id, TOPMOST / NOTOPMOST)` |
| `SetEnabled([bool])` | 入力の有効・無効を切り替える | — |
| `Close()` | 閉じる (WM_CLOSE を送る) | `CTRLWIN(id, CLOSE)` |
| `Kill()` | プロセスごと強制終了する | `CTRLWIN(id, CLOSE2)` |

### 待機

| メソッド | 内容 |
|---|---|
| `WaitClose([timeout = -1])` | 閉じるまで待つ。時間内に閉じれば true。負の値で無制限 |
| `WaitActive([timeout = 5])` | アクティブになるまで待つ。時間内になれば true |

> 注意: `WaitClose` / `WaitActive` で待っている間は、停止ボタンが効きません。

### 入力 (前面に出して操作)

| メソッド | 内容 | UWSC |
|---|---|---|
| `Click(point, [button])` / `Click(x, y, [button])` | ウィンドウ左上からの相対座標をクリックする | `MOUSEORG` + `BTN` |
| `SendText(text)` | アクティブにしてから文字列を入力する | — |
| `SendKeys(params Keys[])` | アクティブにしてからキーを押す (複数指定で同時押し) | — |
| `ScKey(params Keys[])` | ショートカットキーを実行する (`SendKeys` と同じ) | `SCKEY` |

### 入力 (バックグラウンド)

ウィンドウを前面に出さずにメッセージを送ります。アプリによっては反応しないことがあります。UWSC: `MOUSEORG(id, MORG_DIRECT)`

| メソッド | 内容 |
|---|---|
| `PostClick(point, [button])` / `PostClick(x, y, [button])` | 相対座標にある子ウィンドウへクリックを送る |
| `PostKey(Keys)` | フォーカスのある子ウィンドウへキーを送る |
| `PostText(text)` | フォーカスのある子ウィンドウへ文字列を送る |

---

## 7. コントロール操作

ボタン・リスト・スライダーなど、ウィンドウ内のコントロールを操作します。
特に断りが無いものは **UI Automation** で操作するため、Win32・WPF・UWP などの種類を問わず同じ書き方で使えます。

### ClickItem

```csharp
bool w.ClickItem(string name, [double timeout = 3])
```

名前 (ボタンの表示文字など・完全一致) でコントロールを探して操作します。
ボタンは押し、チェックボックスは切り替え、リスト項目は選択し、ツリーは開閉します。
見つからない場合は `timeout` 秒まで待ち、それでも無ければ `false` を返します。UWSC: `CLKITEM`

```csharp
w.ClickItem("保存(S)");
```

### ChkBtn

```csharp
int w.ChkBtn(string name)
```

ボタン類 (チェックボックス・ラジオボタン・トグル) の状態を返します。UWSC: `CHKBTN`

| 戻り値 | 意味 |
|---|---|
| 1 | オン |
| 0 | オフ |
| 2 | 中間状態 (グレー) |
| -1 | 見つからない |

### GetItem

```csharp
string[] w.GetItem([ItemKind kinds = All])
```

ボタン名やリスト項目などの文字を列挙します。種類は [ItemKind](#itemkind) を `|` で組み合わせて指定します。
エディットとコンボボックスは、入力されている値を返します。UWSC: `GETITEM`

```csharp
foreach (var s in w.GetItem(ItemKind.Button | ItemKind.CheckBox)) Print(s);
```

### GetSlctLst

```csharp
string[] w.GetSlctLst([int index = 1])
```

n 番目 (1 から) のリストボックス・コンボボックス・リストビュー・ツリービューで選択中の項目を返します。UWSC: `GETSLCTLST`

### GetSlider / SetSlider

```csharp
SliderInfo? w.GetSlider([int index = 1])
bool        w.SetSlider(double value, [int index = 1])
```

n 番目 (1 から) のスライダー・スクロールバー・スピンの値を取得・設定します。
`GetSlider` は値・最小値・最大値などを [SliderInfo](#sliderinfo) で返し、見つからなければ `null` を返します。
`SetSlider` は範囲外の値を範囲内に丸めて設定し、成功すれば `true` を返します。UWSC: `GETSLIDER` / `SETSLIDER`

```csharp
var s = w.GetSlider();
if (s != null) w.SetSlider(s.Maximum);
```

### GetStr / SendStr (Win32 コントロール)

```csharp
string w.GetStr([int index = 1], [string className = "Edit"])
bool   w.SendStr(string text, [int index = 1], [bool append = false], [string className = "Edit"])
```

指定したクラス (既定はエディット) の n 番目 (1 から) のコントロールの文字列を取得・設定します。
`SendStr` は `append = true` で末尾に追記し、`index = 0` でフォーカスのあるコントロールに送ります。UWSC: `GETSTR` / `SENDSTR`

```csharp
w.SendStr("検索語");
Print(w.GetStr(2));
```

### Children / FindChild (Win32 コントロール)

```csharp
Window[] w.Children([string? className])
Window?  w.FindChild(string text, [string? className])
```

子ウィンドウ (コントロール) を列挙・検索します。`FindChild` はテキストの部分一致で探します。UWSC: `GETCTLHND`

### PosAcc

```csharp
string w.PosAcc(Point point)
string w.PosAcc(int x, int y)
```

ウィンドウ左上からの相対座標にあるコントロールの文字を返します。UWSC: `POSACC`

---

## 8. 画像

### SaveImg

```csharp
void SaveImg(string path, [Rectangle? area])   // 画面 (省略時は全画面)
void w.SaveImg(string path)                    // ウィンドウ部分
```

画面を画像として保存します。形式は拡張子 (`.png` `.jpg` `.bmp` `.gif`) で決まり、それ以外の拡張子は PNG で保存します。UWSC: `SAVEIMG`

```csharp
SaveImg(@"C:\temp\screen.png");
GetWindow("メモ帳")?.SaveImg(@"C:\temp\notepad.png");
```

### ChkImg

```csharp
Point? ChkImg(string imagePath, [int tolerance = 0], [Rectangle? area])   // 画面座標 (MouseOrg 指定時はその基準) で返す
Point? w.ChkImg(string imagePath, [int tolerance = 0])                    // ウィンドウ相対で返す
```

画面上 (またはウィンドウ内) で画像を探し、見つかれば**左上**の座標を、無ければ `null` を返します。UWSC: `CHKIMG`

- `tolerance`: RGB 各成分 (0〜255) の許容差です。0 で完全一致です。
- 画像の透明な部分 (アルファ値が 128 未満) は比較しません。
- 総当たりで探すため、大きな画像を全画面から探すと時間がかかります。`area` で範囲を絞ると速くなります。

```csharp
if (ChkImg(@"C:\img\ok.png", 10) is Point p) Click(p.X + 5, p.Y + 5);
```

---

## 9. スレッド

### Thread

```csharp
void Thread(Action action)
```

処理を別スレッドで並行に実行します。UWSC: `THREAD`

- メインスクリプトが終わると、スレッド内で組み込みコマンドを呼んだ時点でスレッドも止まります。
- スレッド内でエラーが起きると、スクリプト全体が停止してエラーを表示します。

```csharp
// エラーダイアログが出たら自動で閉じる監視スレッド
Thread(() =>
{
    while (true)
    {
        GetWindow("エラー", timeout: 0)?.Close();
        Sleep(0.5);
    }
});
```

> 注意: `Thread` が関数名として使われているため、スクリプト内で `Thread.Sleep` などとは書けません。
> 待機には `Sleep(秒)` を使い、型の `Thread` が必要な場合は `System.Threading.Thread` と書いてください。

---

## 10. プロセス・COM

### Exec

```csharp
Window? Exec(string fileName, [string arguments = ""])
```

プログラムやファイルを起動し、そのメインウィンドウを返します。ウィンドウを取得できなければ `null` を返します。UWSC: `EXEC`

> ストアアプリ版のメモ帳などは、起動したプロセスとウィンドウのプロセスが別になるため `null` になることがあります。
> その場合は `Exec(...) ?? GetWindow("タイトル", timeout: 5)` のように書いてください。

### ExecWait

```csharp
int ExecWait(string fileName, [string arguments = ""])
```

起動したプログラムが終了するまで待ち、終了コードを返します。UWSC: `EXEC(..., TRUE)`

### DosCmd / PowerShell

```csharp
string DosCmd(string command, [bool async = false], [bool show = false])
string PowerShell(string command, [bool async = false], [bool show = false], [bool core = false])
```

コマンドを実行し、標準出力と標準エラー出力をつなげた文字列を返します。`async: true` にすると終了を待たずに戻ります。`show: true` にすると画面を表示しますが、その場合は出力を取得できません。どちらの場合も戻り値は空文字列です。
`DosCmd` は cmd.exe で実行し、出力をコンソールのコードページ (日本語環境では Shift-JIS) で読むので文字化けしません。
`PowerShell` は実行ポリシーを無視し、プロファイルは読み込まずに実行します。`core: true` にすると PowerShell 7 (pwsh.exe) を使います。
待っている間に停止が要求されると、コマンドを強制終了します。UWSC: `DOSCMD` / `POWERSHELL`

```csharp
Print(DosCmd("ipconfig | findstr IPv4"));
Print(PowerShell("Get-Process | Sort-Object CPU -Descending | Select-Object -First 5"));
```

### CreateOleObj / GetActiveOleObj

```csharp
dynamic CreateOleObj(string progId)
dynamic? GetActiveOleObj(string progId)
```

`CreateOleObj` は COM オブジェクトを新しく作ります。`GetActiveOleObj` は起動中のアプリの COM オブジェクトを取得し、起動していなければ `null` を返します。
戻り値は `dynamic` なので、メソッドやプロパティをそのまま呼べます (補完は効きません)。`GetActiveOleObj` は、.NET 5 以降で削除された `Marshal.GetActiveObject` の代わりとして使えます。UWSC: `CREATEOLEOBJ` / `GETACTIVEOLEOBJ`

```csharp
var excel = GetActiveOleObj("Excel.Application") ?? CreateOleObj("Excel.Application");
excel.Visible = true;
Print(excel.ActiveSheet.Range("A1").Value);
```

---

## 11. クリップボード

```csharp
string GetClipboard()
void   SetClipboard(string text)
```

クリップボードの文字列を取得・設定します。`GetClipboard` は、文字列が無ければ空文字を返します。UWSC: `GETSTR(0)` / `SENDSTR(0, ...)`

---

## 12. 列挙型

### MouseButton

`Left` `Right` `Middle`

### KeyAction

`Click` (押して離す) / `Down` (押す) / `Up` (離す)

### Keys

.NET の `System.Windows.Forms.Keys` をそのまま使います。主なキーは次のとおりです。

`Enter` `Escape` `Tab` `Space` `Back` `Delete` `Up` `Down` `Left` `Right` `Home` `End` `PageUp` `PageDown`
`F1`〜`F24` `A`〜`Z` `D0`〜`D9` / 修飾キー: `ControlKey` `ShiftKey` `Menu` (Alt) `LWin`

### BalloonTail

吹き出しのしっぽの位置です: `TopLeft` (既定) `TopRight` `BottomLeft` `BottomRight` `None`

### CursorKind

`Arrow` `IBeam` `Wait` `Cross` `UpArrow` `SizeNWSE` `SizeNESW` `SizeWE` `SizeNS` `SizeAll` `No` `Hand` `AppStarting` `Help`
`Other` (上記以外) / `Hidden` (非表示)

### ItemKind

`Button` `CheckBox` `RadioButton` `Text` `Edit` `ListItem` `ComboBox` `Tab` `Menu` `TreeItem` `DataItem` `Link` `Group`
組み合わせ: `Buttons` (Button | CheckBox | RadioButton) / `All` (すべて)

### SlctKind

`Button` (項目ごとのボタン) / `Radio` (ラジオボタン) / `Combo` (ドロップダウン) / `List` (リストボックス)

### LockMode

`None` (解除) / `Keyboard` / `Mouse` / `All` (キーボードとマウス)

### SliderInfo

`Value` `Minimum` `Maximum` `SmallChange` `LargeChange` (すべて double)

---

## 13. UWSC 対応表

| UWSC | CsWSC |
|---|---|
| `GETID` | `GetWindow` / `WaitWindow` / `ActiveWindow` / `WindowFromPoint` / `ControlUnderMouse` |
| `GETALLWIN` | `GetAllWindows` |
| `HNDTOID` / `IDTOHND` | `WindowFromHandle` / `w.Handle` |
| `MONITOR` | `Monitors` |
| `STATUS` | `Window` のプロパティ (`Title` `IsActive` `Minimized` `Busy` `ClientBounds` …) |
| `ACW` | `w.Activate()` / `w.Move()` |
| `CTRLWIN` | `w.Close()` `w.Kill()` `w.Minimize()` `w.Maximize()` `w.Restore()` `w.Hide()` `w.Show()` `w.SetTopmost()` |
| `CLKITEM` | `w.ClickItem` |
| `CHKBTN` | `w.ChkBtn` |
| `GETITEM` | `w.GetItem` |
| `GETSLCTLST` | `w.GetSlctLst` |
| `GETSLIDER` / `SETSLIDER` | `w.GetSlider` / `w.SetSlider` |
| `GETSTR` / `SENDSTR` | `w.GetStr` / `w.SendStr` (クリップボードは `GetClipboard` / `SetClipboard`) |
| `GETCTLHND` | `w.FindChild` / `w.Children` |
| `SCKEY` | `w.ScKey` |
| `MOUSEORG` | `MouseOrg` / `w.Click` (相対座標) / `w.PostClick` `w.PostKey` `w.PostText` (直接送信) |
| `POSACC` | `PosAcc` / `w.PosAcc` |
| `PEEKCOLOR` | `PeekColor` |
| `CHKIMG` / `SAVEIMG` | `ChkImg` / `SaveImg` |
| `MUSCUR` | `MusCur` |
| `MMV` / `BTN` | `MouseMove` / `Click` / `Btn` / `Wheel` |
| `KBD` | `Kbd` / `Hotkey` / `SendText` |
| `GETKEYSTATE` | `GetKeyState` / `GetToggleState` |
| `LOCKHARD` / `LOCKHARDEX` | `LockHard` / `LockHardEx` |
| `FUKIDASI` | `Fukidasi` |
| `THREAD` | `Thread` |
| `EXEC` | `Exec` / `ExecWait` |
| `DOSCMD` / `POWERSHELL` | `DosCmd` / `PowerShell` |
| `CREATEOLEOBJ` / `GETACTIVEOLEOBJ` | `CreateOleObj` / `GetActiveOleObj` |
| `SLEEP` / `PRINT` / `MSGBOX` / `INPUT` | `Sleep` / `Print` / `MsgBox` / `InputBox` |
| `LOGPRINT` | `LogPrint` / `LogClear` |
| `SLCTBOX` | `SlctBox` / `SlctBoxMulti` |
| `POPUPMENU` | `PopupMenu` |
| `SPEAK` | `Speak` |
