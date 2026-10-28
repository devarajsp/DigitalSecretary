# Requirement - DeDupe Files

| | |
|---|---|
| Feature id | `dedupe-files` | Category | Files | Status | Proposed |

> Finds duplicate files in a folder tree using an **MD5 checksum list** kept in the folder itself, then
> reclaims the space by deleting extra copies - **always keeping one copy of every group**. Safe by
> default (Dry run ON, Recycle Bin, byte-for-byte verification). **100% local: no network, no cloud.**

## 1. Purpose & value
Photos, downloads, backups and archives accumulate identical copies under different names and folders.
This feature lets the user point at a folder, build a checksum list (`dedupe_md5_list.txt`) that
fingerprints every file, see exactly which files are duplicates and how much space they waste, and then -
only when they choose - remove the extra copies. The checksum list is **reusable and incremental**: later
runs hash only new or changed files, so re-checking a large folder is fast.

MD5 is used here as a **content fingerprint, not for security**. Because a fingerprint match is not
treated as proof, every deletion is additionally **verified byte-for-byte** against the copy being kept.

## 2. Users & user stories
- As a user, I pick a folder and the app finds every duplicate file beneath it, however deeply nested.
- As a user, I see how many duplicate groups there are and **how much space I would get back** before
  anything is touched.
- As a user, I choose **which copy is kept** (shortest path, oldest, newest, or first alphabetically).
- As a user, I can **preview** the plan (Dry run, the default) and only delete after an explicit confirmation.
- As a user, I want deleted duplicates to go to the **Recycle Bin** so a mistake is recoverable.
- As a user, I want a **second run to be fast** - only new or changed files are re-hashed.
- As a user, I can **pause, resume or stop** a long scan or deletion without losing the work done so far.
- As a user, I can **save a plain-text report** of what was found and what was done.
- As a user, I want **nothing to leave my PC** - no network, no cloud, no accounts.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| DDF-1 | **Choose folder** - the user picks the root folder to scan; the choice is remembered between runs. |
| DDF-2 | **Recursive enumeration** - walk every file in the folder, its subfolders and their subfolders; skip the checksum list file itself and skip reparse points (symlinks/junctions) so the walk cannot loop. |
| DDF-3 | **MD5 checksum per file** - compute an MD5 fingerprint for every file found, streamed so large files do not load into memory. |
| DDF-4 | **Checksum list file** - write `dedupe_md5_list.txt` in the chosen root folder: one line per file with MD5, size in bytes, last-modified time (UTC, ISO 8601) and the full path, tab-separated, with a comment header. |
| DDF-5 | **Reuse existing list** - if the list file already exists the user can update it (rescan) or use it as-is without scanning; the choice can be made each time (Ask) or fixed in the options. |
| DDF-6 | **Incremental rescan** - a stored checksum is reused when the file's size and last-modified time are unchanged, so only new or changed files are hashed; entries for files that no longer exist are pruned. |
| DDF-7 | **In-memory index** - the checksum list is loaded into memory once and all lookups and updates happen there, so repeated runs do not re-read the list from disk. |
| DDF-8 | **Duplicate analysis** - group files by identical MD5 and report the number of duplicate groups, the number of duplicate files, the duplicate size and the reclaimable space. |
| DDF-9 | **Show duplicates** - list each duplicate group in the window, showing the copy that will be kept and the copies that would be deleted. |
| DDF-10 | **Keeper policy** - the user chooses which copy to keep: shortest path, oldest modified, newest modified, or first alphabetically. |
| DDF-11 | **Ignore empty files** - zero-byte files are excluded from deduplication by default (they all share one checksum). |
| DDF-12 | **Dry run** - report-only mode is the default; nothing is deleted until the user turns Dry run off and confirms the count and size in a dialog. |
| DDF-13 | **Byte-for-byte verification** - before a duplicate is deleted it is compared byte-for-byte with the copy being kept; a mismatch is skipped and logged. |
| DDF-14 | **Recycle Bin or permanent** - duplicates go to the Recycle Bin by default; permanent deletion is opt-in. |
| DDF-15 | **Results summary** - report files deleted, files skipped, failures and the space freed. |
| DDF-16 | **Progress, pause and resume** - live progress with Pause/Resume and Stop for both the scan and the deletion. |
| DDF-17 | **Resumable work** - the checksum list is saved periodically during a scan, so a stopped scan is not redone from scratch next time. |
| DDF-18 | **Save report** - write a plain-text report of the duplicate groups and the results to a file the user chooses. |
| DDF-19 | **Settings persistence** - remember the folder and all options between runs; no secrets or personal data are stored. |

