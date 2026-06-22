# DigitalSecretary — User Guide

> Audience: **end user**. Everything you need to use the app. For what each feature does in detail,
> see the per-feature guides under [`features/`](features/).
>
> 📖 **Prefer a visual manual with screenshots?** Open
> [`DigitalSecretary-User-Manual.html`](DigitalSecretary-User-Manual.html) — a single self-contained
> file (double-click to open in any browser; use its **Print / Save as PDF** button for a PDF).

## What is DigitalSecretary?
DigitalSecretary is your personal toolbox for Windows. It collects handy day-to-day tools
("features") in one window. You choose which ones to show, and new tools can be added over time.

## Install & run
1. You need **Windows 10/11** and the **.NET 9 runtime**.
2. Launch **`DigitalSecretary.exe`** (in `…\DigitalSecretary\src\DigitalSecretary.App\bin\Debug\net9.0-windows\`).
3. The main window opens on the **Home** screen.

## The main window
- **Menu bar** (top): `File`, `Features`, `View`, `Help`.
  - **Features → Category → Feature** opens any tool.
  - **View → Home** returns to the dashboard. **View → Configure Features…** chooses what you see.
  - **Help → About** shows version and installed-feature info.
- **Home dashboard:** a tile for each feature you've chosen to show. Click a tile to open it.
- **Content area:** the open feature appears here.

## Choosing what you see (Configure Features)
1. Click **View → Configure Features…**.
2. For each feature, tick or untick:
   - **On Dashboard** — whether its tile appears on Home.
   - **In Menu** — whether it appears under the Features menu.
3. Click **OK**. Your choices are saved and applied immediately.
> Hidden everything? You can always reopen this dialog from **View → Configure Features…**.

## Features
| Feature | What it's for | Guide |
|---------|---------------|-------|
| Launcher | One-click open your favorite apps, files, folders, links | [Open guide](features/launcher.md) |
| Calculator | Quick keypad / type-an-expression calculator | [Open guide](features/calculator.md) |
| Clipboard History | Re-paste things you copied earlier | [Open guide](features/clipboard-history.md) |
| Download Emails | Save a local copy of your Yahoo Mail | [Open guide](features/email-downloader.md) |
| Download Gmail | Save a local copy of your Gmail | [Open guide](features/gmail-downloader.md) |
| Download Google Drive | Save a local copy of your Google Drive (Docs exported to Office + PDF) | [Open guide](features/google-drive-downloader.md) |

## Where your data is stored
Everything stays on your PC under **`%APPDATA%\DigitalSecretary\`**:
- `app-settings.json` — your show/hide choices.
- `data\<feature>\…` — each feature's own data.

## FAQ / troubleshooting
- **A feature didn't open / showed an error.** The rest of the app keeps working; reopen it from the
  menu. If it persists, note the error message.
- **I don't see a tool I expected.** Check **View → Configure Features…** — it may be hidden.
- **Is my data private?** Yes — nothing is uploaded; data is local. Passwords are never saved.
