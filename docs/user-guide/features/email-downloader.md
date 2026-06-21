# Using Download Emails (Yahoo)

Save a local **copy** of your Yahoo Mail — every folder, message, and attachment — without changing
anything in your mailbox.

## Open it
**Features → Communication → Download Emails**, or click its tile on Home.

## Before you start: get a Yahoo app password
Yahoo does **not** allow your normal password for this. Create an **app password**:
1. Go to **Yahoo Account → Account Security**.
2. Choose **Generate app password**.
3. Copy the generated password — you'll paste it into the app.

## Download your mail
1. Enter your **Yahoo email** and the **app password**.
2. Choose a **Save to folder** (or keep the default) using **Browse…**.
3. Click **Start Download**. Watch the **progress bar** and **log**; click **Cancel** any time.

## What you get
- Your Yahoo folder structure, recreated on your PC.
- Each email saved **twice**: a readable **.txt** and a full-fidelity **.eml**.
- **All attachments** saved as-is, including inline images.
- If two items would share a name, the app adds `_1`, `_2`, … so nothing is overwritten.

## Good to know
- **It's a copy.** Folders are opened read-only, so nothing is deleted, moved, or marked read in Yahoo.
- The app **remembers your email and folder**, but **never saves your password**.
- Errors on individual messages are noted in the log with `!` and the download keeps going.

## Data
`%APPDATA%\DigitalSecretary\data\email-downloader\email_settings.json` (email + folder only — no password).
