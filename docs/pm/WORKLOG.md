# Work log — Digital Secretary

Newest entry on top. One entry per work session: what was done, decisions made, current state, and
the exact **▶ NEXT** step, so any later session can resume with no extra context.

---

## 2026-10-31 — New feature: Similar Videos + shared helper kit (v2.6.0, branch `feature/similar-videos`)

**Done**
- Wrote the Similar Videos strategy; owner: **"use defaults"** - kit first (ENH-12), no trimmed clips,
  no ffmpeg fallback, skip Live Photo clips, separate plugin, review folder.
- **ENH-12 helper kit** `src/DigitalSecretary.Kit` (commit `279328c`): ByteSize, PauseToken, FileBytes,
  GrayImage/ImageHasher, ReviewFolder; then FileRecordCache, IncrementalScanner, FileNames, Ranking,
  SampleScene. Opt-in `<UseKit>true</UseKit>`; the DLL ships in each plugin folder. Golden rule 1,
  coding standards and architecture doc amended. Similar Pictures API and tests unchanged.
- **Similar Videos** (`src/Features/SimilarVideos/`, id `similar-videos`, category *Files*): hand-written
  Media Foundation interop, exact-time seek, codec-padding crop, rotation tag, 10 frames x (pHash + dHash
  x 8 orientations), flat frames out, length gate, Exact / Same video / Similar tiers, star grouping,
  film-strip review, Live Photo skip, review folder + Undo, gzip cache. 16 `.cs`, ~2.54 KLOC, no NuGet,
  **no host change**. SVD-1 … SVD-20.
- Real-video tests without binaries: `TestVideoWriter` encodes MP4s at test time.
- Docs: FEATURE.md, requirement doc, user guide, manual section, hubs, README row, Pages card + badges,
  xlsx, CHANGELOG 2.6.0, journal phase 25 (+2 pitfalls), DevEconomics row + narrative, TESTING.md.

**Decisions**
- Per-frame match on **pHash**; **dHash judged on the mean** (measured: gradients make per-frame dHash
  jump 13-21 under compression). Thresholds Balanced: pHash <= 8 on >= 80% of frames, mean dHash <= 16.
- Frames are cropped to the display aperture / first-announced size (1080p is coded as 1088).
- Exact-copy byte checks are memoised in the window so regrouping never re-reads gigabytes.
- New **ENH-15** (P3): trimmed clips, left out of v1 by the owner.

**State**
- `./build.ps1 -All` => **VERDICT: PASS** - 0 warnings, **622 unit + 30 QA**, overall coverage
  **93.9%**, Similar Videos **92.9%**, kit **96.1%**, docs consistent, PII scan clean.
