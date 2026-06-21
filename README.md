# Digital Secretary

A pluggable personal-assistant app for Windows. Each capability is an independent **feature
plugin** that the app discovers on disk and loads only when you open it — so the app stays fast,
and new features can be added without touching the core.

> Built with .NET 9 + WinForms. Successor to the earlier "SPD Personal App".

[![CI](__REPO_URL__/actions/workflows/ci.yml/badge.svg)](__REPO_URL__/actions/workflows/ci.yml)
&nbsp;**Showcase:** __PAGES_URL__ &nbsp;·&nbsp; **User manual:** [open it](docs/user-guide/DigitalSecretary-User-Manual.html)

## Features included
| Feature | Category | What it does |
|---------|----------|--------------|
| **Launcher** | Tools | One-click launch favorite apps, files, folders, and links. |
| **Calculator** | Tools | Keypad + type-an-expression calculator. |
| **Clipboard History** | Tools | Re-paste text you copied earlier. |
| **Download Emails** | Communication | Download a local copy of all Yahoo Mail folders (emails as `.txt` + `.eml`, with attachments). |

## How it works (1-minute version)
- The app shell shows a **menu** (Features → Category → Feature), a **Home dashboard** of tiles,
  and a content area.
- Each feature lives in its own project and is compiled to a DLL placed under `plugins/<feature>/`
  next to the app, alongside a small `plugin.json` describing it.
- On startup the app reads only the `plugin.json` files (cheap) to build the menus and dashboard.
- A feature's code is **loaded the first time you open it**, in an isolated load context that
  carries the feature's own dependencies.
- **View → Configure Features…** lets you choose — per feature, independently — whether it appears
  on the Home dashboard and/or in the Features menu.

## Build & run
Requires the .NET 9 SDK.
```bash
dotnet build DigitalSecretary.sln -c Debug
# then run:
src/DigitalSecretary.App/bin/Debug/net9.0-windows/DigitalSecretary.exe
```

Or use the quality-gated build (recommended):
```powershell
./build.ps1            # builds, then asks whether to run unit tests / QA automation
./build.ps1 -All       # build + unit tests + QA + coverage + quality report
```

## Quality
Every build runs **static analysis** (.NET analyzers). `build.ps1` also runs **unit tests**
(xUnit, with coverage) and **QA automation** (loads every feature plugin exactly like the app),
then writes [`docs/QUALITY_REPORT.md`](docs/QUALITY_REPORT.md) with the verdict, test results, and
coverage. Current status: build clean (0 warnings), unit + QA green, ~88% logic coverage.

See [`docs/CODING_STANDARDS.md`](docs/CODING_STANDARDS.md), [`docs/TESTING.md`](docs/TESTING.md),
and [`docs/STATIC_ANALYSIS.md`](docs/STATIC_ANALYSIS.md).

### Automated gates (hooks + CI)
- **Pre-commit hook** — runs a fast gate (build + static analysis + docs/traceability consistency) so
  broken or drifted changes can't be committed. Enable once per clone:
  ```powershell
  ./tools/setup-hooks.ps1      # or: git config core.hooksPath .githooks
  ```
  Bypass a single commit with `git commit --no-verify`.
- **CI** ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) — runs the full
  `build.ps1 -All -WarnAsError -MinCoverage 80` on every push/PR to `main`.

## Repository layout
```
DigitalSecretary.sln
build.ps1                 ← quality-gated build (standards + tests + QA + report)
CLAUDE.md                 ← how to develop this app with Claude
README.md
.editorconfig             ← code style + analyzer severities
Directory.Build.props     ← repo-wide build + static-analysis settings
docs/
  ARCHITECTURE.md         ← deep dive on the plugin host
  ADDING_A_FEATURE.md     ← step-by-step recipe to add a feature (incl. tests)
  FEATURE_TEMPLATE.md     ← copy-paste skeleton
  CODING_STANDARDS.md     ← the rules (printed on every build)
  TESTING.md              ← unit tests, QA automation, coverage
  STATIC_ANALYSIS.md      ← analyzers & .editorconfig
  QUALITY_REPORT.md       ← generated each build
src/
  DigitalSecretary.Abstractions/   the feature contract
  DigitalSecretary.App/            the host (shell, loader, dashboard, settings)
  Features/<Name>/                 one folder per feature
tests/
  DigitalSecretary.UnitTests/      unit tests for logic (+ coverage)
  DigitalSecretary.QaTests/        QA automation: loads every feature plugin
  coverage.runsettings             coverage scope (logic, not UI)
```

## Where your data lives
`%APPDATA%\DigitalSecretary\`
- `app-settings.json` — host preferences (dashboard show/hide).
- `data\<feature-id>\…` — each feature's private data.

## Adding a feature
Ask Claude, or follow [`docs/ADDING_A_FEATURE.md`](docs/ADDING_A_FEATURE.md). You don't edit the
host — drop a new feature project in, build, and it appears automatically.
