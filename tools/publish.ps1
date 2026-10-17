<#
.SYNOPSIS
  CsWSC を通常版と ReadyToRun 版の 2 つ、artifacts に発行する。
  発行プロファイル (src/CsWSC.UI/Properties/PublishProfiles) を使うので、Visual Studio の「発行」と同じ設定になる。
    win-x64            → artifacts\CsWSC
    win-x64-ReadyToRun → artifacts\CsWSC-ReadyToRun
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\publish.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\CsWSC.UI'
$artifacts = Join-Path $root 'artifacts'

foreach ($p in @(
    @{ Profile = 'win-x64'; Dir = 'CsWSC' },
    @{ Profile = 'win-x64-ReadyToRun'; Dir = 'CsWSC-ReadyToRun' }
)) {
    # 前回の発行で残ったファイルが混ざらないよう、出力先を空にする
    $out = Join-Path $artifacts $p.Dir
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    Write-Host "発行しています: $($p.Profile)"
    dotnet publish $project -p:PublishProfile=$($p.Profile) --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ($($p.Profile)) に失敗しました" }
    $mb = (Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
    Write-Host ("作成しました: {0} ({1:N0} MB)" -f $out, $mb)
}
