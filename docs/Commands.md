# CsWSC コマンド説明書

CsWSC のスクリプト (`.csws`) で使える組み込みコマンドの一覧です。  
スクリプトは C# で書き、ここに載っている関数はクラス名なしでそのまま呼べます。

- `[引数]` は省略可能な引数です。  
  名前付きで渡すこともできます (例: `GetWindow("メモ帳", timeoutMs: 2000)`)。
- 座標はすべて**ピクセル**、時間はすべて**ミリ秒** (int) です (UWSC の `SLEEP` などは秒なので注意)。
- 「UWSC」欄は、UWSC で対応する関数・定数です。
- すべての関数・プロパティには、名前を全部大文字にした別名もあります (例: `PRINT("x")`、`w.ACTIVATE()`、`c.CHECKCOLOR(…)`)。  
  `Window`・`Control`・`Capture`・`Balloon`・`BalloonDefault` のメンバーも同じです。  
  代入できるプロパティは別名でも代入できます (例: `BALLOONDEFAULT.FONTSIZE = 14;`)。  
  名前は C# 名を大文字にしたもので、UWSC の関数名とは限りません (`MouseOrigin` は `MOUSEORIGIN`。UWSC の `MOUSEORG` ではない)。  
  補完には出ません。
- 操作の記録で生成されるスクリプトについては [記録の仕様](Recording.md) を参照してください。

## 目次

