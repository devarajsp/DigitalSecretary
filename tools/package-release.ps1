<#
.SYNOPSIS
  Build a self-contained, portable Windows release of Digital Secretary (no .NET install needed).
.DESCRIPTION
  Produces release/DigitalSecretary-v<Version>-<Runtime>.zip containing the app (self-contained),
  its plugins, the user manual, and a lightweight Install/Uninstall. The zip is a GitHub Release
  asset (binaries are gitignored, not committed).
.PARAMETER Version    Release version (default 2.1.0).
.PARAMETER Runtime    RID (default win-x64).
#>
[CmdletBinding()]
param(
    [string]$Version = "2.1.0",
    [string]$Runtime = "win-x64"
)

$repo  = Split-Path $PSScriptRoot -Parent
$sln   = Join-Path $repo "DigitalSecretary.sln"
$relDir = Join-Path $repo "release"
$stage  = Join-Path $repo "artifacts\release-stage"
$appOut = Join-Path $stage "DigitalSecretary"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $appOut, $relDir | Out-Null

Write-Host "1/4  Building solution (Release)..." -ForegroundColor Cyan
dotnet build $sln -c Release --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Release build failed."; exit 1 }

Write-Host "2/4  Publishing self-contained host ($Runtime)..." -ForegroundColor Cyan
dotnet publish (Join-Path $repo "src\DigitalSecretary.App\DigitalSecretary.App.csproj") `
    -c Release -r $Runtime --self-contained true `
    -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=en `
    --nologo -o $appOut | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed."; exit 1 }

Write-Host "3/4  Assembling plugins + docs + installer..." -ForegroundColor Cyan
$pluginsSrc = Join-Path $repo "src\DigitalSecretary.App\bin\Release\net9.0-windows\plugins"
Copy-Item $pluginsSrc (Join-Path $appOut "plugins") -Recurse -Force
Copy-Item (Join-Path $repo "docs\user-guide\DigitalSecretary-User-Manual.html") (Join-Path $appOut "User-Manual.html") -Force

# README.txt
@"
Digital Secretary v$Version  ($Runtime, self-contained - no .NET install needed)

PORTABLE USE
  1. Unzip this folder anywhere you like.
  2. Double-click DigitalSecretary.exe.

INSTALL (adds Start Menu + Desktop shortcuts)
  1. Double-click Install.cmd (or right-click -> Run).
  2. Launch "Digital Secretary" from the Start Menu.
  To remove later, run Uninstall.cmd.

User manual: open User-Manual.html.
Your data is stored in %APPDATA%\DigitalSecretary and is kept if you uninstall.
"@ | Set-Content (Join-Path $appOut "README.txt") -Encoding UTF8

# Install.ps1
@'
$ErrorActionPreference = "Stop"
$src  = $PSScriptRoot
$dest = Join-Path $env:LOCALAPPDATA "Programs\DigitalSecretary"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Get-ChildItem $src -Exclude "Install.cmd","Install.ps1","Uninstall.cmd","Uninstall.ps1" |
    Copy-Item -Destination $dest -Recurse -Force
$exe = Join-Path $dest "DigitalSecretary.exe"
$ws  = New-Object -ComObject WScript.Shell
foreach ($dir in @([Environment]::GetFolderPath("Programs"), [Environment]::GetFolderPath("Desktop"))) {
    $lnk = $ws.CreateShortcut((Join-Path $dir "Digital Secretary.lnk"))
    $lnk.TargetPath = $exe; $lnk.WorkingDirectory = $dest; $lnk.Save()
}
Write-Host "Installed to $dest. Launch 'Digital Secretary' from the Start Menu." -ForegroundColor Green
'@ | Set-Content (Join-Path $appOut "Install.ps1") -Encoding UTF8

# Uninstall.ps1
@'
$dest = Join-Path $env:LOCALAPPDATA "Programs\DigitalSecretary"
foreach ($dir in @([Environment]::GetFolderPath("Programs"), [Environment]::GetFolderPath("Desktop"))) {
    $lnk = Join-Path $dir "Digital Secretary.lnk"; if (Test-Path $lnk) { Remove-Item $lnk -Force }
}
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Write-Host "Uninstalled. Your data in %APPDATA%\DigitalSecretary was left intact." -ForegroundColor Green
'@ | Set-Content (Join-Path $appOut "Uninstall.ps1") -Encoding UTF8

'@echo off'                                                              | Set-Content (Join-Path $appOut "Install.cmd") -Encoding ASCII
'powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"' | Add-Content (Join-Path $appOut "Install.cmd") -Encoding ASCII
'pause'                                                                  | Add-Content (Join-Path $appOut "Install.cmd") -Encoding ASCII
'@echo off'                                                                | Set-Content (Join-Path $appOut "Uninstall.cmd") -Encoding ASCII
'powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"' | Add-Content (Join-Path $appOut "Uninstall.cmd") -Encoding ASCII
'pause'                                                                    | Add-Content (Join-Path $appOut "Uninstall.cmd") -Encoding ASCII

Write-Host "4/4  Zipping..." -ForegroundColor Cyan
$zip = Join-Path $relDir "DigitalSecretary-v$Version-$Runtime.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $appOut -DestinationPath $zip

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "Built: $zip  ($mb MB)" -ForegroundColor Green
Write-Host "Contents: DigitalSecretary.exe + plugins/ + User-Manual.html + Install/Uninstall + README.txt"
