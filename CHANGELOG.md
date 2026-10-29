# Changelog

All notable changes to Digital Secretary. Format based on [Keep a Changelog](https://keepachangelog.com).

## [2.4.0] - 2026-10-29

**New:** **Storage Studio** &mdash; see **what is using the space** in any folder, as a picture. A
clickable, colour-coded **size map** (every folder and file is a rectangle sized by the space it takes),
space **by type** and **by age**, the **largest files and folders**, **clean-up candidates** with a
reason for each, and **what grew since your last scan**. Click a folder to drill in; right-click for
Explorer. **Read-only** &mdash; nothing is deleted, moved or uploaded. Added as a plugin with no change to
the host; phase 1 of the Storage Studio plan (similar pictures and videos come next).

### Fixed
- **Docs:** the requirements and user-guide index pages had stopped listing new features (Email
  Intelligence, DeDupe Files) - both hubs now list every feature, including Storage Studio.

> &#9889; **Built with no code &mdash; DevEconomics.** Digital Secretary is built by *asking Claude*
> (Opus 5.5), not by hand-writing code. We publish the cost every release; the per-activity ledger,
> per-feature rows, and the trend live in the
> [DevEconomics workbook](docs/deveconomics/DigitalSecretary-DevEconomics.xlsx).

| Scope | Prod. KLOC | Est. tokens | Est. cost | Time |
|---|--:|--:|--:|--:|
| **This release** (Storage Studio) | ~2.3 | ~207 K | **~$18** | ~100 min |
| **Whole app to date** (est) | ~9.7 | ~3.0 M | **~$106** | ~17.6 h |

_LLM cost/tokens are estimates (the model can't meter itself); the whole-app foundation predates live
tracking. Reconcile with `/cost`._

[2.4.0]: https://github.com/devarajsp/DigitalSecretary/releases/tag/v2.4.0

## [2.3.0] - 2026-10-28

**New:** **DeDupe Files** &mdash; point it at a folder and it fingerprints every file beneath it with
**MD5**, writes a reusable checksum list (`dedupe_md5_list.txt`) into that folder, shows every set of
identical files with the **space you would get back**, and removes the extra copies &mdash; **always
keeping one copy of each**. A second run re-reads only what changed, and **Pause / Stop** never throws
away work. Safe by default: **Dry run**, **Recycle Bin**, and a **byte-for-byte** check against the copy
being kept before anything is deleted. Added as a plugin with no change to the host.

### Fixed
- **DeDupe Files scan:** a scan entered with an already-cancelled token walked no files, so the prune
  step ran against an empty set and discarded the whole checksum list, forcing a full re-hash next
  time. Found by the test suite before release; pinned by a regression test
  (`A_scan_cancelled_before_it_starts_prunes_nothing`).

> &#9889; **Built with no code &mdash; DevEconomics.** Digital Secretary is built by *asking Claude*
> (Opus 5), not by hand-writing code. We publish the cost every release; the per-activity ledger,
> per-feature rows, and the trend live in the
> [DevEconomics workbook](docs/deveconomics/DigitalSecretary-DevEconomics.xlsx).

| Scope | Prod. KLOC | Est. tokens | Est. cost | Time |
|---|--:|--:|--:|--:|
| **This release** (DeDupe Files) | ~2.2 | ~485 K | **~$17** | ~88 min |
| **Whole app to date** (est) | ~7.4 | ~2.8 M | **~$88** | ~15.9 h |

_LLM cost/tokens are estimates (the model can't meter itself); the whole-app foundation predates live
tracking. Reconcile with `/cost`. Three of this release's workstreams ran as parallel subagents, so
the time column is work time, not wall clock._

[2.3.0]: https://github.com/devarajsp/DigitalSecretary/releases/tag/v2.3.0

## [2.2.0] - 2026-06-23

**New:** **Email Intelligence** &mdash; turn your downloaded email archive (`.eml`/`.txt` + attachments)
into an **offline, searchable people-and-life intelligence base**: a de-duplicated contact list with
identity merge, relationship strength + dormant flags, topics, tone, a network graph, per-person
timelines, life-data (purchases / subscriptions / travel / accounts) and a document library &mdash; with
an **Append** mode to consolidate several mailboxes into one de-duplicated dataset. Each run produces a
self-contained **HTML5 report** (people search → dossier + timeline + graph), a native multi-sheet
**Excel `.xlsx`**, plus CSV / JSON / vCard / GraphML. **No cloud, no AI, no database;** added as a
plugin with no change to the host.

> ⚡ **Built with no code &mdash; DevEconomics.** Digital Secretary is built by *asking Claude* (Opus 4.8),
> not by hand-writing code. We publish the cost every release; the per-activity ledger, per-feature rows,
> and the trend live in the
> [DevEconomics workbook](docs/deveconomics/DigitalSecretary-DevEconomics.xlsx).

| Scope | Prod. KLOC | Est. tokens | Est. cost | Time |
|---|--:|--:|--:|--:|
| **This release** (Email Intelligence) | ~2.5 | ~180 K | **~$21** | ~100 min |
| **Whole app to date** (est) | ~5.2 | ~2.3 M | **~$72** | ~14.5 h |

_LLM cost/tokens are estimates (the model can't meter itself); the whole-app foundation predates live
tracking. Reconcile with `/cost`._

[2.2.0]: https://github.com/devarajsp/DigitalSecretary/releases/tag/v2.2.0

## [2.1.0] - 2026-06-22

**New:** **Download Gmail** and **Download Google Drive** &mdash; read-only local backups (Drive exports
Docs/Sheets/Slides to Office + PDF), added as plugins with no change to the host.

> ⚡ **Built with no code &mdash; DevEconomics.** Digital Secretary is built by *asking Claude* (Opus 4.8),
> not by hand-writing code. We publish the cost every release; the per-activity ledger, per-feature rows,
> and the trend live in the
> [DevEconomics workbook](docs/deveconomics/DigitalSecretary-DevEconomics.xlsx).

| Scope | Prod. KLOC | Est. tokens | Est. cost | Time |
|---|--:|--:|--:|--:|
| **This release** (Gmail + Drive) | ~1.0 | ~120 K | **~$5.50** | ~48 min |
| **Whole app to date** (est) | ~2.7 | ~2.1 M | **~$50** | ~13 h |

_LLM cost/tokens are estimates (the model can't meter itself); the whole-app foundation predates live
tracking. Reconcile with `/cost`._

[2.1.0]: https://github.com/devarajsp/DigitalSecretary/releases/tag/v2.1.0

## [2.0.0] - 2026-06-20

First public release of **Digital Secretary** &mdash; a pluggable personal toolbox for Windows,
re-architected from the earlier single-project "SPD Personal App".

**Download:** grab `DigitalSecretary-v2.0.0-win-x64.zip` from the assets below &mdash; it's
**self-contained** (no .NET install needed). Unzip and run `DigitalSecretary.exe`, or use the
included `Install.cmd` for Start Menu / Desktop shortcuts. Windows 10/11, 64-bit.

### Features
- **Launcher** &mdash; one-click open favorite apps, files, folders, and links.
- **Calculator** &mdash; keypad plus a type-an-expression evaluator.
- **Clipboard History** &mdash; re-paste text you copied earlier.
- **Download Emails (Yahoo)** &mdash; save a local copy of every Yahoo Mail folder, with each email as
  `.txt` + `.eml` and all attachments (including inline images); read-only, so the mailbox is untouched.
- **Home dashboard + menu** with a **Configure Features** dialog to show/hide each feature on the
  dashboard and in the menu independently.

### Architecture
- Pluggable host: features are independent projects loaded **lazily** from a `plugins/` folder via an
  isolated `AssemblyLoadContext`; the host never references a feature.
- Per-feature data isolation; feature-private NuGet dependencies (the email feature ships its own MailKit).

### Quality engineering
- Unit tests (xUnit + FluentAssertions) and black-box **QA automation** that loads every plugin.
- **Static code analysis** (.NET analyzers) and **code coverage** (coverlet), reported per feature.
- `build.ps1` single quality gate: prints standards, runs analysis/tests/QA/coverage, and ends with a
  **PASS/FAIL verdict** plus a written quality report.
- **Docs & traceability consistency validator** wired into the build.
- **Pre-commit hook** (fast gate) + **GitHub Actions CI** (full gate) so drift can't be committed or merged.

### Documentation
- Architecture, coding standards, testing, and a per-feature developer doc.
- **Product requirements** (per feature/sub-feature) + an end-user guide.
- A single-file **HTML user manual with screenshots**.
- A **Requirements spreadsheet** (JIRA-importable) and a **bi-directional traceability matrix**.

[2.0.0]: https://github.com/devarajsp/DigitalSecretary/releases/tag/v2.0.0
