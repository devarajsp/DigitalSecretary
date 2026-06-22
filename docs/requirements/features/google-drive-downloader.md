# Requirement — Download Google Drive

| | |
|---|---|
| Feature id | `google-drive-downloader` | Category | Cloud | Status | Implemented |

## 1. Purpose & value
Let the user keep a **local backup copy** of their Google Drive — folders, regular files, and Google-native
documents — without changing anything on the server. Google Docs/Sheets/Slides are exported to usable
formats since they have no raw bytes to download.

## 2. Users & user stories
- As a user, I want to **download my whole Drive** to my PC, keeping the folder structure.
- As a user, I want **Google Docs/Sheets/Slides** saved in formats I can actually open and edit.
- As a user, I want a **copy only** — my Drive must stay untouched (read-only access).
- As a user, I want **progress** and the ability to **cancel**, plus clear **errors**.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| GDR-1 | Authenticate with the **OAuth 2.0 installed-app flow** using the user's own *Desktop app* client (`credentials.json`), requesting **read-only** Drive scope; cache the token for later runs. |
| GDR-2 | List all non-trashed files via the Drive API (paged) and build the folder tree from parents. |
| GDR-3 | **Mirror** the Drive folder structure locally. |
| GDR-4 | Download **regular files** (PDF, images, Office, …) as-is. |
| GDR-5 | **Export Google-native** files to **both** an Office format **and** PDF (Docs→docx, Sheets→xlsx, Slides→pptx, Drawings→png; all +pdf). |
| GDR-6 | Skip non-exportable native items (Forms/Sites/Apps Script/shortcuts) with a note; resolve name collisions with `_1, _2, …`. |
| GDR-7 | Show a **progress bar + log**; allow **Cancel**; report per-item errors and continue. |
| GDR-8 | Remember the credentials-file path + target folder; the OAuth token is cached separately and **never** written to the settings file. |

## 4. Sub-features
### 4.1 OAuth sign-in (read-only)
*Accept:* missing/invalid credentials ⇒ clear message to create a Desktop-app OAuth client; first run opens
a browser to consent; later runs reuse the cached token.
### 4.2 List & build folder tree
*Accept:* all non-trashed items are listed (paged); parent chain resolves to a local path; orphans/cycles handled.
### 4.3 Download regular files
*Accept:* binary files are saved byte-for-byte with their original names.
### 4.4 Export Google documents (Office + PDF)
*Accept:* a Doc yields `.docx` + `.pdf`; a Sheet `.xlsx` + `.pdf`; Slides `.pptx` + `.pdf`; a Drawing `.png` + `.pdf`.
### 4.5 Progress & cancel
*Accept:* bar advances per file; Cancel stops cleanly with a "cancelled" note.
### 4.6 Error/skip handling
*Accept:* a failed item is logged with `!` and the run continues; non-exportable native items are skipped with `–`.

## 5. Acceptance criteria (feature)
A run recreates the Drive tree locally with regular files copied and Google docs exported to Office + PDF;
Drive is unchanged (read-only scope); no OAuth token is written to the settings file.

## 6. Non-functional
- **Security/Privacy:** read-only scope; token cached by Google's secure store inside the feature's data
  folder; only the credentials path + folder are remembered. **Reliability:** resilient to per-item
  failures. **Performance:** async, off the UI thread, cancellable.

## 7. Out of scope / future
Service-account/shared-drive support, incremental/delta sync, scheduling, choosing export format per run.

## 8. Traceability
- Code: `src/Features/GoogleDriveDownloader/` (`GoogleDriveDownloader` = OAuth/API/IO, `GoogleExportFormats` + `DriveFileNaming` = logic, `DownloadDriveControl` = UI, `DriveSettingsStore`).
- Tests: `GoogleExportFormatsTests`, `DriveFileNamingTests`, `StoreTests`; QA loads the feature + verifies private Google.Apis.
- Data: `%APPDATA%\DigitalSecretary\data\google-drive-downloader\drive_settings.json` (path + folder; token cached under `token\`).
