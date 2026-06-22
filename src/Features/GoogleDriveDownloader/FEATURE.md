# Feature: Download Google Drive

Downloads a local **copy** of your Google Drive, mirroring the folder tree on disk. Regular files are
downloaded as-is; **Google-native** files (Docs/Sheets/Slides/Drawings) are **exported** to both an
editable Office format and PDF. Access is **read-only** — your Drive is never changed.

| | |
|---|---|
| **Id** | `google-drive-downloader` |
| **Category** | Cloud |
| **Entry type** | `DigitalSecretary.Features.GoogleDriveDownloader.GoogleDriveDownloaderModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\google-drive-downloader\drive_settings.json` (credentials path + folder); cached OAuth token under `…\token\` |
| **NuGet** | Google.Apis.Drive.v3 (private to this plugin) |

## What it does
1. Authenticates with the **OAuth 2.0 installed-app flow** using the user's own *Desktop app* OAuth
   client (`credentials.json`). A browser opens once to grant **read-only** Drive access; the token is
   cached under the feature's `token` folder so later runs are silent.
2. Lists every non-trashed file with the Drive API (paged), and builds the folder tree from parents.
3. Recreates the Drive folder structure locally.
4. **Regular files** (PDFs, images, Office docs, …) are downloaded **as-is**.
5. **Google-native files** are **exported** — because they have no raw bytes to download:
   - Docs → `.docx` **and** `.pdf`
   - Sheets → `.xlsx` **and** `.pdf`
   - Slides → `.pptx` **and** `.pdf`
   - Drawings → `.png` **and** `.pdf`
   - Non-exportable native items (Forms, Sites, Apps Script, shortcuts) are skipped with a note.
6. Name collisions get `_1`, `_2`, … appended. Progress bar + log update live; **Cancel** stops cleanly.

## Authentication (why OAuth, not an app password)
Google Drive has no IMAP/app-password equivalent; the API requires OAuth. The user creates an OAuth
**client ID of type "Desktop app"** in Google Cloud Console, downloads its JSON, and points the feature
at it. We request only `DriveService.Scope.DriveReadonly`. The refresh token is stored by Google's
`FileDataStore` in the feature's own data folder — never in the settings file.

## Why the Google libraries are "private" to this plugin
`CopyLocalLockFileAssemblies=true` makes the build drop `Google.Apis.*` (and `Newtonsoft.Json`, etc.)
into this plugin's folder. The host loads the plugin in an isolated `AssemblyLoadContext`, which
resolves those DLLs from the plugin's own `deps.json`. No other feature or the host needs them.

## Files
| File | Role |
|------|------|
| `GoogleDriveDownloaderModule.cs` | `IFeatureModule` entry point. |
| `DownloadDriveControl.cs` | The UI (credentials picker, folder, progress, log). |
| `GoogleDriveDownloader.cs` | OAuth sign-in, Drive listing, traversal, download/export. |
| `GoogleExportFormats.cs` | Pure rules: native-vs-folder detection + export targets (unit-tested). |
| `DriveFileNaming.cs` | Pure file/path helpers incl. parent-chain folder resolution (unit-tested). |
| `DriveSettings.cs` | Settings model + JSON store (in the feature's data folder). |
| `plugin.json` | Manifest. |

## Security note
Only the credentials-file path and target folder are remembered. The OAuth token is cached by Google's
own secure store inside the feature's data directory and is never written to the settings file.
