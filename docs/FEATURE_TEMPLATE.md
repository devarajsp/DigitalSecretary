# Feature template

Copy this skeleton when creating a new feature. Replace `Sample` / `sample` everywhere.

```
src/Features/Sample/
├─ DigitalSecretary.Features.Sample.csproj
├─ plugin.json
├─ SampleModule.cs
├─ SampleControl.cs
└─ FEATURE.md
```

### DigitalSecretary.Features.Sample.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>DigitalSecretary.Features.Sample</RootNamespace>
    <AssemblyName>DigitalSecretary.Features.Sample</AssemblyName>
    <!-- Only if the feature uses NuGet packages: -->
    <!-- <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies> -->
  </PropertyGroup>
  <!-- <ItemGroup><PackageReference Include="Pkg" Version="x" /></ItemGroup> -->
</Project>
```

### plugin.json
```json
{
  "id": "sample",
  "title": "Sample",
  "category": "Tools",
  "description": "One-line description shown in menu tooltip and dashboard tile.",
  "order": 100,
  "entryAssembly": "DigitalSecretary.Features.Sample.dll",
  "entryType": "DigitalSecretary.Features.Sample.SampleModule"
}
```

### SampleModule.cs
```csharp
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.Sample;

public sealed class SampleModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new SampleControl(context);
}
```

### SampleControl.cs
```csharp
using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.Sample;

public sealed class SampleControl : UserControl
{
    public SampleControl(IFeatureContext context)
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        // context.DataDirectory is your private, pre-created data folder.
        Controls.Add(new Label
        {
            Text = "Hello from Sample",
            Dock = DockStyle.Top, Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold)
        });
    }
}
```

### FEATURE.md
```markdown
# Feature: Sample

What it does (one paragraph).

| | |
|---|---|
| **Id** | `sample` |
| **Category** | Tools |
| **Entry type** | `DigitalSecretary.Features.Sample.SampleModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\sample\` |

## Files
| File | Role |
|------|------|
| `SampleModule.cs` | IFeatureModule entry point. |
| `SampleControl.cs` | The UI. |
| `plugin.json` | Manifest. |
```

Then: `dotnet sln add …`, `dotnet build`, run. See `ADDING_A_FEATURE.md` for the full checklist.
```
