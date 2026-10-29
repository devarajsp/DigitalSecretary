# Work log — Digital Secretary

Newest entry on top. One entry per work session: what was done, decisions made, current state, and
the exact **▶ NEXT** step, so any later session can resume with no extra context.

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
