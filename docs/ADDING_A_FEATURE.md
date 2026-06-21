# Adding a feature

This is the mechanical recipe for adding a new feature. Following it, a new capability appears in
the app **without any change to the host**. Replace `Notes` / `notes` with your feature name.

> Naming: project `DigitalSecretary.Features.Notes`, id `notes`, folder `src/Features/Notes/`.

## Step 1 — Create the folder and project
`src/Features/Notes/DigitalSecretary.Features.Notes.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>DigitalSecretary.Features.Notes</RootNamespace>
    <AssemblyName>DigitalSecretary.Features.Notes</AssemblyName>
  </PropertyGroup>
</Project>
```
That's all — shared settings (target framework, the `Abstractions` reference, the auto-copy to
`plugins/`) come from `src/Features/Directory.Build.props`.

**If the feature needs a NuGet package**, add it here and copy its dependencies into the plugin:
```xml
  <PropertyGroup>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SomePackage" Version="x.y.z" />
  </ItemGroup>
```

## Step 2 — Add the manifest
`src/Features/Notes/plugin.json`
```json
{
  "id": "notes",
  "title": "Notes",
  "category": "Tools",
  "description": "Jot and save quick notes.",
  "order": 40,
  "entryAssembly": "DigitalSecretary.Features.Notes.dll",
  "entryType": "DigitalSecretary.Features.Notes.NotesModule"
}
```

## Step 3 — Implement the module (entry point)
`src/Features/Notes/NotesModule.cs`
```csharp
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.Notes;

public sealed class NotesModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new NotesControl(context);
}
```

## Step 4 — Build the UI
`src/Features/Notes/NotesControl.cs`
```csharp
using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.Notes;

public sealed class NotesControl : UserControl
{
    private readonly string _file;

    public NotesControl(IFeatureContext context)
    {
        _file = Path.Combine(context.DataDirectory, "notes.txt");   // per-feature storage
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var title = new Label
        {
            Text = "Notes",
            Dock = DockStyle.Top, Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold)
        };
        var editor = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
        if (File.Exists(_file)) editor.Text = File.ReadAllText(_file);
        editor.TextChanged += (_, _) => File.WriteAllText(_file, editor.Text);

        Controls.Add(editor);
        Controls.Add(title);
    }
}
```

## Step 5 — Register the project and build
```bash
dotnet sln DigitalSecretary.sln add src/Features/Notes/DigitalSecretary.Features.Notes.csproj
dotnet build DigitalSecretary.sln -c Debug
```
The build copies the feature into `plugins/DigitalSecretary.Features.Notes/`. Launch the app — the
new feature appears under **Features → Tools → Notes** and on the dashboard.

## Step 6 — Document it (developer + product + end user)
- `src/Features/Notes/FEATURE.md` — developer doc (copy an existing one).
- `docs/requirements/features/notes.md` — product requirements (purpose, user stories, functional
  requirements, sub-features, acceptance criteria, NFRs, traceability). Add it to the index table in
  `docs/requirements/REQUIREMENTS.md`.
- `docs/user-guide/features/notes.md` — end-user how-to. Add it to `docs/user-guide/USER_GUIDE.md`.
- Add a section + screenshot for the feature to `tools/user-manual.template.html`, then regenerate the
  HTML manual: `dotnet run --project tools/DocShots` → `./tools/build-user-manual.ps1`.
- Add the feature's **requirements** (rows) and **traceability** (one row per sub-feature, with every
  artifact reference) to the data tables in `tools/docgen/build_excel_docs.py`, then regenerate:
  `python tools/docgen/build_excel_docs.py`. Confirm the new rows show **Coverage = Complete**.

## Step 7 — Add tests (required)
Put **logic in plain classes** (e.g. a `NotesFormatter`), not in the control, and test it:
1. Reference the new feature from the unit-test project:
   ```bash
   dotnet add tests/DigitalSecretary.UnitTests/DigitalSecretary.UnitTests.csproj \
     reference src/Features/Notes/DigitalSecretary.Features.Notes.csproj
   ```
2. Add `tests/DigitalSecretary.UnitTests/Features/NotesTests.cs` covering the logic.
3. **QA automation needs no change** — it discovers the new plugin and load/view-tests it
   automatically.

## Step 8 — Verify with the quality gate
```powershell
./build.ps1 -All
```
Confirm **`VERDICT: PASS`** (build clean, unit + QA green, coverage on target). Then run the app,
open the feature from the menu, and confirm its data appears under
`%APPDATA%\DigitalSecretary\data\notes\`. See `docs/CODING_STANDARDS.md` and `docs/TESTING.md`.

## Checklist
- [ ] Folder `src/Features/<Name>/` created.
- [ ] `.csproj` (name + assembly name only; NuGet + `CopyLocalLockFileAssemblies` if needed).
- [ ] `plugin.json` with a unique `id` and correct `entryAssembly` / `entryType`.
- [ ] `…Module.cs` implements `IFeatureModule`.
- [ ] `…Control.cs` builds the UI; **logic in plain classes**; persists only under `context.DataDirectory`.
- [ ] Unit tests added for the logic; feature referenced from `DigitalSecretary.UnitTests`.
- [ ] `FEATURE.md` (dev) + `docs/requirements/features/<id>.md` (product) + `docs/user-guide/features/<id>.md` (user) added.
- [ ] User manual updated (template section + regenerated HTML with a screenshot).
- [ ] Requirements + Traceability spreadsheets regenerated; new rows show **Coverage = Complete**.
- [ ] Project added to the solution.
- [ ] `./build.ps1 -All` → **VERDICT: PASS** (build clean, unit + QA green, coverage on target).
- [ ] **No change to `DigitalSecretary.App`.**
