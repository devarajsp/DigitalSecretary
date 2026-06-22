# Requirement — Download Gmail

| | |
|---|---|
| Feature id | `gmail-downloader` | Category | Communication | Status | Implemented |

## 1. Purpose & value
Let the user keep a **local backup copy** of their Gmail — every folder/label, message, and attachment —
without changing anything on the server.

## 2. Users & user stories
- As a user, I want to **download all my Gmail folders/labels** to my PC as readable files.
- As a user, I want **attachments** saved exactly as they are.
- As a user, I want a **copy only** — my mail must stay untouched in Gmail.
- As a user, I want **progress** and the ability to **cancel**, plus clear **errors**.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| GML-1 | Connect to Gmail over IMAP (SSL) using the user's email + **app password**. |
| GML-2 | Traverse **all** folders/labels (Inbox, Sent, `[Gmail]/All Mail`, custom labels, nested). |
| GML-3 | Open folders **read-only** so nothing is deleted, moved, or marked read on the server. |
| GML-4 | Save each email as **both `.txt`** (readable) **and `.eml`** (full fidelity). |
| GML-5 | Save **all** attachments as-is, including inline/embedded images. |
| GML-6 | Mirror the remote folder structure locally; resolve name collisions with `_1, _2, …`. |
| GML-7 | Show a **progress bar + log**; allow **Cancel**; report per-item errors and continue. |
| GML-8 | Remember the email address + target folder; **never store the password**. |

## 4. Sub-features
### 4.1 Connect & authenticate
*Accept:* wrong/normal password ⇒ clear message that an **app password** (with 2-Step Verification) is required.
### 4.2 Folder/label traversal
*Accept:* every selectable folder/label is visited; counts shown. Label overlap (a message under several
labels) may save the message more than once — documented behaviour.
### 4.3 Save formats (.txt + .eml)
*Accept:* each message produces a date-prefixed `.txt` and `.eml` sharing the base name.
### 4.4 Attachments (incl. inline)
*Accept:* file attachments keep their names; inline images with no filename get a synthesised name.
### 4.5 Progress & cancel
*Accept:* bar advances per message; Cancel stops cleanly with a "cancelled" note.
### 4.6 Error handling
*Accept:* a bad message/folder is logged with `!` and the run continues; fatal errors show a dialog.

## 5. Acceptance criteria (feature)
A run copies all folders/messages/attachments locally; the Gmail mailbox is unchanged (read-only);
password is not written to disk.

## 6. Non-functional
- **Security/Privacy:** app password held in memory only; data local. **Reliability:** resilient to
  per-item failures. **Performance:** async, off the UI thread, cancellable.

## 7. Out of scope / future
OAuth/Gmail API, de-duplication across labels, incremental/delta sync, scheduling.

## 8. Traceability
- Code: `src/Features/GmailDownloader/` (`GmailDownloader` = IMAP/IO, `GmailFileNaming` + `GmailAttachmentScanner` = logic, `DownloadGmailControl` = UI, `GmailSettingsStore`).
- Tests: `GmailFileNamingTests`, `GmailAttachmentScannerTests`, `StoreTests`; QA loads the feature + verifies private MailKit.
- Data: `%APPDATA%\DigitalSecretary\data\gmail-downloader\gmail_settings.json` (no password).
