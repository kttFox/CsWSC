# CsWSC スクリプト作成ガイド (AI 向け)

このファイルは、AI が CsWSC のスクリプト (`.csws`) を正しく書くための要約です。
詳しい仕様は [Commands.md](Commands.md)、UWSC との対応は [UWSC.md](UWSC.md) を参照してください。

## 前提

- CsWSC は Windows 操作を自動化する RPA ツール。スクリプトは **C# スクリプト** (.NET 10 / Roslyn Scripting)。
- `class` や `Main` は書かない。上から順に実行されるトップレベルの文として書く。関数 (ローカル関数) はどこに書いても呼べる。
- 組み込みコマンドは**クラス名なし**で呼ぶ (`Mouse(...)`、`GetWindow(...)`)。
- 既定の using: `System` `System.IO` `System.Linq` `System.Text` `System.Collections.Generic` `System.Threading` `System.Threading.Tasks` `System.Drawing` `System.Windows.Forms` `CsWSC`
- 他のアセンブリは先頭に `#r "path.dll"` か `#r "System.Data"` を書く (相対パスの基準はスクリプトのフォルダ)。
- 途中で終了するには `return;`、関数やスレッドの中から全体を終了するには `ExitScript();` を使う。

## 必ず守るルール

1. **時間の単位はすべてミリ秒** (int): `Sleep(500)`、`timeoutMs: 2000`。UWSC の `SLEEP` (秒) とは違うので注意。
2. **座標の単位はピクセル**。グローバル関数の `Mouse(button, action, x, y)` は画面座標。`w.Mouse(button, action, x, y)` と `MouseOrigin(w)` の座標は**クライアント領域の左上が基準**で、タイトルバーや枠は含まない。
3. **ウィンドウが見つからないときは `null`** (UWSC の `-1` ではない)。必ず null を確認する:
   `var w = GetWindow("メモ帳") ?? throw new Exception("メモ帳がありません");`
4. **`Thread.Sleep` は書けない** (`Thread` が組み込み関数の名前のため)。待機は `Sleep(ミリ秒)` を使う。型の Thread が必要なら `System.Threading.Thread` と書く。
5. **`using System.Windows.Input;` は書かない** (`Keyboard` などと名前がぶつかる)。必要なら `using Input = System.Windows.Input;` のように別名にする。
6. キーは `System.Windows.Forms.Keys` を使う。修飾キーは `Keys.ControlKey` / `Keys.ShiftKey` / `Keys.Menu` (Alt) / `Keys.LWin`。(`Keys.Control` / `Keys.Shift` / `Keys.Alt` を渡しても対応する修飾キーとして押されるが、書き方は `ControlKey` / `ShiftKey` / `Menu` にそろえる。)
7. 文字列の入力は `SendText` を使う (IME を通さずに日本語も直接入力できる)。1 文字ずつ `Keyboard` で送らない。
8. 固定の `Sleep` で待つより、`GetWindow(..., timeoutMs: n)` / `WaitWindow` / `w.WaitActive()` / `w.ClickItem(name, timeoutMs)` など、**状態を待つ API** を優先する。
9. 座標でクリックするより、可能なら **UI Automation** (`w.ClickItem("保存(S)")`) を優先する。座標で操作するときは `w.Mouse(MouseButton.Left, KeyAction.Click, x, y)` (クライアント領域相対) を使う。
10. C# なので大文字・小文字を区別し、文末に `;` が必要。条件には `bool` しか書けない。文字列の添字は 0 始まり。
11. `MouseOrigin` の基準はスレッドごとに別で、スレッドは画面座標から始まる (必要ならスレッドの中で `MouseOrigin(w)` を呼ぶ)。`async` 関数の中で切り替えた基準は、抜けると呼び出し前に戻る。

## 主要 API (シグネチャ要約)

`[]` は省略可能な引数。名前付きで渡せる (例: `GetWindow("メモ帳", timeoutMs: 2000)`)。

