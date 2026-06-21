# Development Journal — DigitalSecretary

> **What this is.** A living record of how this app is built **with Claude, without the user writing
> code** — both the *process* we follow and the *timeline* of what we built and why. DigitalSecretary
> is the worked example for "codeless development with Claude"; this journal is the evolving standard.
>
> **Claude maintains this file.** After every meaningful change, append to the Timeline and add any new
> decision/lesson. Keep it honest (record what actually happened, including reversals).

---

## How we work — the codeless development process (reusable)

### Roles
- **User (director):** states intent in plain English — a feature, a change, a quality bar, or a
  correction. Reviews results. **Never writes code.**
- **Claude (builder):** turns intent into working software — scaffolds, codes, tests, runs the quality
  gate, launches the app, and reports with proof. Asks a question only when genuinely blocked.

### The loop (every request)
1. **Understand & decide.** Restate the goal; pick sensible defaults; ask only blocking questions.
2. **Build.** Implement following the active platform standard (`app_standards/standard_winforms.md`)
   and this repo's `CLAUDE.md`. Put **logic in testable classes**, keep UI thin.
3. **Prove it.** `./build.ps1 -All` must reach **VERDICT: PASS** (0 warnings, unit + QA green, coverage
   on target). Launch the app to confirm runtime.
4. **Review for cleanliness.** Re-read every created/generated doc and text output: no encoding
   artifacts / mojibake (`Â`, `â€`), no garbled characters, no leftover template tokens, renders
   correctly. (PowerShell text I/O must use explicit UTF-8 — 5.1 defaults to ANSI.)
5. **Report.** Plainly state what changed, the quality result, and any judgment calls or follow-ups.
6. **Record.** Update docs (`FEATURE.md`, etc.), memory, and **this journal**.

### Standards & enforcement (so the user doesn't repeat themselves)
- The platform-agnostic standard lives in `…/Claude/app_standards/` (per-platform prompt files).
- This app follows `standard_winforms.md`; conventions are enforced by `CLAUDE.md`, `.editorconfig`,
  and `build.ps1` (static analysis + tests + QA + coverage + `VERDICT`).
- New features are added by **asking** — see `docs/ADDING_A_FEATURE.md`. The host never changes.

### Where things live
- Architecture & how-to: `docs/ARCHITECTURE.md`, `docs/ADDING_A_FEATURE.md`, `docs/FEATURE_TEMPLATE.md`.
- Quality: `docs/CODING_STANDARDS.md`, `docs/TESTING.md`, `docs/STATIC_ANALYSIS.md`,
  generated `docs/QUALITY_REPORT.md`.
- Per feature: `src/Features/<X>/FEATURE.md`.

---

## Timeline — what we built, and why

### Phase 1 — First desktop app (2026-06-19)
Created a .NET 9 WinForms personal app (then `SPD_PersonalApp`) with a sidebar shell and "Useful
Tools": **Launcher, Calculator, Clipboard History, About**. Local JSON persistence. _Why: user wanted
a personal utility app, WinForms, tools-focused._

### Phase 2 — Download Emails (2026-06-20)
Added a **Yahoo IMAP** email downloader (MailKit): walks all folders, saves emails as `.txt` +
attachments, **read-only** (copy only — nothing changed on the server), with live progress + cancel.

### Phase 3 — Fuller fidelity (2026-06-20)
Saved each email as **both `.txt` and `.eml`**, and captured **all attachments including inline images**
(walk `message.BodyParts`, not just `Attachments`). _Why: completeness; the old path missed inline parts._

### Phase 4 — Rename + pluggable re-architecture → DigitalSecretary (2026-06-20)
Renamed to **DigitalSecretary** and rebuilt as a **pluggable multi-project solution**: host
(`App`) + contract (`Abstractions`) + independent **feature plugins** under `src/Features/`, each its
own DLL loaded **lazily** via an isolated `AssemblyLoadContext`, discovered from `plugin.json`
manifests. Added menus/dashboard and the full docs set + `CLAUDE.md`. _Why: clean, professional,
independently debuggable features; "develop by asking Claude" without touching the host._

### Phase 5 — User-configurable visibility (2026-06-20)
Added a **Configure Features** dialog to show/hide each feature on the **dashboard** and in the **menu**
*independently*. _Why: user wanted control over both surfaces, not just the dashboard._

### Phase 6 — Quality engineering layer (2026-06-20)
Established the quality regime: extracted **logic into testable classes**; **unit tests**
(xUnit + FluentAssertions); **QA automation** (black-box plugin pipeline); **static analysis**
(.NET analyzers + `.editorconfig`); **coverage** (coverlet); and **`build.ps1`** as the single
quality-gated entry point writing `docs/QUALITY_REPORT.md`. Baseline: 0 warnings, 45 unit + 11 QA
pass, ~88% logic coverage, **PASS**.

### Phase 7 — Quality reporting on every build (2026-06-20)
`build.ps1` now prints + writes a **static-analysis findings report** (grouped by rule) and a
**coverage report: overall + per feature**; persists a coverage snapshot so tests-skipped builds still
show last-measured coverage. _Why: user wants quality visible every build._

### Phase 8 — Reusable, per-platform build standards (2026-06-20)
Distilled everything into reusable prompt files in `…/Claude/app_standards/` (WinForms, WPF,
ASP.NET API, Blazor, MAUI, Console/Worker), then added **React web** and **React Native mobile**.
_Why: stop re-explaining the standard; start a new app by filling in parameters._

### Phase 9 — This journal (2026-06-20)
Began this living journal so the DigitalSecretary build itself becomes a documented, repeatable
**process** for codeless development with Claude.

