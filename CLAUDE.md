# CLAUDE.md — working on Digital Secretary

This file tells Claude how to work in this repo. Read it fully before making changes.

## What this app is
**Digital Secretary** is a pluggable Windows (WinForms, .NET 9) personal assistant. The host
shell (`DigitalSecretary.App`) discovers **features** at runtime from a `plugins/` folder and
loads each one **lazily** (only when the user opens it). Features are independent projects that
know nothing about each other and depend only on a tiny contract assembly.

The user's goal: **add new features by asking Claude, without hand-writing code.** So the most
important workflow in this repo is "add a feature" — keep it mechanical and reliable.

## Golden rules
1. **Never make the host reference a feature.** The host (`DigitalSecretary.App`) references only
   `DigitalSecretary.Abstractions`. Features reference only `DigitalSecretary.Abstractions`.
   Features never reference each other.
2. **One feature = one project** under `src/Features/<Name>/` + a `plugin.json` + a `FEATURE.md`.
3. **Persist per feature**, only under `IFeatureContext.DataDirectory`. Never write to the host's
   settings or another feature's folder.
4. **Feature-specific NuGet packages stay in the feature.** Add the `PackageReference` to that
   feature's `.csproj` and set `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`
   so the dependency ships in the plugin folder (see the EmailDownloader feature).
5. **Keep the contract small.** Only change `DigitalSecretary.Abstractions` when every feature
   needs it; it's a breaking change for all plugins.
6. **Put logic in plain classes, not WinForms controls** — so it can be unit-tested. Controls just
   wire UI to those classes. See `docs/CODING_STANDARDS.md`.
7. **Every change ships tests and stays green.** Add unit tests for new logic; keep static analysis
   clean. **Always finish with `./build.ps1 -All` and confirm `VERDICT: PASS`.**

## Quality workflow (do this for every change)
- `./build.ps1 -All` — builds (with static analysis), runs unit tests + QA automation, collects
  coverage, and writes `docs/QUALITY_REPORT.md`. It must report **PASS**.
- New logic class/method → add unit tests in `tests/DigitalSecretary.UnitTests` (reference the
  feature project there). New feature → QA automation covers loading/viewing it automatically.
- Don't suppress analyzer warnings ad-hoc; tune centrally in `.editorconfig` with a reason.
- **Update `docs/DEVELOPMENT_JOURNAL.md`** — append a Timeline entry (date + what + why) and record any
  new decision/pitfall. This repo is the worked example of codeless development with Claude; the journal
  must stay current. If a convention evolves, mirror it back to `…/Claude/app_standards/`.