### 出力・ダイアログ・待機
```csharp
void    Print(object? value)                      // PRINT 窓に出力
void    LogPrint(bool visible)                       // PRINT 窓の表示・非表示。(visible, x, y, [width], [height]) / (visible, Point, ...) で位置も指定
void    LogClear()                                // PRINT 窓を消去
void    Sleep(int ms)
void    ExitScript()
MsgResult MessageBox(string message, [string? title], [MsgButtons buttons], [MsgIcon icon], [int timeoutMs])
string  InputBox(string prompt, [string defaultValue])        // キャンセルで ""
int     SelectBox(string message, params IReadOnlyList<string> items)      // 0 始まり、キャンセルで -1
int     SelectBox(SelectKind kind, string message, IReadOnlyList<string> items, [int timeoutMs])
int[]   SelectBoxMulti(string message, IReadOnlyList<string> items, [bool asList], [int timeoutMs])
string? PopupMenu(IReadOnlyList<string> items, [Point? point])             // "-" は区切り、"<<" はサブメニュー。(items, x, y) 版もある
void    Speak(string text, [bool async], [bool interrupt], [int rate], [int volume])
Balloon Balloon(string message, [Point point], [BalloonTail tail], [float? fontSize], [string? fontName], [Color? foreColor], [Color? backColor], [double? opacity])   // (message, x, y, ...) 版もある。point 省略で (0, 0)、ほかの省略した値は BalloonDefault の値。返り値の Close() / using で消す
Balloon balloon.Update([string? message], [Point? point])     // 表示中の吹き出しの文言・位置を変える。(message, x, y) 版もある。Message / Target / IsClosed で状態を取得
void    BalloonClear()
BalloonDefaults BalloonDefault   // .FontSize / .FontName / .ForeColor / .BackColor / .Opacity で Balloon の初期値を設定 (実行ごとにリセット)。大文字の別名 BALLOONDEFAULT.FONTSIZE なども可
```

### マウス
```csharp
IDisposable MouseOrigin([Window? window])   // 座標の基準を切り替える。using で範囲を限定できる。スレッドごとに別
void  Move(int x, int y, [int delayMs])     // Point 版もある
void  Mouse(MouseButton button, KeyAction action, int x, int y, [int delayMs])   // Down/Up でドラッグ
void  Mouse(MouseButton button, KeyAction action, [Point? point], [int delayMs])  // point 省略・null で現在位置
void  PostMouse(MouseButton button, KeyAction action, int x, int y, [int delayMs])  // MouseOrigin(w) のウィンドウへ前面に出さず送る。未指定なら例外
void  Wheel(int notches, [int x, int y], [int delayMs])  // 正で下、負で上。Point 版もある
Point MousePosition
CursorKind MouseCursor()
Color PeekColor(int x, int y)               // Point 版もある
Color w.PeekColor(int x, int y)             // ウィンドウ内の色 (クライアント領域相対)。Point 版もある
Color c.PeekColor(int x, int y)             // Capture の色 (作ったときの座標系)。画像ファイルは Capture(path).PeekColor(x, y)
string TextAtPoint(int x, int y)            // Point 版もある
```

### キーボード (アクティブなウィンドウへ送る)
```csharp
void Keyboard(Keys key, [KeyAction action], [int delayMs])
void ShortcutKey(params IEnumerable<Keys> keys)             // 同時押し: ShortcutKey(Keys.ControlKey, Keys.S)
void SendText(string text)
bool GetKeyState(Keys key)                  // Keys.LButton などマウスボタンも可
bool GetToggleState(Keys key)               // CapsLock など
void LockHard(bool lockInput)               // ユーザーの入力をロック。finally で解除する
void LockHardEx([Window? window], [LockMode mode = LockMode.All])   // window 指定でそのウィンドウへの入力だけロック。LockMode.None で解除
```

### ウィンドウの取得
```csharp
Window?  GetWindow(string title, [string? className], [int timeoutMs = 0])   // 部分一致・大小区別なし。負の timeoutMs で無制限
Window?  WaitWindow(string title, [string? className], [int timeoutMs = 2000])
Window   ActiveWindow()
Window[] GetAllWindows([string? title], [string? className])
Window?  WindowFromPoint(int x, int y)                // Point 版もある
Control? ControlFromPoint(int x, int y)               // その位置のコントロール (子ウィンドウ)。Point 版もある
Window?  WindowUnderMouse()                           // マウス下のトップレベルウィンドウ
Control? ControlUnderMouse()                          // マウス下のコントロール (子ウィンドウ)。無ければ null
Window?  WindowFromHandle(IntPtr handle)
Window?  Exec(string fileName, [string arguments])     // 起動してメインウィンドウを返す
int      ExecWait(string fileName, [string arguments])
```

