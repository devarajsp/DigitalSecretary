<#
.SYNOPSIS
  Builds the single-file HTML user manual by embedding the screenshots as base64 data URIs.
.DESCRIPTION
  1. (Re)generate screenshots:  dotnet run --project tools/DocShots
  2. Build the manual:          ./tools/build-user-manual.ps1
  Output: docs/user-guide/DigitalSecretary-User-Manual.html  (open by double-click; print to PDF).
#>
[CmdletBinding()]
param()

$root     = Split-Path $PSScriptRoot -Parent          # repo root
$template = Join-Path $PSScriptRoot 'user-manual.template.html'
$imgDir   = Join-Path $root 'docs\user-guide\images'
$outFile  = Join-Path $root 'docs\user-guide\DigitalSecretary-User-Manual.html'

# Read as UTF-8 explicitly. Windows PowerShell 5.1's Get-Content defaults to ANSI, which would
# mojibake the template's typographic characters (arrows, dashes, dots) into "Â", "â€", etc.
$html = [System.IO.File]::ReadAllText($template, [System.Text.Encoding]::UTF8)

# Replace each {{name}} token with a data URI built from images/name.png
$tokens = [regex]::Matches($html, '\{\{([a-z0-9\-]+)\}\}') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($name in $tokens) {
    $png = Join-Path $imgDir "$name.png"
    if (-not (Test-Path $png)) { throw "Missing screenshot: $png (run tools/DocShots first)" }
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($png))
    $html = $html.Replace("{{$name}}", "data:image/png;base64,$b64")
}

if ($html -match '\{\{[a-z0-9\-]+\}\}') { throw "Unresolved tokens remain in the manual." }

[IO.File]::WriteAllText($outFile, $html, [Text.UTF8Encoding]::new($false))

# --- Cleanliness gate: fail loudly if the manual has encoding artifacts (mojibake) ---
# These are UTF-8 lead bytes misread as ANSI (the classic "A-with-circumflex" / "a-tilde" corruption).
# Built from code points so THIS script stays pure-ASCII and is itself immune to the same bug.
$verify    = [System.IO.File]::ReadAllText($outFile, [System.Text.Encoding]::UTF8)
$markers   = @([char]0x00C2, [char]0x00C3, [char]0x00E2)   # the tell-tale mojibake lead-byte glyphs
$artifacts = $markers | Where-Object { $verify.Contains([string]$_) }
if ($artifacts.Count -gt 0) {
    $codes = ($artifacts | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ', '
    Write-Host "CLEANLINESS CHECK FAILED: encoding artifacts found in $outFile ($codes)." -ForegroundColor Red
    Write-Host "Cause is almost always a non-UTF-8 read/write (PowerShell 5.1 defaults to ANSI)." -ForegroundColor Red
    exit 1
}

$kb = [math]::Round((Get-Item $outFile).Length / 1KB, 0)
Write-Host "Wrote $outFile ($kb KB, self-contained). Cleanliness check PASSED." -ForegroundColor Green
exit 0
