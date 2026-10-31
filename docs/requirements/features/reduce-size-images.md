# Requirement - Reduce Size - Images

| | |
|---|---|
| Feature id | `reduce-size-images` | Category | Files | Status | Done |

> Shows which pictures take more space than they need and makes the chosen ones smaller with **no visible
> change**, keeping the camera details, **checking every copy** and keeping originals in an **undo-able
> folder** (or the Recycle Bin). **100% local.** ffmpeg (optional) is needed only for WebP / AVIF.

## 1. Purpose & value
Cameras and phones save JPEGs at quality 92-98, scanners and screenshot tools keep photos as PNG / BMP / TIFF.
Re-saving at quality 90 or as a JPEG is invisible but saves a third to nine tenths of the space - if it is done
carefully: never on a graphic, never on an already-compressed JPEG, never losing the date taken.

## 2. Users & user stories
- As a user, I pick my pictures folder and see, per picture, whether it can shrink, how, and by how much.
- As a user, I want the safe changes on by default and the ones that trade something off and explained.
- As a user, I want screenshots, logos and see-through pictures kept sharp.
- As a user, I want my photos to keep their dates and camera details.
- As a user, I never want a visibly worse picture to replace mine.
- As a user, I want the originals kept until I am sure, an Undo, and a running total of space saved.

## 3. Functional requirements
| ID | Requirement |
|---|---|
| RIM-1 | Folder and all choices remembered (with limits); corrupt settings fall back to the defaults. |
| RIM-2 | Scan every subfolder for JPEG / PNG / BMP / TIFF (not the originals folder) with GDI+; gzip cache, incremental; leftovers deleted. |
| RIM-3 | Read each JPEG's saved quality from its luminance quantisation table (8/16-bit, any order). |
| RIM-4 | Tell photos from graphics; detect see-through pixels; never make a graphic or transparent picture a JPEG. |
| RIM-5 | Never touch 16-bit, CMYK, multi-page / animated or very small pictures. |
| RIM-6 | Re-save JPEGs saved above the target quality at the target; leave others alone; Recommended from 30% saving. |
| RIM-7 | Turn photos stored as PNG / BMP / TIFF into JPEGs. |
| RIM-8 | Turn BMP / TIFF graphics into PNG with every pixel kept. |
| RIM-9 | Optional downscale above a chosen size, aspect kept, stored pixel size updated; never pre-selected. |
| RIM-10 | Optional WebP / AVIF via ffmpeg, stored upright; never pre-selected; trade-offs and the ffmpeg need explained. |
| RIM-11 | Keep every camera detail (EXIF) GDI+ exposes; keep the orientation tag with the unturned pixels. |
| RIM-12 | Check each copy: saving >= minimum and, at the same size, PSNR >= the limit for the quality; otherwise keep the original. |
| RIM-13 | Safe swap (dates kept, changed originals left alone, `.jpeg` / `.JPG` spelling kept); originals to `_Reduced - originals` or the Recycle Bin. |
| RIM-14 | Undo last run restores originals. |
| RIM-15 | Savings history and "saved so far". |
| RIM-16 | Pre-tick Recommended only, never Skip; Select recommended / all / none with a total; progress, pause, stop; report. |

## 4. Acceptance criteria
- A real quality-98 JPEG is re-saved at ~90, much smaller, with "date taken" and orientation kept and PSNR above
  the limit; the original is in the originals folder byte for byte; Undo restores it.
- A photo PNG becomes a JPEG under half its size; a BMP screenshot becomes a pixel-identical PNG under a fifth.
- A quality-80 JPEG, a transparent logo and a 16-bit TIFF are Skip with the reason.

## 5. Non-functional
- Local only; no new NuGet; logic in pure classes with unit tests; real-picture integration tests made at test time.
