# Requirement - Similar Videos

| | |
|---|---|
| Feature id | `similar-videos` | Category | Files | Status | Done |

> Finds videos that are the **same** - exact copies, the same video **re-encoded, resized or
> re-compressed**, and **turned or mirrored** copies - shows each group as film strips, and moves the
> extras the user chooses to a **review folder** that can be undone. Other takes of the same scene are
> offered for **manual verification only**. **100% local: no network, no cloud, no AI service.**

## 1. Purpose & value
Videos are the biggest files most people keep, and the same clip piles up: the original from the phone,
the copy in a backup folder, the smaller one a chat app re-encoded, the one turned to portrait in an
editor. A byte-for-byte duplicate finder only catches the first kind. This feature recognises the others
by what the video *shows*, suggests the best one to keep, lets the user confirm each group - and undo.

## 2. Users & user stories
- As a user, I pick my videos folder and the app finds every group of matching videos in it.
- As a user, I see each group as film strips, with the one the app suggests keeping clearly marked.
- As a user, I want exact and re-encoded copies pre-marked, but other takes left for me to decide.
- As a user, I want turned and mirrored copies recognised too.
- As a user, I want my iPhone Live Photos left intact.
- As a user, I can say how strict the matching is, and see the result change at once.
- As a user, I can change which video is kept, play any video, and see it in Explorer.
- As a user, I want removed videos moved to a folder I can check, and an Undo.
- As a user, I want a second scan of the same folder to be quick.
- As a user, I want nothing to leave my PC.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| SVD-1 | **Choose folder & settings** - the user picks the folder; it and all options are remembered; a missing folder is reported and no scan starts. |
| SVD-2 | **Find videos** - walk every subfolder for MP4, M4V, MOV, AVI, WMV, ASF, MKV, WebM, 3GP, MTS/M2TS, TS and MPEG files; do not follow links; never scan the review folder; log and skip unreadable folders. |
| SVD-3 | **Skip Live Photo clips** - by default leave out a `.MOV` that has a HEIC/HEIF/JPEG still of the same name beside it; the option can be switched off. |
| SVD-4 | **Decode through Windows** - decode with Windows Media Foundation (any codec Windows has); seek to the exact time, not the nearest key frame; cut off codec padding; apply the rotation tag; count videos that cannot be decoded; say so when Media Foundation is missing. |
| SVD-5 | **Frame fingerprints** - sample 10 frames at 5%, 15%, ... 95% of the length; give each a perceptual hash and a difference hash in all 8 orientations; leave flat frames (black, fades, one colour) out. |
| SVD-6 | **Fingerprint cache** - per scanned folder, gzip-compressed in the feature's data folder; reused while size and modified time are unchanged; autosaved during a scan; deleted files pruned only after a completed scan; records from another sampling plan ignored. |
| SVD-7 | **Length gate** - only compare two videos whose lengths agree within 2% of the longer one or 1 second, whichever is more. |
| SVD-8 | **Match kinds** - Exact copy (same size and bytes, verified), Same video (enough frames within the tight per-frame pHash limit and a low mean dHash), or Similar - check (looser limits, as-is). At least 4 frames must be comparable. |
| SVD-9 | **Turned and mirrored copies** - optionally match a video with its 90/180/270-degree turned or mirrored version (Same video only). |
| SVD-10 | **Similar takes** - optionally group videos of the same scene with differences, for manual review. |
| SVD-11 | **Sensitivity** - Strict, Balanced (default) or Loose; changing it or any matching option regroups from the cache without rescanning. |
| SVD-12 | **Keeper choice** - keep the video with the most pixels, then the higher bit rate, then the longer, then the bigger file, then a name that does not look like a copy, then the earliest, then the shortest path. |
| SVD-13 | **Grouping by keeper** - each group is the best unassigned video plus its unassigned matches (no chaining); groups come with the most space to gain first. |
| SVD-14 | **Review screen with film strips** - a list of groups (match, videos, space marked) and the selected group's videos as cards: four-frame film strip, KEEP / MOVE / keep badge, name, length, picture size, bit rate, file size, date, how it matches, folder. |
| SVD-15 | **Manual verification** - Exact and Same-video members pre-marked; Similar never; click toggles; Mark all / Keep all; Keep this one instead; Play; Show in Explorer; Previous / Next (also Ctrl+arrows, PageUp/PageDown). |
| SVD-16 | **Move to review folder** - move marked videos into `_Similar videos - review` inside the scanned folder, keeping their layout; never a keeper; skip files changed since the scan; never overwrite (add " (2)"); confirm first, Cancel as the default. |
| SVD-17 | **Recycle Bin option** - alternatively send the marked videos to the Windows Recycle Bin. |
| SVD-18 | **Undo last move** - put the last moved batch back, also after the app was closed; never overwrite. |
| SVD-19 | **Progress, pause and stop** - live progress, Pause/Resume and Stop; a stopped scan keeps the work done and prunes nothing. |
| SVD-20 | **Save report** - a plain-text report of every group: keeper, members, length, size, what is marked and how each matches. |

