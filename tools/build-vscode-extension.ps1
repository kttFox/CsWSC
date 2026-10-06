<#
.SYNOPSIS
  VS Code 拡張 (editors/vscode) を VSIX にまとめる。-Install を付けると VS Code にインストールする。
  Node.js / vsce は不要 (解析サーバーを dotnet publish して zip にする)。
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\build-vscode-extension.ps1 -Install
#>
param([switch]$Install)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ext = Join-Path $root 'editors\vscode'
$serverOut = Join-Path $ext 'server'
$pkg = Get-Content (Join-Path $ext 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$vsix = Join-Path $root "artifacts\$($pkg.name)-$($pkg.version).vsix"

# 1. 解析サーバーを拡張フォルダーへ発行
if (Test-Path $serverOut) { Remove-Item $serverOut -Recurse -Force }
dotnet publish (Join-Path $root 'src\CsWSC.LanguageServer') -c Release -o $serverOut --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish に失敗しました' }

# 2. VSIX (zip) を作成
New-Item -ItemType Directory -Force (Split-Path $vsix) | Out-Null
if (Test-Path $vsix) { Remove-Item $vsix -Force }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011" xmlns:d="http://schemas.microsoft.com/developer/vsx-schema-design/2011">
  <Metadata>
    <Identity Language="en-US" Id="$($pkg.name)" Version="$($pkg.version)" Publisher="$($pkg.publisher)" />
    <DisplayName>$($pkg.displayName)</DisplayName>
    <Description xml:space="preserve">$($pkg.description)</Description>
    <Categories>Programming Languages</Categories>
    <Properties>
      <Property Id="Microsoft.VisualStudio.Code.Engine" Value="$($pkg.engines.vscode)" />
      <Property Id="Microsoft.VisualStudio.Code.ExtensionKind" Value="workspace" />
    </Properties>
  </Metadata>
  <Installation>
    <InstallationTarget Id="Microsoft.VisualStudio.Code" />
  </Installation>
  <Dependencies />
  <Assets>
    <Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true" />
  </Assets>
</PackageManifest>
"@

$files = Get-ChildItem $ext -Recurse -File
$types = ($files | ForEach-Object { $_.Extension.ToLowerInvariant() } | Where-Object { $_ } | Sort-Object -Unique |
    ForEach-Object { "  <Default Extension=`"$_`" ContentType=`"application/octet-stream`" />" }) -join "`r`n"
$contentTypes = @"
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension=".vsixmanifest" ContentType="text/xml" />
$types
</Types>
"@

$utf8 = New-Object System.Text.UTF8Encoding($false)
$zip = [System.IO.Compression.ZipFile]::Open($vsix, 'Create')
try {
    foreach ($entry in @(@{ Name = 'extension.vsixmanifest'; Text = $manifest }, @{ Name = '[Content_Types].xml'; Text = $contentTypes })) {
        $w = New-Object System.IO.StreamWriter($zip.CreateEntry($entry.Name).Open(), $utf8)
        $w.Write($entry.Text); $w.Dispose()
    }
    foreach ($f in $files) {
        # zip 内のパスは / 区切りにする (PowerShell 5.1 の CreateFromDirectory は \ になるため使わない)
        $name = 'extension/' + $f.FullName.Substring($ext.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $name) | Out-Null
    }
}
finally { $zip.Dispose() }
Write-Host "作成しました: $vsix"

# 3. インストール
if ($Install) {
    code --install-extension $vsix --force
    if ($LASTEXITCODE -ne 0) { throw 'VS Code へのインストールに失敗しました' }
    Write-Host 'インストールしました。VS Code を再読み込みしてください (Ctrl+Shift+P → Developer: Reload Window)。'
}
