using System.Globalization;
using System.Text;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// Reads and writes <c>dedupe_md5_list.txt</c> - the checksum list that lives in the scanned root
/// folder. The format is deliberately plain text so it can be read, diffed or kept by hand:
/// <code>
/// # dedupe_md5_list.txt - MD5 checksum list written by Digital Secretary (DeDupe Files).
/// # Columns: MD5 TAB SizeBytes TAB ModifiedUtc(ISO 8601) TAB FullPath
/// 900150983cd24fb0d6963f7d28e17f72  3  2026-01-02T03:04:05.0000000Z  C:\Pictures\a.jpg
/// </code>
/// Parsing is tolerant: blank lines and <c>#</c> comments are ignored, a line whose checksum is not
/// 32 hex characters is skipped, and a legacy two-column "checksum TAB path" line is still accepted
/// (it simply carries no size/stamp, so the file gets re-hashed on the next scan).
/// </summary>
public static class ChecksumListFile
{
    /// <summary>The fixed name of the list file, written in the root of the scanned folder.</summary>
    public const string FileName = "dedupe_md5_list.txt";

    private const char Separator = '\t';

    /// <summary>The full path the list takes inside <paramref name="root"/>.</summary>
    public static string PathIn(string root) => Path.Combine(root, FileName);

    /// <summary>
    /// True for the list file itself and for the temporary file used while rewriting it, so a scan
    /// never fingerprints its own bookkeeping.
    /// </summary>
    public static bool IsListFile(string path)
        => Path.GetFileName(path).StartsWith(FileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Renders one entry as a list line.</summary>
    public static string FormatLine(ChecksumEntry entry)
        => string.Join(
            Separator,
            entry.Md5,
            entry.Length.ToString(CultureInfo.InvariantCulture),
            entry.ModifiedUtc.ToString("O", CultureInfo.InvariantCulture),
            entry.FullPath);

    /// <summary>
    /// Parses one list line, or returns <c>null</c> for a blank line, a comment, or anything that is
    /// not a usable entry.
    /// </summary>
    public static ChecksumEntry? ParseLine(string? line)
    {
        if (line is null)
        {
            return null;
        }

        var text = line.TrimEnd('\r', '\n');
        if (text.Trim().Length == 0 || text.TrimStart().StartsWith('#'))
        {
            return null;
        }

        var parts = text.Split(Separator);
        string path;
        var length = -1L;
        var modified = default(DateTime);

        if (parts.Length >= 4)
        {
            length = ParseLength(parts[1]);
            modified = ParseModified(parts[2]);
            path = string.Join(Separator, parts[3..]);
        }
        else if (parts.Length == 2)
        {
            path = parts[1];
        }
        else
        {
            return null;
        }

        var md5 = HexText.NormalizeMd5(parts[0].Trim());
        path = path.Trim();
        return md5 is null || path.Length == 0 ? null : new ChecksumEntry(md5, length, modified, path);
    }

    /// <summary>
    /// Loads every usable entry from the list file. A missing file is not an error - it just means
    /// nothing has been scanned yet.
    /// </summary>
    public static IReadOnlyList<ChecksumEntry> Read(string file)
    {
        var entries = new List<ChecksumEntry>();
        if (!File.Exists(file))
        {
            return entries;
        }

        foreach (var line in File.ReadLines(file))
        {
            var entry = ParseLine(line);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }
        return entries;
    }

    /// <summary>
    /// Writes the list, sorted by path. The file is built next to its destination and then moved
    /// into place, so an interrupted write can never leave a half-written list behind.
    /// </summary>
    public static void Write(string file, string root, IEnumerable<ChecksumEntry> entries)
    {
        var ordered = entries.OrderBy(e => e.FullPath, StringComparer.OrdinalIgnoreCase).ToList();
        var temp = file + ".tmp";

        using (var writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("# " + FileName + " - MD5 checksum list written by Digital Secretary (DeDupe Files).");
            writer.WriteLine("# Columns: MD5 <TAB> SizeBytes <TAB> ModifiedUtc(ISO 8601) <TAB> FullPath");
            writer.WriteLine("# Root: " + root);
            writer.WriteLine("# Generated: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                + "  Files: " + ordered.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var entry in ordered)
            {
                writer.WriteLine(FormatLine(entry));
            }
        }

        File.Move(temp, file, overwrite: true);
    }

    private static long ParseLength(string text)
        => long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : -1L;

    private static DateTime ParseModified(string text)
    {
        if (!DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var value))
        {
            return default;
        }
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }
}
