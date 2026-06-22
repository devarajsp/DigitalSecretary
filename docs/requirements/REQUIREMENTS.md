# DigitalSecretary — Requirements (App level)

> Audience: **product owner / business analyst**. This is the source of truth for *what* the app does
> and *why*. App-level requirements live here; each feature has its own file under
> [`features/`](features/). End-user "how to" lives in [`../user-guide/`](../user-guide/USER_GUIDE.md).

| | |
|---|---|
| Product | DigitalSecretary |
| Type | Pluggable personal-assistant **desktop app** (Windows, .NET 9 / WinForms) |
| Status | Active |
| Doc owner | Product |

## 1. Vision & purpose
A single, growable home for a person's day-to-day utilities ("features"). Each feature is independent
and can be added, shown, or hidden without affecting the rest. The app should be useful offline,
private by default, and easy to extend.

## 2. Target users (personas)
- **Power user (primary):** an individual on Windows who wants quick personal tools in one place.
- **Product owner:** decides which features exist and how they behave (uses this doc set).
- **Builder (Claude):** implements features to the standard without the user writing code.

## 3. Scope
- **In scope:** local desktop utilities; offline-first; per-feature local data; user-configurable
  visibility; add features over time.
- **Out of scope (today):** cloud sync, multi-user/roles, mobile/web clients, telemetry.

## 4. Application-level functional requirements (the shell)
| ID | Requirement |
|----|-------------|
| APP-1 | The app discovers installed features from a `plugins/` folder using lightweight manifests, with no feature code loaded at startup. |
| APP-2 | A feature's code is loaded **only when the user first opens it** (lazy), then reused. |
| APP-3 | Features are reachable via a **menu** grouped as Category → Feature. |
| APP-4 | A **Home dashboard** shows a tile per visible feature; clicking a tile opens it. |
| APP-5 | A **Configure Features** dialog lets the user show/hide each feature on the **dashboard** and in the **menu** *independently*; choices persist. |
| APP-6 | Each feature stores its data in its **own private folder**; features never read/write each other's data. |
| APP-7 | A new feature can be added **without modifying the host**. |
| APP-8 | A failing feature must **not crash** the app; the user sees a clear error. |

## 5. Non-functional requirements (NFR)
| ID | Area | Requirement |
|----|------|-------------|
| NFR-1 | Performance | Startup loads only manifests; opening a feature is near-instant. |
| NFR-2 | Privacy/Security | Secrets (e.g. email app password) are **never persisted**; all data is local to the PC. |
| NFR-3 | Reliability | A feature error is contained and reported; the host stays alive. |
| NFR-4 | Usability | Consistent layout; features discoverable via menu + dashboard. |
| NFR-5 | Quality | Logic unit-tested; plugin pipeline QA-tested; static analysis clean; coverage on target (see `TESTING.md`). |
| NFR-6 | Extensibility | Plugin architecture; features are independent projects. |
| NFR-7 | Portability | Windows 10/11, .NET 9 runtime. |

## 6. Constraints & assumptions
- Windows + .NET 9; WinForms UI. Single local user. Internet only where a feature needs it
  (e.g. email download).

## 7. Feature index
| Feature | Category | Requirements | User guide |
|---------|----------|--------------|------------|
| Launcher | Tools | [features/launcher.md](features/launcher.md) | [user guide](../user-guide/features/launcher.md) |
| Calculator | Tools | [features/calculator.md](features/calculator.md) | [user guide](../user-guide/features/calculator.md) |
| Clipboard History | Tools | [features/clipboard-history.md](features/clipboard-history.md) | [user guide](../user-guide/features/clipboard-history.md) |
| Download Emails | Communication | [features/email-downloader.md](features/email-downloader.md) | [user guide](../user-guide/features/email-downloader.md) |
| Download Gmail | Communication | [features/gmail-downloader.md](features/gmail-downloader.md) | [user guide](../user-guide/features/gmail-downloader.md) |
| Download Google Drive | Cloud | [features/google-drive-downloader.md](features/google-drive-downloader.md) | [user guide](../user-guide/features/google-drive-downloader.md) |
| Email Intelligence | Insights | [features/email-intelligence.md](features/email-intelligence.md) | [user guide](../user-guide/features/email-intelligence.md) |

## 8. Glossary
- **Feature / plugin:** an independent capability, shipped as its own module/DLL.
- **Host / shell:** the main app window (menu + dashboard + content area).
- **Manifest (`plugin.json`):** metadata that lets the host show a feature without loading it.
- **Dashboard:** the home screen of feature tiles.

## 9. Traceability
- App FRs map to the host (`src/DigitalSecretary.App`) and are verified by the QA pipeline
  (`tests/DigitalSecretary.QaTests`). Each feature file links its FRs to code + tests.

## 10. Companion spreadsheets
- **Requirements (flat, JIRA-importable):** [`DigitalSecretary-Requirements.xlsx`](DigitalSecretary-Requirements.xlsx)
  — every requirement (App, Features, Sub-features, NFRs) as rows for import into JIRA / PM tools.
- **Bi-directional traceability matrix:** [`../traceability/DigitalSecretary-Traceability-Matrix.xlsx`](../traceability/DigitalSecretary-Traceability-Matrix.xlsx)
  — maps each feature/sub-feature to its requirement ID, requirement doc, mock/screen, code, unit test,
  QA test, user manual, architecture doc, and code doc, with an auto **Coverage** flag.
- Both are generated from `tools/docgen/build_excel_docs.py` — edit its data tables and run
  `python tools/docgen/build_excel_docs.py`.

## 11. Document rules
- Keep this set current: when a feature is added/changed, update its requirement file, its user-guide
  file, **and the requirements + traceability spreadsheets** (App / Feature / Sub-feature). New feature
  ⇒ new file in `features/` + `../user-guide/features/` + new rows in both spreadsheets.