## 4. Sub-features (acceptance)

### 4.1 Folder, settings and scan (SVD-1, SVD-2, SVD-3, SVD-6, SVD-19)
*Accept:* folder and options are pre-filled next time, corrupt settings fall back to defaults; videos at
any depth are found, other files and the review folder are not; a Live Photo `.MOV` is skipped only with
the option on; a second scan decodes nothing unchanged, a changed file is decoded again; a stopped scan
prunes nothing, a full one drops deleted files; autosave runs during the scan.

### 4.2 Decoding and fingerprints (SVD-4, SVD-5)
*Accept:* a real MP4 written at test time decodes with its length (+-200 ms), its size and its frames the
right way up; a frame padded by the codec is cropped; a non-video or missing file is unreadable, not a
crash; a film strip of four frames is produced; a black frame carries no hash.

### 4.3 Matching and grouping (SVD-7 - SVD-13)
*Accept:* identical frames = Same video 10/10; a half-size, 4x lower bit-rate re-encode = Same video; a
copy = Exact (byte check asked only for equal sizes); turned and mirrored copies match only with the option
on; a 6% closer take is Similar and vanishes with the option off; different videos never match even on
Loose; fewer than 4 comparable frames never match; lengths beyond the gate never group; a high mean dHash
vetoes "Same"; a chain A~B~C does not pull C in; stricter never finds more than looser.

### 4.4 Review, move and undo (SVD-14 - SVD-18, SVD-20)
*Accept:* the keeper is never in the move plan; moved videos land in the review folder with their layout;
a changed file is skipped; Recycle mode calls the recycler and leaves no undo batch; Undo restores; the
report lists KEEP / MOVE, length and match per video; choosing another keeper pre-decides nothing.

## 5. Non-functional
- **Privacy:** nothing leaves the PC; no AI service; fingerprints hold only hashes, sizes, lengths, dates and
  paths, in the feature's own data folder; screenshots use generated frames and placeholder paths.
- **Safety:** reversible by default (review folder + Undo); keepers never move; Live Photos untouched by
  default; confirmation defaults to Cancel.
- **Performance:** 10 frames per video, 2 videos decoded in parallel; the length gate skips almost every
  pair; byte checks run only for equal-size pairs and are remembered.
- **Storage:** about 2 KB of compressed fingerprint per video.

## 6. Traceability
Rows SVD-1 ... SVD-20 in `tools/docgen/build_excel_docs.py` (requirements + traceability matrix);
code in `src/Features/SimilarVideos/` (+ the kit, `src/DigitalSecretary.Kit/`); tests in
`tests/DigitalSecretary.UnitTests/Features/SimilarVideos/` and the QA plugin pipeline.
