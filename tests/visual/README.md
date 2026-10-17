# 目視テスト

組み込み API を 1 つずつ実際に動かし、画面の動きと PRINT 窓の `[OK]` / `[NG]` で確認するスクリプト集。
各ステップは左上の吹き出しで内容を表示してから実行される。

```bash
dotnet run --project src/CsWSC.UI -- tests/visual/01_Output.csws
```

| ファイル | 対象 |
|---|---|
| `01_Output.csws` | Print / 大文字エイリアス (PRINT・SLEEP) / Sleep / MessageBox (ボタン種別・アイコン・timeoutMs) / InputBox / Balloon (Point 版・しっぽ 4 方向) / BalloonClear / Balloon.Update (ダイアログ操作あり) |
| `02_Mouse.csws` | Move / Mouse (Point 版含む) / Wheel / MousePosition / PeekColor / MouseCursor / TextAtPoint / MouseOrigin |
| `03_Keyboard.csws` | Keyboard / ShortcutKey / SendText / Window.SendText / Window.ShortcutKey / GetText / SetText / PostKey / PostText / GetKeyState / GetToggleState |
| `04_Window.csws` | Exec / GetWindow / WaitWindow / GetAllWindows / ActiveWindow / WindowFromPoint (Point 版含む) / WindowFromHandle / WindowUnderMouse / ControlUnderMouse / ウィンドウ情報・状態・制御 / Close / Kill |
| `05_Controls.csws` | テスト用フォームで ClickItem (timeoutMs) / CheckButton / GetItem / GetSelectedItems / Get・SetSlider / GetChildren / FindChild / Window.Mouse / Window.PostMouse / Window.TextAtPoint / Control (Parent・TopWindow・SetText・PostMouse) |
| `06_ImageClipboardThread.csws` | SaveImage / CheckImage / CheckImages / ImageMatch (Color・Shape・Gray・Edge) / transColor (TransColor) / Window.SaveImage / Window.CheckImage / Window.CheckImages / クリップボード / ExecWait / Thread / Cancellation |
| `07_ShowImageArea.csws` | ShowImageArea (色・太さ・表示時間) / SaveImage / CheckImage / PeekColor / Window.SaveImage / Window.CheckImage の範囲枠 (枠が結果に写らないこと) |
| `08_DialogsSystem.csws` | SelectBox / SelectBoxMulti / PopupMenu / LogPrint / LogClear / LockHard / LockHardEx / Cmd / PowerShell / CreateOleObject / Speak / 大文字エイリアス / ExitScript (ダイアログ操作・音声あり) |

- 実行中はマウス・キーボードに触らない。止めるときは **Pause** キー
- ノートパッドは日本語版 Windows 10 の従来版 (タイトル「メモ帳」) が前提
- 画像は `%TEMP%\CsWSC_VisualTest` に保存される
- 時間の引数はすべてミリ秒 (`Sleep(1500)`、`WaitWindow(..., timeoutMs: 5000)` など)
- `08_DialogsSystem.csws` の LockHard は数秒だけ入力をロックする (Pause キーで非常停止可、終了時に自動解除)
