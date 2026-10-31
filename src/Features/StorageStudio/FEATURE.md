# Feature: Storage Studio

Shows **what is using the space** in a chosen folder, visually: a clickable **size map** (treemap)
coloured by file type, bar charts of space **by type** and **by age**, the **largest files** and
**folders**, conservative **clean-up candidates**, and **what changed since the previous scan**.
Phase 1 of the Storage Studio strategy (similar pictures and similar videos come later).
**Read-only:** it never deletes, moves or opens a file.

| | |
|---|---|
| **Id** | `storage-studio` |
| **Category** | Files |
| **Entry type** | `DigitalSecretary.Features.StorageStudio.StorageStudioModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\storage-studio\storage_studio_settings.json` (folder + map levels) and `snapshots\<hash>.json.gz` (one compressed snapshot per scanned folder) |
| **In the scanned folder** | nothing &mdash; the folder is only read |
| **NuGet** | none &mdash; framework only (file I/O, `System.IO.Compression`, `System.Text.Json`, WinForms/GDI+) |

## What it does
1. **Scan** walks the folder and every subfolder reading **directory metadata only** (name, size,
   modified time) &mdash; no file is opened, so it is fast and safe anywhere. Symlinks and junctions are
   not followed; unreadable folders are logged and skipped. Pause / Resume / Stop work throughout, and
   a stopped scan still shows what it found.
2. The result is a **`SpaceNode` tree**. Each folder keeps running totals (size, file and folder
   counts, bytes per `FileCategory`, newest file time) that are updated as each file is added, so a
   partial tree is always consistent and the per-folder type breakdown costs nothing to read.
3. The window **focuses** one folder at a time (the root first). The map, both charts and all lists
   describe the focused folder; clicking a folder tile, a breadcrumb segment, **Up**, or double-clicking
   a list row changes the focus. Re-analysis runs off the UI thread and stale answers are dropped.
4. **Clean-up candidates** are *pointed out*, never acted on: build & package folders (`node_modules`,
   `obj`, `bin` only beside an `obj`, `__pycache__` ...), temporary and partial files, installers and
   disk images (`.exe` only inside a Downloads folder), and files over 1 GB unchanged for 2 years. Each
   file is counted in at most one card and a matched build folder is not searched further.
5. After a **completed** scan a compact **snapshot** (totals + folder sizes four levels deep, gzip) is
   compared with the previous one for that folder, then replaces it. The **Changes since last scan**
   tab lists folders that grew or shrank by 1 MB or more.

## Classes
| Class | Role | Pure? |
|---|---|---|
| `FileTypeClassifier` / `FileCategory` | extension &rarr; category, labels | yes |
| `SpaceNode` | tree node with rolled-up totals, paths, sort | yes |
| `SpaceScanner` | read-only folder walk &rarr; tree; pause/stop/progress | I/O edge |
| `SpaceAnalyzer` | by type, by age, largest files / folders (bounded heap) | yes |
| `CleanupFinder` | the four report-only clean-up rules | yes |
| `TreemapLayout` | squarified treemap geometry + nesting + "smaller items" tail | yes |
| `SpaceSnapshot`, `GrowthComparer`, `SnapshotStore` | snapshot, compare, gzip store | store is I/O |
| `SpaceReport`, `SpaceReportText` | the saved plain-text report | yes |
| `CategoryPalette` | colour-blind-safe colours, tints, contrast-picked label text | yes |
| `StorageSettings(Store)` | remembered folder + map levels | I/O edge |
| `SampleSpaceTree` | placeholder tree for screenshots / QA (no real paths) | yes |
| `TreemapControl`, `BarChartControl`, `StorageStudioControl` | painting + wiring only | UI |
| `ByteSize`, `PauseToken` | from the shared helper kit (`src/DigitalSecretary.Kit`) | yes |

## The design decisions that matter
**Metadata only, never file contents.** A space map only needs sizes and dates, which the directory
listing already returns. Not opening files makes the scan an order of magnitude faster than hashing and
means it cannot be blocked or slowed by locked or huge files.

**Totals roll up on insert.** Adding a file walks up its ancestors once (O(depth)), so every folder's
size, counts and per-type bytes are exact at every moment &mdash; including after Stop &mdash; and the
"by type" chart for any folder is read straight from its node rather than recomputed.

**Squarified layout.** Treemap tiles are laid out with the squarified algorithm (Bruls, Huizing & van
Wijk, 2000), which keeps tiles close to square so sizes can be compared by eye. Only the 200 largest
children of a folder get their own tile; the rest share one grey "N smaller items" tile, which keeps
drawing fast on folders with tens of thousands of files.

**Report, do not act.** Phase 1 is an audit. Deleting is the job of DeDupe Files (exact duplicates)
and of later phases (similar media); clean-up cards say *where to look and why* and offer **Show in
Explorer**.

## Tests
`tests/DigitalSecretary.UnitTests/Features/StorageStudio/` &mdash; one file per class, plus
`StorageStudioPipelineTests` (scan &rarr; analyse &rarr; snapshot &rarr; change &rarr; rescan &rarr; growth)
and the QA test `Storage_Studio_map_drills_into_a_folder_when_its_tile_is_clicked`, which loads the
real plugin, shows the sample tree and clicks a folder tile.

## Known limits
- Sizes are **logical** file sizes, not space on disk (compression / cluster slack are not counted).
- The whole tree is held in memory: roughly 150 bytes per file, so ~150 MB for a million files.
- "Large and old" uses the **modified** time; Windows does not reliably record when a file was last opened.
