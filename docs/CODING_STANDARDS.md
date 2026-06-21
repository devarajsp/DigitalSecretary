# Coding standards

These are the rules for all code in Digital Secretary. `build.ps1` prints a summary of them on
every build; this file is the full version. They are enforced by review, static analysis, and tests.

## 1. Architecture boundaries
- The **host never references a feature**. Features reference only `DigitalSecretary.Abstractions`.
- Features never reference each other. Shared behaviour belongs in `Abstractions` (rarely) or is
  duplicated deliberately.
- A feature persists data **only** under `IFeatureContext.DataDirectory`. Never write to the host's
  settings or another feature's folder.

## 2. Separate logic from UI (so it can be tested)
- Business/logic code lives in **plain classes** (e.g. `CalculatorEngine`, `EmailFileNaming`,
  `AttachmentScanner`, `ClipboardPreview`, `PluginCatalog`).
- WinForms `UserControl`/`Form` classes only wire UI to those classes. They contain no logic worth
  unit-testing. (They are validated by QA automation instead — see `TESTING.md`.)
- If you find yourself wanting to test a private method inside a control, extract it to a class.

## 3. Tests are mandatory
- Every logic class/method ships with unit tests (xUnit + FluentAssertions) in
  `tests/DigitalSecretary.UnitTests`.
- New features are automatically covered by QA automation (the plugin pipeline test) — keep them
  loadable (valid `plugin.json`, parameterless module constructor).
- Keep unit + QA **green** and logic coverage **≥ 80%** (target 90%+). `build.ps1` reports both.

## 4. Static analysis stays clean
- .NET analyzers run on every build (`AnalysisLevel=latest-recommended`). Fix warnings; do not
  blanket-suppress. If a rule genuinely doesn't fit the app, adjust it centrally in `.editorconfig`
  with a comment explaining why (see `STATIC_ANALYSIS.md`).
- Target **zero warnings**. CI can enforce with `build.ps1 -WarnAsError`.

## 5. C# style
- `Nullable` enabled everywhere; no `#nullable disable`. Handle nulls, don't suppress with `!`
  unless provably safe (and comment why).
- File-scoped namespaces, `var` for obvious types, `_camelCase` private fields, `PascalCase`
  members, `sealed` by default for non-inherited classes.
- Prefer `using`/`await using` for `IDisposable`; dispose timers and streams.

## 6. Error handling
- A feature must **never crash the host**. Long-running/IO/clipboard/network code catches broadly
  and reports via the UI/log (this is why `CA1031` is relaxed for the app).
- Pure logic methods do **not** swallow exceptions silently — they return a clear result or throw.

## 7. Async & progress
- Long operations run off the UI thread (`Task.Run` / async) and report through `IProgress<T>`,
  and accept a `CancellationToken` for cancellation (see the Email feature).

## 8. Manifests
- Every feature has a `plugin.json` with a **unique, stable, kebab-case `id`**, correct
  `entryAssembly`/`entryType`, a `category`, and an `order`.

## Definition of done (per change)
- [ ] Logic in testable classes; UI thin.
- [ ] Unit tests added/updated; `build.ps1 -All` is **PASS**.
- [ ] No new analyzer warnings.
- [ ] Coverage on target; quality report regenerated.
- [ ] Docs updated (`FEATURE.md` for feature changes).
