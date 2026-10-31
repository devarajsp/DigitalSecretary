using System.Text.Json;

namespace DigitalSecretary.Kit;

/// <summary>
/// One line of the savings history: a file made smaller (<see cref="Before"/> &gt; <see cref="After"/>) or an
/// undo (written with the sizes the other way round, so it cancels the saving in the totals).
/// </summary>
public sealed record SavingRecord(DateTime WhenUtc, string Path, long Before, long After, string How)
{
    public long Saved => Before - After;
}

/// <summary>Totals over the history.</summary>
public sealed record SavingsTotal(int Files, long Before, long After)
{
    public long Saved => Before - After;
}

/// <summary>
/// The append-only savings history of a Reduce Size feature (<c>history.jsonl</c> in its data folder, one
/// JSON object per line). Appending never rewrites earlier lines, so a crash loses at most the line being
/// written; unreadable lines are skipped when loading.
/// </summary>
public sealed class SavingsLedger(string file)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public string File => file;

    public void Append(SavingRecord record)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        System.IO.File.AppendAllText(file, JsonSerializer.Serialize(record, JsonOpts) + Environment.NewLine);
    }

    public IReadOnlyList<SavingRecord> Load()
    {
        var records = new List<SavingRecord>();
        try
        {
            if (!System.IO.File.Exists(file))
            {
                return records;
            }
            foreach (var line in System.IO.File.ReadLines(file).Where(l => l.Length > 0))
            {
                try
                {
                    if (JsonSerializer.Deserialize<SavingRecord>(line, JsonOpts) is { } r)
                    {
                        records.Add(r);
                    }
                }
                catch (JsonException)
                {
                    // A torn last line from a crash - skip it.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // History is informational; an unreadable file reads as empty.
        }
        return records;
    }

    /// <summary>Files reduced (undos subtract one) and the bytes before / after.</summary>
    public static SavingsTotal Total(IEnumerable<SavingRecord> records)
    {
        int files = 0;
        long before = 0, after = 0;
        foreach (var r in records)
        {
            files += r.Saved >= 0 ? 1 : -1;
            before += r.Before;
            after += r.After;
        }
        return new SavingsTotal(Math.Max(0, files), before, after);
    }
}
