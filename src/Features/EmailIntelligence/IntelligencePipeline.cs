namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Orchestrates the whole offline analysis: scan -> parse -> classify -> de-duplicate -> resolve
/// people -> enrich (signatures) -> score relationships -> topics -> graph -> timelines, then writes
/// the portable outputs (JSON/CSV/vCard/GraphML) and the self-contained HTML report. Reports phased
/// progress via <see cref="AnalysisProgress"/> and honours cancellation. Nothing leaves the machine.
/// </summary>
public sealed class IntelligencePipeline
{
    private static readonly HashSet<string> FreeMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "yahoo.com", "yahoo.co.in", "ymail.com", "rocketmail.com",
        "outlook.com", "hotmail.com", "live.com", "msn.com", "aol.com", "icloud.com", "me.com",
        "protonmail.com", "proton.me", "rediffmail.com", "zoho.com",
    };
    private static readonly HashSet<string> DomainTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "co", "com", "org", "net", "gov", "edu", "ac", "in", "uk", "io",
    };

    private readonly ArchiveScanner _scanner = new();
    private readonly EmailParser _parser = new();
    private readonly MessageClassifier _classifier = new();
    private readonly Deduplicator _deduplicator = new();
    private readonly ContactExtractor _contacts = new();
    private readonly IdentityResolver _identity = new();
    private readonly SignatureExtractor _signatures = new();
    private readonly RelationshipAnalyzer _relationships = new();
    private readonly TopicExtractor _topics = new();
    private readonly NetworkGraphBuilder _graph = new();
    private readonly TimelineBuilder _timelines = new();
    private readonly ToneAnalyzer _tone = new();
    private readonly LifeDataExtractor _lifeData = new();
    private readonly DocumentLibrary _documents = new();

    /// <summary>Runs the analysis on the input folder and returns the in-memory report (no files written).</summary>
    public IntelligenceReport Analyze(
        EmailIntelligenceOptions options,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        var parsed = ScanAndParse(options.InputDir, progress, ct);
        return AnalyzeCore(parsed, options, progress, ct).Report;
    }

    /// <summary>Scans the folder and parses + classifies every email file.</summary>
    private List<ParsedEmail> ScanAndParse(
        string inputDir,
        IProgress<AnalysisProgress>? progress,
        CancellationToken ct)
    {
        Report(progress, "Scanning", 2, "Finding .eml / .txt files...");
        var files = _scanner.Scan(inputDir);

        var parsed = new List<ParsedEmail>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var email = _parser.ParseFile(files[i]);
            if (email is not null)
            {
                email.Category = _classifier.Classify(email);
                parsed.Add(email);
            }
            if (files.Count > 0 && (i % 50 == 0 || i == files.Count - 1))
                Report(progress, "Parsing", 2 + (int)(36.0 * (i + 1) / files.Count), $"Parsed {i + 1}/{files.Count} files");
        }
        return parsed;
    }

    /// <summary>De-duplicates the (possibly merged) message set, then derives all the intelligence.</summary>
    private (IntelligenceReport Report, IReadOnlyList<ParsedEmail> Unique) AnalyzeCore(
        IReadOnlyList<ParsedEmail> rawEmails,
        EmailIntelligenceOptions options,
        IProgress<AnalysisProgress>? progress,
        CancellationToken ct)
    {
        var now = options.Now ?? DateTimeOffset.Now;

        Report(progress, "De-duplicating", 42, "Removing duplicate messages...");
        ct.ThrowIfCancellationRequested();
        var deduped = _deduplicator.Deduplicate(rawEmails);
        var emails = deduped.Unique;

        Report(progress, "Contacts", 52, "Resolving people & identities...");
        var owner = string.IsNullOrWhiteSpace(options.OwnerAddress)
            ? _contacts.DetectOwner(emails)
            : options.OwnerAddress.Trim().ToLowerInvariant();
        var people = _identity.Resolve(emails, owner).ToList();
        ApplySignatures(people, emails, owner);

        Report(progress, "Relationships", 66, "Scoring relationships...");
        _relationships.Analyze(people, emails, owner, now, options.DormantMonths <= 0 ? 12 : options.DormantMonths);

        Report(progress, "Topics & tone", 78, "Extracting topics and tone...");
        _topics.AssignTopics(people, emails, owner);
        _tone.Analyze(people, emails, owner);

        Report(progress, "Graph & timeline", 88, "Building graph & timelines...");
        var edges = _graph.Build(people, emails, owner);
        var timelines = _timelines.Build(people, emails, owner);

        var report = new IntelligenceReport
        {
            OwnerAddress = owner,
            MessageCount = emails.Count,
            DuplicatesRemoved = deduped.DuplicatesRemoved,
            AttachmentCount = emails.Sum(e => e.Attachments.Count),
        };
        report.People.AddRange(people
            .OrderByDescending(p => p.StrengthScore)
            .ThenByDescending(p => p.TotalMessages));
        report.Edges.AddRange(edges);
        foreach (var kv in timelines)
            report.Timelines[kv.Key] = kv.Value;
        report.TopTopics.AddRange(_topics.GlobalTopics(emails));
        report.LifeData.AddRange(_lifeData.Extract(emails));
        report.Documents.AddRange(_documents.Build(emails));

        Report(progress, "Done", 100, $"{report.People.Count} people, {report.MessageCount} messages.");
        return (report, emails);
    }

    /// <summary>
    /// Runs the analysis and writes every output; returns the output folder path. In
    /// <see cref="AnalysisMode.Append"/> mode the previously-analysed messages stored in the output
    /// folder are merged with the new archive and re-de-duplicated, so multiple archives consolidate
    /// into one de-duplicated dataset. In <see cref="AnalysisMode.Overwrite"/> mode only the current
    /// input is used.
    /// </summary>
    public string RunAndExport(
        EmailIntelligenceOptions options,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        var outDir = string.IsNullOrWhiteSpace(options.OutputDir)
            ? Path.Combine(options.InputDir, "_EmailIntelligence")
            : options.OutputDir;
        var storePath = Path.Combine(outDir, "data", "messages.json");
        var store = new MessageStore();

        var parsed = ScanAndParse(options.InputDir, progress, ct);

        IReadOnlyList<ParsedEmail> combined = parsed;
        if (options.Mode == AnalysisMode.Append)
        {
            var prior = store.Load(storePath);
            if (prior.Count > 0)
            {
                Report(progress, "Merging", 40, $"Merging {prior.Count} previously-analysed message(s)...");
                combined = prior.Concat(parsed).ToList();
            }
        }

        var result = AnalyzeCore(combined, options, progress, ct);
        WriteOutputs(result.Report, outDir);
        store.Save(result.Unique, storePath);
        return outDir;
    }

    /// <summary>Writes the portable files + HTML report to <paramref name="outDir"/> (no database).</summary>
    public void WriteOutputs(IntelligenceReport report, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var dataDir = Path.Combine(outDir, "data");
        Directory.CreateDirectory(dataDir);

        var csv = new CsvExporter();
        new JsonExporter().Write(report, Path.Combine(dataDir, "report.json"));
        csv.WriteContacts(report, Path.Combine(outDir, "Contacts.csv"));
        csv.WriteLifeData(report, Path.Combine(outDir, "LifeData.csv"));
        csv.WriteDocuments(report, Path.Combine(outDir, "Documents.csv"));
        new VCardExporter().Write(report, Path.Combine(outDir, "Contacts.vcf"));
        new GraphMlExporter().Write(report, Path.Combine(outDir, "network.graphml"));
        new HtmlReportGenerator().Write(report, outDir);
    }

    private void ApplySignatures(IReadOnlyList<Person> people, IReadOnlyList<ParsedEmail> emails, string owner)
    {
        var index = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in people)
            foreach (var address in person.Addresses)
                index[address] = person;

        foreach (var email in emails)
        {
            if (email.From is null || email.From.Address.Equals(owner, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!index.TryGetValue(email.From.Address, out var person))
                continue;

            var signature = _signatures.Extract(email.Body);
            foreach (var phone in signature.Phones)
                if (person.Phones.Count < 5 && !person.Phones.Contains(phone))
                    person.Phones.Add(phone);
            foreach (var url in signature.Urls)
                if (person.Urls.Count < 10 && !person.Urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                    person.Urls.Add(url);
        }

        foreach (var person in people)
            person.Organization ??= OrganizationFor(person.Id);
    }

    /// <summary>Derives an organization name from a non-free-mail email domain (heuristic).</summary>
    public static string? OrganizationFor(string address)
    {
        var at = address.LastIndexOf('@');
        if (at < 0 || at == address.Length - 1)
            return null;
        var domain = address[(at + 1)..];
        if (FreeMailDomains.Contains(domain))
            return null;

        var parts = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        // Walk in from the TLD past country/second-level suffixes to the registrable label.
        var idx = parts.Length - 2;
        while (idx > 0 && DomainTlds.Contains(parts[idx]))
            idx--;
        var label = parts[idx];
        return label.Length == 0 ? null : char.ToUpperInvariant(label[0]) + label[1..];
    }

    private static void Report(IProgress<AnalysisProgress>? progress, string phase, int percent, string detail) =>
        progress?.Report(new AnalysisProgress(phase, percent, detail));
}
