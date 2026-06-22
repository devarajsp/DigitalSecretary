# Requirement - Email Archive Intelligence

| | |
|---|---|
| Feature id | `email-intelligence` | Category | Insights | Status | Proposed |

> Sibling to the **Download Emails / Download Gmail** features. Those *produce* the local archive
> (`.txt` + `.eml` + attachments); this feature *consumes* it and turns it into structured, searchable
> intelligence. **100% offline, no AI and no network calls; output is portable files (Excel + others),
> never a database.**

## 1. Purpose & value
Turn a folder of downloaded personal email (`.txt`, `.eml`, and attachments from Yahoo + Gmail) into
clean, organized, reusable intelligence about the **people, money, events, content and documents** in
your life - computed entirely on the local machine. The headline experience: **type a person's name and
instantly get a full dossier, a relationship-history timeline, and an interactive relationship graph** -
all presented as a **single self-contained HTML5 report with tabbed views**, with the bulk data stored in
folders beside it. **Progress is shown throughout** the build.

## 2. Users & user stories
- As a user, I point the feature at my archive folder and get a **clean, de-duplicated, organized** dataset.
- As a user, I **type a contact's name** and see everything about them - addresses, phones, company,
  our history, topics we discuss, attachments exchanged, tone over time - as a **card + graph**.
- As a user, I get a **master contact list** I can export (vCard / Excel / CSV) and re-import into
  Gmail / Outlook, including an "update set" of new/changed details for existing contacts.
- As a user, I see a **network "map"** of who I know and how they connect.
- As a user, I get extracted **life data**: purchases, subscriptions, travel, accounts, important dates, documents.
- As a user, all outputs are **Excel / CSV / JSON / HTML files in an output folder** - portable, no DB.
- As a user, **re-running** on a grown archive refreshes results **without duplicating**.
- As a user, **nothing leaves my PC** - no cloud, no AI calls.
- As a user, I see a **relationship-history timeline** for each contact - our whole interaction story in order.
- As a user, I get a library of **useful & reusable documents and content** pulled out of my mail (attachments, templates, snippets, signatures, links).
- As a user, I watch **clear progress** (phase, current item, counts, ETA) while my archive is processed.
- As a user, I explore everything in **one nice HTML5 page with tabs**, backed by data files in folders so it stays fast and portable.

## 3. Functional requirements

### Group A - Ingest & parse
| ID | Requirement |
|----|-------------|
| EI-A1 | Accept one or more **input folders**, scanned recursively; discover `.eml`, `.txt`, and attachment files. |
| EI-A2 | Parse `.eml` with **MimeKit** (already shipped by the Email features): From/To/Cc/Bcc/Reply-To, Date, Subject, Message-ID, In-Reply-To, References, body (text + HTML), attachments. |
| EI-A3 | Parse `.txt` emails heuristically: detect header lines (`From:`/`To:`/`Date:`/`Subject:`) when present, else treat as body. **Pair `.txt`+`.eml` siblings** (same base name from the downloader) and prefer `.eml` fidelity, using `.txt` as fallback. |
| EI-A4 | Correctly decode **MIME encoded-words, quoted-printable, base64, and all charsets**; normalize to UTF-8; convert HTML to text for analysis. **No mojibake** in any output. |
| EI-A5 | **Index attachments**: filename, extension, MIME type, size, SHA-256 hash, source message, date; copy/link them into an organized output tree (by contact / by year). |
| EI-A6 | **Robustness**: a malformed or unreadable file is logged and skipped; the run continues. |

### Group B - Clean & dedupe
| ID | Requirement |
|----|-------------|
| EI-B1 | Remove **exact duplicates** by `Message-ID`. |
| EI-B2 | Remove **near-duplicates** (hash of normalized sender + date + subject + body) to collapse cross-account copies (Yahoo + Gmail) and Gmail label duplication. |
| EI-B3 | **Reconstruct threads** via In-Reply-To / References (fallback: subject + participants). |
| EI-B4 | **Classify** each message - Personal / Newsletter / Transactional / Notification / Spam - using local heuristics (`List-Unsubscribe`, `Precedence: bulk`, sender patterns, keywords). Rules configurable. |
| EI-B5 | **De-duplicate attachments** by content hash (identical files counted once, stored once). |

