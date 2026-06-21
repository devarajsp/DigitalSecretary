# Static code analysis

Static analysis runs automatically on **every build** — there is no separate tool to install.

## What's enabled
Configured once in the repo-root `Directory.Build.props` (inherited by all projects):
```xml
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<AnalysisMode>Recommended</AnalysisMode>
```
This turns on the Roslyn / .NET quality (CAxxxx) analyzers at a curated, sensible level. Code-style
(IDExxxx) rules are defined in `.editorconfig` and surface in the IDE.

## Running it
- Every `./build.ps1` builds with `--no-incremental` so analyzers always run, then prints a
  **Static code analysis report** — findings grouped by rule (Severity | Rule | Count | Example) —
  to the console and into `docs/QUALITY_REPORT.md`. A clean build shows "No analyzer findings".
- Enforce zero-warnings as an error:
  ```powershell
  ./build.ps1 -All -WarnAsError
  ```

## Rule tuning (and why)
Rules are adjusted centrally in `.editorconfig` with rationale, e.g.:
- `CA1031` (catch general exception) → **none**: features deliberately catch broadly so a feature
  can never crash the host.
- `CA1303/1304/1305/1310/1311` (localization/culture) → **none**: this is a single-locale desktop app
  (logic that must be culture-stable uses `CultureInfo.InvariantCulture` explicitly).
- `CA1812` → **none**: manifest/DTO types are created by JSON or reflection.
- `CA2200`, `CA1816` → **warning**: correctness rules we want kept loud.
- Test projects relax `CA1707` (underscores in test names) via `tests/.editorconfig`.

## How to change a rule
1. Prefer fixing the code.
2. If a rule genuinely doesn't fit, set its severity in `.editorconfig` **with a comment** explaining
   why. Don't scatter `#pragma warning disable` through the code.
3. For a one-off justified case, a local `#pragma` with a comment is acceptable.

## Goal
**Zero warnings.** The current build is clean (0 warnings); keep it that way.
