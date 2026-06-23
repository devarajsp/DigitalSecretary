<#
.SYNOPSIS
    Standard build + quality gate for Digital Secretary.
.DESCRIPTION
    Publishes the coding standards, builds the solution (running static analysis), optionally runs
    unit tests (with coverage) and QA automation, and ALWAYS shows + writes:
      * a static code-analysis report (analyzer findings grouped by rule), and
      * a code-coverage report (overall + per feature),
    to the console and to docs/QUALITY_REPORT.md.

    Examples:
      ./build.ps1                  # build, then ASK whether to run unit tests / QA
      ./build.ps1 -All             # build + unit tests + QA (no prompts)
      ./build.ps1 -Test            # build + unit tests only
      ./build.ps1 -Qa              # build + QA only
      ./build.ps1 -NoPrompt        # build only, skip tests without asking
      ./build.ps1 -All -WarnAsError -MinCoverage 80
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$Test,
    [switch]$Qa,
    [switch]$All,
    [switch]$NoPrompt,
    [switch]$WarnAsError,
    [double]$MinCoverage = 0
)

$root      = $PSScriptRoot
$sln       = Join-Path $root 'DigitalSecretary.sln'
$stateFile = Join-Path $root 'artifacts\quality-state.json'

function Write-Section($title) {
    Write-Host ''
    Write-Host ('=' * 72) -ForegroundColor DarkCyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host ('=' * 72) -ForegroundColor DarkCyan
}

function Parse-TestSummary($lines) {
    $row = $lines | Select-String -Pattern 'Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)' | Select-Object -Last 1
    if (-not $row) { return $null }
    $m = $row.Matches[0]
    return [pscustomobject]@{
        Failed = [int]$m.Groups[1].Value; Passed = [int]$m.Groups[2].Value
        Skipped = [int]$m.Groups[3].Value; Total = [int]$m.Groups[4].Value
    }
}

function Friendly-Name($assembly) {
    switch ($assembly) {
        'DigitalSecretary'              { 'Host (DigitalSecretary.App)'; break }
        'DigitalSecretary.Abstractions' { 'Abstractions (contract)'; break }
        default { $assembly -replace '^DigitalSecretary\.Features\.', 'Feature: ' }
    }
}

function Get-Coverage($coberturaPath) {
    [xml]$xml = Get-Content $coberturaPath
    $cov = $xml.coverage
    $overall = [pscustomobject]@{
        Pct = [math]::Round([double]$cov.'line-rate' * 100, 1)
        Covered = [int]$cov.'lines-covered'; Total = [int]$cov.'lines-valid'
    }
    $features = foreach ($pkg in $cov.packages.package) {
        $c = 0; $t = 0
        foreach ($cls in $pkg.classes.class) {
            foreach ($ln in $cls.lines.line) { $t++; if ([int]$ln.hits -gt 0) { $c++ } }
        }
        $pct = if ($t -gt 0) { [math]::Round($c / $t * 100, 1) } else { $null }
        [pscustomobject]@{ Name = Friendly-Name $pkg.name; Pct = $pct; Covered = $c; Total = $t }
    }
    return [pscustomobject]@{ Overall = $overall; Features = ($features | Sort-Object Name) }
}

# --------------------------------------------------------------------------
# 1. Publish the coding standards on every build.
# --------------------------------------------------------------------------
Write-Section 'Coding standards  (full text: docs/CODING_STANDARDS.md)'
Write-Host @'
  1. Logic lives in plain, testable classes - NOT inside WinForms controls.
  2. Every logic method ships with unit tests (xUnit + FluentAssertions).
  3. Features are independent: depend only on Abstractions; persist only under
     IFeatureContext.DataDirectory; never reference another feature.
  4. The host never references a feature. New feature = new project under
     src/Features + plugin.json + FEATURE.md (see docs/ADDING_A_FEATURE.md).
  5. Keep static analysis clean - fix analyzer warnings, do not blanket-suppress.
  6. Run this script before committing; keep unit + QA green and coverage on target.
  7. No real PII - use placeholder data (names/emails/paths) and seed DocShots for screenshots;
     the secret/PII gate enforces it. See docs/CODING_STANDARDS.md section 9.
