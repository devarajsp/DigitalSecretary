# Using Download Gmail

Save a local **copy** of your Gmail — every folder/label, message, and attachment — without changing
anything in your mailbox.

## Open it
**Features → Communication → Download Gmail**, or click its tile on Home.

## Before you start: get a Gmail app password
Gmail does **not** allow your normal password for this. You need an **app password**:
1. Turn on **2-Step Verification** for your Google Account (required before app passwords appear).
2. Go to **Google Account → Security → App passwords**.
3. Create one and copy it — you'll paste it into the app.
4. Make sure IMAP is on: **Gmail → Settings → Forwarding and POP/IMAP → Enable IMAP**.

## Download your mail
1. Enter your **Gmail address** and the **app password**.
2. Choose a **Save to folder** (or keep the default) using **Browse…**.
3. Click **Start Download**. Watch the **progress bar** and **log**; click **Cancel** any time.

## What you get
- Your Gmail folder/label structure, recreated on your PC.
- Each email saved **twice**: a readable **.txt** and a full-fidelity **.eml**.
- **All attachments** saved as-is, including inline images.
- If two items would share a name, the app adds `_1`, `_2`, … so nothing is overwritten.

## Good to know
- **It's a copy.** Folders are opened read-only, so nothing is deleted, moved, or marked read in Gmail.
- **Labels overlap.** In Gmail a message can carry several labels, so the same message may be saved under
  more than one folder (for example *Inbox* and *[Gmail]/All Mail*).
- The app **remembers your email and folder**, but **never saves your password**.
- Errors on individual messages are noted in the log with `!` and the download keeps going.

## Data
`%APPDATA%\DigitalSecretary\data\gmail-downloader\gmail_settings.json` (email + folder only — no password).
