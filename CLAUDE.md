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
- **Update `docs/DEVECONOMICS.md`** — append a per-feature **DevEconomics** entry: measured output
  (hand-authored LOC, files, tests, gate result) and the **activity ledger** classifying every activity as
  Bucket **A** (LLM tokens), **B** (local compute, $0 tokens), or **C** (remote/network, $0 tokens), plus a
  token/cost line. Run `/cost` for the exact session figure (only the harness knows it); if unavailable,
  record a clearly-labeled estimate and reconcile later. The point is to keep the cost-per-feature /
  cost-per-KLOC of "develop by asking" visible and trending down (see the doc's improvement levers).
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
  contains a personal email, password, token / API key, private key, SSN, or credit-card number. Use a
  **generic git identity** (no real name/email in commit metadata). Mark a confirmed false positive with
  a trailing `# pragma: allowlist secret`.
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
- `docs/DEVECONOMICS.md` — living cost/velocity ledger (token vs local-compute split, $/feature, $/KLOC).
- `docs/requirements/` — product/BA requirements (App + per feature/sub-feature).
- `docs/user-guide/` — end-user guides + the single-file HTML **User Manual** (with screenshots).
- `tools/DocShots/` + `tools/build-user-manual.ps1` — regenerate the manual's screenshots + HTML.
- `src/Features/<Name>/FEATURE.md` — per-feature developer documentation (always add one).
