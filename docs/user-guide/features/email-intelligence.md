# Email Intelligence — user guide

Turn the emails you've downloaded into a private, searchable picture of your contacts and your life —
**entirely on your PC**. Nothing is uploaded, and no AI service is used.

## Before you start
Download your mail first using **Download Emails** (Yahoo) and/or **Download Gmail**, so you have a
folder of `.eml` / `.txt` files (with attachments).

## Run an analysis
1. Open **Insights → Email Intelligence**.
2. **Email archive folder** — choose the folder where your downloaded emails live.
3. **Output folder** — choose where to write the results.
4. **Your email (optional)** — leave blank to auto-detect, or type your address so "you" is identified
   exactly.
5. Click **Analyze**. A progress bar and log show each phase (parsing, de-duplicating, resolving
   people, scoring, building the report). You can **Cancel** at any time.
6. When it finishes, click **Open report** to view it in your browser.

## What you get
In the output folder:

- **`index.html`** — an interactive report with tabs:
  - **Overview** — totals and your top topics.
  - **People** — search by name or email; click anyone to open their dossier (addresses, phone, links,
    organization, strength, message counts, topics).
  - **Timeline** — the full relationship history with a chosen person; "open" links jump to the
    original message file.
  - **Graph** — a map of who you know and who appears together on your emails.
  - **Topics** — the most common themes across your archive.
  - **Life Data** — purchases, subscriptions, travel and account sign-ups detected in your mail.
  - **Documents** — every attachment, de-duplicated and grouped by type.
- **`Contacts.csv`** — your master contact list, incl. a heuristic tone column (opens in Excel).
- **`LifeData.csv`** / **`Documents.csv`** — the extracted life data and document library.
- **`Contacts.vcf`** — a vCard you can import into Gmail or Outlook contacts.
- **`network.graphml`** — the relationship graph for Gephi / yEd.
- **`data/report.json`** — the full structured data.

## Consolidating several archives (Yahoo + Gmail + ...)
Tick **"Append to existing results"** and keep the **same output folder** across runs. Each run merges
the new archive with everything analysed before and removes duplicates, so you end up with **one
de-duplicated contact list** spanning all your mailboxes. Leave it **unticked** to overwrite — i.e. to
analyse just the current folder from scratch.

## Notes
- Re-running on the same (or a larger) archive refreshes the results; duplicates are removed
  automatically.
- "Dormant" marks people you used to talk to often but haven't recently — handy for reconnecting.
- **Tone**, **life data** (receipts/subscriptions/travel/accounts) and the **document library** are
  produced by local rules/word-lists, **not AI** — treat them as helpful approximations and verify
  anything important before relying on it.
