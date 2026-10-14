<#
.SYNOPSIS
  VS Code 拡張 (editors/vscode) を VSIX にまとめる。-Install を付けると VS Code にインストールする。
  GitHub Actions (.github/workflows/release-vscode.yml) と同じ手順 (vsce) で作るため Node.js が必要。
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\build-vscode-extension.ps1 -Install
#>
param([switch]$Install)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ext = Join-Path $root 'editors\vscode'
$serverOut = Join-Path $ext 'server'
$artifacts = Join-Path $root 'artifacts'

if (-not (Get-Command npx -ErrorAction SilentlyContinue)) { throw 'Node.js (npx) が見つかりません。Node.js をインストールしてください' }

# 1. 解析サーバーを拡張フォルダーへ発行
if (Test-Path $serverOut) { Remove-Item $serverOut -Recurse -Force }
dotnet publish (Join-Path $root 'src\CsWSC.LanguageServer') -c Release -o $serverOut --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish に失敗しました' }

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