- **Track DevEconomics (required, every feature).** Maintain both the workbook and the narrative:
  1. **Log every activity** in `docs/deveconomics/DigitalSecretary-DevEconomics.xlsx` — *running a local
     script and thinking/LLM work alike* — via the data tables in `tools/docgen/build_deveconomics.py`
     (`ACTIVITIES`): bucket **A** (LLM tokens), **B** (local compute, $0), or **C** (remote/network, $0),
     with **token/cost** (or `$0`) and **time** (measured from command output for local rows; estimated
     for LLM rows). Add a **Feature Summary** row and regenerate: `python tools/docgen/build_deveconomics.py`.
  2. **Keep estimate *and* actual, and show the trend.** Record estimates now; run **`/cost`** (only the
     harness knows exact tokens) and write the figure into the workbook's `Total cost ACTUAL` column, then
     re-run — the variance + the EST-vs-ACTUAL **Trend** chart update automatically.
  3. **Append the narrative entry** to `docs/DEVECONOMICS.md` (output delivered, cost & time, velocity).
  The point: keep cost-per-feature, $/KLOC, and time visible and trending down (see the doc's levers).
- **Release notes = the DevEconomics summary (required).** Each `CHANGELOG.md` entry (the GitHub
  release notes) leads with a short, catchy **"Built with no code"** block: one line naming what's new,
  then a small two-row table — (1) **this release** (its features' Prod. KLOC, est. tokens, est. cost,
  time) and (2) **whole app to date** (cumulative KLOC, tokens, cost, time) — pulled from the
  DevEconomics workbook (the release's feature row + the **App Total** row), linking to it. **Do not
  enumerate the full feature catalog or architecture** in release notes; that detail lives in the
  workbook and the user manual. The release note's job is to keep the whole-app + current-release
  cost/KLOC/token/time trend front-and-centre for every reader.
- **Keep the GitHub-facing showcase current (required, every feature/release).** Update `README.md`
  (the **Features** table) and `docs/index.html` (the GitHub **Pages** landing: feature **cards**, the
  **test-count + coverage badges**, and the footer version) so the public repo and site reflect the
  latest feature — not just the release notes. This is **gated**: `tools/docgen/check_docs.py` fails the
  build if any feature's `plugin.json` **title** is missing from `README.md` or `docs/index.html`, so the
  showcase can never drift behind a release again. Also **regenerate the HTML user manual** (template +
  DocShots screenshots) so it covers the new feature, and **link any rendered HTML doc by its GitHub
  Pages URL** (`https://<owner>.github.io/<repo>/…`), **never** the repo blob path — GitHub serves an
  `.html` blob as raw source, not a page.
- **Update the product & user documentation (required, App → Feature → Sub-feature):**
  - `docs/requirements/` — product/BA view: purpose, user stories, functional requirements, acceptance
    criteria, NFRs, traceability. New feature ⇒ new `docs/requirements/features/<id>.md`.
  - `docs/user-guide/` — end-user how-to per feature. New feature ⇒ new `docs/user-guide/features/<id>.md`.
  - `docs/user-guide/DigitalSecretary-User-Manual.html` — single-file HTML manual **with screenshots**.
    Regenerate after UI changes: `dotnet run --project tools/DocShots` then `./tools/build-user-manual.ps1`.
  - `docs/requirements/DigitalSecretary-Requirements.xlsx` — single flat requirements sheet (JIRA-importable).
  - `docs/traceability/DigitalSecretary-Traceability-Matrix.xlsx` — **bi-directional traceability matrix**
    (feature/sub-feature ↔ requirement ID ↔ requirement doc · mock/screen · code · unit test · QA · user
    manual · architecture doc · code doc), with an auto **Coverage** flag. Both are generated from the data
    tables in `tools/docgen/build_excel_docs.py` — edit those tables and regenerate: `python
    tools/docgen/build_excel_docs.py`. Every requirement must trace to all artifacts (Coverage = Complete).
- **Cleanliness review (required).** Before finishing any change, review every created/generated
  document and text output for encoding artifacts / mojibake (`Â`, `Ã`, `â€`), garbled characters,
  leftover template tokens (`{{…}}`), and correct rendering. When writing text via PowerShell, read
  **and** write with explicit UTF-8 (5.1 defaults to ANSI and corrupts non-ASCII). After regenerating
  the user manual, grep the HTML for `Â|Ã|â€` and confirm **zero** before considering it done.
- **Never hand-edit generated files** — the user manual HTML, the `.xlsx` requirement/traceability
  sheets, and `docs/QUALITY_REPORT.md` are build outputs (they carry an AUTO-GENERATED banner). Edit
  the *source* (`tools/user-manual.template.html` + views, or `tools/docgen/build_excel_docs.py` data
  tables) and regenerate.
- **Artifact consistency is gated.** `build.ps1` runs `tools/docgen/check_docs.py`, which fails the
  build if any traceability path/symbol is broken, a feature is missing an artifact, requirement IDs
  don't match, coverage has a gap, or generated text is unclean. Keep it green (part of the VERDICT).
- **No secrets or personal info, ever — gated before every commit.** The pre-commit hook and
  `build.ps1` run `tools/check_secrets.py`, which **fails** if any tracked artifact (code, docs, config)
  contains a personal email, password, token / API key, private key, SSN, credit-card number, **or a
  local user-home path that leaks the OS username** (`C:\Users\<name>\`, `/home/<name>/`). Use a
  **generic git identity** (no real name/email in commit metadata). Mark a confirmed false positive with
  a trailing `# pragma: allowlist secret`.
  - **Image-baked PII is not text-scannable** — a real name/path/email **rendered into a screenshot**
    won't be caught by the scanner. So **all screenshots and sample/seed data must use placeholders**
    (`C:\Users\You\...`, `yourname@example.com`); feed those to `tools/DocShots`, never real values.
  - **Never commit build caches** that embed absolute source paths — `__pycache__/`, `*.pyc`, `bin/`,
    `obj/` are git-ignored (a `.pyc` records `C:\Users\<you>\...` as its source path = username PII).
- **The repo is git-tracked** — make a commit per logical change so anything can be reviewed/reverted.
- **Gates run automatically.** A **pre-commit hook** (`.githooks/pre-commit`) runs a fast gate
  (build + static analysis + docs consistency) so broken/drifted changes can't be committed; **CI**
  (`.github/workflows/ci.yml`) runs the full `build.ps1 -All -WarnAsError -MinCoverage 80` on push/PR.
  Enable the hook once per clone: `tools/setup-hooks.ps1` (or `git config core.hooksPath .githooks`).
  Bypass for a WIP commit with `git commit --no-verify`.
- Standards: `docs/CODING_STANDARDS.md` · Testing/QA: `docs/TESTING.md` · Analysis: `docs/STATIC_ANALYSIS.md`.

## Layout
```
src/
  DigitalSecretary.Abstractions/   contract: IFeatureModule, IFeatureContext  (referenced by all)
  DigitalSecretary.App/            host: menu, dashboard, plugin loader, settings
  Features/
    Directory.Build.props          shared feature settings + auto-copy to plugins/
    <Name>/                        one feature (project + plugin.json + FEATURE.md)
```
At build time each feature's output is copied to
`src/DigitalSecretary.App/bin/<Config>/net9.0-windows/plugins/<ProjectName>/`, which is what the
host scans.

## How to add a feature (summary)
See **`docs/ADDING_A_FEATURE.md`** for the full copy-paste recipe. In short:
1. Create `src/Features/<Name>/` with `<Name>.csproj`, `plugin.json`, a `…Module.cs` implementing
   `IFeatureModule`, a `…Control.cs` (the UI), and `FEATURE.md`.
2. `dotnet sln add` the new project.
3. `dotnet build` the solution; the host picks the feature up automatically (no host edits).

## Build & run
```
dotnet build DigitalSecretary.sln -c Debug
src/DigitalSecretary.App/bin/Debug/net9.0-windows/DigitalSecretary.exe
```

## Docs map
- `docs/ARCHITECTURE.md` — how the host, manifests, lazy loading, and isolation work.
- `docs/ADDING_A_FEATURE.md` — the step-by-step recipe with templates (incl. tests).
- `docs/FEATURE_TEMPLATE.md` — skeleton files to copy.
- `docs/CODING_STANDARDS.md` — the rules (also printed by `build.ps1`).
- `docs/TESTING.md` — unit tests, QA automation, coverage, how to add tests.
- `docs/STATIC_ANALYSIS.md` — analyzers and `.editorconfig` tuning.
- `docs/QUALITY_REPORT.md` — generated each `build.ps1` run (build/tests/coverage/QA).
- `docs/DEVELOPMENT_JOURNAL.md` — living record of the build + the codeless-dev process (keep updated).
- `docs/DEVECONOMICS.md` + `docs/deveconomics/DigitalSecretary-DevEconomics.xlsx` — cost/velocity ledger:
  per-activity token-vs-local split, time, per-feature summary, estimate-vs-actual trend (generated by
  `tools/docgen/build_deveconomics.py`).
- `docs/requirements/` — product/BA requirements (App + per feature/sub-feature).
- `docs/user-guide/` — end-user guides + the single-file HTML **User Manual** (with screenshots).
- `tools/DocShots/` + `tools/build-user-manual.ps1` — regenerate the manual's screenshots + HTML.
- `src/Features/<Name>/FEATURE.md` — per-feature developer documentation (always add one).
