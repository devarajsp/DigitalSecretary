# Architecture

Digital Secretary is a **plugin host** plus a set of **feature plugins**. The design goals are:
independent features, lazy loading, per-feature dependencies, and "the host never knows about a
specific feature."

## The three layers

### 1. Contract — `DigitalSecretary.Abstractions`
A tiny assembly that both the host and every feature reference. It defines:

- **`IFeatureModule`** — the entry point a feature implements:
  ```csharp
  Control CreateView(IFeatureContext context);
  ```
  Called once, the first time the feature is opened. Returns the WinForms control to show.

- **`IFeatureContext`** — services the host gives a feature:
  - `FeatureId` — the feature's id.
  - `DataDirectory` — a private, pre-created folder for the feature's data.
  - `Log(message)` — write to the host log.

Keep this assembly stable: changing it affects every plugin.

### 2. Host — `DigitalSecretary.App`
Responsibilities:
- **Discovery** (`Hosting/PluginCatalog`): scan `plugins/*/plugin.json`. Only JSON is read, so no
  feature code loads at startup.
- **Menu + dashboard** (`MainForm`, `UI/DashboardControl`): built from the manifests.
- **Lazy activation** (`Hosting/LoadedFeature`): the feature's assembly + view are created the
  first time it's opened, then cached.
- **Isolation** (`Hosting/PluginLoadContext`, `Hosting/PluginLoader`): each feature loads in its
  own `AssemblyLoadContext` with an `AssemblyDependencyResolver`, so a feature can ship its own
  NuGet dependencies without clashing with the host or other features.
- **Settings** (`Settings/*`): host preferences, e.g. which features show on the dashboard.

The host references **only** the contract — never a feature.

### 3. Features — `src/Features/<Name>`
Each feature is its own project that:
- references only `DigitalSecretary.Abstractions`,
- ships a `plugin.json` manifest,
- builds to a DLL that is auto-copied into the host's `plugins/<ProjectName>/` folder.

## The manifest — `plugin.json`
```json
{
  "id": "email-downloader",
  "title": "Download Emails",
  "category": "Communication",
  "description": "Download a local copy of all Yahoo Mail folders…",
  "order": 10,
  "entryAssembly": "DigitalSecretary.Features.EmailDownloader.dll",
  "entryType": "DigitalSecretary.Features.EmailDownloader.EmailDownloaderModule"
}
```
- `id` — unique, stable, kebab-case. Used for the data folder and show/hide settings.
- `category` — groups the feature in the **Features** menu.
- `order` — sort order in menus and on the dashboard.
- `entryAssembly` / `entryType` — what to load and instantiate when the feature is opened.

## Lifecycle (open a feature)
```
startup ─ scan plugin.json ─ build menus + dashboard tiles        (no feature DLL loaded)
                                  │
user clicks a feature ───────────┘
        │
        ▼
LoadedFeature.GetView()
        │  first time only:
        ├─ new PluginLoadContext(entryAssembly)         # isolated context
        ├─ load entryAssembly, find entryType
        ├─ Activator.CreateInstance → IFeatureModule
        └─ module.CreateView(context)  → cached Control
        ▼
host docks the control into the content area
```

## Isolation & shared types
`PluginLoadContext.Load` returns `null` for `DigitalSecretary.Abstractions` so it resolves from
the host's default context. That makes `IFeatureModule`/`IFeatureContext` the **same** types on
both sides, so the cast works. Everything else (e.g. MailKit) is resolved from the plugin folder
via the feature's `deps.json`. This is why a feature must set
`CopyLocalLockFileAssemblies=true` when it uses NuGet packages.

## Per-feature data
`IFeatureContext.DataDirectory` → `%APPDATA%\DigitalSecretary\data\<feature-id>\`. The host creates
it. A feature must store everything there and nowhere else; that keeps features independent and
uninstallable (delete the plugin folder + the data folder).

## Show / hide (dashboard and menu, independently)
**View → Configure Features…** opens a grid with two checkboxes per feature — *On Dashboard* and
*In Menu* — controlled separately:
- `AppSettings.HiddenOnDashboard` — ids hidden from the dashboard tiles.
- `AppSettings.HiddenOnMenu` — ids hidden from the **Features** menu.

Features are visible in both places by default (a newly added feature appears automatically; its id
is simply absent from both lists). After the dialog is accepted, the host re-saves settings,
rebuilds the Features menu (`MainForm.RebuildFeaturesMenu`), and refreshes the dashboard tiles.
A feature hidden from both is still reachable again via **View → Configure Features…**.

## Build glue
`src/Features/Directory.Build.props` gives every feature the shared TFM/settings, the
`Abstractions` reference (with `Private=false` so the host's copy is authoritative), the
`plugin.json` copy, and an `AfterBuild` target that copies the feature output into the host's
`plugins/<ProjectName>/` folder.
