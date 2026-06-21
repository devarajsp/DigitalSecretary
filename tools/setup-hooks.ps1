<#
.SYNOPSIS
  Activate the versioned git hooks for this repo (run once per clone).
.DESCRIPTION
  Points git at the tracked .githooks folder so the pre-commit quality gate runs on every commit.
#>
$repo = Split-Path $PSScriptRoot -Parent
git -C $repo config core.hooksPath .githooks
Write-Host "core.hooksPath set to .githooks. The pre-commit gate is now active." -ForegroundColor Green
Write-Host "Bypass a single commit with: git commit --no-verify" -ForegroundColor Gray
