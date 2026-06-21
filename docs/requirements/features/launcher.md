# Requirement — Launcher

| | |
|---|---|
| Feature id | `launcher` | Category | Tools | Status | Implemented |

## 1. Purpose & value
Let the user open their frequently-used apps, files, folders, and websites from one place in a single
click, instead of hunting through the Start menu, Explorer, or browser bookmarks.

## 2. Users & user stories
- As a user, I want to **save a shortcut** to an app/file/folder/URL so I can reach it quickly.
- As a user, I want to **launch** a saved item with one double-click.
- As a user, I want to **edit/remove** items so my list stays tidy.
- As a user, I want my list to **persist** between sessions.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| LAU-1 | The user can add a shortcut to an application/file, a folder, or a URL. |
| LAU-2 | Each shortcut has a friendly **Name** and a **Target** (path or URL). |
| LAU-3 | Double-clicking (or **Launch**) opens the target with its default Windows handler. |
| LAU-4 | The user can **edit** a shortcut's name and target. |
| LAU-5 | The user can **remove** a shortcut (with confirmation). |
| LAU-6 | Shortcuts persist locally and reload on next launch. |
| LAU-7 | A failed launch shows a clear message and does not crash the app. |

## 4. Sub-features
### 4.1 Add app/file shortcut
File picker → optional rename → saved. *Accept:* selecting a file adds an item whose target is that file.
### 4.2 Add folder shortcut
Folder picker → optional rename → saved. *Accept:* launching opens the folder in Explorer.
### 4.3 Add URL shortcut
Prompt for URL + name. *Accept:* launching opens the URL in the default browser.
### 4.4 Edit shortcut
Change name/target of the selected item. *Accept:* changes persist and show in the list.
### 4.5 Remove shortcut
Delete selected after a Yes/No confirm. *Accept:* item disappears and stays gone after restart.
### 4.6 Launch shortcut
Open selected via shell execute. *Accept:* apps/files/folders/URLs all open correctly.
### 4.7 Persistence
Store as JSON in the feature's data folder. *Accept:* list survives app restart; corrupt file ⇒ empty list, no crash.

## 5. Acceptance criteria (feature)
All sub-feature criteria pass; list reflects add/edit/remove immediately and after restart.

## 6. Non-functional
- Privacy: targets stored locally only. Reliability: launch failures are caught and reported.

## 7. Out of scope / future
Icons, drag-to-reorder, categories/folders, arguments UI (model already supports `Arguments`).

## 8. Traceability
- Code: `src/Features/Launcher/` (`LauncherControl`, `LauncherStore`, `LauncherItem`).
- Tests: `tests/DigitalSecretary.UnitTests/Features/StoreTests.cs` (persistence); QA pipeline (loads/builds view).
- Data: `%APPDATA%\DigitalSecretary\data\launcher\shortcuts.json`.
