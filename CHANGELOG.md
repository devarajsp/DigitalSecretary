# Changelog

All notable changes to Digital Secretary. Format based on [Keep a Changelog](https://keepachangelog.com).

## [2.1.0] - 2026-06-22

> ⚡ **Built with no code.** Both features below &mdash; **~1.0 KLOC** of production C# with unit + QA
> tests, full docs, screenshots, and a green quality gate &mdash; were built **entirely by asking Claude**
> (Opus 4.8), not hand-written: **~48 min**, **~$5.50 est. LLM cost**. We showcase this every release to
> track the no-code-development trend &mdash; full per-activity ledger + trend in
> [DevEconomics](docs/DEVECONOMICS.md).

### Features
- **Download Gmail** &mdash; save a local copy of every Gmail folder/label over IMAP, each email as
  `.txt` + `.eml` with all attachments (incl. inline images); read-only, so the mailbox is untouched.
  Uses a Gmail **app password**.
- **Download Google Drive** &mdash; save a local copy of your Drive over the Drive API (**read-only**
  OAuth); mirrors the folder tree, downloads regular files as-is, and **exports Google Docs/Sheets/Slides
  to both Office formats and PDF**. New **Cloud** feature category.

Both ship as independent plugins with their own private NuGet dependencies &mdash; **no change to the host**.

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
