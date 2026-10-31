# Feature: Reduce Size - Images

Finds pictures that take more space than they need - camera JPEGs saved at very high quality, photos kept
as PNG / BMP / TIFF, uncompressed BMP / TIFF graphics - advises per picture, and makes the chosen ones smaller
with **no visible change**, camera details kept, every copy **checked** before it replaces anything, and the
originals in an **undo-able folder** (or the Recycle Bin). ENH-17, v2.7.0. Built on the shared kit (ENH-18).

| | |
|---|---|
| **Id** | `reduce-size-images` |
| **Category** | Files |
| **Entry type** | `DigitalSecretary.Features.ReduceImages.ReduceImagesModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\reduce-size-images\reduce_images_settings.json`, `fingerprints\<hash>.json.gz` (facts cache), `last_run.json`, `history.jsonl` |
| **In the scanned folder** | `_Reduced - originals\` (shared with Reduce Size - Videos) |
| **NuGet** | none - GDI+ for JPEG / PNG; ffmpeg (optional, never shipped) only for WebP / AVIF |

## What it does
1. **Scan** reads every JPEG / JFIF, PNG, BMP and TIFF (HEIC / WebP / AVIF are already efficient and skipped) with
   GDI+: format, pixel size, **JPEG quality** (`JpegQuality`, from the file's luminance quantisation table by the
   IJG formula), **photo or graphic** (`ImageProbe.Classify` on a 96 px copy: share of distinct colours > 25% and of
   identical neighbours < 35% = photo), see-through pixels, and what makes a file **untouchable** (16-bit, CMYK,
   several pages / frames). Gzip cache, incremental; leftovers of a killed run are deleted.
2. **Advice** (`ImageAdvisor`, pure), first match wins:
   - untouchable, unreadable or < 150 KB -> Skip;
   - modern format chosen and the picture is a photo without transparency -> WebP / AVIF (**Your call**);
   - JPEG saved above the target + 2 (target 90 / 85 / 80) -> re-save (**Recommended** from 30% saving);
     at or below it -> Skip ("saving it again would add loss");
   - lossless photo without transparency -> JPEG; BMP / TIFF otherwise -> PNG (lossless); else Skip;
   - optional downscale of the longer side (aspect kept) -> always **Your call**;
   - below the minimum saving (20% default) -> Skip. Estimates use typical JPEG bits per pixel per quality.
3. **Reduce selected** (`ImageReduceRunner`): `ImageEncoder` writes the copy beside the original (GDI+ JPEG at the
   target quality or PNG, exact pixel clone when the size stays, high-quality bicubic when shrinking, every EXIF
   item copied and the stored pixel size updated; WebP / AVIF via ffmpeg from an upright PNG intermediate). Then
   `Verify`: saving >= minimum and - for same-size copies - brightness **PSNR** >= 38 / 36 / 34 dB. Then
   `ShrinkSwap.Commit` (a `.jpeg` / `.JPG` keeps its own spelling) and a `history.jsonl` line.
4. **Undo last run** = `ShrinkSwap.Undo`.

## Classes
| Class | Role | Pure? |
|---|---|---|
| `ImageFacts`, `ImageFactsCache`, `PictureKind` | facts + gzip cache | yes / cache I/O |
| `JpegQuality` | DQT walk (8/16-bit, any table order), IJG table per quality, best match (ties -> higher) | yes |
| `ImageProbe` | GDI+ read, `Classify`, `Untouchable` | I/O edge, `Classify` pure |
| `ImageAdvisor`, `ImageAdviceOptions`, `ImageAdvice`, `ImageAction`, `ImageQuality`, `ModernFormat` | the advice | yes |
| `ImageEncoder` | GDI+ JPEG / PNG with EXIF, ffmpeg WebP / AVIF, `Upright`, `ModernArgs` | I/O edge |
| `ImageQualityCheck` | `Psnr` (pure), `Compare`, `Brightness` | `Psnr` pure |
| `ImageReduceRunner`, `ImageReduceJob`, `ImageCandidate`, `ImageReduceOutcome` | run loop + `Verify` | `Verify` pure |
| `ImageFactsScanner`, `ReduceImagesSettings(Store)`, `ReduceImagesText`, `SampleImageFacts` | scan, settings, UI text, sample | text pure |
| `ReduceImagesControl` (+ `.Actions`, `.Layout`) | the view; wiring only | UI |

## The design decisions that matter
**Judge a JPEG by the quality it was saved at, not its size.** Re-saving a quality-80 JPEG at 85 makes it bigger
*and* worse; re-saving a camera's 96 at 90 saves a third with no visible change. The quantisation table tells.
GDI+ itself writes "quality 90" with the IJG table of 89, so a copy we made reads back as ~89 and is never
re-compressed again (and the tests allow +-1).

**Graphics never become JPEGs.** Text and sharp edges smudge in JPEG; transparent pixels would turn white. Those
only ever become PNG (lossless) or are left alone.

**Keep raw pixel values.** Pictures are opened without colour management and the ICC profile item is copied, so
colours are not converted twice.

**HEIC is not offered.** Writing HEIC needs the paid HEVC Video Extensions (WIC) and ffmpeg has no HEIF muxer; WebP
and AVIF give the same benefit.

## Tests
`tests/DigitalSecretary.UnitTests/Features/ReduceImages/` - `ImageRulesTests` (advice, tables, classify, PSNR,
verify, run loop with a fake encoder, settings, text, cache) and `ImageRealFileTests` (real pictures made at test
time by `TestPictures`: quality read-back, photo/graphic, transparency, JPEG re-save with date taken + orientation
kept and PSNR, PNG -> JPEG, BMP -> identical PNG, downscale, WebP / AVIF when ffmpeg is installed, full run + undo,
scan). QA: `Reduce_Size_advises_the_sample_preselects_only_recommended_and_never_ticks_a_skip`.
