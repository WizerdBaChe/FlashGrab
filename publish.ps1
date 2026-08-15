<#
.SYNOPSIS
  Produces the FlashGrab release artifact in publish\dist\.

  FlashGrab-Portable.exe : self-contained, compressed. Needs NOTHING installed —
  no .NET runtime, no prerequisites. Download, double-click, done.

  ONLY ONE ARTIFACT IS PRODUCED, deliberately. The earlier build also emitted a
  ~24 MB framework-dependent FlashGrab.exe, and two similarly named files sitting
  side by side in dist\ is a trap: the README can label one "(Recommended)", but
  the README is not in the folder. Hand someone the raw dist\ over USB or a file
  share and the small one gives them "you must install .NET Desktop Runtime" —
  on a folder that carried no way to tell the two apart. The ~75 MB is the price
  of a folder that cannot be picked wrong; it cannot be trimmed away (WinForms
  supports neither trimming nor NativeAOT).

  Pass -FrameworkDependent to build the small variant for local testing. It is
  written to publish\_fd\ and is NOT copied into dist\ or hashed — if you want
  to give it to someone, you have to go and get it on purpose.

  AI models are never bundled (Tier 2 is capability-detected / pluggable); the
  executable carries the app itself only.
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "publish\dist"
$fdOut = Join-Path $root "publish\_fd"
$scOut = Join-Path $root "publish\_sc"

Write-Host "== 清理舊輸出 ==" -ForegroundColor Cyan
Remove-Item $dist, $fdOut, $scOut -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "== 建置自含壓縮單檔 ==" -ForegroundColor Cyan
dotnet publish -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $scOut
Copy-Item (Join-Path $scOut "FlashGrab.exe") (Join-Path $dist "FlashGrab-Portable.exe") -Force

if ($FrameworkDependent) {
    Write-Host "== 額外建置 framework-dependent(僅供本機測試,不進 dist) ==" -ForegroundColor DarkYellow
    dotnet publish -c $Configuration -r $Runtime --self-contained false `
        -p:PublishSingleFile=true -p:DebugType=none -o $fdOut
    Write-Host "   -> $fdOut\FlashGrab.exe(需要 .NET 8 Desktop Runtime,不要轉發)" -ForegroundColor DarkYellow
}

Write-Host "`n== 發行成品(publish\dist) ==" -ForegroundColor Green
Get-ChildItem $dist -Filter *.exe | Select-Object Name,
    @{N='MB';E={ [math]::Round($_.Length / 1MB, 1) }} | Format-Table -AutoSize

# The artifact is not Authenticode-signed and cannot cheaply be: a traditional
# OV/EV certificate costs more per year than this tool is worth. Publishing the
# hash is the substitute — send this file by a channel where YOU are
# authenticated, so the recipient is trusting you and not the file.
$hashFile = Join-Path $dist "SHA256SUMS.txt"
Get-ChildItem $dist -Filter *.exe | ForEach-Object {
    $h = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    "$h  $($_.Name)"
} | Set-Content -Path $hashFile -Encoding utf8

Write-Host "== SHA-256 (publish\dist\SHA256SUMS.txt) ==" -ForegroundColor Green
Get-Content $hashFile

Write-Host "`n這個 exe 沒有數位簽章。收到的人第一次執行會看到藍色的" -ForegroundColor Yellow
Write-Host "「Windows 已保護您的電腦」,要按「其他資訊」->「仍要執行」。" -ForegroundColor Yellow
Write-Host "請在給檔案的同一則訊息裡先講這件事,並附上上面的雜湊值:" -ForegroundColor Yellow
Write-Host "  Get-FileHash .\FlashGrab-Portable.exe -Algorithm SHA256" -ForegroundColor Yellow
