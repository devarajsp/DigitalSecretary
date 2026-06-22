# Feature: Download Gmail

Downloads a local **copy** of every Gmail folder/label — each email as `.txt` **and** `.eml`, with all attachments — mirroring the remote folder structure on disk.

| | |
|---|---|
| **Id** | `gmail-downloader` |
| **Category** | Communication |
| **Entry type** | `DigitalSecretary.Features.GmailDownloader.GmailDownloaderModule` |
| **Data** | `%APPDATA%\DigitalSecretary\data\gmail-downloader\gmail_settings.json` (email + folder; password is never stored) |
| **NuGet** | MailKit 4.17.0 (private to this plugin) |

## What it does
1. Connects to `imap.gmail.com:993` (SSL) and authenticates with a Google **app password**.
2. Discovers all folders/labels (Inbox, Sent, `[Gmail]/All Mail`, custom labels, nested).
3. Opens each folder **read-only (EXAMINE)** so nothing is deleted, moved, or marked read on the server.
4. Saves every message as `yyyy-MM-dd_HHmmss_Subject.txt` (readable) and `.eml` (full fidelity).
5. Saves **all** attachment-like parts as-is — real attachments, inline/embedded images, and attached messages.
6. Name collisions get `_1`, `_2`, … appended. Progress bar + log update live; **Cancel** stops cleanly.

## Gmail specifics
- Gmail requires an **app password** (normal password and "less secure apps" are blocked). The user must
  turn on **2-Step Verification** first, then generate the app password under **Google Account → Security**.
  IMAP must also be enabled in **Gmail Settings → Forwarding and POP/IMAP**.
- Gmail exposes **labels as IMAP folders**. Because a message can carry several labels, the same message
  can appear under more than one folder (e.g. *Inbox* and *[Gmail]/All Mail*) and may be saved more than
  once. This mirrors the Yahoo downloader's "copy every folder" behaviour.

## Why MailKit is "private" to this plugin
`CopyLocalLockFileAssemblies=true` makes the build drop MailKit/MimeKit/etc. into this plugin's
folder. The host loads the plugin in an isolated `AssemblyLoadContext`, which resolves those DLLs
from the plugin's own `deps.json`. No other feature or the host needs MailKit.

## Files
| File | Role |
|------|------|
| `GmailDownloaderModule.cs` | `IFeatureModule` entry point. |
| `DownloadGmailControl.cs` | The UI (credentials, progress, log). |
| `GmailDownloader.cs` | IMAP connection, traversal, and file saving. |
| `GmailFileNaming.cs` | Pure file-name helpers (unit-tested). |
| `GmailAttachmentScanner.cs` | Pure attachment-selection rules (unit-tested). |
| `GmailSettings.cs` | Settings model + JSON store (in the feature's data folder). |
| `plugin.json` | Manifest. |

## Security note
The app password is held only in memory for the run and never written to disk. Only the email
address and target folder are remembered.
