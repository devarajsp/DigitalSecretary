# Testing & QA automation

Two layers of automated quality, plus coverage and a generated report.

## Layers
| Project | Type | What it proves |
|---------|------|----------------|
| `tests/DigitalSecretary.UnitTests` | Unit tests | Pure logic is correct (engine, file naming, attachment scanning, stores, plugin catalog, settings, context). References the production projects directly. |
| `tests/DigitalSecretary.QaTests` | QA automation (integration) | Black-box: discovers every `plugin.json`, loads each feature DLL in an isolated context exactly like the app, instantiates the module, **builds its view on an STA thread**, and verifies the Email feature resolves its own private MailKit. |

Tooling: **xUnit** + **FluentAssertions**; coverage via **coverlet** (`coverage.runsettings`).

## Running
```powershell
./build.ps1            # build, then ASK whether to run unit tests / QA
./build.ps1 -All       # build + unit + QA + coverage + report   (use this before committing)
./build.ps1 -Test      # build + unit tests only
./build.ps1 -Qa        # build + QA only
./build.ps1 -All -MinCoverage 80   # fail the run if logic coverage < 80%
```
Or directly: `dotnet test DigitalSecretary.sln`. (QA needs the solution built first so the
`plugins/` folder exists — `build.ps1` always builds first.)

## Coverage policy
- The metric tracks **testable logic**. WinForms views/forms, `Program`, the plugin loader, and the
  IMAP `EmailDownloader` networking class are excluded in `tests/coverage.runsettings` because they
  are validated by **QA automation** and manual runs, not unit tests.
- Target: logic coverage **≥ 80%** (currently ~88%).
- **Every build shows coverage**: `build.ps1` prints and writes (to `docs/QUALITY_REPORT.md`) the
  **overall** percentage plus a **per-feature** table (line coverage + covered/total lines). When a
  build skips tests, it shows the last-measured snapshot (timestamped) from
  `artifacts/quality-state.json`.

## Writing unit tests
- One test class per logic class; method names use `Given_When_Then` style (underscores OK).
- Use the `TempDir` helper for filesystem tests (`tests/DigitalSecretary.UnitTests/TestSupport`).
- Assert with FluentAssertions (`result.Should().Be(...)`). Note: `Should().NotContain(lambda)`
  builds an expression tree — don't use `is`-patterns inside; use LINQ `.Any(...)` then assert.

## Adding tests for a NEW feature
1. Add a `ProjectReference` to the new feature in `DigitalSecretary.UnitTests.csproj`.
2. Add a test class under `tests/DigitalSecretary.UnitTests/Features/` for each logic class.
3. QA automation needs **no change** — it picks up the new plugin from the `plugins/` folder
   automatically and will load + view-test it.
4. Run `./build.ps1 -All` and confirm **PASS**.

## The quality report
`build.ps1` always writes `docs/QUALITY_REPORT.md`: verdict, build status, analyzer warnings,
unit results, coverage (overall + per assembly), and QA results. Treat a non-`PASS` verdict as a
broken build.

## Optional: full UI automation (future)
The current QA layer drives the **plugin pipeline** (load + construct every view), which catches the
vast majority of integration regressions cheaply and reliably. If end-to-end **clicking** of the UI
is ever needed, add a separate, opt-in project using **FlaUI** (`FlaUI.UIA3`) or WinAppDriver to
launch `DigitalSecretary.exe` and assert on windows/menus. Keep it out of the default `build.ps1`
flow (it's slow and flakier) — run it as a nightly/`-UiAutomation` opt-in so day-to-day builds stay
fast.