### Window オブジェクト
操作系のメソッドは `Window` を返すので、メソッドチェーンで書ける (`Close()` `Kill()` は void、`WaitClose()` `WaitActive()` は bool なので除く): `w.Activate().MoveWindow(0, 0, 800, 600);`
`==` / `!=` は同じウィンドウ (ハンドルが同じ) かどうかを比べる: `if (ActiveWindow() == w) …`
閉じたウィンドウ・コントロールの操作や情報の取得は `InvalidOperationException`。例外にならないのは `Exists` `WaitClose()` `ToString()` `==` と状態の bool (閉じていれば false)。

- 情報: `Title` `ClassName` `Text` `Bounds` `ClientBounds` `ClientCenter` (クライアント相対の中央) `X` `Y` `Width` `Height` `ProcessId` `ProcessName` `ProcessPath` `Handle`
- 状態: `Exists` `Visible` `IsActive` `Minimized` `Maximized` `Enabled` `Busy` `Topmost`
- 制御: `Activate()` `MoveWindow(x, y, [w], [h])` (`MoveWindow(Point, ...)` もある) `Resize(w, h)` `Minimize()` `Maximize()` `Restore()` `Hide()` `Show()` `SetTopmost([bool])` `SetEnabled([bool])` `Close()` `Kill()`
- 待機: `WaitClose([timeoutMs = -1])` `WaitActive([timeoutMs = 2000])`
- 実際のマウス・キー入力: `Move(x, y, [delayMs])` / `Mouse(button, action, x, y, [delayMs])` / `Wheel(notches, x, y, [delayMs])` (クライアント相対、Point 版もある。point 省略で現在のマウス位置。前面に出さないので、必要なら先に `Activate()`) / `SendText(text)` / `ShortcutKey(params IEnumerable<Keys>)` (前面に出してから送る)
- バックグラウンド入力: `PostMouse(button, action, x, y, [delayMs])` / `PostKey(Keys, [delayMs])` / `PostText(text, [delayMs])` (前面に出さない。アプリによっては反応しない)
- コントロール (UI Automation):
  - `bool ClickItem(string name, [int timeoutMs = 2000])`: 名前は**完全一致**。ボタンを押す、チェックを切り替える、項目を選ぶ
  - `int CheckButton(string name)`: 1=オン / 0=オフ (普通のボタンも 0) / 2=中間 / -1=なし・無効
  - `string[] GetItem([ItemKind kinds])` / `string[] GetSelectedItems([int index])`
  - `SliderInfo? GetSlider([int index])` / `bool SetSlider(double value, [int index])`
  - `string TextAtPoint(int x, int y)`
- Win32 コントロール: `GetText([index], [className = "Edit"])` / `SetText(text, [index], [append], [className])` (子孫のエディットなど) / `Control[] GetChildren([className])` / `Control? FindChild(text, [className])`。index は 1 始まり
- 画像: `SaveImage(path, [area])` / `Capture([area])` / `PeekColor(x, y)` / `CheckColor(color, [area], [tolerance])` / `CheckColors(...)` / `CheckImage(path, [area], [tolerance])` / `CheckImage(path, transColor, [area], [tolerance])` / `CheckImage(path, area, ImageMatch)` / `CheckImage(path, transColor, area, ImageMatch)` / `CheckImages(...)` (同じ引数で全件を返す。探す画像に Capture も渡せる。座標はすべてクライアント相対)

### Control オブジェクト
ウィンドウの中のボタン・入力欄など (HWND を持つ子ウィンドウ)。`ControlFromPoint()` / `ControlUnderMouse()` / `w.GetChildren()` / `w.FindChild()` で取得する。
`Window` と共通の基底クラス `ControlBase` を持ち、情報 (`Handle` `ClassName` `Text` `Bounds` `ClientBounds` `ClientCenter` `Exists` `Visible` `Enabled` など)・UI Automation・画像・`GetChildren`/`FindChild`・`WaitClose`・`==` は同じように使える (座標はそのコントロールのクライアント相対)。
- 固有: `Parent` (`ControlBase?`) / `TopWindow` (`Window?`) / `string GetText()` / `bool SetText(text, [append])` / `MoveControl(x, y, [w], [h])` (親のクライアント相対) / `Resize(w, h)` / `Hide()` `Show()` `SetEnabled([bool])`
- 入力: `Move(x, y, [delayMs])` / `Mouse(button, action, x, y, [delayMs])` / `Wheel(notches, x, y, [delayMs])` / `PostMouse(button, action, x, y, [delayMs])` (クライアント相対、Point 版もある。point 省略で現在のマウス位置) / `PostKey(Keys, [delayMs])` / `PostText(text, [delayMs])` (このコントロールへ送る)
- `Activate` `Close` `Kill` `SendText` など、トップレベルウィンドウ向けの操作は無い
- WinForms の `Control` と名前が重なるので、型名を書かずに `var` で受ける (書くなら `CsWSC.Control`)

