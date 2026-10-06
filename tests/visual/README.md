# 目視テスト

組み込み API を 1 つずつ実際に動かし、画面の動きと PRINT 窓の `[OK]` / `[NG]` で確認するスクリプト集。
各ステップは左上の吹き出しで内容を表示してから実行される。

```bash
dotnet run --project src/CsWSC.UI -- tests/visual/01_Output.csws
```

| ファイル | 対象 |
|---|---|
| `01_Output.csws` | Print / Sleep / MsgBox / InputBox / Fukidasi (ダイアログ操作あり) |
| `02_Mouse.csws` | MouseMove / Click / Btn / Wheel / MousePos / PeekColor / MusCur / PosAcc |
| `03_Keyboard.csws` | Kbd / Hotkey / SendText / SendKeys / ScKey / GetStr / SendStr / PostKey / PostText |
| `04_Window.csws` | Exec / GetWindow / WaitWindow / GetAllWindows / ウィンドウ情報・状態・制御 / Close / Kill |
| `05_Controls.csws` | テスト用フォームで ClickItem / ChkBtn / GetItem / GetSlctLst / Get・SetSlider / Children / FindChild / PostClick |
| `06_ImageClipboardThread.csws` | SaveImg / ChkImg / クリップボード / ExecWait / Thread / Cancellation |

- 実行中はマウス・キーボードに触らない。止めるときは **Pause** キー
- ノートパッドは日本語版 Windows 10 の従来版 (タイトル「メモ帳」) が前提
- 画像は `%TEMP%\CsWSC_VisualTest` に保存される
