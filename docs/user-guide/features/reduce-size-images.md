# Using Reduce Size - Images

Cameras save JPEGs at very high quality, and scans or screenshots of photos are often kept uncompressed. This
feature shows **which pictures can shrink with no visible change**, makes them smaller with their **camera details
kept** (date taken, camera, location), checks every copy, and keeps the originals until you delete them.

## Open it
**Features -> Files -> Reduce Size - Images**, or click its tile on Home.

## Safe by default
- **Nothing changes until you click "Reduce selected"** and confirm.
- **Every copy is checked**: it must open, be really smaller and - when it keeps its size - pass a
  pixel-by-pixel quality check. Otherwise the original stays.
- **Screenshots, drawings and see-through pictures are never turned into JPEGs.** 16-bit, CMYK and multi-page
  files are never touched.
- **The copy keeps the original's name and dates.** Originals go to `_Reduced - originals` (or the Recycle Bin);
  **Undo last run** puts them back.
- **Everything stays on your PC.**

## 1. Pick a folder and Scan
Click **Browse...**, choose your pictures folder (for example `C:\Users\You\Pictures`) and click **Scan**.

## 2. Choose what may change
The blue line under the options explains your current choice.

| Option | Default | What it does |
|---|---|---|
| **Re-compress big JPEGs** | on | A camera JPEG saved at quality 92-98 is re-saved at 90: no visible change, often 30-60% smaller. JPEGs already at about 85 or below are left alone - saving them again would only add loss. |
| **Photos in PNG/BMP/TIFF to JPEG** | on | Photos kept without compression become JPEGs, typically 80-95% smaller. |
| **BMP/TIFF graphics to PNG** | on | No loss at all - every pixel kept. |
| **Shrink pictures larger than ... px** | off | Fine for screens, but removes detail for big prints or crops. Never pre-selected. |
| **Convert photos to WebP / AVIF** | off | Smaller still, but camera details are not kept and some programs cannot open them. Never pre-selected. Needs ffmpeg (`winget install --id Gyan.FFmpeg -e`). |

**Quality**: *Visually lossless (JPEG 90, recommended)*, *Balanced (85)*, *Smallest (80)*.

## 3. Check the list and Reduce
**Recommended** pictures are ticked; **Your call** ones you may tick; **Skip** ones cannot be ticked - the **Why**
column says why. The **Format** column shows each JPEG's saved quality (e.g. "JPEG q96"). Click **Reduce
selected**, confirm, and watch the log.

## 4. Check, then free the space
Open a few of the new pictures, then delete the `_Reduced - originals` folder (or use **Undo last run**).
**Saved so far** keeps a running total.

## Good to know
- HEIC, WebP and AVIF pictures are already efficient and are not touched.
- A shrunk picture records its new pixel size in its camera details; its orientation still applies.
