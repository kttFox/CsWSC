<#
.SYNOPSIS
  VS Code 拡張 (editors/vscode) を VSIX にまとめる。実行用の CsWSC.UI も Release でビルドする。-Install を付けると VS Code にインストールする。
  GitHub Actions (.github/workflows/release-vscode.yml) と同じ手順 (vsce) で作るため Node.js が必要。
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\build-vscode-extension.ps1 -Install
#>
param([switch]$Install)

$ErrorActionPreference = 'Stop'

# 結果を読めるよう、終了前にキー入力を待つ (入力がリダイレクトされているときは待たない)
function Wait-Key {
    if ([Console]::IsInputRedirected) { return }
    Write-Host 'キーを押すと終了します...'
    [Console]::ReadKey($true) | Out-Null
}
trap { Write-Host "エラー: $_" -ForegroundColor Red; Wait-Key; exit 1 }

$root = Split-Path -Parent $PSScriptRoot
$ext = Join-Path $root 'editors\vscode'
$serverOut = Join-Path $ext 'server'
$artifacts = Join-Path $root 'artifacts'

if (-not (Get-Command npx -ErrorAction SilentlyContinue)) { throw 'Node.js (npx) が見つかりません。Node.js をインストールしてください' }

# 1. 解析サーバーを拡張フォルダーへ発行
if (Test-Path $serverOut) { Remove-Item $serverOut -Recurse -Force }
dotnet publish (Join-Path $root 'src\CsWSC.LanguageServer') -c Release -o $serverOut --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish に失敗しました' }

# 実行に使う CsWSC.exe (bin\Release) も解析サーバーと同じ版にそろえる
dotnet build (Join-Path $root 'src\CsWSC.UI') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'CsWSC.UI のビルドに失敗しました' }

# 2. VSIX を作成 (解析サーバーが Windows 専用なので win32-x64 向け)
New-Item -ItemType Directory -Force $artifacts | Out-Null
Push-Location $ext
try {
    npx --yes @vscode/vsce package --target win32-x64 --no-dependencies -o "$artifacts\"
    if ($LASTEXITCODE -ne 0) { throw 'vsce package に失敗しました' }
}
finally { Pop-Location }
# 既存の vsix は残す (同名なら vsce が上書き) ので、いま作ったもの = 最新のものを使う
$vsix = (Get-ChildItem (Join-Path $artifacts '*.vsix') | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
Write-Host "作成しました: $vsix"

# 3. インストール
if ($Install) {
    code --install-extension $vsix --force
    if ($LASTEXITCODE -ne 0) { throw 'VS Code へのインストールに失敗しました' }
    Write-Host 'インストールしました。VS Code を再読み込みしてください (Ctrl+Shift+P → Developer: Reload Window)。'
}
Wait-Key
