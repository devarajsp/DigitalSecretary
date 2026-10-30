# Using Similar Pictures

Find pictures that **look the same** - not only exact copies, but the same photo shrunk by a chat app,
re-saved by an editor, turned the right way up or mirrored, and the ten burst shots you took of one
moment. You see each group side by side, decide what goes, and the extras are moved to a **review
folder** you can undo.

## Open it
**Features -> Files -> Similar Pictures**, or click its tile on Home.

## Safe by default
- **Nothing moves until you click "Move marked"** - and you are asked to confirm (Cancel is the default).
- **The picture marked KEEP never moves.** Every group keeps at least one picture.
- **Moved pictures go to a review folder**, `_Similar pictures - review`, inside the folder you scanned,
  in the same sub-folders they came from. **Undo last move** puts them back - even after closing the app.
- **"Similar - check" groups are never pre-marked.** Near-identical shots may be different moments; you
  decide each one.
- **Everything stays on your PC.** No internet, no cloud, no AI service.

## 1. Pick a folder and Scan
Click **Browse...**, choose your pictures folder (for example `C:\Users\You\Pictures`) and click **Scan**.
The app looks at every picture in the folder and all its subfolders: JPEG, PNG, GIF, BMP, TIFF, and also
**HEIC** (iPhone), **WebP** and **camera RAW** when Windows can show them (for HEIC and RAW you may need
the free *HEIF Image Extensions* / *Raw Image Extension* from the Microsoft Store).

The first scan reads every picture; later scans of the same folder only read new or changed ones, so they
are quick. Use **Pause** / **Resume** or **Stop** at any time - stopping keeps the work already done.

## 2. Choose how to match
| Option | What it does |
|---|---|
| **Matching** | *Strict* finds only very close matches; *Balanced* (recommended) is the usual choice; *Loose* finds more, for you to check. |
| **Turned / mirrored copies** | Also finds a picture that was turned 90/180/270 degrees or mirrored. |
| **Similar scenes and bursts** | Also groups shots of almost the same scene - bursts, retakes. These are shown as **Similar - check** and never pre-marked. |
| **Move to** | The review folder (undo-able) or the Recycle Bin. |

Changing any of these regroups instantly - no new scan.

## 3. Review the groups
The list on the left shows every group: how it matches, how many pictures, how many are marked. Click a
group to see its pictures side by side.

| Badge | Meaning |
|---|---|
| **KEEP** (green) | The picture the app suggests keeping - the biggest and sharpest, with a name that is not a copy's. It never moves. |
| **MOVE** (red) | Marked to move. Click the picture to keep it instead. |
| **keep** (grey) | Not marked. Click to mark it. |

Under each picture: its name, dimensions, file size, date, sharpness compared with the best of the group,
how it matches (*Exact copy*, *Same picture*, *turned 90*, *mirrored*, *Similar - check*) and its folder.

- **Mark all but the keeper** / **Keep all in this group** - for the whole group at once.
- **Right-click a picture** -> **Keep this one instead**, **Open**, **Show in Explorer**.
- **Double-click** a picture to open it.
- **< Previous / Next >** (or **Ctrl + Left / Right**, **Page Up / Page Down**) moves between groups.

### What the match kinds mean
- **Exact copy** - the very same file, byte for byte.
- **Same picture** - the same photo re-saved, resized, compressed, lightly edited, turned or mirrored.
- **Similar - check** - almost the same scene. Often a burst or a retake; sometimes simply two similar
  views. Look closely - pick the best and mark the rest yourself.

## 4. Move the marked pictures
The button at the bottom says how many pictures are marked and how much space they take. Click it,
confirm, and they are moved. **Open review folder** shows them in Explorer; when you are happy, delete
that folder yourself to free the space. Changed your mind? **Undo last move**.

## 5. Save a report
**Save report...** writes a plain-text list of every group: which picture is kept, which are marked to
move and how each matches.

## Good to know
- Turned copies are only recognised as "the same picture", not as "similar".
- Heavily cropped or edited pictures may not be matched; *Loose* finds more of them as "Similar".
- Burst shots from a camera that does not record fractions of a second, taken within the same second,
  can look like copies of each other and be pre-marked. Always glance at a group before moving.
- Very large collections (100,000+ pictures) take longer to group.

## Where your data is stored
`%APPDATA%\DigitalSecretary\data\similar-pictures\` - your folder and options, a small compressed
fingerprint file per folder you scanned (hashes, sizes, dates, paths - no pictures), and the details of
the last move so it can be undone. Delete the folder to forget them.
