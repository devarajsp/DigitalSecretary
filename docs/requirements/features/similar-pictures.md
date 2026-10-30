# Requirement - Similar Pictures

| | |
|---|---|
| Feature id | `similar-pictures` | Category | Files | Status | Done |

> Finds pictures that **look** the same - exact copies, re-saved / resized / lightly edited versions,
> **turned or mirrored** copies, and **burst shots or retakes** - shows each group side by side, and
> moves the extras the user chooses to a **review folder** that can be undone. Near-identical scenes are
> offered for **manual verification only**. **100% local: no network, no cloud, no AI service.**

## 1. Purpose & value
Photo collections fill with look-alikes that a byte-for-byte duplicate finder cannot see: the copy a
chat app shrank, the one an editor re-saved, the one turned the right way up, and ten burst frames of
the same moment. They waste space and make albums harder to browse. This feature recognises them by
what they *look like*, picks the best one to keep, and lets the user confirm each group before
anything moves - and undo the move afterwards.

## 2. Users & user stories
- As a user, I pick my pictures folder and the app finds every group of look-alike pictures in it.
- As a user, I see each group side by side, with the one the app suggests keeping clearly marked.
- As a user, I want exact and re-saved copies pre-marked, but near-identical *different* shots left for
  me to decide.
- As a user, I want turned and mirrored copies recognised too.
- As a user, I can say how strict the matching is, and see the result change at once.
- As a user, I can change which picture is kept, open any picture, and see it in Explorer.
- As a user, I want removed pictures moved to a folder I can check, and an Undo.
- As a user, I want a second scan of the same folder to be quick.
- As a user, I want my iPhone HEIC and camera RAW pictures included when Windows can show them.
- As a user, I want nothing to leave my PC.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| SIM-1 | **Choose folder & settings** - the user picks the folder; it and all options are remembered; a missing folder is reported and no scan starts. |
| SIM-2 | **Find pictures** - walk every subfolder for JPEG, PNG, GIF, BMP, TIFF, HEIC/HEIF, WebP, AVIF and common camera RAW files; do not follow links; never scan the review folder; log and skip unreadable folders. |
| SIM-3 | **Read through Windows** - read each picture as a small thumbnail through the Windows Shell thumbnail engine (any format Windows has a codec for, EXIF rotation applied), falling back to the built-in decoder; read dimensions and the EXIF capture time (with sub-seconds) from the header; count pictures that cannot be read. |
| SIM-4 | **Visual fingerprint** - compute a perceptual hash (DCT) and a difference hash for the picture in each of its 8 orientations, plus a sharpness score. |
| SIM-5 | **Fingerprint cache** - keep fingerprints per scanned folder, gzip-compressed in the feature's data folder; reuse them while a file's size and modified time are unchanged; autosave during a scan; prune entries for deleted files only after a completed scan. |
| SIM-6 | **Match kinds** - classify each match as Exact copy (byte-identical, verified), Same picture (both hashes within tight limits, not two different moments) or Similar - check (both hashes within looser limits). |
| SIM-7 | **Turned and mirrored copies** - optionally match a picture with its 90/180/270-degree turned or mirrored version (Same picture only). |
| SIM-8 | **Similar scenes and bursts** - optionally group near-identical scenes and burst shots, with extra tolerance for shots taken within 30 seconds; two pictures whose capture times differ are never "Same picture". |
| SIM-9 | **Sensitivity** - Strict, Balanced (default) or Loose; changing it or any option regroups from the cache without rescanning. |
| SIM-10 | **Keeper choice** - keep the picture with the most pixels, then the sharpest, then the biggest file, then a name that does not look like a copy, then the earliest, then the shortest path. |
| SIM-11 | **Grouping by keeper** - each group is the best unassigned picture plus its unassigned matches, so every member is compared with the keeper itself (no chaining). |
| SIM-12 | **Review screen** - a list of groups (match kind, pictures, marked) and the selected group's pictures side by side, each with a KEEP / MOVE / keep badge, file name, dimensions, size, date, relative sharpness, how it matches, and its folder. |
| SIM-13 | **Manual verification** - Exact and Same-picture members are pre-marked; Similar - check members never are; a click toggles a picture's mark; Mark all / Keep all per group; "Keep this one instead"; open a picture; show it in Explorer; Previous / Next (also Ctrl+arrows, PageUp/PageDown). |
| SIM-14 | **Move to review folder** - move the marked pictures into `_Similar pictures - review` inside the scanned folder, keeping their sub-folder layout; never move a group's keeper; skip files changed since the scan; never overwrite (add " (2)"); confirm first, with Cancel as the default. |
| SIM-15 | **Recycle Bin option** - alternatively send the marked pictures to the Windows Recycle Bin. |
| SIM-16 | **Undo last move** - put the last moved batch back, also after the app was closed; never overwrite a file that has taken a picture's old place. |
| SIM-17 | **Progress, pause and stop** - live progress during scan and move, Pause/Resume and Stop; a stopped scan keeps the work done and prunes nothing. |
| SIM-18 | **Save report** - write a plain-text report of every group: keeper, members, what is marked and how each matches. |

