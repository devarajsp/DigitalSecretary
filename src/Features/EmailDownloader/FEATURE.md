# Feature: Download Emails (Yahoo)

Downloads a local **copy** of every Yahoo Mail folder — each email as `.txt` **and** `.eml`, with all attachments — mirroring the remote folder structure on disk.

| | |
|---|---|
| **Id** | `email-downloader` |
| **Category** | Communication |
| **Entry type** | `DigitalSecretary.Features.EmailDownloader.EmailDownloaderModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\email-downloader\email_settings.json` (email + folder; password is never stored) |
| **NuGet** | MailKit 4.17.0 (private to this plugin) |

## What it does
1. Connects to `imap.mail.yahoo.com:993` (SSL) and authenticates with an **app password**.
2. Discovers all folders (Inbox, Sent, Archive, Spam, custom, nested).
3. Opens each folder **read-only (EXAMINE)** so nothing is deleted, moved, or marked read on the server.
4. Saves every message as `yyyy-MM-dd_HHmmss_Subject.txt` (readable) and `.eml` (full fidelity).
5. Saves **all** attachment-like parts as-is — real attachments, inline/embedded images, and attached messages.
6. Name collisions get `_1`, `_2`, … appended. Progress bar + log update live; **Cancel** stops cleanly.

## Why MailKit is "private" to this plugin
`CopyLocalLockFileAssemblies=true` makes the build drop MailKit/MimeKit/etc. into this plugin's
folder. The host loads the plugin in an isolated `AssemblyLoadContext`, which resolves those DLLs
from the plugin's own `deps.json`. No other feature or the host needs MailKit. This is the model
for **any** feature that needs its own NuGet packages.

## Files
| File | Role |
|------|------|
| `EmailDownloaderModule.cs` | `IFeatureModule` entry point. |
| `DownloadEmailsControl.cs` | The UI (credentials, progress, log). |
| `EmailDownloader.cs` | IMAP connection, traversal, and file saving. |
| `EmailSettings.cs` | Settings model + JSON store (in the feature's data folder). |
| `plugin.json` | Manifest. |

## Security note
The app password is held only in memory for the run and never written to disk. Only the email
address and target folder are remembered.
