# Tasks — Digital Secretary

Bugs and enhancements are tracked **separately** (machine rule §8). IDs are never reused.
Open items first, then the history. Keep this current **as** work happens, not after.

**Counts:** 0 open bugs · 5 open enhancements (0 P1) · 3 items need the owner.

---

## 🐞 Bugs

### Open
_None known._

### Fixed
_None recorded yet. A fixed bug stays listed here with its severity, cause, fix version/commit and
the regression test that pins it._

---

## ✨ Enhancements

### Open

| ID | Type | Priority | Area | Summary | Status |
|----|------|----------|------|---------|--------|
| ENH-1 | Test gap | P2 | Quality | No sequence-diagram SPD (`docs/SEQUENCE.html`, mermaid, high→low drill-down) as the machine standard §5.6 requires. The architecture doc covers the plugin lifecycle in prose only. | Open |
| ENH-2 | Tech debt | P2 | Build | No function-length audit in the regression gate (§5.3, ≤100 lines per function). Functions are kept short by review alone; nothing enforces it for C#. | Open |
| ENH-3 | Docs | P3 | Showcase | The test count and coverage figures in `README.md` and `docs/index.html` are hand-maintained, so they drift between releases. `check_docs.py` gates feature *titles* but not these numbers. | Open |
| ENH-4 | Improvement | P3 | DeDupe Files | Duplicate grid is capped at 5,000 rows for responsiveness; the full set is only in the saved report. Virtual-mode `ListView` would lift the cap. | Open |
| ENH-5 | Feature | P3 | DeDupe Files | Deduplicate *across* two folders (compare folder A against folder B, delete only in B) — currently one tree at a time. | Open |

### Done

| ID | Type | Area | Summary | Version | Commit |
|----|------|------|---------|---------|--------|
| ENH-6 | Feature | DeDupe Files | New feature plugin: MD5 checksum list per folder, incremental rescan, duplicate analysis with reclaimable space, guarded deletion (dry run, byte-for-byte verify, Recycle Bin), pause/resume/stop. 19 requirements DDF-1…DDF-19. | 2.3.0 | see tag `v2.3.0` |
| ENH-7 | Docs | PM | Added the project-management trackers the machine standard requires: this file plus `docs/pm/WORKLOG.md` and `docs/pm/STORAGE.md`. | 2.3.0 | see tag `v2.3.0` |

---

## ⏸ Needs owner

| ID | Area | What is needed | Why |
|----|------|----------------|-----|
| OWN-1 | Storage | Decide whether to delete the regenerable build output: `release/` (282 MB — v2.0.0/v2.1.0/v2.2.0 zips plus an unpacked v2.2.0 tree) and `artifacts/` (129 MB). | The repo folder is ~654 MB, of which ~411 MB is regenerable. This trips the machine standard's >200 MB untracked/ignored flag (§9). Release zips are also attached to the GitHub releases, so local copies are a convenience. Deleting the owner's files needs the owner's go (§2, §9.4). |
| OWN-2 | Repo hygiene | Decide what to do with `tribeCoding.pdf` and `tribeCoding.pptx` in the repo root (untracked, dated 2026-09-06): move them out, or add them to `.gitignore`. | They are unrelated to the app and are not git-ignored, so a `git add .` would commit them — and a slide deck may carry identity that the PII gate cannot read out of a binary. |
| OWN-3 | DevEconomics | Run `/cost` and write the figure into the workbook's `Total cost ACTUAL` column for the 2.3.0 row, then re-run `python tools/docgen/build_deveconomics.py`. | Only the harness knows the real token spend; the workbook currently holds the estimate, and the EST-vs-ACTUAL trend chart needs the actual (CLAUDE.md, DevEconomics step 2). |
