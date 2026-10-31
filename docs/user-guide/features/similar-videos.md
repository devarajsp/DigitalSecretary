# Using Similar Videos

Find videos that are **the same** - not only exact copies, but the same clip shrunk and re-encoded by a
chat app, exported again by an editor, or turned to portrait. You see each group as film strips, decide
what goes, and the extras are moved to a **review folder** you can undo. Videos are big, so this is often
where the most space comes back.

## Open it
**Features -> Files -> Similar Videos**, or click its tile on Home.

## Safe by default
- **Nothing moves until you click "Move marked"** - and you are asked to confirm (Cancel is the default).
- **The video marked KEEP never moves.** Every group keeps at least one video.
- **Moved videos go to a review folder**, `_Similar videos - review`, inside the folder you scanned, in the
  same sub-folders they came from. **Undo last move** puts them back - even after closing the app.
- **"Similar - check" groups are never pre-marked.** They may be different takes; you decide each one.
- **iPhone Live Photos are left alone** (their short `.MOV` clips are skipped) unless you switch that off.
- **Everything stays on your PC.** No internet, no cloud, no AI service.

## 1. Pick a folder and Scan
Click **Browse...**, choose your videos folder (for example `C:\Users\You\Videos`) and click **Scan**. The
app looks at every video in the folder and its subfolders - MP4, MOV, M4V, AVI, WMV, MKV, WebM, 3GP,
MTS/M2TS and MPEG - using the same Windows video engine your Films & TV / Media Player app uses. If a
format does not play in Windows (for example HEVC without the *HEVC Video Extensions*), it is listed as
*unreadable* in the log; exact copies of it are still found.

For each video the app looks at **ten frames** spread over its length. The first scan does this for every
video, so it takes a while on a large collection; later scans only look at new or changed videos. Use
**Pause** / **Resume** or **Stop** at any time - stopping keeps the work already done.

## 2. Choose how to match
| Option | What it does |
|---|---|
| **Matching** | *Strict* finds only very close matches; *Balanced* (recommended) is the usual choice; *Loose* finds more, for you to check. |
| **Turned / mirrored copies** | Also finds a video that was turned 90/180/270 degrees or mirrored. |
| **Similar takes** | Also groups videos of the same scene that are not quite the same (another take, a light edit). Shown as **Similar - check**, never pre-marked. |
| **Skip Live Photo clips** | Leaves out a `.MOV` that has a photo of the same name beside it (`IMG_1234.MOV` next to `IMG_1234.HEIC`). Change it, then scan again. |
| **Move to** | The review folder (undo-able) or the Recycle Bin. |

Changing the matching options regroups instantly - no new scan.

## 3. Review the groups
The list on the left shows every group: how it matches, how many videos, and how much space the marked
ones take. Click a group to see its videos as film strips.

| Badge | Meaning |
|---|---|
| **KEEP** (green) | The video the app suggests keeping - the largest picture, then the best quality (bit rate), then the longest, with a name that is not a copy's. It never moves. |
| **MOVE** (red) | Marked to move. Click the video to keep it instead. |
| **keep** (grey) | Not marked. Click to mark it. |

Under each film strip: the file name, length, picture size, bit rate, file size, date, how it matches
(*Exact copy*, *Same video*, *turned 90*, *Similar - check*, and how many of the ten frames matched) and
its folder.

- **Double-click** a video (or right-click -> **Play**) to watch it in your usual player.
- **Right-click** -> **Keep this one instead**, **Show in Explorer**.
- **Mark all but the keeper** / **Keep all in this group** - for the whole group at once.
- **< Previous / Next >** (or **Ctrl + Left / Right**, **Page Up / Page Down**) moves between groups.

### What the match kinds mean
- **Exact copy** - the very same file, byte for byte.
- **Same video** - the same video re-encoded, resized, compressed, turned or mirrored.
- **Similar - check** - the same scene, but not the same video: often another take. Play them, keep the
  best and mark the rest yourself.

## 4. Move the marked videos
The button at the bottom says how many videos are marked and how much space they take. Click it, confirm,
and they are moved. **Open review folder** shows them in Explorer; when you are happy, delete that folder
yourself to free the space. Changed your mind? **Undo last move**.

## 5. Save a report
**Save report...** writes a plain-text list of every group: which video is kept, which are marked to move,
their length and size, and how each matches.

## Good to know
- Two videos are only compared when their lengths are about the same (within 2%, or 1 second for short
  clips). A **short clip cut from a longer video** is therefore not found - that may come in a later version.
- Videos that are mostly black or one colour (a dark screen recording, a title card) may not have enough
  picture to compare; they are only found as exact copies.
- Decoding is heavy work: the first scan of a large collection can take a long time. You can stop and
  continue later - finished videos are remembered.

## Where your data is stored
`%APPDATA%\DigitalSecretary\data\similar-videos\` - your folder and options, a small compressed
fingerprint file per folder you scanned (hashes, lengths, sizes, dates, paths - no video), and the details
of the last move so it can be undone. Delete the folder to forget them.
