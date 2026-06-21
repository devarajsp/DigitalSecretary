# Feature: Clipboard History

Captures text you copy anywhere in Windows so you can re-paste earlier items.

| | |
|---|---|
| **Id** | `clipboard-history` |
| **Category** | Tools |
| **Entry type** | `DigitalSecretary.Features.ClipboardHistory.ClipboardHistoryModule` |
| **Data** | none (in-memory for the current session) |

## What it does
- A timer polls the clipboard (~0.8s) and records new text (deduped, newest first, capped at 50).
- Double-click or **Copy** puts an entry back on the clipboard; **Delete** / **Clear All** manage the list.

## Files
| File | Role |
|------|------|
| `ClipboardHistoryModule.cs` | `IFeatureModule` entry point. |
| `ClipboardHistoryControl.cs` | The UI + clipboard polling. |
| `plugin.json` | Manifest. |

## Notes for future changes
- History is intentionally session-only. To persist it, save/load under `context.DataDirectory`
  (pass the context from the module into the control first).