### Group C - Contacts & identity
| ID | Requirement |
|----|-------------|
| EI-C1 | Extract **every participant** (address + display name) across From/To/Cc/Bcc/Reply-To, with role and direction (sent-by-me vs received). |
| EI-C2 | Identify **"me"** (the archive owner) from the most-frequent From address(es); allow user override in config. |
| EI-C3 | **Identity resolution (deterministic, no AI)**: merge addresses belonging to one person via exact/normalized name match, shared display names, and signature matching; support **manual merge/split** in the UI. |
| EI-C4 | Build a **Person record**: canonical name, all addresses (primary + secondary), display-name variants, domains/organizations. |
| EI-C5 | **Signature / detail extraction** (regex + heuristics): phone numbers (international incl. +91 India and US), job title, company, postal-address block, website / LinkedIn / social URLs; keep most-recent value **plus a change history**. |
| EI-C6 | **Export contacts** to vCard (`.vcf`), Excel and CSV; produce an **"update set" diff** (new/changed fields only) for re-importing into Gmail / Outlook. |

### Group D - Relationship intelligence
| ID | Requirement |
|----|-------------|
| EI-D1 | Per-contact metrics: total messages, sent vs received, first/last contact, active span, frequency over time, **who initiates**, average + median **response time** (mine and theirs), longest silence. |
| EI-D2 | **Relationship strength score** - transparent composite of frequency + recency + reciprocity, with configurable weights. |
| EI-D3 | **Dormant / reconnect flag**: historically frequent contacts with no recent contact (threshold configurable). |
| EI-D4 | **Co-occurrence edges**: people who appear together on the same emails (To/Cc), weighted by count - the basis of the network graph. |
| EI-D5 | **Cluster** contacts (by email domain to organizations; personal vs work via a free-mail domain list). |

### Group E - Content intelligence (local rule/lexicon-based - explicitly NOT AI)
| ID | Requirement |
|----|-------------|
| EI-E1 | **Keyword / topic extraction** per contact and per thread via local TF-IDF / frequency over the corpus (stopword-filtered). |
| EI-E2 | **Tone & sentiment indicators** using a bundled **offline lexicon** (VADER/AFINN-style word lists) plus signals (gratitude, exclamation, caps, emoji); produce a per-contact and over-time **tone trend**. Clearly labelled a heuristic, not AI; limits documented. |
| EI-E3 | **Important-date & event extraction**: dates, birthdays/anniversaries, meetings, deadlines mentioned in text (regex + date parsing). |
| EI-E4 | **Reusable-content library**: extract every piece of **reusable text** - frequently-sent phrases / templates, your own signatures, boilerplate you repeat, links you've shared, addresses and reference numbers you've given out, instructions - each **tagged by type** with a **reuse/usefulness score**, copy-to-clipboard, and a link back to the source email. |
| EI-E5 | **Offline language detection** per message (optional, low priority). |