'@ -ForegroundColor Gray

if ($All) { $Test = $true; $Qa = $true }
if (-not $Test -and -not $Qa -and -not $NoPrompt) {
    if ((Read-Host "`nRun UNIT tests with coverage? (y/N)") -match '^(y|yes)$') { $Test = $true }
    if ((Read-Host "Run QA automation (plugin pipeline)? (y/N)") -match '^(y|yes)$') { $Qa = $true }
}

# --------------------------------------------------------------------------
# 2. Build (static analysis runs here). --no-incremental so analyzers always emit.
# --------------------------------------------------------------------------
Write-Section "Build  ($Configuration)"
$buildArgs = @('build', $sln, '-c', $Configuration, '--nologo', '--no-incremental')
if ($WarnAsError) { $buildArgs += '-warnaserror' }
$buildOut = & dotnet @buildArgs 2>&1
$buildOut | ForEach-Object { Write-Host $_ }
$buildOk = $LASTEXITCODE -eq 0

# --- Static analysis report: parse + de-duplicate analyzer findings ---
$seen = @{}
foreach ($line in $buildOut) {
    $s = ([string]$line).Trim()
    if ($s -match ':\s+(warning|error)\s+[A-Za-z]+\d+:') { $seen[$s] = $true }
}
$diagnostics = foreach ($s in $seen.Keys) {
    $m = [regex]::Match($s, ':\s+(warning|error)\s+([A-Za-z]+\d+):\s+(.+)$')
    if ($m.Success) {
        [pscustomobject]@{
            Severity = $m.Groups[1].Value
            Code     = $m.Groups[2].Value
            Message  = ($m.Groups[3].Value -replace '\s*\[[^\]]+\]\s*$', '').Trim()
        }
    }
}
$diagnostics = @($diagnostics | Where-Object { $_ })   # normalize to an array for reliable .Count
$diagCount = $diagnostics.Count
$diagGroups = $diagnostics | Group-Object Code | ForEach-Object {
    [pscustomobject]@{ Code = $_.Name; Severity = $_.Group[0].Severity; Count = $_.Count; Example = $_.Group[0].Message }
} | Sort-Object @{Expression='Severity';Descending=$true}, Code
$warnings = @($diagnostics | Where-Object Severity -eq 'warning').Count
$errors   = @($diagnostics | Where-Object Severity -eq 'error').Count

Write-Section 'Static code analysis report'
if ($diagCount -eq 0) {
    Write-Host '  No analyzer findings - clean.' -ForegroundColor Green
} else {
    Write-Host ("  {0} finding(s): {1} warning(s), {2} error(s)" -f $diagCount, $warnings, $errors) -ForegroundColor Yellow
    Write-Host ('  {0,-9} {1,-8} {2,-6} {3}' -f 'Severity','Rule','Count','Example')
    Write-Host ('  ' + ('-' * 64))
    foreach ($g in $diagGroups) {
        Write-Host ('  {0,-9} {1,-8} {2,-6} {3}' -f $g.Severity, $g.Code, $g.Count, ($g.Example.Substring(0, [Math]::Min(40, $g.Example.Length))))
    }
}