## 4. Sub-features (acceptance)

### 4.1 Choose folder (DDF-1)
*User story:* as a user I pick the folder to clean up and do not have to pick it again next time.
*Accept:* a folder browser sets the root; the path is shown; on the next launch the same folder is
pre-filled; a missing folder is reported clearly and no scan starts.

### 4.2 Recursive enumeration (DDF-2)
*User story:* as a user I want every file in the tree considered, without the app getting stuck.
*Accept:* files at any depth are found; `dedupe_md5_list.txt` is never listed as a file to hash; a
symlink or junction pointing back up the tree is not followed, so the walk terminates; an unreadable
folder or file is logged and skipped, and the run continues.

### 4.3 MD5 checksum per file (DDF-3)
*Accept:* two files with identical content get the same MD5; files differing by one byte get different
MD5s; a multi-gigabyte file is hashed with bounded memory (streamed in blocks).

### 4.4 Checksum list file (DDF-4)
*Accept:* after a scan the root folder contains `dedupe_md5_list.txt` starting with `#` comment header
lines, followed by one tab-separated line per file: `MD5<TAB>size<TAB>modified-UTC-ISO8601<TAB>full path`.
Paths containing spaces or non-ASCII characters round-trip correctly; the file is UTF-8.

### 4.5 Reuse existing list (DDF-5)
*User story:* as a user, if I already built a list I can analyze it right away without waiting for a rescan.
*Accept:* when the list exists the user is offered **Ask / Update / Use as-is**; *Update* rescans;
*Use as-is* goes straight to analysis without hashing anything; with the option set to *Ask* a prompt
appears each time, otherwise the fixed choice is applied silently.

### 4.6 Incremental rescan (DDF-6)
*Accept:* a rescan with no changes hashes zero files; a modified file (size or timestamp changed) is
re-hashed; a new file is hashed; entries whose file no longer exists are removed from the list.

### 4.7 In-memory index (DDF-7)
*Accept:* the list is read from disk once per session; analysis, rescan and delete work against the
in-memory index and only write the list back when it changes.

### 4.8 Duplicate analysis (DDF-8)
*User story:* as a user I want to know how much space I can get back before deciding anything.
*Accept:* files sharing an MD5 form one group; the summary shows duplicate groups, duplicate files,
duplicate size and reclaimable space (the size of all copies except the one kept per group); a group of
N identical files of size S contributes (N-1) duplicate files and (N-1) x S reclaimable bytes.

### 4.9 Show duplicates (DDF-9)
*Accept:* every group is listed; in each, the keeper is clearly marked and the others are shown as
"would be deleted"; the list is readable for thousands of groups (virtualized or paged).

### 4.10 Keeper policy (DDF-10)
*Accept:* with *shortest path* the copy with the shortest full path is kept; *oldest/newest modified*
keeps the earliest/latest last-modified time; *first alphabetically* keeps the first path in
case-insensitive order; ties are broken deterministically (alphabetical path) so results are repeatable.

### 4.11 Ignore empty files (DDF-11)
*Accept:* with the option on (default) zero-byte files never appear in any duplicate group and are never
deleted; with it off they are treated like any other duplicates.

### 4.12 Dry run (DDF-12)
*User story:* as a user I want to see exactly what would happen before anything is removed.
*Accept:* Dry run is ON by default and after a fresh install; in Dry run the Delete action only reports
the plan and removes nothing; with Dry run off, a confirmation dialog states the number of files and total
size and nothing is deleted unless the user confirms.

