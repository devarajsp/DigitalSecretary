<#
.SYNOPSIS
  Publish DigitalSecretary to GitHub: create the repo, push, enable GitHub Pages (/docs), and cut a release.
.DESCRIPTION
  Prerequisite (one-time, interactive): authenticate the GitHub CLI ->  gh auth login
  Then run:                                                              ./tools/publish-github.ps1
.PARAMETER RepoName    Repository name (default: DigitalSecretary).
.PARAMETER Visibility  public | private  (Pages needs public on a free plan).
#>
[CmdletBinding()]
param(
    [string]$RepoName = "DigitalSecretary",
    [ValidateSet("public", "private")][string]$Visibility = "public",
    [string]$Description = "A pluggable personal toolbox for Windows (.NET 9 / WinForms), built end-to-end with Claude."
)

$repo = Split-Path $PSScriptRoot -Parent

# 0. Require an authenticated gh.
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Error "GitHub CLI 'gh' not found. Install it, then run: gh auth login"; exit 1 }
$owner = (gh api user --jq .login)
if (-not $owner) { Write-Error "Not authenticated. Run: gh auth login"; exit 1 }
Write-Host "Publishing as: $owner" -ForegroundColor Cyan

$repoUrl  = "https://github.com/$owner/$RepoName"
$pagesUrl = "https://$owner.github.io/$RepoName/"

# 1. Fill URL tokens (UTF-8 in and out).
foreach ($rel in @("docs\index.html", "CHANGELOG.md", "README.md")) {
    $p = Join-Path $repo $rel
    if (Test-Path $p) {
        $t = [System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)
        $t = $t.Replace("__REPO_URL__", $repoUrl).Replace("__PAGES_URL__", $pagesUrl).Replace("__OWNER__", $owner).Replace("__REPO__", $RepoName)
        [System.IO.File]::WriteAllText($p, $t, [System.Text.UTF8Encoding]::new($false))
    }
}

# 2. Commit the finalized links (the pre-commit gate runs and must pass).
git -C $repo add -A
if ((git -C $repo status --porcelain).Length -gt 0) {
    git -C $repo commit -m "Publish: finalize GitHub Pages / repo URLs"
}

# Safety: never push commits that carry a personal (non-noreply) email.
$leaky = git -C $repo log --format='%ae%n%ce' |
    Where-Object { $_ -and ($_ -notmatch '^noreply@github\.com$') -and ($_ -notmatch '@users\.noreply\.github\.com$') } |
    Sort-Object -Unique
if ($leaky) {
    Write-Error "Refusing to push: commit(s) contain non-noreply email(s): $($leaky -join ', '). Scrub history first."
    exit 1
}

# 3. Create the repo and push (skips if origin already exists).
if (-not (git -C $repo remote)) {
    gh repo create $RepoName "--$Visibility" --source $repo --remote origin --push --description $Description
} else {
    Write-Host "Remote 'origin' exists; pushing." -ForegroundColor Yellow
    git -C $repo push -u origin main
}

# 4. Enable GitHub Pages from /docs on main (ignore if not permitted / already on).
try {
    '{"source":{"branch":"main","path":"/docs"}}' | gh api -X POST "repos/$owner/$RepoName/pages" --input - | Out-Null
    Write-Host "GitHub Pages enabled (main /docs)." -ForegroundColor Green
} catch {
    Write-Host "Pages not auto-enabled (private repo on a free plan won't allow it). Enable manually: Settings > Pages > Branch=main, Folder=/docs." -ForegroundColor Yellow
}

# 5. Build the downloadable release artifact, then create the release with it attached.
Write-Host "Building the downloadable release zip..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "package-release.ps1") -Version "2.1.0" -Runtime "win-x64"
$zip = Join-Path $repo "release\DigitalSecretary-v2.1.0-win-x64.zip"
if (-not (Test-Path $zip)) { Write-Error "Release artifact not found: $zip"; exit 1 }

gh release create v2.1.0 --repo "$owner/$RepoName" --title "Digital Secretary v2.1.0" `
    --notes-file (Join-Path $repo "CHANGELOG.md") "$zip"
Write-Host "Attached release asset: $(Split-Path $zip -Leaf)" -ForegroundColor Green

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Repo:  $repoUrl"
Write-Host "  Pages: $pagesUrl   (allow ~1 minute for first publish)"
Write-Host "  Release: $repoUrl/releases/tag/v2.1.0"
