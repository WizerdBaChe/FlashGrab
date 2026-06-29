<#
.SYNOPSIS
  產出 FlashGrab 兩種發行成品(皆為單一 exe)到 publish\dist\。

  - FlashGrab.exe          : framework-dependent(~24MB,需 .NET 8 Desktop Runtime,主推、對齊 G-Helper)
  - FlashGrab-Portable.exe : 自含壓縮(~78MB,無需任何前置,真・開箱即用)

  AI 模型一律不打包(Tier 2 為能力偵測/外掛),主程式只含 app 本體。
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "publish\dist"
$fdOut = Join-Path $root "publish\_fd"
$scOut = Join-Path $root "publish\_sc"

Write-Host "== 清理舊輸出 ==" -ForegroundColor Cyan
Remove-Item $dist, $fdOut, $scOut -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "== 1/2 framework-dependent 單檔 ==" -ForegroundColor Cyan
dotnet publish -c $Configuration -r $Runtime --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o $fdOut
Copy-Item (Join-Path $fdOut "FlashGrab.exe") (Join-Path $dist "FlashGrab.exe") -Force

Write-Host "== 2/2 自含壓縮單檔 ==" -ForegroundColor Cyan
dotnet publish -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $scOut
Copy-Item (Join-Path $scOut "FlashGrab.exe") (Join-Path $dist "FlashGrab-Portable.exe") -Force

Write-Host "`n== 發行成品(publish\dist) ==" -ForegroundColor Green
Get-ChildItem $dist | Select-Object Name,
    @{N='MB';E={ [math]::Round($_.Length / 1MB, 1) }} | Format-Table -AutoSize