# --------------------------------------------------------------------------
# 3. Unit tests + coverage.
# --------------------------------------------------------------------------
$unit = [pscustomobject]@{ Run = $false; Summary = $null }
if ($Test -and $buildOk) {
    Write-Section 'Unit tests + code coverage'
    $covDir = Join-Path $root 'artifacts\coverage'
    if (Test-Path $covDir) { Remove-Item $covDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $covDir | Out-Null

    $utProj = Join-Path $root 'tests\DigitalSecretary.UnitTests\DigitalSecretary.UnitTests.csproj'
    $runSettings = Join-Path $root 'tests\coverage.runsettings'
    $out = & dotnet test $utProj -c $Configuration --no-build --nologo --collect:"XPlat Code Coverage" --settings $runSettings --results-directory $covDir 2>&1
    $out | ForEach-Object { Write-Host $_ }
    $unit.Run = $true
    $unit.Summary = Parse-TestSummary $out
}

# --------------------------------------------------------------------------
# 4. QA automation (plugin pipeline).
# --------------------------------------------------------------------------
$qaResult = [pscustomobject]@{ Run = $false; Summary = $null }
if ($Qa -and $buildOk) {
    Write-Section 'QA automation  (loads every feature DLL like the app does)'
    $qaProj = Join-Path $root 'tests\DigitalSecretary.QaTests\DigitalSecretary.QaTests.csproj'
    $out = & dotnet test $qaProj -c $Configuration --no-build --nologo 2>&1
    $out | ForEach-Object { Write-Host $_ }
    $qaResult.Run = $true
    $qaResult.Summary = Parse-TestSummary $out
}

# --------------------------------------------------------------------------
# 5. Resolve coverage (fresh this run, else last measured snapshot).
# --------------------------------------------------------------------------
$coverage = $null; $coverageWhen = $null; $coverageFresh = $false
$cob = Get-ChildItem (Join-Path $root 'artifacts\coverage') -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($unit.Run -and $cob) {
    $coverage = Get-Coverage $cob.FullName
    $coverageWhen = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'); $coverageFresh = $true
    New-Item -ItemType Directory -Force -Path (Split-Path $stateFile) | Out-Null
    [pscustomobject]@{ MeasuredAt = $coverageWhen; Overall = $coverage.Overall; Features = $coverage.Features } |
        ConvertTo-Json -Depth 6 | Set-Content -Path $stateFile -Encoding UTF8
} elseif (Test-Path $stateFile) {
    $snap = Get-Content $stateFile -Raw | ConvertFrom-Json
    $coverage = [pscustomobject]@{ Overall = $snap.Overall; Features = $snap.Features }
    $coverageWhen = $snap.MeasuredAt
}

Write-Section 'Code coverage report  (overall + per feature)'
if ($coverage) {
    $tag = if ($coverageFresh) { 'measured now' } else { "last measured $coverageWhen" }
    Write-Host ("  OVERALL: {0}%  ({1}/{2} lines covered)   [{3}]" -f `
        $coverage.Overall.Pct, $coverage.Overall.Covered, $coverage.Overall.Total, $tag) -ForegroundColor Green
    Write-Host ''
    Write-Host ('  {0,-32} {1,8}   {2}' -f 'Module / Feature', 'Coverage', 'Lines')
    Write-Host ('  ' + ('-' * 60))
    foreach ($f in $coverage.Features) {
        $pctText = if ($null -ne $f.Pct) { "$($f.Pct)%" } else { 'n/a' }
        $lines   = if ($f.Total -gt 0) { "$($f.Covered)/$($f.Total)" } else { 'no executable lines' }
        Write-Host ('  {0,-32} {1,8}   {2}' -f $f.Name, $pctText, $lines)
    }
} else {
    Write-Host '  Coverage not measured yet. Run: ./build.ps1 -Test' -ForegroundColor Yellow
}

# --------------------------------------------------------------------------
# 6. Docs & traceability consistency (always; fast, no app needed).
# --------------------------------------------------------------------------
Write-Section 'Docs & traceability consistency'
$docs = [pscustomobject]@{ Run = $false; Errors = 0; Warnings = 0 }
$checkScript = Join-Path $root 'tools\docgen\check_docs.py'
if ((Get-Command python -ErrorAction SilentlyContinue) -and (Test-Path $checkScript)) {
    $docsOut = & python $checkScript 2>&1
    $docsOut | ForEach-Object { Write-Host $_ }
    $docs.Run = $true
    $rm = $docsOut | Select-String -Pattern 'RESULT errors=(\d+) warnings=(\d+)' | Select-Object -Last 1
    if ($rm) { $docs.Errors = [int]$rm.Matches[0].Groups[1].Value; $docs.Warnings = [int]$rm.Matches[0].Groups[2].Value }
} else {
    Write-Host '  Skipped (python or tools/docgen/check_docs.py not found).' -ForegroundColor Yellow
}

# --------------------------------------------------------------------------
# 6b. Secret / PII scan (always; fast). No emails, passwords, tokens, keys, or PII in any artifact.
# --------------------------------------------------------------------------
Write-Section 'Secret / PII scan'
$secrets = [pscustomobject]@{ Run = $false; Findings = 0 }
$secretScript = Join-Path $root 'tools\check_secrets.py'
if ((Get-Command python -ErrorAction SilentlyContinue) -and (Test-Path $secretScript)) {
    $secOut = & python $secretScript 2>&1
    $secOut | ForEach-Object { Write-Host $_ }
    $secrets.Run = $true
    $sm = $secOut | Select-String -Pattern 'RESULT findings=(\d+)' | Select-Object -Last 1
    if ($sm) { $secrets.Findings = [int]$sm.Matches[0].Groups[1].Value }
} else {
    Write-Host '  Skipped (python or tools/check_secrets.py not found).' -ForegroundColor Yellow
}

# --------------------------------------------------------------------------
# 7. Verdict + quality report (always written).
# --------------------------------------------------------------------------
$testsFailed = ($unit.Summary -and $unit.Summary.Failed -gt 0) -or ($qaResult.Summary -and $qaResult.Summary.Failed -gt 0)
$coverageOk  = (-not ($coverageFresh -and $MinCoverage -gt 0)) -or ($coverage.Overall.Pct -ge $MinCoverage)
$overallOk   = $buildOk -and ($errors -eq 0) -and -not $testsFailed -and $coverageOk -and ($docs.Errors -eq 0) -and ($secrets.Findings -eq 0)
$verdict     = if ($overallOk) { 'PASS' } else { 'FAIL' }

function Fmt($summary) {
    if (-not $summary) { return 'not run' }
    return "$($summary.Passed)/$($summary.Total) passed, $($summary.Failed) failed, $($summary.Skipped) skipped"
}

$now = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$rep = New-Object System.Collections.Generic.List[string]
$rep.Add("# Quality Report")
$rep.Add('')
$rep.Add("_Generated $now by build.ps1 ($Configuration)._")
$rep.Add('')
$rep.Add("## Verdict: $verdict")
$rep.Add('')
$rep.Add('| Gate | Result |')
$rep.Add('|------|--------|')
$rep.Add("| Build | $(if ($buildOk) {'succeeded'} else {'**FAILED**'}) |")
$rep.Add("| Static analysis | $warnings warning(s), $errors error(s) |")
$rep.Add("| Unit tests | $(Fmt $unit.Summary) |")
$rep.Add("| Code coverage (overall) | $(if ($coverage) { "$($coverage.Overall.Pct)%" } else { 'n/a' }) |")
$rep.Add("| QA automation | $(Fmt $qaResult.Summary) |")
$rep.Add("| Docs & traceability | $(if (-not $docs.Run) { 'skipped' } elseif ($docs.Errors -eq 0) { "consistent ($($docs.Warnings) warning(s))" } else { "**$($docs.Errors) error(s)**" }) |")
$rep.Add("| Secret / PII scan | $(if (-not $secrets.Run) { 'skipped' } elseif ($secrets.Findings -eq 0) { 'clean' } else { "**$($secrets.Findings) finding(s)**" }) |")
$rep.Add('')

$rep.Add('## Static code analysis report')
$rep.Add('')
if ($diagCount -eq 0) {
    $rep.Add('No analyzer findings - the build is clean. (Analyzers: .NET `latest-recommended`; see docs/STATIC_ANALYSIS.md.)')
} else {
    $rep.Add("$diagCount finding(s): **$warnings warning(s), $errors error(s)**.")
    $rep.Add('')
    $rep.Add('| Severity | Rule | Count | Example |')
    $rep.Add('|----------|------|-------|---------|')
    foreach ($g in $diagGroups) { $rep.Add("| $($g.Severity) | $($g.Code) | $($g.Count) | $($g.Example) |") }
}
$rep.Add('')

$rep.Add('## Code coverage report')
$rep.Add('')
if ($coverage) {
    $tag = if ($coverageFresh) { 'measured this run' } else { "last measured $coverageWhen" }
    $rep.Add("**Overall: $($coverage.Overall.Pct)%** ($($coverage.Overall.Covered)/$($coverage.Overall.Total) lines) - _$($tag)_.")
    $rep.Add('')
    $rep.Add('| Module / Feature | Line coverage | Lines covered |')
    $rep.Add('|------------------|---------------|---------------|')
    foreach ($f in $coverage.Features) {
        $pctText = if ($null -ne $f.Pct) { "$($f.Pct)%" } else { 'n/a' }
        $lines   = if ($f.Total -gt 0) { "$($f.Covered)/$($f.Total)" } else { 'no executable lines' }
        $rep.Add("| $($f.Name) | $pctText | $lines |")
    }
    $rep.Add('')
    $rep.Add('_Coverage tracks testable logic; UI/bootstrap/loader/IMAP are excluded (see tests/coverage.runsettings) and validated by QA automation._')
} else {
    $rep.Add('Coverage not measured yet. Run `./build.ps1 -Test` or `-All`.')
}
$rep.Add('')
$rep.Add('## Docs & traceability consistency report')
$rep.Add('')
if (-not $docs.Run) {
    $rep.Add('Skipped (Python not available).')
} elseif ($docs.Errors -eq 0) {
    $rep.Add("All artifacts consistent - every traceability reference exists, every feature has its full doc set, requirement IDs match, coverage has no gaps, generated text is clean. ($($docs.Warnings) warning(s).)")
} else {
    $rep.Add("**$($docs.Errors) consistency error(s)** found - see console output / run ``python tools/docgen/check_docs.py``.")
}
$rep.Add('')
$rep.Add('## Secret / PII scan')
$rep.Add('')
if (-not $secrets.Run) {
    $rep.Add('Skipped (Python not available).')
} elseif ($secrets.Findings -eq 0) {
    $rep.Add('No secrets or personal information (emails, passwords, tokens, keys, SSN/credit-card) found in any tracked artifact.')
} else {
    $rep.Add("**$($secrets.Findings) secret/PII finding(s)** - see console / run ``python tools/check_secrets.py``. Remove before committing.")
}
$rep.Add('')
$rep.Add('## Notes')
$rep.Add('- Standards: `docs/CODING_STANDARDS.md` - Testing/QA: `docs/TESTING.md` - Analysis: `docs/STATIC_ANALYSIS.md`.')
$rep.Add('- Regenerate any time with `./build.ps1 -All`.')

$reportPath = Join-Path $root 'docs\QUALITY_REPORT.md'
New-Item -ItemType Directory -Force -Path (Split-Path $reportPath) | Out-Null
($rep -join "`r`n") | Set-Content -Path $reportPath -Encoding UTF8

# --------------------------------------------------------------------------
# Console summary.
# --------------------------------------------------------------------------
Write-Section "Quality summary  ->  $verdict"
$color = if ($overallOk) { 'Green' } else { 'Red' }
Write-Host ("  Build .............. {0}" -f $(if ($buildOk) {'succeeded'} else {'FAILED'}))
Write-Host ("  Static analysis .... {0} warning(s), {1} error(s)" -f $warnings, $errors)
Write-Host ("  Unit tests ......... {0}" -f (Fmt $unit.Summary))
Write-Host ("  Coverage (overall) . {0}" -f $(if ($coverage) { "$($coverage.Overall.Pct)%" } else { 'n/a' }))
Write-Host ("  QA automation ...... {0}" -f (Fmt $qaResult.Summary))
Write-Host ("  Docs & traceability  {0}" -f $(if (-not $docs.Run) { 'skipped' } elseif ($docs.Errors -eq 0) { "consistent ($($docs.Warnings) warning(s))" } else { "$($docs.Errors) error(s)" }))
Write-Host ("  Secret / PII scan .. {0}" -f $(if (-not $secrets.Run) { 'skipped' } elseif ($secrets.Findings -eq 0) { 'clean' } else { "$($secrets.Findings) finding(s)" }))
Write-Host ("  Report ............. docs/QUALITY_REPORT.md")
Write-Host ''
Write-Host "  VERDICT: $verdict" -ForegroundColor $color
Write-Host ''

if (-not $overallOk) { exit 1 }
exit 0
