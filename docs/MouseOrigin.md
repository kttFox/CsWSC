# MouseOrigin とスレッド・async

`MouseOrigin` で切り替えた座標の基準が、スレッド・`async` 関数・`Task.Run` の中でどう扱われるかを説明します。  
コマンドの一覧は [Commands.md の MouseOrigin](Commands.md#mouseorigin) を見てください。

## まとめ

| 場面 | 始まりの基準 | 中で切り替えた基準は外に残るか |
|---|---|---|
| メインスクリプト | 画面座標 | — (`MouseOrigin` を呼ぶと、その後ずっと続く) |
| `using (MouseOrigin(w)) { ... }` | — | 残らない (ブロックを抜けると直前の基準に戻る) |
| 普通の関数 | 呼び出し元と同じ | **残る** |
| `Thread(...)` | **画面座標** | 残らない |
| `async` 関数 | 呼び出し元と同じ | 残らない (抜けると呼び出し前に戻る) |
| `Task.Run(...)` | 作った時点の呼び出し元と同じ | 残らない |

- 基準はスレッドごとに別です。あるスレッドで切り替えても、ほかのスレッドには影響しません。
- `async` 関数・`Task.Run` を使わないスクリプトでは、「メインスクリプト」「`using`」「普通の関数」「`Thread`」の 4 つだけを覚えれば足ります。

以下の例では、`w1` と `w2` は別々のウィンドウです。

```csharp
var w1 = GetWindow("メモ帳");
var w2 = GetWindow("電卓");
```

## メインスクリプト

画面座標から始まります。
`MouseOrigin(w)` を呼ぶと、それ以降の座標は `w` のクライアント領域の左上が基準になります。  
`MouseOrigin()` (引数なし) で画面座標に戻ります。

```csharp
MouseOrigin(w1);
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w1 のクライアント領域の (10, 20)
Move(100, 50);	// w1 基準
MouseOrigin();	// 画面座標に戻す
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// 画面の (10, 20)
```

## using

`using` を付けると、ブロックを抜けたときに**直前の基準**に戻ります。  
例外でブロックを抜けたときも戻ります。

```csharp
MouseOrigin(w1);
using (MouseOrigin(w2)) {
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w2 基準
}
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w1 基準に戻る (画面座標ではない)
```

## 普通の関数

関数の中で切り替えた基準は、関数を抜けた後も続きます。

```csharp
void Setup() {
	MouseOrigin(w2);
}

MouseOrigin(w1);
Setup();
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w2 基準
```

## Thread

スレッドは、親の基準を引き継がずに**画面座標**から始まります。UWSC の `THREAD` と `MOUSEORG` の関係と同じです。  
スレッドの中で切り替えても、親やほかのスレッドには影響しません。

```csharp
MouseOrigin(w1);	// 親: w1 基準

Thread(() => {
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// 画面座標 (親の w1 は引き継がない)
	MouseOrigin(w2);	// このスレッドだけ w2 基準
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w2 基準
});

Thread(() => {
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// 画面座標 (1 つ目のスレッドの w2 は関係ない)
});

Sleep(1000);
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// 親は w1 基準のまま
```

スレッドで座標の基準を使うときは、スレッドの中で `MouseOrigin(w)` を呼んでください。

## async 関数

`async` 関数は、次のように動きます。

1. 入るときは、呼び出し元の基準をそのまま受け取ります。
2. 中で切り替えると、関数の中だけ新しい基準になります。`await` をまたいでも続きます。
3. 抜けると、呼び出し元は呼ぶ前の基準に戻ります。

```csharp
async Task Work() {
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// 呼び出し元の基準 (下の例では w1)
	MouseOrigin(w2);	// ここから w2 基準
	await Task.Delay(500);
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// await の後も w2 基準
}

MouseOrigin(w1);
await Work();
Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w1 基準に戻っている
```

普通の関数と違い、`async` を付けると中で切り替えた基準が外に残りません。  
関数の外でも同じ基準を使いたいときは、`async` 関数の外で `MouseOrigin` を呼んでください。

## Task.Run

`Task.Run` のタスクは、作った時点の呼び出し元の基準から始まります。  
中で切り替えても、呼び出し元には影響しません。

```csharp
MouseOrigin(w1);

await Task.Run(() => {
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w1 基準 (作った時点の基準を引き継ぐ)
	MouseOrigin(w2);	// このタスクだけ w2 基準
	Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// w2 基準
});

Mouse(MouseButton.Left, KeyAction.Click, 10, 20);	// 呼び出し元は w1 基準のまま
```

画面座標から始めたいときは、タスクの最初で `MouseOrigin()` を呼んでください。
並行処理には、なるべく `Thread` を使ってください。

## 仕組み

基準は .NET の `AsyncLocal` に入れています。動きはすべて `AsyncLocal` の仕様によるものです。

- **スレッドごとに別**: 値はスレッドの実行コンテキスト (ExecutionContext) に入っています。切り替えると、そのスレッドのコンテキストだけが新しいものに差し替わり、ほかのスレッドのコンテキストは変わりません。
- **`Task.Run` が引き継ぐ**: 新しいスレッドやタスクは、開始時の呼び出し元のコンテキストを受け取ります。
- **`Thread` が引き継がない**: `Thread` は、スレッドの最初で基準を画面座標に戻しています。`Task.Run` は .NET 本体の関数なので、同じ処理を入れられません。
- **`async` 関数を抜けると戻る**: `async` 関数を呼ぶと、.NET が呼び出し前のコンテキストを覚えておき、呼び出し元に戻るときにそれに戻します。
- **`await` をまたいでも続く**: `await` の後で別のスレッドで続きが動いても、コンテキストは一緒に運ばれます。
