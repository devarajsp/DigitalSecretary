# Feature: Similar Videos

Finds videos that are the **same** - exact copies, the same video **re-encoded, resized or
re-compressed** (e.g. by a chat app), and **turned or mirrored** copies - shows each group as film strips,
and moves the extras the user marks into a **review folder** (undo-able) or the Recycle Bin. Other takes
of the same scene are offered for manual verification only. Phase 3 of the Storage Studio plan (ENH-10),
built as its own plugin like Similar Pictures, on the shared helper kit (ENH-12).

| | |
|---|---|
| **Id** | `similar-videos` |
| **Category** | Files |
| **Entry type** | `DigitalSecretary.Features.SimilarVideos.SimilarVideosModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\similar-videos\similar_videos_settings.json` (folder + options), `fingerprints\<hash>.json.gz` (one compressed fingerprint cache per scanned folder), `last_move.json` (the batch "Undo last move" restores) |
| **In the scanned folder** | `_Similar videos - review\` - only once something has been moved there |
| **NuGet** | none - framework only (Windows Media Foundation via COM, GDI+, GZip, JSON, WinForms) + the kit |

## What it does
1. **Scan** finds every video under the folder (MP4, M4V, MOV, AVI, WMV, ASF, MKV, WebM, 3GP, MTS/M2TS,
   TS, MPEG), skipping links, the review folder and - by default - the `.MOV` half of iPhone Live Photos
   (a `.MOV` with a HEIC/JPEG of the same name beside it).
2. Each video is opened with the **Media Foundation Source Reader** (any codec Windows has), output
   converted to RGB32 by Windows. **10 frames** are decoded at 5%, 15%, ... 95% of the length - seeking
   to the **exact** time (decode forward from the key frame), because two encodes of one video put their
   key frames in different places. Codec padding is cut off (1080p is coded as 1088 rows) and the
   rotation tag is applied.
3. Each frame becomes a **fingerprint** with the kit's `ImageHasher`: pHash + dHash in 8 orientations.
   **Flat** frames (brightness std-dev < 6: black, fades, one colour) get no hash and are never compared.
   The cache is incremental (size + modified time), autosaved, gzip at rest, pruned only after a full scan.
4. **Grouping.** Videos are sorted by length; only pairs whose lengths agree (2% or 1 s) are compared.
   Frame *i* is compared with frame *i*. A pair is **Same video** when >= 80% (Balanced) of the comparable
   frames are within 8 pHash bits in one orientation and the mean dHash distance is <= 16; **Similar -
   check** when >= 60% are within 12 bits as-is (mean dHash <= 20); **Exact** when the sizes are equal
   and the bytes are identical (checked once and remembered in the window, so regrouping never re-reads
   gigabytes). At least 4 frames must be comparable. Star grouping with `VideoKeeperRanking`.
5. **Review** as film-strip cards. Exact and Same-video members are pre-marked **MOVE**; Similar never.
   Click toggles; "Keep this one instead", Play (default player), Show in Explorer.
6. **Move marked** into `_Similar videos - review\` (layout kept) or the Recycle Bin; **Undo last move**.

## Classes
| Class | Role | Pure? |
|---|---|---|
| `MF`, `IMFSourceReader`, `IMFSample`, `IMFMediaBuffer`, `IMFAttributes`, `IMFSinkWriter`, `NativeMethods` | hand-written Media Foundation interop (vtable order matters); the sink writer is for the tests only | interop |
| `VideoDecoder`, `ReaderSession`, `VideoSample`, `RawFrame` | open, seek exactly, crop padding, rotate; greyscale frames for hashing, film strips for cards | I/O edge (pure helpers `ToGray`, `Upright`, `ViewOf`, `ParseVideoArea`, `Join`) |
| `VideoRecord`, `FrameHash`, `VideoCache` | one video's facts and frame hashes; gzip cache per folder (kit `FileRecordCache`) | cache is I/O |
| `VideoScanner`, `VideoScanRequest` | extensions, Live Photo rule; the incremental scan is the kit's `IncrementalScanner` | I/O edge |
| `VideoMatcher`, `VideoThresholds`, `VideoFindOptions`, `VideoMatch`, `VideoGroup`, `VideoMember` | length gate, frame-by-frame matching, tiers, star grouping | yes |
| `VideoKeeperRanking` | which video is kept | yes |
| `VideoReviewMover`, `VideoRemoveRequest`, `VideoGroupEdits` | plan (keepers never move); moving/undo are the kit's `ReviewFolder`; "keep this one instead" | plan/edits pure |
| `VideoReportText` | labels, durations, bit rates, summary, saved report | yes |
| `SimilarVideosSettings(Store)` | remembered options | I/O edge |
| `SampleVideoSet` | generated "videos" (painted frames, placeholder paths) for screenshots / QA / tests | yes |
| `VideoCardControl`, `SimilarVideosControl` | review screen; wiring only | UI |

## The design decisions that matter
**Frames at fractions, compared in order.** Two encodes of the same video have the same length, so frame
*i* of one shows what frame *i* of the other shows - one comparison per frame, no searching. The length
gate makes this valid and also skips almost every pair before any hash is compared.

**pHash decides per frame, dHash only on the mean.** Measured on real H.264 round trips (half size, 4x
lower bit rate): pHash stays at 0 - 2 bits per frame, but dHash jumps to 13 - 21 on frames with smooth
gradients (compression bands them); different videos sit at pHash 20 - 36, mean dHash 24+. So a frame
"matches" on pHash, and the mean dHash is a second guard over the whole video.

**Exact time, not key frames.** Seeking lands on the key frame before the target; the reader decodes
forward to the frame that covers the target time (falling back to the last decoded frame at the end).

**Codec padding.** Decoders hand back frames padded to a multiple of 16 (the probe test showed 160x88
coming back as 160x96 with a flat bar at the bottom, which wrecked the hashes). The session keeps the size
first announced (or the display aperture) and copies only that area.

**Turning is for copies only; takes are never pre-marked; reversible by default** - the same rules as
Similar Pictures, for the same reasons.

**Not in v1:** trimmed clips (a short clip cut from a longer video - needs time-based sampling plus a
sliding alignment), and an ffmpeg fallback (undecodable files are listed instead).

## Tests
Unit (`tests/DigitalSecretary.UnitTests/Features/SimilarVideos/`): matcher, record + scanner + cache,
keeper + mover + edits, report + settings + sample, decoder helpers. Integration: the scan -> group ->
move -> rescan -> undo pipeline with a fake decoder, and `MediaFoundationRoundTripTests`, which encode
tiny MP4s **at test time** (`TestVideoWriter`, Media Foundation Sink Writer - no binary files in the repo)
and decode them for real: length, size, upright frames, film strip, copy = Exact, half-size re-encode =
Same video. Those tests return early when Media Foundation is absent. QA: the real plugin loads, shows the
sample and a click toggles a mark.
