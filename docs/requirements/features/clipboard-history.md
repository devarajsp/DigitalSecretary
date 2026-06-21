# Requirement — Clipboard History

| | |
|---|---|
| Feature id | `clipboard-history` | Category | Tools | Status | Implemented |

## 1. Purpose & value
Windows keeps only the latest clipboard text. This feature remembers recent copied text so the user
can paste something they copied earlier.

## 2. Users & user stories
- As a user, I want text I copy anywhere to be **remembered** automatically.
- As a user, I want to **re-copy** an earlier item to paste it again.
- As a user, I want to **delete** an item or **clear** the whole list.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| CLP-1 | While the feature is open, new clipboard **text** is captured automatically. |
| CLP-2 | Entries are listed newest-first, de-duplicated, capped at 50; each shows a single-line preview. |
| CLP-3 | The user can **copy** a selected entry back to the clipboard (double-click or Copy). |
| CLP-4 | The user can **delete** a selected entry or **clear all**. |
| CLP-5 | Clipboard read/write errors are ignored gracefully (no crash). |

## 4. Sub-features
### 4.1 Auto-capture
Poll the clipboard (~0.8s) and record changed, non-empty text. *Accept:* copying new text adds it to the top.
### 4.2 Re-copy
*Accept:* selecting an entry and clicking Copy puts the full original text on the clipboard.
### 4.3 Delete entry / Clear all
*Accept:* delete removes one; clear empties the list.

## 5. Acceptance criteria (feature)
Newest-first, deduped, max 50; preview collapses newlines and truncates long text with "…".

## 6. Non-functional
- Privacy: history is **session-only** (in memory), not written to disk. Resilient to clipboard locks.

## 7. Out of scope / future
Persist across sessions (opt-in), pin favorites, capture images/files, search/filter.

## 8. Traceability
- Code: `src/Features/ClipboardHistory/` (`ClipboardPreview` = logic, `ClipboardHistoryControl` = UI + polling).
- Tests: `tests/DigitalSecretary.UnitTests/Features/ClipboardPreviewTests.cs`.