### Phase 10 — Product & user documentation (2026-06-20)
Added two audience-specific doc sets, structured **App → Feature → Sub-feature**: `docs/requirements/`
(product/BA: purpose, user stories, functional requirements, acceptance criteria, NFRs, traceability)
and `docs/user-guide/` (plain-English end-user how-to). Made it a **rule** in `CLAUDE.md`,
`ADDING_A_FEATURE.md`, and all `app_standards/` files. _Why: both a product owner and an end user
should understand the app fully._

### Phase 11 — Visual HTML user manual with screenshots (2026-06-20)
Built a **single self-contained HTML user manual** (`docs/user-guide/DigitalSecretary-User-Manual.html`,
Windows 11 "Get Help" style, searchable, print-to-PDF) with **real screenshots** of every view. Added a
reproducible screenshot generator (`tools/DocShots`, renders each actual view with seeded sample data
via `DrawToBitmap`) and `tools/build-user-manual.ps1` (embeds images as base64 → one file). _Why: the
modern replacement for deprecated `.chm`; a picture-led manual is the most useful for end users._

### Phase 12 — Manual encoding fix + cleanliness rule (2026-06-20)
Fixed mojibake (`Â`, `â€`) in the generated HTML manual — root cause was `build-user-manual.ps1`
reading the template with `Get-Content` (PowerShell 5.1 defaults to ANSI, not UTF-8); switched to
`[IO.File]::ReadAllText(path,[Text.Encoding]::UTF8)` and replaced the emoji print glyph with an inline
SVG. Added a standing **cleanliness-review rule** (review created/generated text for encoding
artifacts/garbled chars/leftover tokens; UTF-8 for all PowerShell text I/O) to `CLAUDE.md`, the process
loop above, and `app_standards/README.md`. Also hardened `build-user-manual.ps1` with a **cleanliness
gate** that re-reads the output and **exits non-zero** if mojibake (U+00C2/U+00C3/U+00E2) or stray
tokens appear (markers built from code points so the script itself stays pure-ASCII). _Why: generated
docs must be clean and trustworthy, and the build should fail loudly if they aren't._

### Phase 13 — Requirements spreadsheet + bi-directional traceability matrix (2026-06-20)
Added two Excel deliverables (generated by `tools/docgen/build_excel_docs.py`, openpyxl):
`docs/requirements/DigitalSecretary-Requirements.xlsx` (40 requirements, flat, JIRA-importable) and
`docs/traceability/DigitalSecretary-Traceability-Matrix.xlsx` (23 rows mapping each feature/sub-feature
to requirement ID · requirement doc · mock/screen · code · unit test · QA · user manual · architecture
doc · code doc, with an auto **Coverage** formula + conditional formatting). All rows trace to all
artifacts (Coverage = Complete). Made it a **rule** in `CLAUDE.md`, `ADDING_A_FEATURE.md`, and every
`app_standards/` file. _Why: one place to trace any artifact to any other and verify full requirement
coverage; the requirements sheet publishes to JIRA/PM tools._

### Phase 14 — Artifact integrity: consistency gate + version control (2026-06-20)
Added `tools/docgen/check_docs.py`, a **docs & traceability consistency validator** wired into
`build.ps1` (and the VERDICT): it fails the build if a traceability path/symbol is broken, the
spreadsheets are stale/hand-edited, a feature is missing an artifact, requirement IDs don't match,
coverage has a gap, or generated text is unclean. Verified it fails loudly (renamed a referenced .cs
→ 2 errors, exit 1). Stamped generated files (manual HTML, both `.xlsx`) with **AUTO-GENERATED**
banners. **Initialized git** (commit `4b78a73`, 102 files) + `.gitattributes`, so every change is
reviewable/revertable. Made all of this a **rule** (CLAUDE.md + `app_standards/`). _Why: as the
artifact web grew, we needed to detect drift automatically and keep a revertable history — manual
edits to any artifact now get caught or can be undone._

_(Append new phases here as we go.)_

---

## Key decisions & rationale
- **Plugin architecture over a monolith** — features must be independent, lazy, and addable without
  touching the host.
- **Logic out of the UI** — the single most important rule for testability/quality.
- **Coverage measures logic, not UI** — UI/bootstrap/loader/IMAP are covered by QA automation; coverage
  is scoped in `tests/coverage.runsettings` so the number is meaningful (~88%, not a misleading 12%).
- **One build entry point (`build.ps1`)** — standards, build, static analysis, tests/QA, coverage, and a
  single `VERDICT: PASS|FAIL`, so quality is never optional or invisible.
- **Per-platform standards, not one mega-file** — same quality regime, different composition/UI/test
  tooling per platform.

## Lessons learned / pitfalls (carried into the standards)
- Avoid framework type-name clashes (`Shortcut` vs `System.Windows.Forms.Shortcut`).
- `AssemblyLoadContext.LoadUnmanagedDll(string)` signature; ALC returns null for the contract assembly.
- `CopyLocalLockFileAssemblies=true` to ship a feature's NuGet deps in its plugin folder.
- FluentAssertions `NotContain(lambda)` builds an expression tree — no `is`-patterns.
- `DataTable.Compute` divides as double, returns `Infinity` on /0.
- MimeKit `BodyBuilder` drops orphan linked resources — build MIME trees explicitly in tests.
- Stop the running app before rebuild (exe is file-locked).
- PowerShell: `"_$($v)_"` not `"_$v_"`; wrap counts with `@(...)`; don't reuse a `[switch]` name as a
  `$variable`; build `--no-incremental` so analyzers always emit.

## How Claude maintains this journal
On each session/change: add a Timeline entry (date + what + why), record any new decision or pitfall,
and keep it consistent with `app_standards/`. If a standard evolves here, mirror it back into the
matching `app_standards/standard_*.md`.