### Group F - Life-data extraction (local pattern/rule-based; accuracy limits acknowledged)
| ID | Requirement |
|----|-------------|
| EI-F1 | **Purchases / receipts / orders** -> amount, currency, merchant, date, order#; spending sheet by merchant / category / time. |
| EI-F2 | **Subscriptions & recurring charges** -> recurring sender/amount detection; "candidates to cancel". |
| EI-F3 | **Travel** -> flight / hotel / car confirmations -> trips list (date, route/location, confirmation#) + totals. |
| EI-F4 | **Account inventory** -> services you signed up for (welcome / verify-email / password-reset patterns) -> your security & privacy surface. |
| EI-F5 | **Bills & dues, deliveries / tracking, tickets / bookings** -> respective sheets. |
| EI-F6 | **Document / attachment classification** -> invoices, statements, tickets, photos, contracts -> an indexed "vault". |
| EI-F7 | **Useful-document library**: gather **every attachment and embedded document worth keeping** (PDF, Office, images, contracts, invoices, IDs, certificates, tickets), **de-duplicate by content hash**, tag by document type + usefulness, link each to its **source message + date**, and organize into a browsable **library folder** surfaced in the report's *Documents & Reusables* tab. |

### Group G - People search, dossier & graph (headline UX)
| ID | Requirement |
|----|-------------|
| EI-G1 | **Type-ahead search**: type a name or email -> fuzzy-matched people. |
| EI-G2 | **Person dossier (card)**: canonical name, all emails/phones, company/title, social links, strength score, first/last contact, totals; sections for **timeline, topics, attachments exchanged, tone trend, important dates, related people**. |
| EI-G3 | **Interactive relationship graph**: nodes = people, edges = co-occurrence/strength; **click a node -> open its dossier**; filter by time range, strength, cluster/org; rendered **fully offline**. |
| EI-G4 | **Drill-through**: from the dossier open the actual local messages/threads (`.eml` / `.txt`). |
| EI-G5 | **Export** a dossier to HTML/PDF (offline) and the graph to **GraphML/GEXF** (opens in Gephi) + a PNG image. |
| EI-G6 | **Relationship-history timeline**: per-contact chronological timeline of every interaction - messages sent/received, attachments exchanged, extracted events/important dates, detected role/company changes - with **first / last / longest-gap** markers; **zoom** and **filter** by type + date range. Plus an optional global "your life in email" timeline across all contacts. |

### Group H - Outputs & storage (NO database; portable files)
| ID | Requirement |
|----|-------------|
| EI-H1 | Primary structured output = **Excel workbooks**: `Contacts`, `Messages_Index` (metadata), `Relationships`, `Attachments_Index`, `Topics`, plus life-data sheets (`Spending`, `Subscriptions`, `Travel`, `Accounts`, `Documents`, `ImportantDates`). |
| EI-H2 | Companion machine-readable output: **JSON** (full model) + **CSV** per sheet; **GraphML/GEXF** for the network; **vCard** for contacts; **HTML** for dossiers; **KML** for any locations (optional). |
| EI-H3 | Attachments and dossier exports written to an **organized folder tree** under the output folder. |
| EI-H4 | **Scale guard**: when message count exceeds Excel's ~1,048,576-row limit (or a configurable threshold), write the bulk `Messages_Index` as CSV/JSON and keep Excel for curated summaries; warn the user. Parsing **streams** - never loads the whole corpus into memory. |
| EI-H5 | All outputs land under a **user-chosen output folder** (and the feature's own data folder for settings). **No database. Nothing leaves the PC.** |
| EI-H6 | The structured data also feeds the **HTML5 report's `data/` folder** (see Group K), which is the **human-facing view**; the Excel/CSV/JSON files are the **analyst / portable view** of the same model. |
| EI-H7 | **Native Excel workbook (`.xlsx`)**: a single multi-sheet workbook (Contacts / Life Data / Documents) written with a **built-in OOXML writer (no third-party library)**, so results open directly in Excel with **typed numeric columns**. |

### Group I - Re-run / incremental
| ID | Requirement |
|----|-------------|
| EI-I1 | Re-running on the same or a grown archive is **idempotent** - dedupe + content-hash keys prevent double counting. |
| EI-I2 | Keep a **manifest of processed files** (path + hash) so unchanged files are skipped on re-run (incremental). |
| EI-I3 | Output is **refreshed**, not blindly appended; previous outputs versioned or overwritten per config. |
| EI-I4 | **Append vs overwrite mode**: *Overwrite* replaces previous results using only the current input; *Append* merges the new archive with the previously-analysed messages (a de-duplicated corpus persisted beside the report) and **re-de-duplicates**, consolidating multiple archives (Yahoo + Gmail + ...) into **one de-duplicated contact/relationship dataset**. |

### Group J - Configuration & UI
| ID | Requirement |
|----|-------------|
| EI-J1 | Configure input folder(s), output folder, owner addresses, dormant threshold, strength weights, enabled extractors, free-mail domain list, classification rules. |
| EI-J2 | Long-running processing on a **background worker**: progress bar + live log + **Cancel** (mirrors the Email Downloader UX). |
| EI-J3 | Persist settings **only** under `IFeatureContext.DataDirectory`; never store secrets; no passwords. |
| EI-J4 | **Run-summary screen**: counts (messages, people, duplicates removed, attachments, extracted records) + buttons to open the output folder/files. |
| EI-J5 | **Granular progress**: report progress **per phase** (parse -> dedupe -> contacts -> metrics -> extraction -> report generation) with **percent complete**, **current file/contact**, running counts, and an **ETA**; live log; fully **cancellable**; stays responsive on tens of thousands of messages. |

### Group K - Unified HTML5 report & folder data store
| ID | Requirement |
|----|-------------|
| EI-K1 | Generate a **single self-contained HTML5 report** (`index.html`) as the primary way to explore results, with **tabbed views**: **Overview** (dashboard), **People** (searchable list), **Person Dossier**, **Relationship Timeline**, **Network Graph**, **Topics**, **Life Data** (Spending / Travel / Subscriptions / Accounts), **Documents & Reusables**, **Attachments**. |
| EI-K2 | Store **bulk data in folders** beside the report: a `data/` tree of **JSON shards** (per-person, per-sheet), plus `attachments/`, `documents/`, and `dossiers/`; the page loads shards **on demand** so it stays light on large archives. |
| EI-K3 | The report is **fully offline** - no CDN, no remote fonts/scripts; all CSS/JS bundled locally. Because browsers restrict `file://` data loading, the report renders either through the app's embedded **WebView2** (local folder mapped as a virtual host) **or** via a **localhost-only static file server** the app starts (no internet); small datasets may be inlined as a fallback. |
| EI-K4 | **Client-side navigation & search**: type-ahead people search, sortable/filterable tables, click a graph node or contact to open its dossier, switch tabs with **no reload**. |
| EI-K5 | **Portable**: zipping the report folder lets it open on any machine with a browser; no database, no install. |
| EI-K6 | Report generation **shows progress** (Group J) and writes **incrementally**; a re-run **refreshes** the report from the latest data. |

## 4. Sub-features (acceptance)
### 4.1 Ingest & parse (A) - *Accept:* a mixed folder of `.eml`/`.txt`/attachments parses into a normalized message set; encodings render clean (no `Â`/`Ã`/`â€`); bad files are skipped with a logged `!`.
### 4.2 Clean & dedupe (B) - *Accept:* identical mail across Yahoo+Gmail collapses to one; threads group correctly; duplicate counts reported.
### 4.3 Contacts & identity (C) - *Accept:* a person with 3 addresses appears as **one** Person with all 3; phone/company pulled from signatures; vCard + "update set" export open correctly.
### 4.4 Relationship intelligence (D) - *Accept:* per-contact metrics and a strength score compute; dormant contacts are flagged; co-occurrence edges exist for graphing.
### 4.5 Content intelligence (E) - *Accept:* top topics per contact list sensibly; tone trend renders; the heuristic nature is labelled in the UI.
### 4.6 Life-data extraction (F) - *Accept:* sample receipts/travel/subscriptions populate their sheets with source-message links; misses are acceptable and flagged low-confidence.
### 4.7 People search, dossier & graph (G) - *Accept:* typing a name opens a dossier in <1s on a built dataset; clicking a graph node opens that person; dossier/graph export to file.
### 4.8 Outputs & storage (H) - *Accept:* an `output/` folder contains the Excel workbook(s) + JSON/CSV/vCard/GraphML; no database file exists; large indexes spill to CSV with a warning.
### 4.9 Re-run / incremental (I) - *Accept:* running twice yields the same counts (no duplication); adding files updates only the deltas.
### 4.10 Configuration & UI (J) - *Accept:* settings persist in the feature folder; progress + Cancel work; the summary screen opens the outputs.
### 4.11 Relationship-history timeline (G6) - *Accept:* opening a contact shows an ordered, zoomable timeline of messages/attachments/events with first/last/longest-gap markers; filtering by type/date updates it live.
### 4.12 Useful & reusable documents/content (E4, F7) - *Accept:* the *Documents & Reusables* tab lists de-duplicated documents and reusable snippets, each tagged and linked to its source email; items open/copy correctly.
### 4.13 Unified HTML5 report (K) - *Accept:* one `index.html` opens **offline** with all tabs working, type-ahead search and graph/dossier navigation functioning, and bulk data served from the `data/` folder; the folder zips and opens elsewhere.
### 4.14 Granular progress (J5) - *Accept:* during a run the user sees phase, current item, counts and an ETA; Cancel stops cleanly; the UI stays responsive on a large archive.
### 4.15 Append / overwrite (I4) - *Accept:* running on a second archive in **Append** mode (same output folder) yields one dataset containing contacts from both, with a message shared across archives counted once; **Overwrite** mode replaces the results with the current input only.
### 4.16 Native Excel workbook (H7) - *Accept:* a run writes `EmailIntelligence.xlsx` containing **Contacts**, **Life Data** and **Documents** sheets that open in Excel; numeric columns (counts, amounts, strength) are stored as numbers.

## 5. Acceptance criteria (feature)
A run over a folder of `.txt`/`.eml`/attachments produces, **fully offline** and with **live progress
shown throughout**: a de-duplicated dataset; a **master contact list** with merged identities and
enriched details; per-contact **relationship metrics** and a **relationship-history timeline**; extracted
**life-data** and a **useful/reusable documents & content library**; **Excel + companion files** in the
output folder (**no DB**); and a single self-contained **HTML5 report with tabbed views** (people search
-> dossier + timeline + graph + documents), backed by **data stored in folders**. Re-running does not
duplicate. **No network or AI calls occur** during the run.

## 6. Non-functional
- **Privacy / offline (hard constraint):** zero network calls, verifiable; all data local; no secrets
  persisted. This deliberately rules out online map tiles, Gravatar, geocoding services and AI APIs -
  so **geographic mapping is limited to offline / exported KML**; the in-app "map" is the **network graph**.
- **Performance:** streaming parse; tens of thousands of messages; background + cancellable; bounded memory.
- **Correctness:** robust MIME/encoding handling; grep outputs for `Â|Ã|â€` must be **zero** (cleanliness gate).
- **Reliability:** per-file failures isolated; one bad email never aborts the run.
- **Portability:** outputs are plain files; no DB or services; open on any machine with Excel/text tools.
- **Testability:** parsing, dedupe, identity-resolution, scoring and extractors are **plain classes**
  (per `docs/CODING_STANDARDS.md`) with unit tests; QA loads the feature.
- **Honesty of heuristics:** sentiment and life-data are rule-based approximations, surfaced as such,
  with a confidence flag where feasible.

## 7. Out of scope / future
- **AI / LLM enrichment** (summaries, semantic Q&A, smart drafting) - intentionally excluded now to
  honour the no-network rule; possible later as an **opt-in local-LLM** module.
- Live mailbox sync (this consumes the downloader's output, it does not fetch mail).
- Online geographic maps; OCR of scanned attachments; face/photo recognition.
- **Two-way write-back** to Gmail/Outlook contacts (we export an update set; the actual write is manual
  or a future feature).

## 8. Recommended phased delivery
| Phase | Scope | Deliverable |
|-------|-------|-------------|
| **1 - Foundation** | A, B, C, H, I, J (incl. **J5 granular progress**) + **K1-K3 report shell** | Clean, de-duplicated **master contact list** + message index, exported to Excel/CSV/JSON/vCard; re-runnable; **progress UI**; a basic **offline HTML5 report** reading from the `data/` folder. |
| **2 - Relationship, dossier & timeline** | D, G1-G4, **G6 timeline**, E1 topics, K4 | **Type-a-name dossier** with metrics, **relationship-history timeline** and topics, inside the HTML report. |
| **3 - Graph & content** | G3/G5 graph, E2 tone, E3 dates, **E4 reusable content** | Interactive **network graph** tab + tone/dates + **reusable-content library**. |
| **4 - Life data & documents** | F (incl. **F7 useful-document library**) | Spending / subscriptions / travel / accounts sheets + the **Documents & Reusables** tab/library. |

> Build Phase 1 first: it is the substrate every later phase reads from. Do not start the flashy
> graph/life-data work until the clean store + resolved contact identities exist.

## 9. Design notes (non-binding, for implementation)
- **Parsing:** reuse **MimeKit** (`MimeMessage.Load`) already referenced by `EmailDownloader`; add it
  to this feature's `.csproj` with `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`.
- **Excel output:** **ClosedXML** or **EPPlus** (local, no Excel install needed) - a per-feature NuGet dep.
- **Graph rendering (offline options):** Microsoft Automatic Graph Layout (**MSAGL**) WinForms control;
  or render to SVG/HTML in an **offline WebView2**; or a custom GDI+ force-directed view. No tiles/network.
- **Sentiment lexicon:** bundle a VADER/AFINN word-list file as an embedded resource (no download).
- **HTML5 report:** vanilla JS + a small **locally-bundled** library for the graph/timeline (e.g.
  `vis-network` + `vis-timeline`, or `d3`) - **no CDN**. Render inside the app via **WebView2** using
  `SetVirtualHostNameToFolderMapping` so the page can fetch local JSON shards, or start a **localhost
  static server** (`HttpListener` / Kestrel) for the default browser. Shard `data/` by person and by
  sheet to keep loads fast.
- **Progress:** drive the UI from an `IProgress<T>` reporter carrying phase + current item + counts;
  compute the **ETA** from processed/total.

## 10. Proposed manifest (`plugin.json`)
```json
{
  "id": "email-intelligence",
  "title": "Email Intelligence",
  "category": "Insights",
  "description": "Turn your downloaded email archive (.txt/.eml + attachments) into an offline, searchable people-and-life intelligence base with dossiers and a relationship graph. No cloud, no AI.",
  "order": 30,
  "entryAssembly": "DigitalSecretary.Features.EmailIntelligence.dll",
  "entryType": "DigitalSecretary.Features.EmailIntelligence.EmailIntelligenceModule"
}
```

## 11. Traceability (to be completed during implementation)
- **Code:** `src/Features/EmailIntelligence/` - `MimeParser` / `TxtParser`, `Deduplicator`,
  `IdentityResolver`, `RelationshipMetrics`, `Extractors/*` (signature, life-data, topics, sentiment),
  `GraphBuilder`, `TimelineBuilder`, `DocumentLibrary`, `ExcelWriter` / `JsonWriter` / `VCardWriter` /
  `GraphExporter`, `HtmlReportGenerator`, `ProgressReporter`, `EmailIntelligenceModule` + `Control`(s).
- **Tests:** unit tests per logic class in `tests/DigitalSecretary.UnitTests`; QA loads + views the feature.
- **Data:** settings under `%APPDATA%\DigitalSecretary\data\email-intelligence\`; results under the
  user-chosen output folder.
- **Companion docs to add when built:** `docs/user-guide/features/email-intelligence.md`; rows in
  `DigitalSecretary-Requirements.xlsx` + the traceability matrix; `FEATURE.md`; this `plugin.json`;
  feature-index row in `REQUIREMENTS.md` (new **Insights** category).
