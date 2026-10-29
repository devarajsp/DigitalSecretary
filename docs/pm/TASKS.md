# Tasks — Digital Secretary

Bugs and enhancements are tracked **separately** (machine rule §8). IDs are never reused.
Open items first, then the history. Keep this current **as** work happens, not after.

**Counts:** 0 open bugs · 10 open enhancements (1 P1) · 1 item needs the owner.

---

## 🐞 Bugs

### Open
_None known._

### Fixed
| ID | Sev | Area | Symptom | Cause | Found | Fixed in | Regression guard |
|----|-----|------|---------|-------|-------|----------|------------------|
| BUG-2 | S3 | Docs | `docs/requirements/REQUIREMENTS.md` and `docs/user-guide/USER_GUIDE.md` no longer listed every feature (Email Intelligence and DeDupe Files missing). | The hub tables were hand-edited and not gated - `check_docs.py` checks feature titles only in README and the Pages site. | 2026-10-29 | 2.4.0 | Manual for now; gating the hubs too is folded into ENH-3. |
| BUG-1 | S2 | DeDupe Files | A scan started with an already-cancelled token pruned the whole checksum list. | `cancelled` flag initialised to `false` and only set inside a loop that ran zero times. | 2026-10-28 | 2.3.0 | `A_scan_cancelled_before_it_starts_prunes_nothing` |

---

## ✨ Enhancements

### Open

| ID | Type | Priority | Area | Summary | Status |
|----|------|----------|------|---------|--------|
| ENH-9 | Feature | P1 | Storage Studio | **Phase 2 - Similar Pictures:** exact duplicates first (MD5), then perceptual hashes (dHash + pHash, both must agree), burst grouping by EXIF time, a suggested "keep" per group, side-by-side review with a Strict/Balanced/Loose slider; default action moves to a review folder. | Open |
| ENH-10 | Feature | P2 | Storage Studio | **Phase 3 - Similar Videos:** group by duration (+-2%), then ~10 sampled-frame hashes compared in order (with shift for trimmed copies) via Windows Media Foundation; optional user-installed ffmpeg as fallback. | Open |
| ENH-1 | Test gap | P2 | Quality | No sequence-diagram SPD (`docs/SEQUENCE.html`, mermaid, high→low drill-down) as the machine standard §5.6 requires. The architecture doc covers the plugin lifecycle in prose only. | Open |
| ENH-2 | Tech debt | P2 | Build | No function-length audit in the regression gate (§5.3, ≤100 lines per function). Functions are kept short by review alone; nothing enforces it for C#. | Open |
| ENH-3 | Docs | P3 | Showcase | The test count and coverage figures in `README.md` and `docs/index.html` are hand-maintained, so they drift between releases. `check_docs.py` gates feature *titles* but not these numbers - nor the two hub tables (see BUG-2). | Open |
| ENH-4 | Improvement | P3 | DeDupe Files | Duplicate grid is capped at 5,000 rows for responsiveness; the full set is only in the saved report. Virtual-mode `ListView` would lift the cap. | Open |
| ENH-5 | Feature | P3 | DeDupe Files | Deduplicate *across* two folders (compare folder A against folder B, delete only in B) — currently one tree at a time. | Open |
| ENH-11 | Improvement | P3 | Storage Studio | Show *size on disk* (compression, cluster slack) next to the logical size. | Open |
| ENH-12 | Tech debt | P3 | Features | `ByteSize` and `PauseToken` are now copied in DeDupe Files and Storage Studio (features may not reference each other). Consider a shared, non-contract helper kit once a third copy appears. | Open |
| ENH-13 | Feature | P3 | Storage Studio | **Phase 4 (optional):** "similar in meaning" pictures via a local ONNX image-embedding model, off by default (~150 MB download). | Open |

### Done

| ID | Type | Area | Summary | Version | Commit |
|----|------|------|---------|---------|--------|
| ENH-8 | Feature | Storage Studio | **Phase 1 - Space Map:** read-only metadata scan, clickable squarified size map coloured by type (1-3 levels), space by type and by age, largest files/folders, report-only clean-up candidates, "changes since last scan" from a gzip snapshot, pause/stop, Explorer actions, text report. 17 requirements SST-1…SST-17. | 2.4.0 | see tag `v2.4.0` |
| ENH-6 | Feature | DeDupe Files | New feature plugin: MD5 checksum list per folder, incremental rescan, duplicate analysis with reclaimable space, guarded deletion (dry run, byte-for-byte verify, Recycle Bin), pause/resume/stop. 19 requirements DDF-1…DDF-19. | 2.3.0 | see tag `v2.3.0` |
| ENH-7 | Docs | PM | Added the project-management trackers the machine standard requires: this file plus `docs/pm/WORKLOG.md` and `docs/pm/STORAGE.md`. | 2.3.0 | see tag `v2.3.0` |

---

## ⏸ Needs owner

| ID | Area | What is needed | Why |
|----|------|----------------|-----|
| OWN-3 | DevEconomics | Run `/cost` and write the figure into the workbook's `Total cost ACTUAL` column for the 2.3.0 row, then re-run `python tools/docgen/build_deveconomics.py`. | Only the harness knows the real token spend; the workbook currently holds the estimate, and the EST-vs-ACTUAL trend chart needs the actual (CLAUDE.md, DevEconomics step 2). |

### Resolved by owner

| ID | Area | Decision | Date |
|----|------|----------|------|
| OWN-1 | Storage | Keep the latest release only. Deleted the v2.0.0/v2.1.0/v2.2.0 zips, the unpacked v2.2.0 tree, `artifacts/coverage` and `artifacts/release-stage` (all four zips verified on GitHub releases first). Kept `release/DigitalSecretary-v2.3.0-win-x64.zip`, `release/README.md` and `artifacts/quality-state.json` (the gate's last snapshot, read by `build.ps1`). 681.5 MB → 309.2 MB. The >200 MB flag remains from `bin`/`obj` build output (~250 MB), which is regenerable working output, not release clutter. | 2026-10-29 |
| OWN-2 | Repo hygiene | Moved `tribeCoding.pdf` / `tribeCoding.pptx` out of the repo to `D:\DevFolder\Projects\Claude\PersonalTools\_docs\`. Working tree is now clean of untracked files. | 2026-10-29 |
