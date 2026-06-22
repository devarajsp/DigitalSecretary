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

### Phase 15 — Automated gates: pre-commit hook + CI (2026-06-20)
Added a versioned **pre-commit hook** (`.githooks/pre-commit`, enabled via `tools/setup-hooks.ps1` →
`core.hooksPath`) that runs a fast gate (`build.ps1 -NoPrompt`: build + static analysis + docs
consistency) so broken/drifted changes can't be committed, and a **GitHub Actions CI**
(`.github/workflows/ci.yml`, windows-latest + .NET 9 + Python) that runs the full
`build.ps1 -All -WarnAsError -MinCoverage 80` on push/PR to block merges. Untracked the per-run
`docs/QUALITY_REPORT.md` (regenerated each build) so the hook doesn't create churn. _Why: make the
quality + consistency gate impossible to skip — drift can't be committed (hook) or merged (CI)._

### Phase 16 — Secret / PII gate before every commit (2026-06-20)
Added `tools/check_secrets.py` (Python), a scanner over all tracked text artifacts that flags personal
emails, hardcoded passwords/secrets/tokens/API keys, GitHub/AWS/Google/Slack tokens, JWTs, private-key
blocks, US SSNs, and Luhn-valid credit-card numbers (with placeholder/example allow-listing and a
`# pragma: allowlist secret` escape). Wired it into the **pre-commit hook** (fast fail, before the
build) and into `build.ps1` as a gate (part of the VERDICT) so it also runs in CI. Verified it catches
all 7 leak types and doesn't self-flag. Also scrubbed git history to a generic identity
`DigitalSecretary <noreply@github.com>` (no real name/email) and added a pre-push guard. Made it a
**rule** (CLAUDE.md + `app_standards/`). _Why: no personal information or secrets should ever reach git
— enforced automatically, not by memory._

### Phase 17 — Downloadable release for end users (2026-06-20)
Added `tools/package-release.ps1`, which builds a **self-contained** (no .NET install) portable Windows
zip: `DigitalSecretary.exe` + the `plugins/` folder + `User-Manual.html` + a lightweight
`Install.cmd`/`Uninstall.cmd` + `README.txt` → `release/DigitalSecretary-v2.0.0-win-x64.zip` (~47 MB).
Verified the published self-contained app launches and discovers its plugins. **Release binaries are
gitignored** (distributed via GitHub Releases, not committed); `tools/publish-github.ps1` now builds
and **attaches the zip to the release**, and the showcase page + README have a **Download** button.
`release/README.md` documents it. _Why: users should be able to download and run the app directly from
the project page without building it._

### Phase 18 — Gmail + Google Drive downloaders (2026-06-22)
Added two new feature plugins alongside the Yahoo **Download Emails** tool, both following the same
"download a read-only local copy" model:
- **Download Gmail** (`gmail-downloader`) — a near-clone of the Yahoo downloader over **Gmail IMAP**
  (`imap.gmail.com`, MailKit, private to the plugin). Uses a Google **app password** (2-Step Verification
  required); walks all folders/labels read-only; saves each message as `.txt` + `.eml` with all
  attachments. Documents the Gmail-label overlap (a message can appear under several folders).
- **Download Google Drive** (`google-drive-downloader`, category **Cloud**) — uses the **Google Drive
  API** with the **OAuth 2.0 installed-app flow** (`Google.Apis.Drive.v3`, private to the plugin). The
  user supplies a *Desktop app* OAuth `credentials.json`; the first run consents in a browser
  (read-only scope) and the token is cached under the feature's `token/` folder. Mirrors the Drive tree,
  downloads regular files as-is, and **exports Google-native files to both Office and PDF**
  (Docs→docx, Sheets→xlsx, Slides→pptx, Drawings→png; all +pdf).