### 画像
```csharp
void   SaveImage(string path, [Rectangle? area])
Point? CheckImage(string imagePath, [Rectangle? area], [int tolerance = 0])   // 色で探す。見つかった左上。なければ null
Point? CheckImage(string imagePath, TransColor transColor, [Rectangle? area], [int tolerance = 0])   // 透過色を指定
Point? CheckImage(string imagePath, ImageMatch match)   // 全体を探す
Point? CheckImage(string imagePath, Rectangle area, ImageMatch match)   // match: ImageMatch.Color(tol, [max]) / Shape([max]) 色無視 / Gray(0.95) 明るさの相関 / Edge(0.95) 輪郭の相関 (Gray/Edge は遅いので area を指定)
Point? CheckImage(string imagePath, TransColor transColor, ImageMatch match)   // 透過色と照合方法を指定して全体を探す
Point? CheckImage(string imagePath, TransColor transColor, Rectangle area, ImageMatch match)   // 透過色を指定
Point[] CheckImages(string imagePath, [Rectangle? area], [int tolerance = 0], [int max])   // 見つかったすべての左上。なければ空配列
Point[] CheckImages(string imagePath, TransColor transColor, [Rectangle? area], [int tolerance = 0], [int max])   // 透過色を指定
Point[] CheckImages(string imagePath, ImageMatch match)
Point[] CheckImages(string imagePath, Rectangle area, ImageMatch match)
Point[] CheckImages(string imagePath, TransColor transColor, ImageMatch match)   // 透過色と照合方法を指定して全体を探す
Point[] CheckImages(string imagePath, TransColor transColor, Rectangle area, ImageMatch match)   // 透過色を指定
Point?  CheckImage(Capture capture, …) / Point[] CheckImages(Capture capture, …)   // 探す画像を Capture で渡す (Capture(path) や Capture(rect) で作る)。capture 以降は上と同じ
Point?  CheckColor(Color color, [Rectangle? area], [int tolerance = 0])   // 指定の色を探す。CheckColors で全件。w. 版もある
(Color Color, Point Point)? CheckColor(Color[] colors, [Rectangle? area], [int tolerance = 0])   // いずれかの色。一致した色 (colors の要素) と位置。CheckColors で全件
Capture Capture([Rectangle? area])   // 画面を一度だけ取り込む。w.Capture([area]) / Capture(string imagePath) もある
//   c.CheckImage(…) / c.CheckImages(…) (引数は同じ) / c.CheckColor(Color または Color[], [area], [tolerance]) / c.CheckColors(…) / c.PeekColor(x, y) / c.Save(path) / c.ToBitmap()
//   座標は作ったときの座標系で受け取り・返す。複数の画像や色を同じ瞬間の画面で調べるときに使う
void   ShowImageArea([bool enabled], [Color? color], [int durationMs])     // デバッグ用に範囲を枠で表示
```

### スレッド・プロセス・COM・クリップボード
```csharp
void    Thread(Action action)
string  Cmd(string command, [bool async], [bool showWindow])
string  PowerShell(string command, [bool async], [bool showWindow], [bool useCore])
dynamic CreateOleObject(string progId)
dynamic? GetActiveOleObject(string progId)
string  GetClipboard()
void    SetClipboard(string text)
CancellationToken Cancellation              // 長い計算の途中で停止を確認する
string  ScriptName                           // 実行中のスクリプトのファイル名
```

