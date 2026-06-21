# Requirement — Download Emails (Yahoo)

| | |
|---|---|
| Feature id | `email-downloader` | Category | Communication | Status | Implemented |

## 1. Purpose & value
Let the user keep a **local backup copy** of their Yahoo Mail — every folder, message, and attachment —
without changing anything on the server.

## 2. Users & user stories
- As a user, I want to **download all my Yahoo folders** to my PC as readable files.
- As a user, I want **attachments** saved exactly as they are.
- As a user, I want a **copy only** — my mail must stay untouched in Yahoo.
- As a user, I want **progress** and the ability to **cancel**, plus clear **errors**.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| EML-1 | Connect to Yahoo over IMAP (SSL) using the user's email + **app password**. |
| EML-2 | Traverse **all** folders (Inbox, Sent, Archive, Spam, custom, nested). |
| EML-3 | Open folders **read-only** so nothing is deleted, moved, or marked read on the server. |
| EML-4 | Save each email as **both `.txt`** (readable) **and `.eml`** (full fidelity). |
| EML-5 | Save **all** attachments as-is, including inline/embedded images. |
| EML-6 | Mirror the remote folder structure locally; resolve name collisions with `_1, _2, …`. |
| EML-7 | Show a **progress bar + log**; allow **Cancel**; report per-item errors and continue. |
| EML-8 | Remember the email address + target folder; **never store the password**. |

## 4. Sub-features
### 4.1 Connect & authenticate
*Accept:* wrong/normal password ⇒ clear message telling the user an **app password** is required.
### 4.2 Folder traversal
*Accept:* every selectable folder is visited; counts shown.
### 4.3 Save formats (.txt + .eml)
*Accept:* each message produces a date-prefixed `.txt` and `.eml` sharing the base name.
### 4.4 Attachments (incl. inline)
*Accept:* file attachments keep their names; inline images with no filename get a synthesised name.
### 4.5 Progress & cancel
*Accept:* bar advances per message; Cancel stops cleanly with a "cancelled" note.
### 4.6 Error handling
*Accept:* a bad message/folder is logged with `!` and the run continues; fatal errors show a dialog.

## 5. Acceptance criteria (feature)
A run copies all folders/messages/attachments locally; the Yahoo mailbox is unchanged (read-only);
password is not written to disk.

## 6. Non-functional
- **Security/Privacy:** app password held in memory only; data local. **Reliability:** resilient to
  per-item failures. **Performance:** async, off the UI thread, cancellable.

## 7. Out of scope / future
Other providers (Gmail/Outlook/generic IMAP), OAuth, incremental/delta sync, scheduling, de-dup across runs.

## 8. Traceability
- Code: `src/Features/EmailDownloader/` (`EmailDownloader` = IMAP/IO, `EmailFileNaming` + `AttachmentScanner` = logic, `DownloadEmailsControl` = UI, `EmailSettingsStore`).
- Tests: `EmailFileNamingTests`, `AttachmentScannerTests`, `StoreTests`; QA loads the feature + verifies private MailKit.
- Data: `%APPDATA%\DigitalSecretary\data\email-downloader\email_settings.json` (no password).