Logic was kept in pure, testable classes (`GmailFileNaming`/`GmailAttachmentScanner`,
`GoogleExportFormats`/`DriveFileNaming` incl. parent-chain folder resolution with cycle guard); UI stays
thin. Added **40 unit tests** (now 85 total) and **2 QA tests** verifying each plugin resolves its own
private MailKit / Google.Apis (now 17 QA total). Updated the full doc set: per-feature requirement +
user-guide docs, the App requirement/user-guide indexes, the requirements (56) + traceability (37)
spreadsheets (extended `check_docs.py`'s ID regex with `GML`/`GDR`), and the HTML user manual with new
sections + real screenshots (seeded sample data in `DocShots`). **No host change** — both features were
added purely as plugins. `./build.ps1 -All` ⇒ **VERDICT: PASS**. _Why: the user asked for Gmail and
Google Drive downloaders as features; Drive needed OAuth + export because native docs have no raw bytes._

### Phase 19 — Email Intelligence (2026-06-22)
Added a new **Insights** feature plugin, `email-intelligence`, that *consumes* the archive the
downloaders produce (`.eml`/`.txt` + attachments) and turns it into an offline people-and-life
intelligence base — **no network, no AI, no database**. Pipeline of pure, testable classes:
`ArchiveScanner` (prefers `.eml` over its `.txt` sibling) → `EmailParser` (MimeKit for `.eml`,
heuristic for the downloader `.txt`; HTML→text via `MailText`) → `MessageClassifier` →
`Deduplicator` (Message-Id + content hash + thread key) → `ContactExtractor`/`IdentityResolver`
(owner detection + merge addresses by full name) → `SignatureExtractor` (phones/links) →
`RelationshipAnalyzer` (counts, first/last, strength score, dormant flag) → `TopicExtractor` →
`NetworkGraphBuilder` (co-occurrence edges) → `TimelineBuilder` (per-person history). Outputs are
portable files (CSV, JSON, vCard, GraphML) plus a **self-contained HTML5 report** with tabbed views
(Overview / People+dossier / Timeline / Graph / Topics) — data is emitted to `data/data.js` and loaded
via a `<script>` tag so the single page opens straight from `file://` with no server. UI
(`EmailIntelligenceControl`) is thin: pick folders, phased progress + cancel, open report. Added **36
unit tests** (now 121 total) at **92.2%** feature coverage; QA loads the plugin (now 19 QA). Full doc
set updated: requirement + user-guide docs, the App index, requirements (93) + traceability (53)
spreadsheets (extended `check_docs.py`'s ID regex for `EI-` with an optional letter group). Chose **CSV
over a native `.xlsx` library** to stay dependency-light/portable. **No host change.** `./build.ps1
-All` ⇒ **VERDICT: PASS**. _Why: the user downloaded their Yahoo+Gmail mail and wanted to clean,
organize and mine it locally — contacts, relationships, timelines and a graph — with a name-search
dossier as the headline, all offline._

_Follow-up (2026-06-22): added an **Append vs Overwrite** mode (`EI-I4`). Overwrite replaces results
from the current input only; Append merges the new archive with a persisted de-duplicated corpus
(`MessageStore` → `data/messages.json`) and **re-de-duplicates**, so several mailboxes
(Yahoo + Gmail + ...) consolidate into one contact/relationship dataset. The pipeline was split into
`ScanAndParse` + `AnalyzeCore` so the merge happens at the message level (dedupe / identity / metrics /
graph re-derived over the union). UI gained an "Append" checkbox (remembered in settings). Now 125 unit
tests, requirements (94) + traceability (54); `build.ps1 -All` ⇒ PASS. Why: running on a second archive
overwrote the first; the user wanted to consolidate all mailboxes into one de-duplicated contact set._

### Phase 20 — Email Intelligence: tone, life-data & documents (2026-06-22)
Second increment on `email-intelligence`, all still **offline / no AI**:
- **Tone & sentiment (`EI-E2`)** — `ToneAnalyzer` scores each relationship using a bundled AFINN-style
  `SentimentLexicon` (positive/negative word weights, shipped in the assembly) and labels it
  Positive / Neutral / Negative. Surfaced in the dossier + a `Tone` column in `Contacts.csv`.
- **Life-data extraction (`EI-F1..F4`)** — `LifeDataExtractor` categorizes transactional mail into
  Purchase / Subscription / Travel / Account by keyword, and pulls an amount + currency via regex →
  `LifeData.csv` + a **Life Data** report tab.
- **Useful-document library (`EI-F7`)** — `DocumentLibrary` de-duplicates attachments by content hash,
  classifies them (PDF/Image/Document/Spreadsheet/...), counts occurrences → `Documents.csv` + a
  **Documents** report tab.
The pipeline runs these in `AnalyzeCore`; the HTML report gained the two new tabs and a tone line.
Added **22 unit tests** (now 147), feature coverage 91%; requirements (100) + traceability (57)
spreadsheets regenerated. **No host change.** `./build.ps1 -All` ⇒ **VERDICT: PASS**. _Why: these were
the deferred analytics from the original requirements; all kept rule/lexicon-based to honour the
no-network, no-AI constraint, and clearly labelled as heuristics in the UI._

### Phase 21 — Email Intelligence: native Excel (.xlsx) output (2026-06-22)
Added `XlsxWriter` — a **dependency-free OOXML (SpreadsheetML) writer** built on `ZipArchive` — and
`WorkbookExporter`, so a run now also produces **`EmailIntelligence.xlsx`** with Contacts / Life Data /
Documents sheets and **typed numeric cells**. Validated the output with `openpyxl` (loads cleanly: 3
sheets, numbers typed). Chose a built-in writer over a NuGet library (e.g. ClosedXML) to keep the
plugin fully offline / portable and avoid a network restore. Added 6 unit tests (now 153), feature
coverage ~92%; requirement `EI-H7` + a traceability row registered (101 reqs / 58 rows). **No host
change.** `./build.ps1 -All` ⇒ **VERDICT: PASS**. _Why: the user's original ask was Excel output; CSV
was the interim, this delivers a real multi-sheet workbook without adding a dependency._

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
