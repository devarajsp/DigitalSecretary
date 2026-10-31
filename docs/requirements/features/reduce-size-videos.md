# Requirement - Reduce Size - Videos

| | |
|---|---|
| Feature id | `reduce-size-videos` | Category | Files | Status | Done |

> Shows which videos take far more space than their picture needs, how much a smaller copy would save, and
> makes the copies the user chooses - **checked before they replace anything**, with originals kept in an
> **undo-able folder** (or the Recycle Bin). **100% local: nothing is uploaded.** ffmpeg is optional and
> installed by the user; Windows' own encoder works without it.

## 1. Purpose & value
Phones and cameras record H.264 at 15-50 Mbit/s so they can encode quickly; a few minutes take hundreds of MB.
A modern encoder (HEVC) shows the same picture at a third of that. Most people never re-encode because it is
technical and feels risky. This feature makes it a guided, checked, undo-able routine.

## 2. Users & user stories
- As a user, I pick my videos folder and see, per video, whether it can shrink and by how much.
- As a user, I want help choosing the engine and quality, in plain words.
- As a user, I want only clearly worthwhile videos pre-selected, and the final say on each.
- As a user, I never want a worse or broken copy to replace my video.
- As a user, I want my videos to keep their names, dates, sound and details.
- As a user, I want the originals kept until I am sure, and an Undo.
- As a user, I want to see how much space I have saved over time.
- As a user, I want to pause or stop a long run at any time.

## 3. Functional requirements
| ID | Requirement |
|---|---|
| RVD-1 | Folder and all choices are remembered; corrupt settings fall back to the safe defaults. |
| RVD-2 | Scan every subfolder (no links, not the originals folder); read with ffprobe, else Windows; gzip cache, incremental; delete leftovers of a killed run. |
| RVD-3 | Read codec, visible picture size, frame rate, length, bit depth, HDR, sound, rotation; non-videos are unreadable, not a crash. |
| RVD-4 | Advise each video by bits per pixel per frame: Recommended (>= 40%), Your call (>= minimum), Skip with the reason (below minimum, compact, HEVC/AV1 under 40%, HDR, 10-bit on Windows, too short). |
| RVD-5 | Three engines with plain-words help: ffmpeg x265 (default), ffmpeg graphics card (detected by a test encode), Windows built-in; show how to install ffmpeg; locate it or a chosen folder. |
| RVD-6 | Three qualities (Visually lossless default, Balanced, Smallest); changes re-advise with no rescan. |
| RVD-7 | ffmpeg: HEVC, 10-bit kept, sound copied when possible (else AAC 192k), metadata kept, Apple tag, modern containers kept, others MP4, progress. |
| RVD-8 | Windows engine: Media Foundation HEVC (or H.264) + AAC at the target bit rate, visible size, clear refusal for unsupported sound, stoppable. |
| RVD-9 | Verify each copy: same length, sound kept, saving >= minimum, SSIM >= limit (ffmpeg); otherwise keep the original. |
| RVD-10 | Swap safely: copy beside the original, original's dates kept, changed originals and full drives left alone, original put back if the swap fails. |
| RVD-11 | Originals go to `_Reduced - originals` in the scanned folder, sub-folders kept, no name clashes. |
| RVD-12 | Optional Recycle Bin for originals (no in-app undo). |
| RVD-13 | Undo last run restores originals, deletes a copy only while unchanged, never overwrites; survives a restart. |
| RVD-14 | Savings history (`history.jsonl`) and a "saved so far" total; undos cancel savings. |
| RVD-15 | Progress per file and overall; pause between videos; stop kills the encoder and cleans up. |
| RVD-16 | Pre-tick Recommended only; Skip can never be ticked; Select recommended / all worth it / none with a running total; plain-text report. |

## 4. Acceptance criteria
- A real H.264 video with sound becomes an HEVC copy with the same length, the sound copied and an SSIM above the
  limit; the original is in `_Reduced - originals` byte for byte; the copy has the original's dates; Undo restores it.
- The Windows engine turns a real MP4 into a smaller MP4 that players see at the same 640x360 size and length.
- A copy that is too big, shorter, silent or below the SSIM limit never replaces the original and leaves no temp file.
- A file changed since the scan, or a drive without room, is left alone.

## 5. Non-functional
- Local only; ffmpeg is never downloaded by the app. Runs at below-normal priority. No user path appears in screenshots.
- Every function <= 100 lines; logic in pure classes with unit tests; real-video integration tests made at test time.
