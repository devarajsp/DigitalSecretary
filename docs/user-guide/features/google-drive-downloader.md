# Using Download Google Drive

Save a local **copy** of your Google Drive — folders, files, and Google documents — without changing
anything in your Drive. Google Docs/Sheets/Slides are **exported** to formats you can open and edit.

## Open it
**Features → Cloud → Download Google Drive**, or click its tile on Home.

## Before you start: one-time Google setup
Google Drive has no "app password" — it uses secure **OAuth** sign-in, so you create a small credentials
file once:
1. Go to the **Google Cloud Console** and create (or pick) a project.
2. Enable the **Google Drive API** for that project.
3. Under **APIs & Services → Credentials**, create an **OAuth client ID** of type **Desktop app**.
4. **Download** its JSON file (often called `credentials.json`) and keep it somewhere safe.

> The first time you run a download, a browser opens asking you to allow **read-only** access. After you
> approve, the app remembers it (a token is cached) so you won't be asked again.

## Download your Drive
1. Click **Browse…** next to **Credentials (.json)** and select the file you downloaded.
2. Choose a **Save to folder** (or keep the default) with the second **Browse…**.
3. Click **Start Download**. Approve access in the browser if asked. Watch the **progress bar** and **log**;
   click **Cancel** any time.

## What you get
- Your Drive folder structure, recreated on your PC.
- **Regular files** (PDFs, images, Office files, …) downloaded exactly as they are.
- **Google documents exported twice** so they're always usable:
  - **Docs → .docx and .pdf**
  - **Sheets → .xlsx and .pdf**
  - **Slides → .pptx and .pdf**
  - **Drawings → .png and .pdf**
- Items that can't be exported (Forms, Sites, Apps Script, shortcuts) are skipped with a note.
- If two items would share a name, the app adds `_1`, `_2`, … so nothing is overwritten.

## Good to know
- **It's a copy.** The app asks only for **read-only** access; nothing in your Drive is changed.
- The app **remembers your credentials file and folder**. Your sign-in **token is stored securely** in the
  feature's own data folder — never in the settings file.
- Errors on individual files are noted in the log with `!` and the download keeps going.

## Data
`%APPDATA%\DigitalSecretary\data\google-drive-downloader\drive_settings.json` (credentials path + folder).
The cached OAuth token lives under `…\google-drive-downloader\token\`.
