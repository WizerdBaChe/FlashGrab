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
# Write-Host, not Format-Table: Format-Table emits format-descriptor objects
# into the success stream, and only an interactive host's default formatter
# turns those into the "Name / MB" table a person sees. Any non-interactive
# invocation - captured/redirected stdout, CI, a wrapper script - never reaches
# that formatter, so the objects fall back to ToString() and the operator gets
# bare type names (Microsoft.PowerShell.Commands.Internal.Format.FormatStartData
# and friends) instead of a filename and a size - at exactly the point where
# they would check what is about to be handed to someone else. Every other
# operator-facing line in this script already goes through Write-Host; this one
# has to as well, built as plain text ourselves.
Get-ChildItem $dist -Filter *.exe | ForEach-Object {
    Write-Host ("  {0}  ({1} MB)" -f $_.Name, [math]::Round($_.Length / 1MB, 1))
}

# The artifact is not Authenticode-signed and cannot cheaply be: a traditional
# OV/EV certificate costs more per year than this tool is worth. Publishing the
# hash is the substitute — send this file by a channel where YOU are
# authenticated, so the recipient is trusting you and not the file.
$hashFile = Join-Path $dist "SHA256SUMS.txt"
$hashLines = Get-ChildItem $dist -Filter *.exe | ForEach-Object {
    $h = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    "$h  $($_.Name)"
}
# UTF-8 WITHOUT a BOM, LF line endings only. This file exists to be verified by
# the RECIPIENT, quite possibly with `sha256sum -c` under Git Bash/WSL/Linux/
# macOS rather than Get-FileHash on Windows. `Set-Content -Encoding utf8` on
# Windows PowerShell 5.1 writes a BOM, and Set-Content's default line ending on
# Windows is CRLF; GNU coreutils sha256sum treats the BOM as part of the first
# hash and rejects the trailing CR on every line, so a BOM+CRLF file fails to
# verify with "no properly formatted SHA256 checksum lines found" - the exact
# cross-checking this file is here to enable. Building the text ourselves and
# writing it with an explicit no-BOM UTF8Encoding sidesteps both, identically on
# Windows PowerShell 5.1 and PowerShell 7.
$hashText = ($hashLines -join "`n") + "`n"
[System.IO.File]::WriteAllText($hashFile, $hashText, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "== SHA-256 (publish\dist\SHA256SUMS.txt) ==" -ForegroundColor Green
Get-Content $hashFile

Write-Host "`n這個 exe 沒有數位簽章。收到的人第一次執行會看到藍色的" -ForegroundColor Yellow
Write-Host "「Windows 已保護您的電腦」,要按「其他資訊」->「仍要執行」。" -ForegroundColor Yellow
Write-Host "請在給檔案的同一則訊息裡先講這件事,並附上上面的雜湊值:" -ForegroundColor Yellow
Write-Host "  Get-FileHash .\FlashGrab-Portable.exe -Algorithm SHA256" -ForegroundColor Yellow