### 列挙型
- `MouseButton`: `Left` `Right` `Middle`
- `KeyAction`: `Click` `Down` `Up`
- `ItemKind`: `Button` `CheckBox` `RadioButton` `Text` `Edit` `ListItem` `ComboBox` `Tab` `Menu` `TreeItem` `DataItem` `Link` `Group` / `Buttons` `All` (`|` で組み合わせる)
- `SelectKind`: `Button` `Radio` `Combo` `List`
- `LockMode`: `None` `Keyboard` `Mouse` `All`
- `CursorKind`: `Arrow` `IBeam` `Wait` `Hand` など
- `BalloonTail`: `None` (既定) `TopLeft` `TopRight` `BottomLeft` `BottomRight`
- `MsgButtons`: `OK` `OKCancel` `YesNo` `YesNoCancel` `RetryCancel` `AbortRetryIgnore`
- `MsgIcon`: `None` `Information` `Warning` `Error` `Question`
- `MsgResult`: `None` `OK` `Cancel` `Yes` `No` `Retry` `Abort` `Ignore` `Timeout` (時間切れ)
- `TransColor`: `None` `TopLeft` `TopRight` `BottomLeft` `BottomRight` (透過色にする隅の色。UWSC の 1〜4)

## よくある間違い

| 間違い | 正しい書き方 |
|---|---|
| `Sleep(1)` (秒のつもり) | `Sleep(1000)` |
| `Thread.Sleep(500)` | `Sleep(500)` |
| `var w = GetWindow("メモ帳"); w.Activate();` (null を確認していない) | `var w = GetWindow("メモ帳") ?? throw new Exception("...");` |
| `if (GetWindow("x") == -1)` | `if (GetWindow("x") == null)` |
| `Exec("notepad.exe").SendText("abc");` (null になることがある) | `var w = Exec("notepad.exe") ?? GetWindow("メモ帳");` |
| `w.ClickItem("保存")` (表示は「保存(S)」) | 表示どおりの名前で完全一致させる: `w.ClickItem("保存(S)")` |
| `Marshal.GetActiveObject("Excel.Application")` | `GetActiveOleObject("Excel.Application")` |
| `GETID` `BTN` `KBD` など UWSC の書き方 | [UWSC.md](UWSC.md) の対応表で書き換える |
| `class Program { static void Main() { ... } }` | 文をそのままトップレベルに書く |

## サンプル

### メモ帳に入力して保存する
```csharp
var w = Exec("notepad.exe") ?? GetWindow("メモ帳", timeoutMs: 5000);
if (w == null) { Print("メモ帳が見つかりません"); return; }

w.Activate().MoveWindow(100, 100, 800, 600);
for (var i = 1; i <= 3; i++)
{
    w.SendText($"{i} 行目");
    Keyboard(Keys.Enter);
}
w.ShortcutKey(Keys.ControlKey, Keys.S);

var dlg = WaitWindow("名前を付けて保存");
if (dlg == null) { Print("保存ダイアログが出ません"); return; }
dlg.SendText(@"C:\temp\test.txt");
dlg.ClickItem("保存(S)");
```

### ダイアログを待ってボタンを押す
```csharp
var dlg = GetWindow("確認", "#32770", timeoutMs: 30_000);
if (dlg == null || !dlg.ClickItem("OK")) Print("OK を押せませんでした");
```

### 画像を探してクリックする
```csharp
var w = GetWindow("対象アプリ") ?? throw new Exception("対象アプリがありません");
if (w.CheckImage(@"C:\img\button.png", tolerance: 10) is Point p)
    w.Mouse(MouseButton.Left, KeyAction.Click, p.X + 5, p.Y + 5);   // CheckImage の結果は左上なので少し内側を押す
else
    Print("ボタンの画像が見つかりません");
```

### エラーダイアログを自動で閉じる監視スレッド
```csharp
Thread(() =>
{
    while (true)
    {
        GetWindow("エラー", timeoutMs: 0)?.Close();
        Sleep(500);
    }
});
// ... メイン処理 ...
```

### Excel のセルを読む (COM)
```csharp
var excel = GetActiveOleObject("Excel.Application") ?? CreateOleObject("Excel.Application");
excel.Visible = true;
Print(excel.ActiveSheet.Range("A1").Value);
```

## 書くときの手順

1. 対象のウィンドウをタイトル (と必要ならクラス名) で取得し、null を確認する。
2. 操作の前に `Activate()` するか、前面に出してから送る `w.SendText` / `w.ShortcutKey` を使う。`w.Move` / `w.Mouse` / `w.Wheel` は座標がウィンドウ基準になるだけで前面には出さないので、必要なら先に `Activate()` する。
3. 画面の切り替わりは `WaitWindow` / `ClickItem` の timeoutMs で待つ。`Sleep` は最後の手段にする。
4. 途中の状態は `Print` で出力して確認できるようにする。
5. 迷ったら [Commands.md](Commands.md) の該当する節を確認する。
