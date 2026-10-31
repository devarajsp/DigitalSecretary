# Feature: DeDupe Files

Finds files whose **contents** are identical anywhere under a chosen folder, shows what the extra
copies cost, and removes them &mdash; always keeping one copy of every set. Identity is established by
an **MD5 checksum list** kept in the folder itself, so a second run is nearly free.

| | |
|---|---|
| **Id** | `dedupe-files` |
| **Category** | Files |
| **Entry type** | `DigitalSecretary.Features.DeDupeFiles.DeDupeFilesModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\dedupe-files\dedupe_settings.json` (folder + options) |
| **In the scanned folder** | `dedupe_md5_list.txt` (the checksum list); `dedupe_report.txt` only if the user saves one |
| **NuGet** | none &mdash; framework only (`System.Security.Cryptography`, WinForms) |

## What it does
1. **Scan** walks the folder, every subfolder and their subfolders, and fingerprints each file with a
   streamed MD5. The result is written to `dedupe_md5_list.txt` **in the scanned folder**:
   `MD5 <TAB> size <TAB> modified-UTC <TAB> full path`, with a comment header. The list file itself is
   never fingerprinted, and reparse points (symlinks/junctions) are skipped so the walk cannot loop.
2. **A list that already exists** can be *updated* or *used as it is* &mdash; the choice is asked each
   time by default, or fixed in the options.
3. **Analyze** groups the entries by checksum and reports the duplicate sets, the removable copies,
   the space those copies occupy and the space a clean-up would reclaim. Each set is listed with the
   copy that will be kept and the copies that would go.
4. **Delete** removes the duplicates. One copy of every set is always kept; **Dry run** is on until
   the user turns it off and confirms, deletions go to the **Recycle Bin** by default, and each file
   is compared **byte-for-byte** with the kept copy first.

## The two design decisions that matter
**The checksum list is the cache, and it is incremental.** A stored checksum is reused whenever the
file's size *and* last-modified time are unchanged, so re-running over a 200 GB folder re-reads only
what actually changed. The list is autosaved every 500 files, so **Stop** never throws away work: the
next run picks up where it left off. Entries for files that have since disappeared are pruned &mdash;
but only after a scan that ran to completion, because a scan that was stopped has not seen every file
and must not conclude the rest are gone.

**MD5 decides what to compare; bytes decide what to delete.** MD5 is a *content fingerprint* here,
never a security measure, and two different files can share one. So nothing is deleted on the strength
of a hash: `FileComparer.AreIdentical` re-reads the duplicate and the kept copy and compares them
block by block, and a mismatch is logged and skipped. That is also why the keeper must still exist at
the moment of deletion &mdash; if it has gone, the whole set is left alone.

## Files
| File | Role |
|------|------|
| `DeDupeFilesModule.cs` | `IFeatureModule` entry point. |
| `DeDupeFilesControl.cs` | The UI: folder + options, command bar, progress, duplicate grid, log. |
| `ExistingListPrompt.cs` | The "a list already exists" question (update / use as-is / cancel). |
| `ScanEngine.cs` | Folder walk (`Collect`) and the incremental hash pass (`Run`), with pause/stop. |
| `FileHasher.cs` | `IFileHasher` + the streaming `Md5FileHasher` (cancellable between blocks). |
| `ChecksumEntry.cs` | One list line: checksum, size, modified stamp, path (+ `Matches`). |
| `ChecksumListFile.cs` | Reads/writes `dedupe_md5_list.txt`; tolerant parser, atomic write. |
| `ChecksumIndex.cs` | The in-memory list: lookup by path, grouping by checksum, pruning. |
| `DuplicateAnalyzer.cs` | Pure analysis: groups, savings, and which copy to keep (`PickKeeper`). |
| `DeleteEngine.cs` | Guarded deletion + `IFileDeleter` (Recycle Bin or permanent). |
| `FileComparer.cs` | Byte-for-byte comparison &mdash; the guard in front of every delete. |
| `PauseToken` (kit) | The pause/resume gate the engines wait on. |
| `DeDupeReportText.cs` | Renders the saved text report. |
| `ByteSize` (kit) | Human-readable sizes. |
| `HexText.cs` | Lower-case hex, with no culture-sensitive casing. |
| `DeDupeSettings.cs` | Settings model + JSON store, and the `KeeperPolicy` / `ExistingListAction` choices. |
| `plugin.json` | Manifest. |

## Keeping one copy
`PickKeeper` applies the chosen policy &mdash; shortest path (default), oldest, newest, or first
alphabetically &mdash; and **every policy ends with a path tie-break**, so the same folder always
yields the same survivor. A user who re-runs a scan must never get a different answer.

## Safety notes
- Nothing leaves the PC: no network, no cloud, no AI.
- Zero-byte files share one checksum, so they are left alone by default (`Ignore empty files`).
- Only the chosen folder and the option toggles are remembered; there is nothing secret to store.
- The feature writes exactly one file into the scanned folder (`dedupe_md5_list.txt`), plus a report
  if the user asks for one.