1. [基本](#1-基本)
2. [出力・ダイアログ・待機](#2-出力ダイアログ待機)
3. [マウス](#3-マウス)
4. [キーボード](#4-キーボード)
5. [ウィンドウの取得](#5-ウィンドウの取得)
6. [Window オブジェクト](#6-window-オブジェクト)
7. [Control オブジェクト](#7-control-オブジェクト)
8. [画像](#8-画像)
9. [Capture オブジェクト](#9-capture-オブジェクト)
10. [スレッド](#10-スレッド)
11. [コントロール操作](#11-コントロール操作)
12. [プロセス・COM](#12-プロセスcom)
13. [クリップボード](#13-クリップボード)
14. [列挙型](#14-列挙型)
15. [UWSC 対応表](#15-uwsc-対応表)

---

## 1. 基本

### 使える名前空間

次の名前空間は最初から `using` されています。

`System` `System.IO` `System.Linq` `System.Text` `System.Collections.Generic`  
`System.Threading` `System.Threading.Tasks` `System.Drawing` `System.Windows.Forms` `CsWSC`

### アセンブリの参照 (#r)

スクリプトの先頭に `#r` を書くと、アセンブリを追加で参照できます。  
相対パスはスクリプトファイルのフォルダが基準です (未保存のときは CsWSC.exe のフォルダ)。

```csharp
#r "Libs\MyLib.dll"		// スクリプトからの相対パス
#r "C:\Libs\Other.dll"	// 絶対パス
#r "System.Data"		// .NET のアセンブリは名前だけで指定できる
```

WPF の `System.Windows.Input` は `#r` なしで使えますが、`using` は既定でされていません。  
`using System.Windows.Input;` と書くと、組み込みの `Keyboard` などと名前がぶつかってエラーになるので、別名で `using` してください。

```csharp
using Input = System.Windows.Input;

if (Input.Keyboard.IsKeyDown(Input.Key.LeftShift)) Print("Shift");
```

読み込んだ DLL は CsWSC を終了するまで解放されません。

### 停止

再生中は、停止ボタン・停止ホットキー (既定 `ALT+F2`)・**Pause キー**で停止できます。  
停止要求は、組み込みコマンド (`Sleep` `Mouse` `Keyboard` など) や `Window` / `Control` などのメソッドを呼んだ時点と、ループ (`while` `for` `foreach` `do`) の 1 周ごとに反映されます。  
ただし、後片付けに使うもの (`Close` `Kill` `BalloonClear` `LockHard(false)` など) は停止要求を確認しないので、`finally` の中でも呼べます。  
それでも 1 秒以内に止まらない場合 (.NET のメソッド (`Thread.Sleep` や `System.Windows.Forms.MessageBox.Show` など) の中で待ち続けているときなど) は、スクリプトを実行しているプロセスごと強制終了します。  
強制終了された場合、`finally` などの後始末は実行されません。

### Cancellation

```csharp
CancellationToken Cancellation
```

停止要求のトークンです。  
ループは自動で停止を確認しますが、ループ以外の長い処理の途中で止めたいときは、次のように書きます。

```csharp
while (true)
{
    Cancellation.ThrowIfCancellationRequested();
    // 重い計算
}
```

### ScriptName

```csharp
string ScriptName
```

実行中のスクリプトのファイル名 (フォルダーを除く) です。UWSC の `GET_UWSC_NAME` に相当します。  
`InputBox` のタイトルにも表示されます。

```csharp
Print(ScriptName); // main.csws
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
void LogPrint(bool visible)
void LogPrint(bool visible, Point point, [int? width], [int? height])
void LogPrint(bool visible, int x, int y, [int? width], [int? height])
void LogClear()
```

PRINT 窓の表示/非表示を切り替え、位置やサイズを変えます。  
`false` で隠すと、そのあと `Print` しても窓は自動で表示されません (次のスクリプト実行時に元に戻ります)。  
位置は画面座標で、`MouseOrigin` 指定時はその基準です。  
`LogClear` は内容を消去します。UWSC: `LOGPRINT`

```csharp
LogPrint(false);				// PRINT 窓を出さずに実行
LogPrint(true, 0, 0, 400, 300);	// 左上に 400x300 で表示
```

### Sleep

```csharp
void Sleep(int ms)
```

指定ミリ秒数待ちます (1000 で 1 秒)。  
待っている間に停止要求があると、すぐに中断します。UWSC: `SLEEP`

```csharp
Sleep(500);
```

### ExitScript

```csharp
void ExitScript()
```

スクリプト全体を終了します。  
関数の中や `Thread` の中から呼んでも、メインスクリプトと全スレッドを停止します。UWSC: `EXITEXIT`

```csharp
if (GetWindow("メモ帳") == null) ExitScript();
```

### MessageBox

```csharp
MsgResult MessageBox(string message, [string? title = null], [MsgButtons buttons = OK], [MsgIcon icon = None], [int timeoutMs = 0])
```

メッセージボックスを出し、押されたボタンを `MsgResult` で返します。  
常に最前面に表示されます。UWSC: `MSGBOX`

- `buttons`: `OK`・`OKCancel`・`YesNo`・`YesNoCancel`・`RetryCancel`・`AbortRetryIgnore`
- `icon`: `None`・`Information`・`Warning`・`Error`・`Question` (アイコンに合わせた通知音も鳴ります)
- 戻り値: `OK`・`Cancel`・`Yes`・`No`・`Retry`・`Abort`・`Ignore`・`Timeout`。  
  × / Esc で閉じると `Cancel` (OK のみのときは `OK`) です。  
  `YesNo` と `AbortRetryIgnore` は × で閉じられません。
- `timeoutMs` ミリ秒たつと自動で閉じ、`Timeout` を返します (0 以下で無制限)。

```csharp
MessageBox("完了しました", icon: MsgIcon.Information);
if (MessageBox("続行しますか？", "確認", MsgButtons.YesNo, MsgIcon.Question) != MsgResult.Yes) return;
MessageBox("エラーが発生しました", icon: MsgIcon.Error, title: "集計");
```

Windows Forms のメッセージボックスは `MessageBox` がこのメソッドと名前が重なるため `System.Windows.Forms.MessageBox.Show` と完全修飾名で呼ぶ必要があります。  
最前面にならず対象ウィンドウの後ろに隠れることがあるため `MessageBox` を推奨します。

### InputBox

```csharp
string InputBox(string prompt, [string defaultValue = ""])
```

入力ダイアログを表示し、入力された文字列を返します。  
キャンセルされた場合は空文字を返します。UWSC: `INPUT`

```csharp
var name = InputBox("名前を入力", "山田");
```

### SelectBox / SelectBoxMulti

```csharp
int SelectBox(string message, params IReadOnlyList<string> items)
int SelectBox(SelectKind kind, string message, IReadOnlyList<string> items, [int timeoutMs = 0])
int[] SelectBoxMulti(string message, IReadOnlyList<string> items, [bool asList = false], [int timeoutMs = 0])
```

選択ダイアログを出し、選ばれた項目の番号 (0 から) を返します。  
キャンセルやタイムアウトのときは `-1` (`SelectBoxMulti` は空の配列) です。  
`kind` は `Button` (項目ごとのボタン、既定)・`Radio`・`Combo`・`List` です。  
`SelectBoxMulti` は複数選択で、既定はチェックボックス、`asList: true` で複数選択できるリストボックスになります。  
`timeoutMs` ミリ秒たつと自動で閉じます (0 以下で無制限)。UWSC: `SLCTBOX`

```csharp
var i = SelectBox("どれを実行しますか？", "集計", "印刷", "終了");
string[] items = ["売上", "在庫", "顧客"];
foreach (var n in SelectBoxMulti("出力する帳票", items)) Print(items[n]);
```

### PopupMenu

```csharp
string? PopupMenu(IReadOnlyList<string> items, [Point? point])
string? PopupMenu(IReadOnlyList<string> items, int x, int y)
```

ポップアップメニューを出し、選ばれた項目の文字列を返します。  
キャンセルのときは `null` です。  
`point` を省略するとマウスの位置に出します (`MouseOrigin` の影響を受けます)。  
`"-"` は区切り線になります。  
先頭に `"<<"` を付けた項目は直前の項目のサブメニューになり、`"<<<<"` で 2 段下になります。  
戻り値に `"<<"` は含みません。UWSC: `POPUPMENU`

```csharp
var s = PopupMenu(["開く", "保存", "-", "エクスポート", "<<CSV", "<<Excel", "-", "終了"]);
if (s == "CSV") { /* ... */ }
```

### Speak

```csharp
void Speak(string text, [bool async = false], [bool interrupt = false], [int rate = 0], [int volume = 100])
```

Windows の音声合成 (SAPI) で読み上げます。  
既定では読み終わるまで待ちます。  
`async: true` にすると待たずに戻り、`interrupt: true` にすると読み上げ中の音声を止めてから読みます。  
`rate` は速さ (-10〜10)、`volume` は音量 (0〜100) です。UWSC: `SPEAK`

```csharp
Speak("処理が完了しました");
Speak("", interrupt: true);	// 読み上げを止める
```

### Balloon

```csharp
Balloon Balloon(string message, [Point point], [BalloonTail tail = None],
                 [float? fontSize], [string? fontName], [Color? foreColor],
                 [Color? backColor], [double? opacity])
Balloon Balloon(string message, int x, int y, [BalloonTail tail = None],
                 [float? fontSize], [string? fontName], [Color? foreColor],
                 [Color? backColor], [double? opacity])
void BalloonClear()
BalloonDefaults BalloonDefault { get; }
```

吹き出しを表示し、その吹き出し (`Balloon`) を返します。UWSC: `FUKIDASI` `BALLOON`

- 呼ぶたびに新しい吹き出しが増えます。複数を同時に表示できます。
- 位置を省略すると、左上 (0, 0) に表示します (`MouseOrigin` 指定時はその基準の左上)。しっぽの先が指定位置を指します。
- 既定値 (フォント・色・不透明度) の変更は `BalloonDefault` を使用します。
  - `fontSize` = 10
  - `fontName` = システムのメッセージ用フォント (例: `"メイリオ"`。見つからない場合は代替フォント)
  - `foreColor` = #000000 (黒)
  - `backColor` = #FFFFE1 (薄い黄色)
  - `opacity` = 1.0 (0.0 = 透明 ～ 1.0 = 不透明。範囲外は丸める)
- 前面に出ずフォーカスを奪わないので、操作中のウィンドウに影響しません。
- 返り値の `Balloon` で操作します。
  - `Update([message], [Point? point])` / `Update(message, x, y)`: 文字列や位置を変えます。  
    省略した値はそのままです。
  - `Close()` / `Dispose()`: 消します。  
    `using` で囲むと、ブロックを抜けたときに消えます。
  - `IsClosed`, `Message`, `Target`: 状態を取得します。
  - `Update` の `point` と `Target` は、呼んだ時点の `MouseOrigin` の基準です。
- `BalloonClear()` で、表示中の吹き出しをすべて消します。
- スクリプトが終わると自動で消えます。
- UWSC の「引数なしで呼ぶと消す」には対応していません。  
  `Close()` か `BalloonClear()` を使ってください。

```csharp
var b = Balloon("処理中です…");
b.Update("あと少し…");
b.Close();

var a = Balloon("ここ", new Point(600, 300), BalloonTail.BottomRight, 14, backColor: Color.LightBlue);
var c = Balloon("そこ", 900, 300, opacity: 0.6);	// 半透明
BalloonClear();										// 全部消す

using (Balloon("この間だけ表示")) { Sleep(2000); }

BalloonDefault.FontSize = 14;	// 以降の Balloon の初期値を変える
BalloonDefault.BackColor = Color.LightBlue;
Balloon("14pt・水色");
Balloon("引数が優先", fontSize: 20);
```

---

## 3. マウス

### MouseOrigin

```csharp
IDisposable MouseOrigin([Window? window = null])
```

以降の座標の基準を切り替えます。  
座標を受け取る・返すグローバルのコマンドすべてが対象です。  
原点はウィンドウのクライアント領域左上です。  
引数なしの `MouseOrigin()` で画面座標に戻ります。  
戻り値を破棄すると直前の基準に戻るので、`using` で範囲を限定できます。  
基準は呼び出しのたびにウィンドウの現在位置から求めます。  
基準はスレッドごとに別で、`Thread` で作ったスレッドは画面座標から始まります。スレッド・`async` 関数・`Task.Run` での扱いは [MouseOrigin.md](MouseOrigin.md) を見てください。  
閉じられたウィンドウを指定したときや、基準のウィンドウが閉じられた後に対象の操作を行うと `InvalidOperationException` になります。UWSC: `MOUSEORG`

```csharp
var w = GetWindow("メモ帳");
MouseOrigin(w);	// クライアント領域基準
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);
MouseOrigin();	// 画面座標に戻す

using (MouseOrigin(w)) {	// ブロック内だけクライアント領域基準
    Mouse(MouseButton.Left, KeyAction.Click, 10, 20);
}
```

### Move

```csharp
void Move(Point point, [int delayMs = 0])
void Move(int x, int y, [int delayMs = 0])
```

マウスカーソルを移動します (画面座標。`MouseOrigin` 指定時はその基準)。  
`delayMs` を指定すると、その時間 (ミリ秒) 待ってから移動します。UWSC: `MMV`

### Mouse

```csharp
void    Mouse(MouseButton button, KeyAction action, [Point? point = null], [int delayMs = 0])
void    Mouse(MouseButton button, KeyAction action, int x, int y, [int delayMs = 0])
Window  window.Mouse(MouseButton button, KeyAction action, [Point? point = null], [int delayMs = 0])	// ウィンドウのクライアント領域相対
Window  window.Mouse(MouseButton button, KeyAction action, int x, int y, [int delayMs = 0])
Control control.Mouse(MouseButton button, KeyAction action, [Point? point = null], [int delayMs = 0])	// コントロールのクライアント領域相対
Control control.Mouse(MouseButton button, KeyAction action, int x, int y, [int delayMs = 0])
```

指定位置へマウスを移動してからボタンを操作します。  
`point` を省略するか `null` を渡すと現在のマウス位置で操作します。  
`delayMs` を指定すると、その時間 (ミリ秒) 待ってから実行します (UWSC と同じく実行**前**の待ち)。  
`KeyAction.Down` と `KeyAction.Up` を組み合わせるとドラッグできます。UWSC: `BTN`

```csharp
Mouse(MouseButton.Left, KeyAction.Click, 300, 250);		// 左クリック
Mouse(MouseButton.Left, KeyAction.Down, 100, 100);
Mouse(MouseButton.Left, KeyAction.Up, 300, 200, 500);	// 500ms 待ってから移動して離す
Mouse(MouseButton.Right, KeyAction.Click);				// 現在位置で右クリック
```

### PostMouse

```csharp
void    PostMouse(MouseButton button, KeyAction action, [Point? point = null], [int delayMs = 0])
void    PostMouse(MouseButton button, KeyAction action, int x, int y, [int delayMs = 0])
Window  window.PostMouse(MouseButton button, KeyAction action, [Point? point = null], [int delayMs = 0])	// ウィンドウのクライアント領域相対
Window  window.PostMouse(MouseButton button, KeyAction action, int x, int y, [int delayMs = 0])
Control control.PostMouse(MouseButton button, KeyAction action, [Point? point = null], [int delayMs = 0])	// コントロールのクライアント領域相対
Control control.PostMouse(MouseButton button, KeyAction action, int x, int y, [int delayMs = 0])
```

`MouseOrigin(w)` で指定したウィンドウへ、前面に出さずにボタン操作を送ります。  
座標はそのウィンドウのクライアント領域基準で、マウスカーソルは動きません。  
グローバル版は `MouseOrigin(w)` を指定していないと例外になります。`window.PostMouse` / `control.PostMouse` はそのウィンドウ・コントロールへ送るので、`MouseOrigin` は不要です。  
アプリによっては反応しません。UWSC: `MOUSEORG(id, MORG_DIRECT)` + `BTN`

```csharp
using (MouseOrigin(w))
{
    PostMouse(MouseButton.Left, KeyAction.Click, 10, 20);
}
```

### Wheel

```csharp
void    Wheel(int notches, [Point? point = null], [int delayMs = 0])
void    Wheel(int notches, int x, int y, [int delayMs = 0])
Window  window.Wheel(int notches, [Point? point = null], [int delayMs = 0])		// ウィンドウのクライアント領域相対
Window  window.Wheel(int notches, int x, int y, [int delayMs = 0])
Control control.Wheel(int notches, [Point? point = null], [int delayMs = 0])	// コントロールのクライアント領域相対
Control control.Wheel(int notches, int x, int y, [int delayMs = 0])
```

ホイールを回します。  
正の値で下方向、負の値で上方向です。  
座標を指定すると、`delayMs` 待ってからその位置へ移動して回します (省略・`null` なら現在位置)。UWSC: `BTN(WHEEL, ...)`

### MousePosition

```csharp
Point MousePosition
```

現在のマウス座標を返します (画面座標。`MouseOrigin` 指定時はその基準)。UWSC: `G_MOUSE_X` / `G_MOUSE_Y`

### MouseCursor

```csharp
CursorKind MouseCursor()
```

現在のマウスカーソルの種類を返します ([CursorKind](#cursorkind))。UWSC: `MUSCUR`

```csharp
while (MouseCursor() == CursorKind.Wait) Sleep(200);	// 砂時計の間は待つ
```

### PeekColor

```csharp
Color PeekColor(Point point)
Color PeekColor(int x, int y)
Color window.PeekColor(Point point)		// ウィンドウのクライアント領域相対
Color window.PeekColor(int x, int y)
Color control.PeekColor(Point point)	// コントロールのクライアント領域相対
Color control.PeekColor(int x, int y)
Color capture.PeekColor(Point point)	// Capture を作ったときの座標系
Color capture.PeekColor(int x, int y)
```

画面上 (またはウィンドウ内・`Capture` 内) の指定位置の色を返します。UWSC: `PEEKCOLOR`

- 画面の外 (`capture.PeekColor` は取り込んだ範囲の外) など、色を取得できない位置を指定すると `ArgumentOutOfRangeException` になります。
- 同じ画面で何か所も調べるときは `Capture` を使うと速くなります。  
  画像ファイルの色は `Capture(path).PeekColor(x, y)` で調べます。

```csharp
if (PeekColor(10, 10) == Color.FromArgb(255, 0, 0)) Print("赤");
var c = Capture(@"C:\img\ok.png").PeekColor(0, 0);	// 画像ファイルの左上の色
```

---

## 4. キーボード

キー入力は、**アクティブなウィンドウ**に送られます。

### Keyboard

```csharp
void Keyboard(Keys key, [KeyAction action = Click], [int delayMs = 0])
```

キーを 1 つ押します。  
`delayMs` を指定すると、その時間 (ミリ秒) 待ってから実行します。UWSC: `KBD(キー, 状態, ms)`

```csharp
Keyboard(Keys.Enter);

Keyboard(Keys.ShiftKey, KeyAction.Down);
Keyboard(Keys.Tab);
Keyboard(Keys.ShiftKey, KeyAction.Up);
```

### ShortcutKey

```csharp
void ShortcutKey(params IEnumerable<Keys> keys)
```

複数のキーを同時押しします。  
修飾キーには `ControlKey` `ShiftKey` `Menu` (Alt) `LWin` を使います。

```csharp
ShortcutKey(Keys.ControlKey, Keys.S);
ShortcutKey(Keys.Menu, Keys.F4);
```

### SendText

```csharp
void SendText(string text)
```

文字列を入力します。  
日本語も IME を通さずに直接入力できます。UWSC: `SENDSTR(0, ...)` / `KBD` での文字入力

### GetKeyState / GetToggleState

```csharp
bool GetKeyState(Keys key)
bool GetToggleState(Keys key)
```

`GetKeyState` はキーやマウスボタンがいま押されているかを返します。  
マウスボタンは `Keys.LButton` `Keys.RButton` `Keys.MButton` で指定します。  
`GetToggleState` は `CapsLock` `NumLock` `Scroll` などのトグルキーがオンかを返します。UWSC: `GETKEYSTATE`

```csharp
while (!GetKeyState(Keys.Escape)) {
	Mouse(MouseButton.Left, KeyAction.Click, 100, 100);
	Sleep(1000);
}
if (GetToggleState(Keys.CapsLock)) Keyboard(Keys.CapsLock);
```

### LockHard / LockHardEx

```csharp
void LockHard(bool lockInput)
void LockHardEx([Window? window], [LockMode mode = All])
```

ユーザーのキーボード・マウス入力をロックします。  
`LockHard(false)` または `LockHardEx(null, LockMode.None)` で解除します。  
`LockHardEx` は `mode` (`All` / `Keyboard` / `Mouse`) でロックする入力を選べます。  
`window` を指定すると、キーボードはそのウィンドウがアクティブなときだけ、マウスはカーソルがそのウィンドウ上にあるときだけロックします。UWSC: `LOCKHARD` / `LOCKHARDEX`

- スクリプトからの入力 (`Mouse` `Keyboard` など) は通ります。
- 非常停止のため、ロック中も **Pause キー** は効きます (設定画面の停止ホットキーは効きません)。
- スクリプトが終了すると自動で解除されます。
- 管理者権限で動いているウィンドウへの入力はロックできません。

```csharp
LockHard(true);
try {
	/* 邪魔されたくない操作 */
} finally { LockHard(false); }
```

---

## 5. ウィンドウの取得

### GetWindow

```csharp
Window? GetWindow(string title, [string? className], [int timeoutMs = 0])
```

タイトル (部分一致・大文字小文字を区別しない) でウィンドウを探します。  
`className` を指定すると、クラス名 (部分一致) でも絞り込みます。  
見つからない場合は `timeoutMs` ミリ秒まで待ち、それでも無ければ `null` を返します。  
`timeoutMs` に負の値を指定すると、見つかるまで待ち続けます。UWSC: `GETID`

```csharp
var w = GetWindow("メモ帳") ?? throw new Exception("メモ帳がありません");
```

### WaitWindow

```csharp
Window? WaitWindow(string title, [string? className], [int timeoutMs = 2000])
```

`GetWindow` と同じですが、待ち時間の既定値が 2000 (2 秒) です。

### ActiveWindow

```csharp
Window ActiveWindow()
```

アクティブなウィンドウを返します。UWSC: `GETID(GET_ACTIVE_WIN)`

### GetAllWindows

```csharp
Window[] GetAllWindows([string? title], [string? className])
```

条件に合う表示中のウィンドウをすべて、手前から順に返します。  
`title` を省略すると、タイトルのあるすべてのウィンドウを返します (`className` を指定したときは、タイトルが空のウィンドウも返します)。UWSC: `GETALLWIN`

```csharp
foreach (var w in GetAllWindows()) Print(w.Title);
```

### WindowFromPoint / ControlFromPoint / WindowUnderMouse / ControlUnderMouse

```csharp
Window? WindowFromPoint(Point point)
Window? WindowFromPoint(int x, int y)
Control? ControlFromPoint(Point point)
Control? ControlFromPoint(int x, int y)
Window?  WindowUnderMouse()
Control? ControlUnderMouse()
```

指定座標 (画面座標。`MouseOrigin` 指定時はその基準) のウィンドウ、マウス下のウィンドウ、マウス下の[コントロール](#7-control-オブジェクト)を返します。  
`ControlFromPoint()` / `ControlUnderMouse()` はコントロールの上でなければ `null` を返します。  
WPF・ブラウザーなど、コントロールが子ウィンドウになっていないアプリでも `null` です。  
UWSC: `GETID(GET_FROMPOINT_WIN)` / `GETID(GET_FROMPOINT_OBJ)`

```csharp
var c = ControlUnderMouse();
if (c != null) Print($"{c.ClassName} {c.Text} ({c.TopWindow?.Title})");
```

### WindowFromHandle

```csharp
Window? WindowFromHandle(IntPtr handle)
```

ウィンドウハンドル (HWND) から `Window` を取得します。  
無効なハンドルのときは `null` を返します。  
逆向きの変換は `w.Handle` です。UWSC: `HNDTOID` / `IDTOHND`

```csharp
var w = WindowFromHandle(hwnd);
IntPtr h = ActiveWindow().Handle;
```

---

## 6. Window オブジェクト

`GetWindow` などで取得したウィンドウです。  
UWSC のウィンドウ ID に相当します。  
操作系のメソッドは `Window` 自身を返すので、メソッドチェーンで続けて書けます (`Close()` / `Kill()` は void、`WaitClose()` / `WaitActive()` は bool を返すので除きます)。  
`==` / `!=` で比べると、同じウィンドウ (ハンドルが同じ) かどうかを判定します (`if (ActiveWindow() == w) …`)。  
取得し直した `Window` どうしでも等しくなります。

```csharp
GetWindow("メモ帳")?.Activate().MoveWindow(0, 0, 800, 600).SendText("abc");
```

閉じられたウィンドウ・コントロールを操作したり情報を読んだりすると `InvalidOperationException` になります。  
例外にならないのは `Exists`・`WaitClose()`・`ToString()`・`==` / `!=` と、状態を表す bool (`Visible` `Enabled` `IsActive` `Minimized` `Maximized` `Busy` `Topmost`。閉じていれば false) です。

### 情報 (UWSC: `STATUS`)

| プロパティ | 型 | 内容 | UWSC |
|---|---|---|---|
| `Title` | string | タイトル | `ST_TITLE` |
| `ClassName` | string | クラス名 | `ST_CLASS` |
| `Text` | string | ウィンドウのテキスト (エディットなら内容、ボタンなら表示名) | — |
| `Bounds` | Rectangle | 位置とサイズ (画面座標)。閉じていれば `InvalidOperationException` | — |
| `X` `Y` `Width` `Height` | int | 位置とサイズ (画面座標。影を除いた見た目の範囲)。閉じていれば `InvalidOperationException` | `ST_X` `ST_Y` `ST_WIDTH` `ST_HEIGHT` |
| `ClientBounds` | Rectangle | クライアント領域 (画面座標)。閉じていれば `InvalidOperationException` | `ST_CLX` `ST_CLY` `ST_CLWIDTH` `ST_CLHEIGHT` |
| `ClientCenter` | Point | クライアント領域の中央 (クライアント領域の左上が基準)。`w.Mouse(MouseButton.Left, KeyAction.Click, w.ClientCenter)` のように使う。閉じていれば `InvalidOperationException` | — |
| `ProcessId` | int | プロセス ID | `ST_PROCESS` |
| `ProcessName` | string | プロセス名 (拡張子なし) | — |
| `ProcessPath` | string | 実行ファイルのパス (取得できなければ空文字) | `ST_PATH` |
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
| `MoveWindow(x, y, [w], [h])` / `MoveWindow(Point point, [w], [h])` | 移動する (画面座標。影を除いた見た目の範囲で指定)。サイズを省略すると現在のサイズのまま | `ACW(id, x, y, w, h)` |
| `Resize(w, h)` | サイズだけ変更する | — |
| `Minimize()` `Maximize()` `Restore()` | 最小化・最大化・元のサイズに戻す | `CTRLWIN(id, MIN / MAX / NORMAL)` |
| `Hide()` `Show()` | 非表示にする・表示する | `CTRLWIN(id, HIDE / SHOW)` |
| `SetTopmost([bool])` | 常に手前に表示するかを切り替える | `CTRLWIN(id, TOPMOST / NOTOPMOST)` |
| `SetEnabled([bool])` | 入力の有効・無効を切り替える | — |
| `Close()` | 閉じる (WM_CLOSE を送る) | `CTRLWIN(id, CLOSE)` |
| `Kill()` | プロセスごと強制終了する | `CTRLWIN(id, CLOSE2)` |

### 待機

| メソッド | 内容 |
|---|---|
| `WaitClose([timeoutMs = -1])` | 閉じるまで待つ。時間内に閉じれば true。負の値で無制限 |
| `WaitActive([timeoutMs = 2000])` | アクティブになるまで待つ。時間内になれば true |

### 入力 (実際のマウス・キー操作)

`Move` / `Mouse` / `Wheel` は座標がクライアント領域基準になるだけで、ウィンドウを前面に出しません。必要なら先に `Activate()` してください。  
前面に出してから送るのは `SendText` / `ShortcutKey` だけです。

| メソッド | 内容 | UWSC |
|---|---|---|
| `Move(Point point, [delayMs])` / `Move(x, y, [delayMs])` | クライアント領域左上からの相対座標へマウスを移動する | `MOUSEORG` + `MMV` |
| `Mouse(button, action, [point], [delayMs])` / `Mouse(button, action, x, y, [delayMs])` | クライアント領域左上からの相対座標でボタンを操作する (座標省略で現在のマウス位置) | `MOUSEORG` + `BTN` |
| `Wheel(notches, [point], [delayMs])` / `Wheel(notches, x, y, [delayMs])` | クライアント領域の相対座標へ移動してホイールを回す (座標省略で現在のマウス位置) | `MOUSEORG` + `BTN(WHEEL)` |
| `SendText(text)` | アクティブにし、なったのを確認してから文字列を入力する。2 秒以内にアクティブにならなければ `InvalidOperationException` | `ACW` + `KBD` |
| `ShortcutKey(params IEnumerable<Keys>)` | アクティブにし、なったのを確認してからキーを押す (複数指定で同時押し)。2 秒以内にアクティブにならなければ `InvalidOperationException` | `SCKEY` |

### 入力 (バックグラウンド)

ウィンドウを前面に出さずにメッセージを送ります。  
アプリによっては反応しないことがあります。UWSC: `MOUSEORG(id, MORG_DIRECT)`

| メソッド | 内容 |
|---|---|
| `PostMouse(button, action, [point], [delayMs])` / `PostMouse(button, action, x, y, [delayMs])` | クライアント領域の相対座標にある子ウィンドウへボタン操作を送る (座標省略で現在のマウス位置) |
| `PostKey(Keys, [delayMs])` | フォーカスのある子ウィンドウへキーを送る |
| `PostText(text, [delayMs])` | フォーカスのある子ウィンドウへ文字列を送る |

---

## 7. Control オブジェクト

ウィンドウの中のボタン・入力欄など (子ウィンドウ) です。  
UWSC の `GETID(GET_FROMPOINT_OBJ)` で得る ID や `GETCTLHND` で得るハンドルに相当します。  
`ControlFromPoint()`・`ControlUnderMouse()`・`w.GetChildren()`・`w.FindChild()` で取得します。  
HWND を持つコントロール (Win32・WinForms など) が対象です。

`Window` と共通のもの (どちらでも使える):

- 情報: `Handle` `ClassName` `Text` `Bounds` `X` `Y` `Width` `Height` `ClientBounds` `ClientCenter` `ProcessId` `Exists` `Visible` `Enabled`
- `WaitClose()`、`GetChildren()` / `FindChild()`
- UI Automation: `ClickItem` `CheckButton` `GetItem` `GetSelectedItems` `GetSlider` `SetSlider` `TextAtPoint` (そのコントロールの中を探す)
- 画像: `PeekColor` `SaveImage` `Capture` `CheckColor(s)` `CheckImage(s)` (座標はそのコントロールのクライアント領域が基準)
- `==` / `!=` (ハンドルが同じなら等しい。`Window` と `Control` の比較も可)

`Control` だけのもの:

| メンバー | 内容 | UWSC |
|---|---|---|
| `Parent` | 親 (`Control` か `Window`。型は `ControlBase?`) | — |
| `TopWindow` | コントロールがあるトップレベルウィンドウ (`Window?`) | — |
| `GetText()` | テキストを取得する (`Text` と同じ) | `GETSTR(id)` |
| `SetText(text, [append = false])` | テキストを設定する。`append = true` で末尾に追記。コントロールが受け付ければ true | `SENDSTR(id, …)` |
| `MoveControl(x, y, [w], [h])` / `MoveControl(Point point, [w], [h])` | 移動する (座標は親のクライアント領域が基準) | — |
| `Resize(w, h)` | サイズだけ変更する | — |
| `Hide()` `Show()` `SetEnabled([bool])` | 非表示・表示・有効/無効の切り替え | — |
| `Move(Point point, [delayMs])` / `Move(x, y, [delayMs])` | マウスを移動する。座標はコントロールのクライアント領域の左上が基準 | `MMV` |
| `Mouse(button, action, [point], [delayMs])` / `Mouse(button, action, x, y, [delayMs])` | ボタンを操作する。座標はコントロールのクライアント領域の左上が基準 (省略で現在のマウス位置) | `BTN` |
| `PostMouse(button, action, [point], [delayMs])` / `PostMouse(button, action, x, y, [delayMs])` | 前面に出さずにボタン操作を送る。座標の基準は `Mouse` と同じ (省略で現在のマウス位置) | — |
| `Wheel(notches, [point], [delayMs])` / `Wheel(notches, x, y, [delayMs])` | 移動してホイールを回す。座標の基準は `Mouse` と同じ (省略で現在のマウス位置) | `BTN(WHEEL)` |
| `PostKey(Keys, [delayMs])` / `PostText(text, [delayMs])` | 前面に出さずに、このコントロールへキー・文字列を送る | — |

```csharp
var edit = GetWindow("メモ帳")?.FindChild("", "Edit");
edit?.SetText("こんにちは");
Print(edit?.Text);
```

スクリプトでは WinForms の `Control` も使えるため、型名を書くと名前があいまいになります。  
`var` で受けるか `CsWSC.Control` と書いてください。

---

## 8. 画像

### SaveImage

```csharp
void SaveImage(string path, [Rectangle? area])			// 画面。省略時は全画面
void window.SaveImage(string path, [Rectangle? area])	// ウィンドウのクライアント領域。area もクライアント領域相対
void control.SaveImage(string path, [Rectangle? area])	// コントロールのクライアント領域。area もクライアント領域相対
```

画面を画像として保存します。  
形式は拡張子 (`.png` `.jpg` `.jpeg` `.gif`) で決まり、それ以外の拡張子は BMP で保存します。UWSC: `SAVEIMG`

```csharp
SaveImage(@"C:\temp\screen.png");
GetWindow("メモ帳")?.SaveImage(@"C:\temp\notepad.png");
GetWindow("メモ帳")?.SaveImage(@"C:\temp\part.png", new Rectangle(0, 0, 200, 100));	// クライアント領域の左上 200x100
```

### CheckImage

```csharp
Point? CheckImage(string imagePath, [Rectangle? area], [int tolerance = 0])				// 色で探す ImageMatch.Color
Point? CheckImage(string imagePath, TransColor transColor, [Rectangle? area], [int tolerance = 0])
Point? CheckImage(string imagePath, ImageMatch match)									// 照合方法を指定して探す
Point? CheckImage(string imagePath, Rectangle area, ImageMatch match)
Point? CheckImage(string imagePath, TransColor transColor, ImageMatch match)
Point? CheckImage(string imagePath, TransColor transColor, Rectangle area, ImageMatch match)
Point? CheckImage(Capture capture, …)	// 探す画像を Capture で渡す。capture 以降は上と同じ 6 形式
Point? window.CheckImage(…)				// ウィンドウのクライアント領域内を探し、クライアント領域相対で返す
Point? control.CheckImage(…)			// コントロールのクライアント領域内を探し、クライアント領域相対で返す
Point? capture.CheckImage(…)			// Capture の中を探し、Capture を作ったときの座標系で返す
```

画面上 (またはウィンドウ内) で画像を探し、見つかれば**左上**の座標を、無ければ `null` を返します。  
画面版は画面座標 (MouseOrigin 指定時はその基準) で返します。UWSC: `CHKIMG`

- `capture`: 探す画像を `Capture` で渡します。  
  画面の一部を取り込んで探す画像にするときは `Capture(rect)` / `w.Capture(rect)`、ファイルなら `Capture(path)` で作ります。  
  パスで渡す版のキャッシュは使いませんが、探す画像の準備 (比べるピクセルの一覧など) は `Capture` の中に保存され、同じ `Capture` を何度渡しても作り直しません。
- `tolerance`: RGB 各成分 (0〜255) の許容差です。  
  0 で完全一致です。  
  `CheckImage(path, area, ImageMatch.Color(tolerance))` と同じです。
- `match`: 照合方法です。  
  以下の 4 つから作ります。

| 照合方法 | 一致する条件 | 速さ |
|---|---|---|
| `ImageMatch.Color([int tolerance = 0], [int max])` | RGB 各成分の差が `tolerance` (0〜255) 以下 | 速い |
| `ImageMatch.Shape([int max])` | 色を無視して形が同じ (UWSC の透過色 `-1`)。画像の各色が画面上の色と 1 対 1 に対応していれば一致 (同じ色の部分は同じ色、違う色の部分は違う色)。色は各成分の下位 3 ビットを無視して比べる | 速い |
| `ImageMatch.Gray([double threshold = 0.95])` | 明るさ (グレースケール) の相関係数が `threshold` (0〜1) 以上。全体の明るさやコントラストが変わっても見つかる。白黒反転は見つからない | やや遅い |
| `ImageMatch.Edge([double threshold = 0.95])` | 輪郭の強さ (Sobel フィルタ) の相関係数が `threshold` 以上。色や明るさが変わったり反転したりしても、輪郭が同じなら見つかる。画像の外周 1 ピクセルは比較しない | 遅い |

- `transColor`: 透過色にする画像の隅です (UWSC の透過 1〜4)。  
  `TopLeft` / `TopRight` / `BottomLeft` / `BottomRight` を指定すると、その隅のピクセルと同じ色 (RGB 完全一致) の部分を比較しません。  
  どの照合方法とも組み合わせられます。
- 画像の透明な部分 (アルファ値が 128 未満) は比較しません。
- 範囲外の値 (`Color(300)`・`Gray(1.5)` など) を指定すると `ArgumentOutOfRangeException` になります。
- 総当たりで探すため、大きな画像を全画面から探すと時間がかかります。  
  `area` で範囲を絞ると速くなります。  
  目安 (1920x1080 の画面から 32x32 の画像を探す場合): `Color` 約 1〜3ms、`Shape` 約 8ms、`Gray` 約 30ms、`Edge` 約 65ms。  
  `Gray` / `Edge` は縮小した画像で候補を探し、倍率を半分ずつ戻しながら (例: 4 倍 → 2 倍 → 元の大きさ) 候補の周りだけを調べます (画像の短い辺が 16 ピクセル以上のとき)。  
  相関の高い位置から順に、重ならない位置を選びます。

```csharp
if (CheckImage(@"C:\img\ok.png", tolerance: 10) is Point p) Mouse(MouseButton.Left, KeyAction.Click, p.X + 5, p.Y + 5);
if (CheckImage(@"C:\img\icon.png", ImageMatch.Shape()) is Point q) Mouse(MouseButton.Left, KeyAction.Click, q);							// 色違いのアイコンも見つける
if (CheckImage(@"C:\img\btn.bmp", transColor: TransColor.TopLeft) is Point r) Mouse(MouseButton.Left, KeyAction.Click, r);						// 左上の背景色を無視
if (CheckImage(@"C:\img\btn.png", new Rectangle(0, 0, 400, 300), ImageMatch.Gray(0.9)) is Point s) Mouse(MouseButton.Left, KeyAction.Click, s);	// 明るさが変わるボタン
```

### CheckImages

```csharp
Point[] CheckImages(string imagePath, [Rectangle? area], [int tolerance = 0], [int max])			// 色で探す ImageMatch.Color
Point[] CheckImages(string imagePath, TransColor transColor, [Rectangle? area], [int tolerance = 0], [int max])
Point[] CheckImages(string imagePath, ImageMatch match)									// 照合方法を指定して探す
Point[] CheckImages(string imagePath, Rectangle area, ImageMatch match)
Point[] CheckImages(string imagePath, TransColor transColor, ImageMatch match)
Point[] CheckImages(string imagePath, TransColor transColor, Rectangle area, ImageMatch match)
Point[] CheckImages(Capture capture, …)		// 探す画像を Capture で渡す。capture 以降は上と同じ 6 形式
Point[] window.CheckImages(…)				// 引数は同じ。ウィンドウのクライアント領域内を探し、クライアント領域相対で返す
Point[] control.CheckImages(…)				// 引数は同じ。コントロールのクライアント領域内を探し、クライアント領域相対で返す
Point[] capture.CheckImages(…)				// 引数は同じ。Capture の中を探し、Capture を作ったときの座標系で返す
```

`CheckImage` と同じ条件で画像を**全て**探し、見つかった左上の座標を配列で返します。  
無ければ空の配列です。UWSC: `CHKIMG` の番号 -1 (`ALL_IMG_X[]` / `ALL_IMG_Y[]`)

- 順序は上から順、同じ行は左から順です。
- 既に見つかった位置と重なる位置は返しません。
- `max`: 返す件数の上限です。  
  上限に達した時点で探すのをやめるので、全件が必要ないときに速くなります。  
  `ImageMatch` を渡すときは `ImageMatch.Color(tolerance, max)` / `ImageMatch.Shape(max)` で指定します (`Gray` / `Edge` は全体を調べるので上限を指定できません)。  
  省略すると全件です。  
  1 未満なら `ArgumentOutOfRangeException` になります。
- 引数の意味は `CheckImage` と同じです。

```csharp
foreach (var p in CheckImages(@"C:\img\checkbox.png", tolerance: 10)) Mouse(MouseButton.Left, KeyAction.Click, p.X + 5, p.Y + 5);
```

### CheckColor

```csharp
Point?  CheckColor(Color color, [Rectangle? area], [int tolerance = 0])						// 最初に見つかった位置。無ければ null
Point[] CheckColors(Color color, [Rectangle? area], [int tolerance = 0], [int max])			// 全て。無ければ空の配列
(Color Color, Point Point)?  CheckColor(Color[] colors, [Rectangle? area], [int tolerance = 0])	// いずれかの色。一致した色と位置
(Color Color, Point Point)[] CheckColors(Color[] colors, [Rectangle? area], [int tolerance = 0], [int max])
Point?  window.CheckColor(…) / Point[] window.CheckColors(…)	// ウィンドウのクライアント領域内を探し、クライアント領域相対で返す
Point?  control.CheckColor(…) / Point[] control.CheckColors(…)	// コントロールのクライアント領域内を探し、クライアント領域相対で返す
Point?  capture.CheckColor(…) / Point[] capture.CheckColors(…)	// Capture の中を探し、Capture を作ったときの座標系で返す
```

画面上 (またはウィンドウ内) で指定の色のピクセルを探します。  
順序は上から順、同じ行は左から順です。

- `tolerance`: RGB 各成分 (0〜255) の許容差です。  
  0 で完全一致です。
- `max`: `CheckColors` で返す件数の上限です。  
  省略すると全件です。
- 複数色版は、どれか 1 色に一致するピクセルを探し、一致した色 (`colors` の要素。実際のピクセルの色ではない) と位置を返します。  
  1 つのピクセルが複数の色に一致するときは、`colors` の先に並ぶ色を返します。
- 呼ぶたびに画面をキャプチャします。  
  同じ画面で何度も調べるときは `Capture` を使ってください。
- 画像ファイルの中を探すときは `Capture(画像ファイルのパス).CheckColor(…)` を使います。

```csharp
if (CheckColor(Color.Red, new Rectangle(0, 0, 400, 300), 10) is Point p) Mouse(MouseButton.Left, KeyAction.Click, p);
if (CheckColor([Color.Red, Color.Lime], tolerance: 10) is { } f) Print($"{f.Color} が {f.Point} にある");
foreach (var (color, point) in CheckColors([Color.Red, Color.Lime])) Print($"{color} {point}");
```

### ShowImageArea

```csharp
void ShowImageArea([bool enabled = true], [Color? color], [int? durationMs])
```

デバッグ用です。  
`SaveImage` / `Capture` / `CheckImage` / `CheckImages` / `CheckColor` / `CheckColors` (Window 版も含む) / `PeekColor` の対象範囲に枠を表示します。  
`CheckImage` / `CheckImages` では見つかった範囲にも枠を表示します。

- `enabled`: `true` で表示します。  
  `false` で表示をやめ、枠を消します。
- `color`: 枠の色です。  
  省略すると前回の指定のままです (初回は赤)。
- `durationMs`: 枠を表示し続けるミリ秒数です。  
  省略すると前回の指定のままです (初回は 1000)。  
  0 以下なら次に対象の操作をするまで表示し続けます。
- 枠は範囲の外側に描かれ、キャプチャの直前に隠すため `SaveImage` / `CheckImage` などの結果に影響しません。
- `PeekColor` では対象の 1 ピクセルを囲む枠になります。

```csharp
ShowImageArea(true);
CheckImage(@"C:\img\ok.png", new Rectangle(100, 100, 400, 300));
```

---

## 9. Capture オブジェクト

```csharp
Capture Capture([Rectangle? area])			// 画面をキャプチャ。MouseOrigin 指定時はその基準
Capture window.Capture([Rectangle? area])	// ウィンドウのクライアント領域をキャプチャ
Capture control.Capture([Rectangle? area])	// コントロールのクライアント領域をキャプチャ
Capture Capture(string imagePath)			// 画像ファイルから作る。画像の左上が基準

// Capture のメンバー
Point?  capture.CheckImage(…)  / Point[] capture.CheckImages(…)							// 引数は CheckImage / CheckImages と同じ
Point?  capture.CheckColor(Color color, [Rectangle? area], [int tolerance = 0])				// 指定の色を探す。最初に見つかった位置
Point[] capture.CheckColors(Color color, [Rectangle? area], [int tolerance = 0], [int max])	// 指定の色のピクセルを全て
(Color, Point)? capture.CheckColor(Color[] colors, …)  
(Color, Point)[] capture.CheckColors(Color[] colors, …)
Color   capture.PeekColor(Point point)
Color   capture.PeekColor(int x, int y)
void    capture.Save(string path)		// ファイルに保存。形式は SaveImage と同じく拡張子で決まる
Bitmap  capture.ToBitmap()
int capture.Width / int capture.Height / Point capture.Origin / Rectangle capture.Bounds
```

画面または、画像を一度だけ取り込み、その中で画像や色を何度でも調べます。  
複数の画像を探すときに毎回キャプチャし直さずに済み、同じ瞬間の画面に対して判定できます。

- 座標は作ったときの座標系 (画面座標・`MouseOrigin` の基準・クライアント領域・画像の左上) で受け取り、同じ座標系で返します。  
  結果はそのまま `Mouse` に渡せます。
- `area` がキャプチャした範囲からはみ出した部分は無視します。  
  `PeekColor` で範囲外を指定すると `ArgumentOutOfRangeException` になります。
- 取り込んだ時点の内容なので、後で画面が変わっても結果は変わりません。  
  画像が現れるのを待つ場合は、繰り返しのたびに作り直してください。
- 決まった位置の色を調べるだけなら `capture.PeekColor` (配列から読むだけなので速い)、色がどこにあるか分からないなら `capture.CheckColor` を使います。

```csharp
var c = Capture(new Rectangle(0, 0, 800, 600));
if (c.CheckImage(@"C:\img\ok.png") is Point p) Mouse(MouseButton.Left, KeyAction.Click, p);
else if (c.CheckImage(@"C:\img\cancel.png") is Point q) Mouse(MouseButton.Left, KeyAction.Click, q);
if (c.CheckColor(Color.Red, tolerance: 10) != null) Print("赤がある");
var color = c.PeekColor(10, 10);
```

---

## 10. スレッド

### Thread

```csharp
void Thread(Action action)
```

処理を別スレッドで並行に実行します。UWSC: `THREAD`

- メインスクリプトが終わると、スレッド内で組み込みコマンドを呼んだ時点でスレッドも止まります。
- スレッド内でエラーが起きると、スクリプト全体が停止してエラーを表示します。
- [`MouseOrigin`](#mouseorigin) の基準はスレッドごとに別です。スレッドは画面座標から始まります ([詳しく](MouseOrigin.md))。
- 次の設定はスクリプト全体で 1 つです。どのスレッドで変えても、すべてのスレッドに効きます。
  - [`ShowImageArea`](#showimagearea) の表示・色・表示時間
  - [`LockHard` / `LockHardEx`](#lockhard--lockhardex) のロック (最後に呼んだ指定が有効)
  - [吹き出し](#balloon)。`BalloonClear` は、ほかのスレッドが出した吹き出しも消します。
- マウスカーソル・キーボード・アクティブウィンドウ・クリップボードは PC に 1 つしかありません。  
  複数のスレッドから同時に `Move` / `Mouse` / `Keyboard` / `Activate` / クリップボード操作を行うと、動きが混ざります。  
  たとえば、あるスレッドが Ctrl を押している間に別のスレッドがキーを送ると、Ctrl 付きの入力になります。  
  スレッドでは、前面に出さずに直接送る `window.PostMouse` / `window.PostKey` / `window.PostText` (`control.` 版も同じ) や、[コントロール操作](#11-コントロール操作)を使ってください。

```csharp
// エラーダイアログが出たら自動で閉じる監視スレッド
Thread(() =>
{
    while (true)
    {
        GetWindow("エラー")?.Close();
        Sleep(500);
    }
});
```

> 注意: `Thread` が関数名として使われているため、スクリプト内で `Thread.Sleep` などとは書けません。
> 待機には `Sleep(ミリ秒)` を使い、型の `Thread` が必要な場合は `System.Threading.Thread` と書いてください。

---

## 11. コントロール操作

ボタン・リスト・スライダーなど、ウィンドウ内のコントロールを操作します。  
特に断りが無いものは **UI Automation** で操作するため、Win32・WPF・UWP などの種類を問わず同じ書き方で使えます。

### ClickItem

```csharp
bool window.ClickItem(string name, [int timeoutMs = 2000])
bool control.ClickItem(string name, [int timeoutMs = 2000])
```

名前 (ボタンの表示文字など・完全一致) でコントロールを探して操作します。  
ボタンは押し、チェックボックスは切り替え、リスト項目は選択し、ツリーは開閉します。  
見つからない場合は `timeoutMs` ミリ秒まで待ち、それでも無ければ `false` を返します。UWSC: `CLKITEM`

```csharp
w.ClickItem("保存(S)");
```

### CheckButton

```csharp
int window.CheckButton(string name)
int control.CheckButton(string name)
```

ボタン類 (チェックボックス・ラジオボタン・トグル) の状態を返します。UWSC: `CHKBTN`

| 戻り値 | 意味 |
|---|---|
| 1 | オン |
| 0 | オフ (普通のボタンも 0) |
| 2 | 中間状態 (チェックボックスの不確定) |
| -1 | 見つからない・無効 (グレー)・ボタン類ではない |

### GetItem

```csharp
string[] window.GetItem([ItemKind kinds = All])
string[] control.GetItem([ItemKind kinds = All])
```

ボタン名やリスト項目などの文字を列挙します。  
種類は [ItemKind](#itemkind) を `|` で組み合わせて指定します。  
エディットとコンボボックスは、入力されている値を返します。UWSC: `GETITEM`

```csharp
var w = GetWindow("メモ帳") ?? throw new Exception("メモ帳がありません");
foreach (var s in w.GetItem(ItemKind.Button | ItemKind.CheckBox)) Print(s);
```

### GetSelectedItems

```csharp
string[] window.GetSelectedItems([int index = 1])
string[] control.GetSelectedItems([int index = 1])
```

n 番目 (1 から) のリストボックス・コンボボックス・リストビュー・ツリービューで選択中の項目を返します。UWSC: `GETSLCTLST`

### GetSlider / SetSlider

```csharp
SliderInfo? window.GetSlider([int index = 1])
bool        window.SetSlider(double value, [int index = 1])
SliderInfo? control.GetSlider([int index = 1])
bool        control.SetSlider(double value, [int index = 1])
```

n 番目 (1 から) のスライダー・スクロールバー・スピンの値を取得・設定します。  
`GetSlider` は値・最小値・最大値などを [SliderInfo](#sliderinfo) で返し、見つからなければ `null` を返します。  
`SetSlider` は範囲外の値を範囲内に丸めて設定し、成功すれば `true` を返します。UWSC: `GETSLIDER` / `SETSLIDER`

```csharp
var w = GetWindow("音量") ?? throw new Exception("音量がありません");
var s = w.GetSlider();
if (s != null) w.SetSlider(s.Maximum);
```

### GetText / SetText (Win32 コントロール)

```csharp
string window.GetText([int index = 1], [string className = "Edit"])
bool   window.SetText(string text, [int index = 1], [bool append = false], [string className = "Edit"])
string control.GetText()
bool   control.SetText(string text, [bool append = false])
```

`window.` 版は、指定したクラス (既定はエディット) の n 番目 (1 から) の子孫コントロールの文字列を取得・設定します。ウィンドウ自身のタイトルではありません。  
`control.` 版は、そのコントロール自身の文字列を取得・設定します。  
`SetText` は `append = true` で末尾に追記します。`window.SetText` は `index = 0` でフォーカスのあるコントロールに送ります。  
`SetText` はコントロールが受け付ければ true を返します (容量不足などで拒否されたら false。追記は送れたかだけを見ます)。UWSC: `GETSTR` / `SENDSTR`

```csharp
w.SetText("検索語");
Print(w.GetText(2));
```

### GetChildren / FindChild (Win32 コントロール)

```csharp
Control[] window.GetChildren([string? className])
Control?  window.FindChild(string text, [string? className])
Control[] control.GetChildren([string? className])
Control?  control.FindChild(string text, [string? className])
```

子孫のコントロールを列挙・検索し、[Control](#7-control-オブジェクト) で返します。  
`FindChild` はテキストの部分一致で探します。UWSC: `GETCTLHND`

### TextAtPoint

```csharp
string TextAtPoint(Point point)
string TextAtPoint(int x, int y)
string window.TextAtPoint(Point point)	// ウィンドウのクライアント領域相対
string window.TextAtPoint(int x, int y)
string control.TextAtPoint(Point point)	// コントロールのクライアント領域相対
string control.TextAtPoint(int x, int y)
```

指定座標 (画面座標。`MouseOrigin` 指定時はその基準) にあるコントロールの文字 (名前。名前が無ければ値) を返します。  
`window.` / `control.` 版は、ウィンドウ / [コントロール](#7-control-オブジェクト)のクライアント領域左上が基準です。UWSC: `POSACC`

```csharp
Print(TextAtPoint(100, 100));	// 画面座標 (100, 100) の文字
var w = GetWindow("メモ帳") ?? throw new Exception("メモ帳がありません");
Print(w.TextAtPoint(10, 10));	// ウィンドウのクライアント領域 (10, 10) の文字
var c = w.FindChild("", "Edit");
Print(c?.TextAtPoint(5, 5));	// コントロールのクライアント領域 (5, 5) の文字
```

---

## 12. プロセス・COM

### Exec

```csharp
Window? Exec(string fileName, [string arguments = ""])
```

プログラムやファイルを起動し、そのメインウィンドウを返します。  
ウィンドウを取得できなければ `null` を返します。UWSC: `EXEC`

> ストアアプリ版のメモ帳などは、起動したプロセスとウィンドウのプロセスが別になるため `null` になることがあります。
> その場合は `Exec(...) ?? GetWindow("タイトル")` のように書いてください。

### ExecWait

```csharp
int ExecWait(string fileName, [string arguments = ""])
```

起動したプログラムが終了するまで待ち、終了コードを返します。UWSC: `EXEC(..., TRUE)`

### Cmd / PowerShell

```csharp
string Cmd(string command, [bool async = false], [bool showWindow = false])
string PowerShell(string command, [bool async = false], [bool showWindow = false], [bool useCore = false])
```

コマンドを実行し、標準出力と標準エラー出力をつなげた文字列を返します。  
`async: true` にすると終了を待たずに戻ります。  
`showWindow: true` にすると画面を表示しますが、その場合は出力を取得できません。画面はコマンドが終わると閉じます (残したい場合はコマンドの最後で `pause` や `Read-Host` を実行します)。  
どちらの場合も戻り値は空文字列です。  
`Cmd` は cmd.exe で実行し、出力をコンソールのコードページ (日本語環境では Shift-JIS) で読むので文字化けしません。  
`PowerShell` は実行ポリシーを無視し、プロファイルは読み込まずに実行します。  
`useCore: true` にすると PowerShell 7 (pwsh.exe) を使います。  
待っている間に停止が要求されると、コマンドを強制終了します。UWSC: `DOSCMD` / `POWERSHELL`

```csharp
Print(Cmd("ipconfig | findstr IPv4"));
Print(PowerShell("Get-Process | Sort-Object CPU -Descending | Select-Object -First 5"));
```

### CreateOleObject / GetActiveOleObject

```csharp
dynamic CreateOleObject(string progId)
dynamic? GetActiveOleObject(string progId)
```

`CreateOleObject` は COM オブジェクトを新しく作ります。  
`GetActiveOleObject` は起動中のアプリの COM オブジェクトを取得し、起動していなければ `null` を返します。  
戻り値は `dynamic` なので、メソッドやプロパティをそのまま呼べます (補完は効きません)。  
`GetActiveOleObject` は、.NET 5 以降で削除された `Marshal.GetActiveObject` の代わりとして使えます。UWSC: `CREATEOLEOBJ` / `GETACTIVEOLEOBJ`

```csharp
var excel = GetActiveOleObject("Excel.Application") ?? CreateOleObject("Excel.Application");
excel.Visible = true;
Print(excel.ActiveSheet.Range("A1").Value);
```

---

## 13. クリップボード

```csharp
string GetClipboard()
void   SetClipboard(string text)
```

クリップボードの文字列を取得・設定します。  
`GetClipboard` は、文字列が無ければ空文字を返します。UWSC: `GETSTR(0)` / `SENDSTR(0, ...)`

---

## 14. 列挙型

### MouseButton

`Left` `Right` `Middle`

### KeyAction

`Click` (押して離す) / `Down` (押す) / `Up` (離す)

### Keys

.NET の `System.Windows.Forms.Keys` をそのまま使います。  
主なキーは次のとおりです。

`Enter` `Escape` `Tab` `Space` `Back` `Delete` `Up` `Down` `Left` `Right` `Home` `End` `PageUp` `PageDown`  
`F1`〜`F24` `A`〜`Z` `D0`〜`D9` / 修飾キー: `ControlKey` `ShiftKey` `Menu` (Alt) `LWin`

### BalloonTail

吹き出しのしっぽの位置です: `None` (既定) `TopLeft` `TopRight` `BottomLeft` `BottomRight`

### CursorKind

`Arrow` `IBeam` `Wait` `Cross` `UpArrow` `SizeNWSE` `SizeNESW` `SizeWE` `SizeNS` `SizeAll` `No` `Hand` `AppStarting` `Help`  
`Other` (上記以外) / `Hidden` (非表示)

### ItemKind

`Button` `CheckBox` `RadioButton` `Text` `Edit` `ListItem` `ComboBox` `Tab` `Menu` `TreeItem` `DataItem` `Link` `Group`  
組み合わせ: `Buttons` (Button | CheckBox | RadioButton) / `All` (すべて)

### SelectKind

`Button` (項目ごとのボタン) / `Radio` (ラジオボタン) / `Combo` (ドロップダウン) / `List` (リストボックス)

### LockMode

`None` (解除) / `Keyboard` / `Mouse` / `All` (キーボードとマウス)

### SliderInfo

`Value` `Minimum` `Maximum` `SmallChange` `LargeChange` (すべて double)

### MsgButtons

`MessageBox` のボタンの組み合わせです: `OK` (既定) `OKCancel` `YesNo` `YesNoCancel` `RetryCancel` `AbortRetryIgnore`

### MsgIcon

`MessageBox` のアイコンです: `None` (既定) `Information` `Warning` `Error` `Question`

### MsgResult

`MessageBox` の戻り値です: `OK` `Cancel` `Yes` `No` `Retry` `Abort` `Ignore`  
`Timeout` (`timeoutMs` で時間切れ) / `None` (結果なし。通常は返らない)

### TransColor

`CheckImage` / `CheckImages` で、画像のどの隅の色を透過色にするかを指定します: `None` (既定・透過しない) `TopLeft` `TopRight` `BottomLeft` `BottomRight` (UWSC の 1〜4)

---

## 15. UWSC 対応表

UWSC の命令との対比表・書き換え例は [UWSC からの移行ガイド](UWSC.md) を参照してください。