- Two commits on `feature/similar-videos` (kit, then feature); **not pushed, not merged, no tag/release**.
- Storage (§9): tracked 2.2 MB (+~0.1 MB: code, docs, one 60 KB screenshot); ignored build output now
  471.5 MB (two new projects' `bin`/`obj`, all regenerable). No video files are committed - test videos
  are made at test time in a temp folder and deleted.
- Not done: a run on a real video folder in the app (verified with real MP4s made at test time, the QA
  click test and the generated sample).

**▶ NEXT** — Owner reviews the branch, tries Similar Videos on a real videos folder, then decides:
merge + tag `v2.6.0` + publish the release. Reconcile DevEconomics ACTUAL via `/cost` (OWN-3).

---

## 2026-10-30 — New feature: Similar Pictures (v2.5.0)

**Done**
- Put four design questions to the owner; answers: **separate plugin**, **review folder** (undo-able),
  match **exact, re-saved, bursts and turned/mirrored**, with near-identical scenes **left for manual
  verification**, read **every format Windows can preview**.
- Built **Similar Pictures** (`src/Features/SimilarPictures/`, id `similar-pictures`, category *Files*):
  Shell thumbnails + GDI+ fallback, EXIF size/time/sub-seconds/rotation, pHash + dHash in 8
  orientations, Exact / Same picture / Similar tiers, star grouping with a ranked keeper, side-by-side
  review cards, move to `_Similar pictures - review` or the Recycle Bin, Undo last move, gzip cache,
  pause/stop, text report. 17 `.cs` files, ~2.66 KLOC, no NuGet, **no host change**. SIM-1 … SIM-18.
- Calibrated thresholds from measured distances; tests caught an overflow bug that stopped every match.
- Fixed BUG-3: DocShots cropped the bottom of every feature screenshot.
- Docs: FEATURE.md, requirement doc, user guide, manual section, README row, Pages card, xlsx, CHANGELOG
  2.5.0, journal phase 24, DevEconomics row + narrative.

**Decisions**
- "Same picture" needs both hashes within tight limits **and** not two different EXIF moments; "Similar"
  is judged as-is only (turned matching is for copies); "Similar" is never pre-marked.
- ENH-12 (shared helper kit) has hit its "third copy" trigger; raised to P2, needs the owner's go.

**State**
- `./build.ps1 -All` => **VERDICT: PASS** - 0 warnings, **584 unit + 27 QA**, overall coverage
  **94.2%**, Similar Pictures **95.3%**, docs consistent, PII scan clean.
- Committed `0039ca6`, pushed (CI green), tagged `v2.5.0` and published the GitHub release with
  `DigitalSecretary-v2.5.0-win-x64.zip` (54.6 MB, self-contained). Release staging folder deleted.
- Not done: a run on a real photo folder in the app (verified via the generated sample, the QA click
  test and the real-file pipeline test).

**▶ NEXT** — Owner tries Similar Pictures on a real photo folder and reports anything off; decide
ENH-12 (shared helper kit). Then ENH-14 (faster grouping for huge collections) or ENH-10 (Similar Videos).

---

## 2026-10-29 — New feature: Storage Studio, phase 1 Space Map (v2.4.0)

**Done**
- Agreed the Storage Studio strategy with the owner: one plugin, phased - (1) Space Map audit,
  (2) Similar Pictures, (3) Similar Videos, (4) optional semantic similarity. Owner: "first understand
  and audit and view what is consuming more", then improve.
- Built **Storage Studio** (`src/Features/StorageStudio/`, id `storage-studio`, category *Files*):
  read-only metadata scan, clickable squarified size map coloured by type, space by type / by age,
  largest files and folders, report-only clean-up candidates, changes since the last scan (gzip
  snapshot per root), pause/resume/stop, Explorer actions, text report. 17 `.cs` files, ~2.28 KLOC,
  no NuGet, **no host change**. Requirements SST-1 … SST-17, 13 traceability rows.
- Tests: 14 unit-test files + the scan → snapshot → rescan integration test + a QA test that loads the
  real plugin and clicks a map tile. Screenshot via a placeholder-only `SampleSpaceTree`.
- Docs: FEATURE.md, requirement doc, user guide, manual section, README row, Pages card, xlsx
  regenerated, CHANGELOG 2.4.0, journal phase 23, DevEconomics row + narrative.
- Fixed BUG-2 (S3): the requirements and user-guide hubs were missing Email Intelligence and DeDupe
  Files. Recorded BUG-1 (the 2.3.0 prune-on-cancel bug) in TASKS, which had been left out.

**Decisions**
- **Read-only first.** Phase 1 never deletes; clean-up cards point and explain. Deletion stays with
  DeDupe Files (exact) and later phases (similar media, default "move to review folder").
- **Metadata only, totals roll up on insert** - fast, safe, and a stopped scan is still correct.
- **No real paths in images:** DocShots and QA show `SampleSpaceTree` via `ShowTree`.

**State**
- `./build.ps1 -All` => **VERDICT: PASS** - 0 warnings, **444 unit + 24 QA**, overall coverage
  **93.8%**, Storage Studio **98.3%**, docs/traceability consistent, PII scan clean.
- Committed `0734639`, pushed, tagged `v2.4.0` and published the GitHub release with
  `DigitalSecretary-v2.4.0-win-x64.zip` (54.5 MB, self-contained). Release staging folder deleted.
- Not yet done: a manual run of the real app against a real folder (the view was verified via the
  rendered screenshot and the QA click test).

**▶ NEXT** — Owner tries Storage Studio on a real folder and reports anything off; then ENH-9
(Similar Pictures, P1).

---

## 2026-10-29 — Owner decisions OWN-1 / OWN-2 (storage + stray files)

**Done**
- **OWN-1 → keep latest release only.** Verified all four release zips are attached to their GitHub
  releases, then deleted the v2.0.0/v2.1.0/v2.2.0 zips, the unpacked v2.2.0 tree,
  `artifacts/coverage` and `artifacts/release-stage`. Kept the v2.3.0 zip, `release/README.md`
  (tracked) and `artifacts/quality-state.json` (2.5 KB gate snapshot that `build.ps1` reads).
- **OWN-2 → moved out.** `tribeCoding.pdf` / `tribeCoding.pptx` moved to
  `D:\DevFolder\Projects\Claude\PersonalTools\_docs\`.
- Refreshed `docs/pm/STORAGE.md`; corrected the stale note below — v2.3.0 *is* pushed and tagged.

**State**
- Storage **681.5 MB → 309.2 MB** (−372 MB). The >200 MB flag still trips on `bin`/`obj`
  (~250 MB), which `dotnet build` regenerates; left as working output.
- Only OWN-3 (actual `/cost` into DevEconomics) still needs the owner.

**▶ NEXT** — ENH-1 (`docs/SEQUENCE.html` sequence-diagram SPD), then ENH-2 (function-length audit in
the gate).

---

## 2026-10-28 — New feature: DeDupe Files (v2.3.0)

**Done**
- Reviewed the project's state after ~4 months idle: v2.2.0, 7 feature plugins, `main` in sync with
  `origin/main` at `c8e5ab0`, last recorded gate PASS at 91.5% coverage.
- Built the **DeDupe Files** feature plugin (`src/Features/DeDupeFiles/`, id `dedupe-files`,
  category *Files*): MD5 checksum list per folder (`dedupe_md5_list.txt`), incremental rescan,
  duplicate analysis with reclaimable space, guarded deletion, pause/resume/stop.
  16 source files, ~2.2 KLOC, no NuGet dependencies, **no host change**.
- Requirements **DDF-1 … DDF-19** registered; 15 traceability rows; requirement doc, user guide,
  `FEATURE.md`, README row, Pages card, user-manual section and DocShots seed all added.
- Added the PM trackers this machine's standard requires (`docs/pm/TASKS.md`, this file,
  `docs/pm/STORAGE.md`).

**Decisions**
- **The checksum list is the cache.** A stored checksum is reused when a file's size *and*
  last-modified time are unchanged, so a re-run over a large tree re-reads almost nothing. The list
  is autosaved every 500 files, so **Stop** never discards work — that is what makes the operation
  resumable rather than all-or-nothing.
- **Pruning only after a completed scan.** A stopped scan has not seen every file, so it must not
  conclude the unseen ones are gone. `ScanEngine` prunes only when it ran to the end; there is a unit
  test pinning this.
- **MD5 decides what to compare; bytes decide what to delete.** MD5 is a content fingerprint, not a
  security measure, and two files can share one. `FileComparer.AreIdentical` re-reads the duplicate
  and the kept copy block by block before anything is removed, and a mismatch is skipped and logged.
- **Safe by default:** Dry run on, Recycle Bin on, verification on, empty files ignored, one copy of
  every set always kept, and the confirmation dialog defaults to Cancel.
- **Work was parallelised across subagents** (docs/showcase, docgen+manual, tests) on disjoint files
  while the production code was written here — a first for this repo; worth repeating.

**State**
- `./build.ps1 -All` => **VERDICT: PASS** — 0 warnings, **316 unit + 21 QA** tests (was 153 + 19),
  overall coverage **92.2%**, DeDupe Files **93.7%**, docs/traceability consistent, PII scan clean.
- One S2 bug was found by the test suite and fixed before release: a scan entered with an
  already-cancelled token pruned the entire checksum list. Pinned by
  `A_scan_cancelled_before_it_starts_prunes_nothing`, proved to fail before the fix and pass after.
- **Storage:** 654 MB -> 681.5 MB after this work (new `bin`/`obj` build output); `.git` is 805 KB and
  tracked files 1.2 MB, so the growth is entirely regenerable. See `docs/pm/STORAGE.md` and OWN-1.
- Committed on `main` as release **2.3.0** (since pushed and tagged `v2.3.0`).

**Known, left for the owner** (TASKS.md ⏸ Needs owner)
- OWN-1: ~411 MB of regenerable build output (`release/` 282 MB, `artifacts/` 129 MB) — deleting the
  owner's files needs the owner's go.
- OWN-2: `tribeCoding.pdf` / `tribeCoding.pptx` sit untracked in the repo root and are not ignored.
- OWN-3: run `/cost` and write the actual token spend into the DevEconomics workbook.

**▶ NEXT** — Decide OWN-1 and OWN-2 (storage + stray files), then consider ENH-1 (`docs/SEQUENCE.html`
sequence-diagram SPD) and ENH-2 (function-length audit in the gate), which are the two standing gaps
against the machine engineering standard.
