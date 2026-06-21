# Feature: Launcher

One-click launch your favorite apps, files, folders, and links.

| | |
|---|---|
| **Id** | `launcher` |
| **Category** | Tools |
| **Entry type** | `DigitalSecretary.Features.Launcher.LauncherModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\launcher\shortcuts.json` |

## What it does
- Add shortcuts to applications/files (file picker), folders (folder picker), or URLs.
- Double-click (or **Launch**) opens the target with its default Windows handler.
- **Edit** / **Remove** manage existing entries. All changes persist immediately.

## Files
| File | Role |
|------|------|
| `LauncherModule.cs` | `IFeatureModule` entry point — returns the view. |
| `LauncherControl.cs` | The UI (ListView + toolbar). |
| `LauncherItem.cs` | Data model for one shortcut. |
| `LauncherStore.cs` | JSON load/save inside the feature's data folder. |
| `Prompt.cs` | Tiny modal text-input dialog. |
| `plugin.json` | Manifest the host reads to show the feature. |

## Notes for future changes
- Persistence is isolated to this feature via `IFeatureContext.DataDirectory`; it does not touch the host or other features.
- To add columns/fields, extend `LauncherItem` and update `RefreshList()` + the add/edit flows.
