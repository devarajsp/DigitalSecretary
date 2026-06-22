# Feature: Email Intelligence (`email-intelligence`)

Turns a folder of downloaded emails (`.eml` / `.txt` + attachments, e.g. from the Download Emails /
Download Gmail features) into an offline people-and-life intelligence base: a de-duplicated message
set, a resolved master contact list, relationship metrics, topics, a network graph, per-person
timelines, and a self-contained **HTML5 report**. **100% local — no network calls, no AI, no database.**

## How it works (pipeline)
`IntelligencePipeline.Analyze` runs the stages, reporting phased `AnalysisProgress`:

1. **Scan** — `ArchiveScanner` finds `.eml`/`.txt`; a `.eml` is preferred over its `.txt` sibling.
2. **Parse** — `EmailParser` reads `.eml` via **MimeKit** (MIME, encodings, attachments) and the
   readable `.txt` form; `MailText` handles HTML-to-text, normalization and hashing.
3. **Classify** — `MessageClassifier` labels Personal / Newsletter / Transactional / Notification.
4. **De-duplicate** — `Deduplicator` collapses duplicates by `Message-Id` or a content hash, and
   computes a thread key.
5. **Resolve people** — `ContactExtractor` finds the owner; `IdentityResolver` builds `Person`
   records and merges addresses that share a full name; `SignatureExtractor` pulls phones/links.
6. **Score** — `RelationshipAnalyzer` computes counts, first/last contact, a strength score and a
   dormant flag.
7. **Enrich** — `TopicExtractor` (keywords), `ToneAnalyzer` + `SentimentLexicon` (offline tone),
   `NetworkGraphBuilder` (co-occurrence edges), `TimelineBuilder` (per-person history),
   `LifeDataExtractor` (purchases/subscriptions/travel/accounts) and `DocumentLibrary` (deduped
   attachments).
8. **Export** — `JsonExporter`, `CsvExporter`, `VCardExporter`, `GraphMlExporter`,
   `WorkbookExporter` + `XlsxWriter` (a dependency-free native `.xlsx`), and `HtmlReportGenerator`
   (the `index.html` + `assets/` + `data/data.js` report).

## Design notes
- **Logic is in plain classes** (above); `EmailIntelligenceControl` only wires the UI and reports
  progress. `EmailIntelligenceModule` is the plugin entry point.
- **Offline report**: data is emitted to `data/data.js` as `window.__DATA__ = {...}` and loaded by a
  `<script>` tag, so the single HTML page opens directly from `file://` with no server and no network.
- **No spreadsheet dependency**: tabular output is CSV (opens in Excel) to keep the plugin portable;
  native multi-sheet `.xlsx` (ClosedXML) is a possible later addition.
- **Settings** (`EmailIntelligenceSettingsStore`) persist only the input/output folders and the
  optional owner address under `IFeatureContext.DataDirectory`. No secrets are stored.

## Dependencies
- `MimeKit` (NuGet) — shipped into the plugin folder via `CopyLocalLockFileAssemblies`.

## Tests
`tests/DigitalSecretary.UnitTests/Features/EmailIntelligence/*` covers each logic class; QA
automation loads the plugin and builds its view.
