# Using Storage Studio

See **what is using the space** in a folder - as a picture. Every folder and file becomes a rectangle
sized by how much space it takes and coloured by what kind of file it is, so the biggest space users
are, quite literally, the biggest rectangles. Click into them to find out exactly where your disk went.

## Open it
**Features -> Files -> Storage Studio**, or click its tile on Home.

## It only looks
Storage Studio **never deletes, moves or changes anything**. It does not even open your files - it only
reads their names, sizes and dates. Nothing leaves your PC.

## 1. Pick a folder and Scan
Click **Browse...**, choose a folder (for example `C:\Users\You\Documents`, or a whole drive such as
`D:\`), then click **Scan**. The status line counts files, folders and size as it goes. Use **Pause** /
**Resume** or **Stop** at any time - if you stop, you still see everything found so far.

Shortcuts, links and junctions are not followed, so nothing is counted twice. Folders you are not
allowed to read are skipped and listed in the **Log** tab.

## 2. Read the size map
- **Bigger rectangle = more space.** Folders big enough show their name and size in a strip at the top,
  with their contents drawn inside.
- **Colour = type of file.** The **Space by type** chart on the right is also the colour key
  (Videos, Pictures, Music & audio, Documents, Archives, Installers & disk images, Code & build files,
  Other). A folder takes the colour of whatever fills most of it.
- **Hover** over any rectangle for its name, size, share of the folder, type, date and full path.
- **Map levels** (1-3) sets how many folders deep the map draws at once. 2 is a good start; 3 shows
  more detail on a large screen.

## 3. Drill in
- **Click a folder** on the map to open it. Clicking a file opens the folder it is in.
- The **breadcrumb** above the map shows where you are - click any part of it, or **Up**, to go back.
- Everything else on the screen - both charts and all the lists - follows the folder you are in.

## 4. The charts
- **Space by type** - how much is videos, pictures, documents and so on, with the share of each.
- **Space by age** - how much was changed in the last year, and how much has sat untouched for 1-3,
  3-5 or more than 5 years. Old, big data is often the best candidate to archive.

## 5. The tabs underneath
| Tab | What it shows |
|---|---|
| **Largest files** | The 100 biggest files in the current folder (any depth). |
| **Largest folders** | The 100 biggest folders inside it. |
| **Clean-up candidates** | Space that is commonly safe to reclaim, grouped, with a reason for each (see below). |
| **Changes since last scan** | What grew or shrank since the last time you scanned this same folder. |
| **Log** | Folders that could not be read, links skipped, timings. |

**Double-click** a row to open its folder in the map. **Right-click** a row or a rectangle to **Show in
Explorer**, **Copy path**, or open a folder in the map.

### Clean-up candidates
These are *pointers*, not actions - Storage Studio removes nothing.
- **Build & package folders** - `node_modules`, `obj`, `bin` (only next to an `obj`), `__pycache__` and
  similar. Programming tools rebuild them when needed.
- **Temporary & partial files** - `.tmp`, unfinished downloads (`.crdownload`, `.part`), crash dumps,
  `.bak` copies and Office lock files (`~$...`).
- **Installers & disk images** - `.msi`, `.iso` and similar anywhere, and `.exe` files in a Downloads
  folder. Usually not needed once the program is installed.
- **Large files not changed in years** - files of 1 GB or more not changed in 2 years. Consider moving
  them to an external drive or archive.

For exact duplicate files, use **DeDupe Files**.

### Changes since last scan
The first time you scan a folder there is nothing to compare with. Scan the **same folder** again later
(next week, next month) and this tab lists the folders that grew or shrank by 1 MB or more, biggest
change first - the quickest way to find what is eating your disk *lately*. Only complete scans are
remembered; a scan you stopped is not used for comparison.

## 6. Save a report
**Save report...** writes a plain-text file for the folder you are in: totals, space by type and age,
the largest files and folders, clean-up candidates and changes since the last scan.

## Good to know
- Sizes are the files' own sizes; the space Windows actually reserves on disk can be slightly different.
- "Not changed in years" uses the date a file was last *modified* - Windows does not reliably record
  when a file was last opened.
- Very large drives (millions of files) need a few hundred MB of memory while the map is open.

## Where your data is stored
`%APPDATA%\DigitalSecretary\data\storage-studio\` - your last folder and map setting, plus one small
compressed snapshot per folder you have scanned (sizes and folder names only), used for "Changes since
last scan". Delete the folder to forget them.
