# Feature: Similar Pictures

Finds pictures that **look** the same - exact copies, the same picture re-saved / resized /
re-compressed / lightly edited, **turned or mirrored** copies, and **burst shots or retakes** of almost
the same scene - shows each group side by side, and moves the extras the user marks into a **review
folder** (undo-able) or the Recycle Bin. Phase 2 of the Storage Studio plan (ENH-9), built as its own
plugin at the owner's request.

| | |
|---|---|
| **Id** | `similar-pictures` |
| **Category** | Files |
| **Entry type** | `DigitalSecretary.Features.SimilarPictures.SimilarPicturesModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\similar-pictures\similar_pictures_settings.json` (folder + options), `fingerprints\<hash>.json.gz` (one compressed fingerprint cache per scanned folder), `last_move.json` (the batch "Undo last move" restores) |
| **In the scanned folder** | `_Similar pictures - review\` - only once something has been moved there |
| **NuGet** | none - framework only (GDI+, the Windows Shell thumbnail API via COM, GZip, JSON, WinForms) |

## What it does
1. **Scan** finds every picture file under the folder (JPEG, PNG, GIF, BMP, TIFF, HEIC/HEIF, WebP,
   AVIF and common camera RAW), skipping links and the review folder. Each picture is read as a
   ~256 px thumbnail through the **Windows Shell** (`IShellItemImageFactory`, the engine Explorer uses -
   any format Windows has a codec for, EXIF rotation applied), falling back to **GDI+**. The header
   gives dimensions and the EXIF capture time, including sub-seconds.
2. Each thumbnail becomes a **fingerprint**: a 64-bit perceptual hash (DCT) and a 64-bit difference
   hash for each of the **8 orientations**, plus a sharpness score (variance of the Laplacian). The
   cache is incremental (size + modified time), autosaved, gzip at rest, pruned only after a full scan.
3. **Grouping** compares every pair (parallel). A pair is **Same picture** when both hashes are within
   the tight limits in some orientation and the capture times do not say "two different moments";
   **Exact** when it is also byte-identical (checked); **Similar - check** when, as-is, both hashes
   are within the looser limits (more tolerance for shots taken within 30 s of each other).
   Pictures are ranked by `KeeperRanking`; the best unassigned picture keeps its unassigned matches
   ("star" grouping), so each member is compared with the keeper itself.
4. **Review.** Exact and Same-picture members are pre-marked **MOVE**; **Similar - check** members are
   never pre-marked. The user clicks cards to toggle, can make another picture the keeper, open it or
   show it in Explorer. Changing the sensitivity or options regroups from the cache instantly.
5. **Move marked** puts the marked pictures into `_Similar pictures - review\` inside the scanned
   folder, keeping their sub-folder layout (or into the Recycle Bin). **Undo last move** puts them back.

## Classes
| Class | Role | Pure? |
|---|---|---|
| `GrayImage`, `PictureOrientation` (kit) | greyscale pixels, area-average resize, 8 transforms, Laplacian sharpness | yes (except `FromBitmap`) |
| `ImageHasher`, `Fingerprint` (kit) | pHash (32x32 DCT, 8x8 low frequencies, median), dHash (9x8), 8 orientations | yes |
| `PictureReader`, `ShellThumbnailProvider`, `GdiThumbnailProvider` | thumbnails + header (size, EXIF time, rotation) | I/O edge |
| `PictureRecord`, `PictureCache` | one picture's facts; gzip cache per folder | cache is I/O |
| `PictureScanner` | find pictures, bounded-parallel reading, incremental, pause/stop, autosave, prune | I/O edge |
| `SimilarityFinder`, `Thresholds`, `FindOptions`, `SimilarGroup` | pair matching, tiers, star grouping, suggestions | yes |
| `KeeperRanking` | which picture is kept | yes |
| `GroupEdits` | "keep this one instead" | yes |
| `ReviewMover` | plan (keepers never move); moving, undo and the last-batch file are the kit's `ReviewFolder` | I/O edge |
| `FileBytes` (kit) | byte-for-byte check for exact copies | I/O edge |
| `SimilarReportText` | labels, summary, saved report | yes |
| `SimilarPicturesSettings(Store)` | remembered options | I/O edge |
| `SamplePictureSet` | generated pictures + placeholder paths for screenshots / tests | yes |
| `PictureCardControl`, `SimilarPicturesControl` | review screen; wiring only | UI |
| `ByteSize`, `PauseToken` | from the shared helper kit (`src/DigitalSecretary.Kit`) | yes |

## The design decisions that matter
**Two hashes must agree.** pHash is robust to scaling, compression and brightness; dHash is cheap and
tells apart pictures with similar tones. Requiring both keeps false matches low. The limits were set
from measured distances (resize / JPEG 40 / +15% brightness / 3% crop: pHash 0-4, dHash <= 7;
unrelated pictures: pHash >= 12 as-is even for scenes built from one template).

**Turning is for copies only.** Eight orientations are searched at the tight "Same picture" limits,
where a turned copy matches at ~0 bits. "Similar" is judged as-is; searching eight orientations at the
loose limits would multiply false matches by eight.

**Two moments are never "the same picture".** Burst frames can be as close as a re-saved copy. When
both pictures carry an EXIF capture time (sub-seconds included) and the times differ, the pair can only
be "Similar - check", so it is never pre-marked. Copies keep the original's EXIF time; chat apps that
strip EXIF leave no time, which never counts as "different".

**Star grouping, best picture first.** Union-find on loose links would chain A~B~C into one group even
when A and C are unlike. Here every member matches the keeper directly.

**Reversible by default.** Moves go to a review folder inside the scanned folder (never scanned
itself), the last batch is saved so Undo works after a restart, the keeper of a group can never be
moved, files changed since the scan are skipped, and name clashes get " (2)". The confirmation dialog
defaults to Cancel.

**No real photos in images.** Screenshots and the QA test use `SamplePictureSet` - painted scenes under
`C:\Users\You\Pictures` - shown through `ShowRecords`, so no user photo, name or path is ever captured.

## Tests
`tests/DigitalSecretary.UnitTests/Features/SimilarPictures/` - one file per area plus
`SimilarPicturesPipelineTests` (real files: scan -> group -> move -> rescan -> undo) and the QA test
`Similar_Pictures_groups_the_sample_and_a_click_toggles_a_pictures_mark`.

## Known limits
- Comparison is all-pairs: fine for tens of thousands of pictures (seconds), slower beyond ~100,000.
- Crops beyond a few percent, heavy edits and filters are not "the same picture"; strong crops may show
  as "Similar" only at Loose.
- Burst frames from cameras that record no sub-second EXIF time and fall in the same second look like
  one moment, so they may be pre-marked as "Same picture" - always review before moving.
- HEIC, WebP and RAW need the matching Windows codec (e.g. HEIF / Raw Image Extensions); without it
  those files are listed as unreadable. Their dimensions are not read (GDI+ cannot), so size ranking
  falls back to file size.
