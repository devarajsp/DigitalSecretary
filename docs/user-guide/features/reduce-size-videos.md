# Using Reduce Size - Videos

A few minutes of phone video often take **hundreds of MB** - far more than the picture needs. This feature
shows you **which videos can shrink without a visible difference and by how much**, makes them smaller, checks
every copy, and keeps your originals until you decide to delete them.

## Open it
**Features -> Files -> Reduce Size - Videos**, or click its tile on Home.

## Safe by default
- **Nothing changes until you click "Reduce selected"** and confirm.
- **Every copy is checked** before it replaces the original: same length, sound kept, really smaller and - with
  ffmpeg - a picture-quality score. A copy that fails is deleted and the original stays as it was.
- **The copy keeps the original's name and dates**, so your photo apps sort it the same way.
- **Originals go to `_Reduced - originals`** inside the folder you scanned (or the Recycle Bin if you choose).
  **Undo last run** puts them back - even after closing the app. Space is freed when you delete that folder.
- **HDR videos are left alone** by default, and so is anything changed since the scan.
- **Everything stays on your PC.**

## 1. Install ffmpeg (once, recommended)
The best results come from the free tool **ffmpeg**. Click **How to install** - it copies this command:

    winget install --id Gyan.FFmpeg -e

Paste it into a Terminal / PowerShell window, press Enter, then click **Find ffmpeg**. (If ffmpeg is somewhere
else, **Find ffmpeg** lets you pick its folder.) Without ffmpeg, choose **Windows built-in**.

## 2. Pick a folder and Scan
Click **Browse...**, choose your videos folder (for example `C:\Users\You\Videos`) and click **Scan**. Later
scans only read new or changed videos.

## 3. Choose engine and quality
The blue line under the options explains your current choice.

| Engine | Choose it when |
|---|---|
| **ffmpeg - best quality per MB (recommended)** | You want the smallest files and everything kept (sound untouched, camera details, quality check). It is slow - about as long as the videos - so leave it running. |
| **ffmpeg - fast, uses the graphics card** | You have a big backlog and accept files ~30% bigger. |
| **Windows built-in** | ffmpeg is not installed. Bigger files, sound re-encoded, location etc. not kept. |

| Quality | Means |
|---|---|
| **Visually lossless (recommended)** | No visible difference, even full-screen. |
| **Balanced** | A little smaller; differences only if you pause and hunt for them. |
| **Smallest** | Fine on a phone or laptop; dark or busy scenes may look softer on a big TV. |

**Offer a video only if it saves at least ... %** hides small savings (25% by default).

## 4. Check the list
| Advice | Means |
|---|---|
| **Recommended** (ticked) | Saves 40% or more - typical for phone and camera videos. |
| **Your call** | A real but smaller saving. Tick it if you like. |
| **Skip** | Not worth it or not safe - the **Why** column says which. Cannot be ticked. |

**Select recommended**, **Select all worth it** and **Select none** help; the line beside them adds up the
selection. Double-click a video to play it.

## 5. Reduce
Click **Reduce selected** and confirm. Progress shows the current video. **Pause** waits after the current video;
**Stop** ends the run and cleans up. The log lists each result (with its quality score).

## 6. Check, then free the space
Click **Open originals folder**, play a few of the new copies, and when you are happy delete the
`_Reduced - originals` folder - that is when the space comes back. Changed your mind? **Undo last run**.
**Saved so far** keeps a running total.

## Good to know
- Videos already in HEVC or AV1 are only offered when they are still very generous.
- Old formats (AVI, WMV, MTS, MPEG) become `.mp4` with the same name.
- **Save report...** writes the whole list with the reasons to a text file.