### 4.13 Byte-for-byte verification (DDF-13)
*Accept:* before each deletion the candidate is compared byte-for-byte with the kept copy; a difference
(a rare MD5 collision, or a file that changed since the scan) means the file is **not** deleted and the
event is logged and counted as skipped; the kept copy is never deleted.

### 4.14 Recycle Bin or permanent (DDF-14)
*Accept:* by default deleted duplicates are found in the Recycle Bin and can be restored; permanent
deletion happens only when the user explicitly enables it, and the confirmation dialog says so.

### 4.15 Results summary (DDF-15)
*Accept:* when deletion ends the window shows files deleted, files skipped (verification mismatch, file
missing, keeper unavailable), failures (access denied, in use) and the space freed, whether the run
completed or was stopped.

### 4.16 Progress, pause and resume (DDF-16)
*Accept:* scanning and deleting both show a progress bar with current item and counts; **Pause** halts
work at the next file and **Resume** continues; **Stop** ends the operation cleanly, keeping what has been
done; the window stays responsive throughout.

### 4.17 Resumable work (DDF-17)
*Accept:* during a long scan the checksum list is written to disk at regular intervals; if the scan is
stopped or the app is closed, the next *Update* reuses everything already hashed and continues with the
rest.

### 4.18 Save report (DDF-18)
*Accept:* **Save report** asks for a file name and writes a plain-text report containing the settings used,
each duplicate group (keeper and duplicates) and the results summary; the file opens in Notepad.

### 4.19 Settings persistence (DDF-19)
*Accept:* the folder and every option (keeper policy, ignore empty files, verify, Recycle Bin/permanent,
Dry run, existing-list behaviour) are restored on the next launch; the settings file contains no
passwords, tokens or personal data.

## 5. Acceptance criteria (feature)
Pointing the feature at a folder tree builds `dedupe_md5_list.txt`; a second run on an unchanged tree
hashes nothing; analysis reports the correct groups and reclaimable space; in the default configuration
(Dry run ON) no file is ever deleted; with Dry run off and the user's confirmation, extra copies are moved
to the Recycle Bin after byte-for-byte verification, **exactly one copy of every group always remains**,
and the results summary and saved report match what actually happened. No network access occurs.

## 6. Non-functional
- **Safety (hard constraint):** one copy of each duplicate group is always retained; Dry run ON by
  default; Recycle Bin by default; byte-for-byte verification before every delete; a deletion error never
  aborts the run or touches the kept copy.
- **Privacy / offline:** local only - no network, no cloud, no accounts. Only the folder path and option
  values are remembered.
- **Performance:** streamed hashing with bounded memory; incremental rescans via size + modified time; the
  checksum index is held in memory (DDF-7); all work runs off the UI thread and is pausable/cancellable.
- **Reliability:** per-file failures (locked, denied, vanished) are logged and skipped; the checksum list
  is saved periodically so interrupted work is not lost.
- **Honest about MD5:** MD5 is a content fingerprint for finding candidates, not a security measure; it is
  never trusted alone for deletion (DDF-13).
- **Testability:** enumeration, hashing, list read/write, grouping, keeper selection and verification are
  plain classes (per `docs/CODING_STANDARDS.md`) with unit tests; QA loads the feature.

## 7. Out of scope / future
- Near-duplicate or "similar image" detection (only exact byte-identical content is matched).
- Deduplicating across several root folders in one run; replacing duplicates with hard links.
- Scheduling, and any cloud or network storage target.

## 8. Traceability
- Code: `src/Features/DeDupeFiles/` (enumeration, hashing, checksum list and in-memory index, grouping and
  keeper policy, verified deletion = logic; the control = UI; a settings store).
- Tests: unit tests per logic class in `tests/DigitalSecretary.UnitTests`; QA loads and views the feature.
- Data: settings under `%APPDATA%\DigitalSecretary\data\dedupe-files\`; the checksum list
  `dedupe_md5_list.txt` lives in the user-chosen root folder.
- Companion docs: `docs/user-guide/features/dedupe-files.md`.
