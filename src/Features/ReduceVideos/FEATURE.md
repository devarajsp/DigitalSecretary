# Feature: Reduce Size - Videos

Finds videos that take far more space than their picture needs, advises per video whether a smaller copy
is worth making (and how much it would save), and makes the copies the user chooses - **verified** before
they replace anything, with the originals kept in an **undo-able folder** (or the Recycle Bin). ENH-16,
v2.7.0. Built on the shared kit (ENH-18).

| | |
|---|---|
| **Id** | `reduce-size-videos` |
| **Category** | Files |
| **Entry type** | `DigitalSecretary.Features.ReduceVideos.ReduceVideosModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\reduce-size-videos\reduce_videos_settings.json` (folder + options), `fingerprints\<hash>.json.gz` (gzip facts cache per scanned folder), `last_run.json` (what "Undo last run" restores), `history.jsonl` (savings history) |
| **In the scanned folder** | `_Reduced - originals\` (shared with Reduce Size - Images) - only once something was reduced |
| **NuGet** | none. ffmpeg is **optional and never shipped**: the user installs it (`winget install --id Gyan.FFmpeg -e`) and `FfmpegLocator` finds it |

## What it does
1. **Scan** finds every video (MP4, M4V, MOV, AVI, WMV, ASF, MKV, WebM, 3GP, MTS/M2TS, TS, MPEG, FLV), skipping
   links and the originals folder, and deletes `*.ds-reducing.*` leftovers of a killed run. Each video is read by
   **ffprobe** when ffmpeg is installed, otherwise by **Windows Media Foundation** (`MfVideoProbe`): codec, visible
   picture size, frame rate, length, bit depth, HDR transfer, sound, rotation. Facts are cached (gzip, incremental).
2. **Advice** (`VideoAdvisor`, pure): bits per pixel per frame vs. what the chosen engine + quality need
   (HEVC 1080p30 targets 0.075 / 0.055 / 0.040, x1.35 for GPU or Windows HEVC, x2.0 for Windows H.264, scaled for
   resolution and frame rate). Estimated size = target video + 160 kbit/s audio + 1%. **Recommended** >= 40%,
   **Your call** >= the minimum (25% default), otherwise **Skip** - also for HDR (option, on), 10-bit on the
   Windows engine, already-HEVC/AV1/VP9 under 40%, < 2 s or unreadable.
3. **Reduce selected** (`VideoReduceRunner`): per video - free-space check, encode to a temp file beside it,
   read it back and **verify** (length within 1 s / 1%, sound kept, saving >= minimum, SSIM >= limit with ffmpeg),
   then `ShrinkSwap.Commit` (original to the review folder or Recycle Bin, copy takes its name - or `.mp4` for
   old containers - and its dates). Every swap goes to `history.jsonl`. Pause between videos; Stop kills ffmpeg.
4. **Undo last run** = `ShrinkSwap.Undo` (deletes a copy only while it is still exactly the file made).

## Engines
| Engine | How | Trade-off |
|---|---|---|
| ffmpeg x265 (default) | `-c:v libx265 -crf 20/23/26 -preset medium`, sound copied (else AAC 192k), `-map_metadata 0`, `-tag:v hvc1`, `+faststart+use_metadata_tags` | smallest; slow (about real time 1080p); SSIM-checked |
| ffmpeg graphics card | `hevc_nvenc` / `hevc_amf` / `hevc_qsv`, found by a 1-second test encode (ffmpeg lists all, hardware decides) | several times faster, ~30% bigger |
| Windows built-in | `MfTranscoder`: Source Reader (advanced video processing, NV12 at the visible size; PCM 16-bit) -> Sink Writer (HEVC, or H.264 when `HasHevcEncoder` is false; AAC 192k; hardware transforms on) at the advisor's bit rate | nothing to install; bigger; sound re-encoded; no GPS etc.; no SSIM; sound must be 44.1/48 kHz, 1/2/6 ch |

## Classes
| Class | Role | Pure? |
|---|---|---|
| `VideoFacts`, `VideoFactsCache`, `Codecs` | facts, gzip cache, codec names (MF FourCC -> ffmpeg name, labels) | yes / cache I/O |
| `FfprobeReader` | ffprobe args + JSON parse (display-matrix rotation, cover art ignored) | parse pure |
| `MfVideoProbe` | facts through Media Foundation | I/O edge |
| `VideoAdvisor`, `VideoAdviceOptions`, `VideoAdvice`, `VideoEngineKind`, `VideoQuality` | the advice | yes |
| `FfmpegVideoArgs`, `GpuEncoder` | command lines, container + audio rules, progress / SSIM parsing | yes |
| `IVideoEngine`, `FfmpegVideoEngine`, `WindowsVideoEngine`, `EncodeJob`, `EncodeResult` | the engines | I/O edge |
| `MfTranscoder` | Windows re-encode (reader -> writer pump, stream that is behind goes first) | interop |
| `VideoReduceRunner`, `VideoReduceJob`, `VideoCandidate`, `VideoReduceOutcome`, `ReduceProgress` | run loop + `Verify` | loop I/O, `Verify` pure |
| `VideoFactsScanner` | extensions, leftovers, incremental scan (kit) | I/O edge |
| `ReduceVideosSettings(Store)`, `ReduceVideosText`, `SampleVideoFacts` | settings, every UI string, sample for docs/QA | text pure |
| `ReduceVideosControl` (+ `.Actions`, `.Layout`) | the view; wiring only | UI |

From the kit: `MediaFoundation.cs` (moved here from Similar Videos), `FfmpegLocator` / `ToolRunner`, `ShrinkSwap`,
`SavingsLedger`, `Verdict` / `FileOutcome` / `SyncProgress`, `IncrementalScanner`, `FileRecordCache`, `ReviewFolder`.

## The design decisions that matter
**Bits per pixel, not file size.** A 1 GB video can be lean (long, 4K) and a 200 MB one wasteful. Bits per pixel
per frame is the measure that compares them; phones record H.264 at 0.2-0.4 where HEVC needs ~0.07.

**Never trust the encoder, check the copy.** Every copy is read back and compared before the swap; anything
that fails is deleted. The original is only moved once the copy is in place; if the final rename fails the
original is put back.

**The visible size, not the coded size.** Hardware encoders pad 360 to 368 (and 1080 to 1088). The Windows
engine asks the video processor for the aperture size and `MfVideoProbe` reports the aperture, so players and
the length/size checks see the real picture (a real-video test asserts ffprobe sees 640x360).

**No PII in screenshots.** The ffmpeg folder (under the user profile) is logged, not shown on a label, and the
sample view clears the log.

## Tests
`tests/DigitalSecretary.UnitTests/Features/ReduceVideos/` - advisor, ffmpeg args/progress/SSIM, ffprobe JSON,
codecs, runner with a fake engine (every safety rule), text/settings/scanner/cache, and **real videos made at test
time** (`RealVideos`: Windows Sink Writer MP4s; ffmpeg test sources with sound when ffmpeg is installed - those
tests return early without it, e.g. on CI). Kit pieces: `tests/DigitalSecretary.UnitTests/Kit/ShrinkKitTests.cs`.
QA: `Reduce_Size_advises_the_sample_preselects_only_recommended_and_never_ticks_a_skip`.
