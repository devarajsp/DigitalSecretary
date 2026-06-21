# Quality Report

_Generated 2026-06-21 11:17:40 by build.ps1 (Debug)._

## Verdict: PASS

| Gate | Result |
|------|--------|
| Build | succeeded |
| Static analysis | 0 warning(s), 0 error(s) |
| Unit tests | 45/45 passed, 0 failed, 0 skipped |
| Code coverage (overall) | 88.2% |
| QA automation | 11/11 passed, 0 failed, 0 skipped |
| Docs & traceability | consistent (0 warning(s)) |

## Static code analysis report

No analyzer findings - the build is clean. (Analyzers: .NET `latest-recommended`; see docs/STATIC_ANALYSIS.md.)

## Code coverage report

**Overall: 88.2%** (127/144 lines) - _measured this run_.

| Module / Feature | Line coverage | Lines covered |
|------------------|---------------|---------------|
| Abstractions (contract) | n/a | no executable lines |
| Feature: Calculator | 100% | 16/16 |
| Feature: ClipboardHistory | 100% | 4/4 |
| Feature: EmailDownloader | 89.3% | 50/56 |
| Feature: Launcher | 100% | 13/13 |
| Host (DigitalSecretary.App) | 80% | 44/55 |

_Coverage tracks testable logic; UI/bootstrap/loader/IMAP are excluded (see tests/coverage.runsettings) and validated by QA automation._

## Docs & traceability consistency report

All artifacts consistent - every traceability reference exists, every feature has its full doc set, requirement IDs match, coverage has no gaps, generated text is clean. (0 warning(s).)

## Notes
- Standards: `docs/CODING_STANDARDS.md` - Testing/QA: `docs/TESTING.md` - Analysis: `docs/STATIC_ANALYSIS.md`.
- Regenerate any time with `./build.ps1 -All`.
