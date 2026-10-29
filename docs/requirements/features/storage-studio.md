# Requirement - Storage Studio (phase 1: Space Map)

| | |
|---|---|
| Feature id | `storage-studio` | Category | Files | Status | Done (phase 1) |

> Shows **what is using the space** in a folder, visually and at a glance: a clickable size map
> coloured by file type, space by type and by age, the largest files and folders, clean-up candidates,
> and what grew since the previous scan. **Read-only and 100% local: no network, no cloud, nothing is
> deleted or moved.** Later phases add *similar pictures* and *similar videos*.

## 1. Purpose & value
Disks fill up silently. Before anything can be optimised the user needs to **see** where the space
went: which folders, which kinds of file, how old, and what changed lately. Folder sizes are not shown
by Windows Explorer, and lists of numbers are hard to compare. Storage Studio turns a folder into a
size map where the biggest things are literally the biggest rectangles, then lets the user drill in.

## 2. Users & user stories
- As a user, I pick a folder and see, in one picture, what takes up the most space inside it.
- As a user, I click into the biggest folder, and the next, until I find the culprit.
- As a user, I see how much of the space is videos, pictures, installers, archives and so on.
- As a user, I see how much of it I have not touched in years.
- As a user, I get the largest files and folders as lists I can sort through and open in Explorer.
- As a user, I get pointed at space that is commonly safe to reclaim, with a reason for each.
- As a user, when I scan the same folder again later, I see what grew and what shrank.
- As a user, I can pause or stop a long scan and still see what was found.
- As a user, I can save a plain-text report.
- As a user, I want nothing deleted, moved or uploaded by this view.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| SST-1 | **Choose folder** - the user picks the folder to map; the choice is remembered between runs; a missing folder is reported and no scan starts. |
| SST-2 | **Read-only recursive scan** - walk the folder and all subfolders reading directory metadata only (no file is opened); do not follow symlinks/junctions; log and skip unreadable folders. |
| SST-3 | **Size tree with rolled-up totals** - every folder knows its total size, file and folder counts, bytes per file type and newest file time, consistent even after a stopped scan. |
| SST-4 | **Size map** - draw the focused folder as a squarified treemap: area proportional to size, colour by file type (a folder by its dominant type), 1-3 nested levels (user setting, default 2), names and sizes on tiles that fit, long tails folded into one "smaller items" tile. |
| SST-5 | **Drill down** - clicking a folder tile (or a file's tile: its folder) focuses that folder; a breadcrumb and an Up button navigate back; double-clicking a row in the lists focuses its folder. |
| SST-6 | **Hover details** - hovering a tile shows its name, size, share of the focused folder, file count and dominant type (folders) or type and date (files), and the full path. |
| SST-7 | **Space by type** - a bar chart of bytes and share per file type for the focused folder; it doubles as the map's colour legend. |
| SST-8 | **Space by age** - a bar chart of bytes by time since last change: under 1 year, 1-3, 3-5, over 5 years. |
| SST-9 | **Largest files** - the 100 largest files under the focused folder with size, path, type and date. |
| SST-10 | **Largest folders** - the 100 largest folders under the focused folder with size, path and file count. |
| SST-11 | **Clean-up candidates (report only)** - build & package folders, temporary & partial files, installers & disk images, and large files (>= 1 GB) unchanged for 2+ years; each with total size, item list and a one-line reason; no file counted twice; nothing is changed. |
| SST-12 | **Changes since last scan** - after a completed scan, compare with the previous snapshot of the same folder and list folders that grew or shrank by >= 1 MB, biggest change first; the snapshot is stored gzip-compressed under the feature's data folder. |
| SST-13 | **Progress, pause and stop** - live progress (files, folders, bytes); Pause/Resume and Stop; a stopped scan shows the partial result and does not overwrite the snapshot. |
| SST-14 | **Explorer actions** - right-click a tile or row to show it in Explorer, copy its path, or open a folder in the map. |
| SST-15 | **Save report** - write a plain-text report of the focused folder (totals, by type, by age, largest files and folders, clean-up candidates, changes). |
| SST-16 | **Settings persistence** - remember the folder and map levels; nothing secret is stored. |
| SST-17 | **Readable, colour-blind-safe colours** - one fixed colour per file type from a colour-blind-safe set; label text is black or white by contrast (>= 3:1 on every tile colour). |

## 4. Sub-features (acceptance)

### 4.1 Choose folder & settings (SST-1, SST-16)
*Accept:* Browse sets the folder; it is pre-filled next time; map levels are remembered and clamped to
1-3; corrupt settings fall back to defaults; a missing folder shows a message and no scan runs.

### 4.2 Scan (SST-2, SST-3, SST-13)
*Accept:* sizes and counts match the files on disk at every depth; no file is opened; a junction to a
folder inside the tree is not counted twice; an unreadable or vanished folder is logged and counted as
an error and the scan continues; Pause holds the scan, Stop (also while paused) ends it as stopped with
the partial tree shown; a scan stopped before it starts returns an empty tree.

### 4.3 Size map & navigation (SST-4, SST-5, SST-6, SST-17)
*Accept:* tile areas are proportional to size, tiles never overlap and fill the map; folders big enough
are expanded with a name strip and their children inside; more than 200 children fold the rest into one
"N smaller items" tile; clicking a folder tile focuses it, the breadcrumb and Up return; tooltips show
the details listed; every tile colour gives its label at least 3:1 contrast.

### 4.4 Charts & lists (SST-7 - SST-10)
*Accept:* type shares sum to 100% and empty types are not shown; age always shows four buckets in age
order; the largest lists are sorted by size with ties by name and re-computed for the focused folder.

### 4.5 Clean-up candidates (SST-11)
*Accept:* `node_modules`, `obj`, `__pycache__` and similar count as build output, `bin` only beside an
`obj`; a nested `node_modules` is not double-counted; `.tmp`/`.crdownload`/`~$` files are temporary;
`.msi`/`.iso` count anywhere, `.exe` only under a Downloads folder (including when the scanned folder
itself is inside Downloads); a large file counts only when also old; cards are sorted by size; nothing
on disk changes.

### 4.6 Changes since last scan (SST-12)
*Accept:* the first scan of a folder says there is nothing to compare with; a second completed scan
lists grown, shrunk, new and removed folders by size of change; the snapshot is gzip, one file per
folder, named by a hash so no path text appears in the file name; a stopped scan does not replace it.

### 4.7 Report & Explorer (SST-14, SST-15)
*Accept:* Save report writes every section listed; Show in Explorer opens Explorer with the item
selected; Copy path puts the full path on the clipboard.

## 5. Non-functional
- **Privacy:** nothing leaves the PC; no file contents are read; snapshots hold sizes and relative
  folder paths only, in the feature's own data folder.
- **Performance:** metadata-only walk; roughly a million files per minute on an SSD; analysis of a
  focused folder runs off the UI thread; at most 200 tiles per folder level are drawn.
- **Memory:** the tree is held in memory (~150 bytes per file).
- **Safety:** read-only; no delete or move action exists in this phase.

## 6. Traceability
Rows SST-1 ... SST-17 in `tools/docgen/build_excel_docs.py` (requirements + traceability matrix);
code in `src/Features/StorageStudio/`; tests in
`tests/DigitalSecretary.UnitTests/Features/StorageStudio/` and the QA plugin pipeline.