## 4. Sub-features (acceptance)

### 4.1 Folder, settings and scan (SIM-1, SIM-2, SIM-5, SIM-17)
*Accept:* the folder and options are pre-filled next time; pictures at any depth are found, other files
and the review folder are not; a second scan reads nothing that did not change; a changed file is read
again, a deleted one leaves the cache after a full scan; a scan stopped before it starts forgets
nothing; Pause holds and Stop (also while paused) ends the scan.

### 4.2 Reading and fingerprints (SIM-3, SIM-4)
*Accept:* JPEG and PNG are read with correct dimensions; a non-picture or missing file is unreadable,
not a crash; the reader falls back to the next provider; EXIF times parse with sub-seconds; a resized,
re-compressed (JPEG quality 40), 15%-brighter or 3%-cropped copy stays within the Same-picture limits;
a turned or mirrored copy matches at <= 2 bits in the right orientation; unrelated pictures are never
the Same picture in any orientation.

### 4.3 Matching and grouping (SIM-6 - SIM-11)
*Accept:* byte-identical files are Exact when verified; a resized copy is the Same picture with the
original kept; turned copies match only when the option is on; burst frames with different capture times
are Similar and not pre-marked, and vanish when the option is off; the limits loosen from Strict to
Loose; a chain A~B~C does not pull C into A's group; groups are numbered with the most space to gain
first.

### 4.4 Review, move and undo (SIM-12 - SIM-16, SIM-18)
*Accept:* the keeper is never in the move plan; moved pictures land in the review folder with their
layout; a clash gets " (2)"; a changed file is skipped; Recycle Bin mode calls the recycler and leaves
no undo batch; one failure does not stop the rest; Undo restores everything it can and never overwrites;
the last batch survives a restart; the report lists KEEP / MOVE per picture and how it matches.

## 5. Non-functional
- **Privacy:** nothing leaves the PC; no AI service; fingerprints hold only hashes, sizes, dates and paths,
  in the feature's own data folder; screenshots use generated sample pictures and placeholder paths.
- **Safety:** reversible by default (review folder + Undo); keepers never move; confirmation defaults to Cancel.
- **Performance:** thumbnails come from the Windows thumbnail cache where possible; 2 - 4 pictures are
  read in parallel; all-pairs comparison is parallel and suits collections up to tens of thousands.

## 6. Traceability
Rows SIM-1 ... SIM-18 in `tools/docgen/build_excel_docs.py` (requirements + traceability matrix);
code in `src/Features/SimilarPictures/`; tests in
`tests/DigitalSecretary.UnitTests/Features/SimilarPictures/` and the QA plugin pipeline.
