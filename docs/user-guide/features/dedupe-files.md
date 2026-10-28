# Using DeDupe Files

Find **duplicate files** in a folder (and everything inside it) and get your disk space back. The app
fingerprints every file, shows you which ones are exact copies, and - only when you say so - removes the
extra copies while **always keeping one copy of each**.

## Open it
**Features -> Files -> DeDupe Files**, or click its tile on Home.

## Safe by default
- **Dry run is ON.** Out of the box the app only *reports* what it would do. Nothing is deleted until you
  turn Dry run off **and** confirm.
- **One copy of every group is always kept.** The app can never delete all the copies of a file.
- **Byte-for-byte check.** Right before a duplicate is deleted, it is compared with the copy being kept.
  If they are not identical, it is left alone and noted in the log.
- **Recycle Bin by default.** Deleted duplicates can be restored. Permanent deletion is an opt-in.
- **Everything stays on your PC.** No internet, no cloud, no account.

> About MD5: the app uses MD5 only as a quick *fingerprint* to find files that look identical. It is not
> used for security - which is exactly why the byte-for-byte check exists as well.

## 1. Pick a folder
Click **Browse...** and choose the folder to clean up, for example `C:\Users\You\Pictures`. The app looks
in that folder **and all its subfolders**. It remembers your choice for next time.

## 2. Scan
Click **Scan**. The app reads every file and works out its MD5 fingerprint, then writes a list called
**`dedupe_md5_list.txt`** inside the folder you picked. Each line holds the fingerprint, the file size,
the last-modified time and the full path (separated by tabs; open it in Notepad if you are curious).

- A **progress bar** shows how far along it is. Big folders can take a while - the first scan reads every
  file; later scans are much faster (see below).
- The list is **saved as it goes**, so if you stop or close the app you do not lose the work already done.
- Shortcuts, links and junctions are not followed, and the list file itself is ignored.

### If the list already exists
If `dedupe_md5_list.txt` is already in the folder, you choose what to do:
- **Update** - rescan, but only new or changed files are fingerprinted again (unchanged files are
  recognised by size and modified time), and files you have since deleted are dropped from the list.
- **Use as-is** - skip scanning and go straight to Analyze using the list you have. Handy when you have
  just scanned and only want to look again.
- **Ask** - the app asks you every time. You can set Update or Use as-is as a fixed choice in the options
  so it never asks.

## 3. Analyze
Click **Analyze**. The app groups files that have the same fingerprint and shows:
- **Duplicate groups** - how many sets of identical files there are.
- **Duplicate files** - how many extra copies exist in total.
- **Duplicate size** - the space those extra copies take.
- **Reclaimable space** - what you would get back by removing them (one copy per group stays).

Below that, the window lists each group, showing the copy that **will be kept** and the copies that
**would be deleted**. Nothing has been changed at this point.

## Options
- **Which copy to keep:** *shortest path*, *oldest modified*, *newest modified*, or *first alphabetically*.
  If two copies tie, the app picks the same one every time, so results are repeatable.
- **Ignore empty files** (on by default): zero-byte files all look identical, so they are left out.
- **Verify before delete** (on by default): the byte-for-byte check described above.
- **Delete to Recycle Bin** (default) or **delete permanently**. Permanent deletion cannot be undone, so
  choose it only if you are sure.
- **Dry run** (on by default): report only, delete nothing.
- **If the list exists:** Ask / Update / Use as-is.

Change an option, then Analyze again to see the plan update.

## 4. Delete
1. Review the duplicate groups and check the keeper choice looks right.
2. Do a **Dry run** first - click **Delete** with Dry run on to see what *would* happen.
3. When you are happy, **turn Dry run off** and click **Delete**.
4. A **confirmation** box tells you exactly how many files and how much space will be removed (and whether
   they go to the Recycle Bin or are deleted permanently). Nothing happens unless you confirm.

## Pause, Resume and Stop
Both scanning and deleting can take time, so you stay in control:
- **Pause** holds the work after the current file; **Resume** carries on.
- **Stop** ends it cleanly. Whatever was already done is kept (and the list is saved), so you can continue
  later without starting over.

## Results and report
When deleting finishes (or you stop it) a summary shows:
- **Files deleted**
- **Files skipped** - for example a copy that failed the byte-for-byte check, or one that no longer exists
- **Failures** - for example a file that was in use or you had no permission to delete
- **Space freed**

Click **Save report** to write a plain-text report - the duplicate groups and the results - to a file you
choose. Handy for keeping a record of what was cleaned up.

## Good to know
- Only **exact** duplicates are found (identical content). Similar-looking photos are not matched.
- File **names** do not matter; two files with different names and the same content are duplicates.
- Re-run Scan after you add or change files so the list is up to date; with **Update** only the new and
  changed files are processed.
- The app remembers your folder and all your options. No passwords or personal details are stored.

## Data
Settings: `%APPDATA%\DigitalSecretary\data\dedupe-files\` (folder and options).
The checksum list `dedupe_md5_list.txt` lives in the folder you scanned - it is a plain text file you can
delete at any time (the next Scan simply rebuilds it).
