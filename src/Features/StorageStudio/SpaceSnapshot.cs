using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// A compact record of one completed scan: the root's totals and the size of every folder down to
/// <see cref="SpaceSnapshot.MaxDepth"/> levels, keyed by path relative to the root. It is what lets
/// the next scan answer "what grew since last time" without keeping the whole tree.
/// </summary>
public sealed class SpaceSnapshot
{
    /// <summary>Folders deeper than this are folded into their ancestors - enough to point at a culprit.</summary>
    public const int MaxDepth = 4;

    public string RootPath { get; init; } = "";
    public DateTime TakenUtc { get; init; }
    public long TotalBytes { get; init; }
    public int TotalFiles { get; init; }

    /// <summary>Folder size by path relative to the root ("" is not stored; see <see cref="TotalBytes"/>).</summary>
    public Dictionary<string, long> Folders { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Captures <paramref name="root"/> as a snapshot taken at <paramref name="takenUtc"/>.</summary>
    public static SpaceSnapshot From(SpaceNode root, DateTime takenUtc)
    {
        var folders = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<SpaceNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            foreach (var child in stack.Pop().Children.Where(c => c.IsFolder))
            {
                folders[child.RelativeTo(root)] = child.Size;
                if (child.Depth < MaxDepth)
                {
                    stack.Push(child);
                }
            }
        }
        return new SpaceSnapshot
        {
            RootPath = root.Name,
            TakenUtc = takenUtc,
            TotalBytes = root.Size,
            TotalFiles = root.FileCount,
            Folders = folders
        };
    }
}

/// <summary>One folder whose size changed between two snapshots.</summary>
public sealed record GrowthEntry(string RelativePath, long OldBytes, long NewBytes)
{
    public long Delta => NewBytes - OldBytes;
}

/// <summary>Pure comparison of two snapshots of the same root.</summary>
public static class GrowthComparer
{
    /// <summary>
    /// Folders that changed by at least <paramref name="minDelta"/> bytes (grew or shrank, including
    /// new and removed folders), biggest change first, at most <paramref name="count"/>.
    /// </summary>
    public static IReadOnlyList<GrowthEntry> Compare(SpaceSnapshot before, SpaceSnapshot after, long minDelta, int count)
    {
        var keys = before.Folders.Keys.Union(after.Folders.Keys, StringComparer.OrdinalIgnoreCase);
        return [.. keys
            .Select(k => new GrowthEntry(k, before.Folders.GetValueOrDefault(k), after.Folders.GetValueOrDefault(k)))
            .Where(e => Math.Abs(e.Delta) >= Math.Max(1, minDelta))
            .OrderByDescending(e => Math.Abs(e.Delta))
            .ThenBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, count))];
    }
}

/// <summary>
/// Keeps the latest snapshot per root under the feature's data folder, gzip-compressed at rest
/// (one small file per root, named by a hash of the root path so no path text appears in names).
/// </summary>
public sealed class SnapshotStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly string _folder;

    public SnapshotStore(string dataDirectory) => _folder = Path.Combine(dataDirectory, "snapshots");

    /// <summary>The file a root's snapshot lives in.</summary>
    public string PathFor(string rootPath)
    {
        var key = Path.GetFullPath(rootPath).TrimEnd('\\', '/').ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        return Path.Combine(_folder, hash.ToLowerInvariant() + ".json.gz");
    }

    /// <summary>The saved snapshot for <paramref name="rootPath"/>, or null if none / unreadable.</summary>
    public SpaceSnapshot? Load(string rootPath)
    {
        var file = PathFor(rootPath);
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            using var stream = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
            return JsonSerializer.Deserialize<SpaceSnapshot>(stream, JsonOpts);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Replaces the saved snapshot for its root.</summary>
    public void Save(SpaceSnapshot snapshot)
    {
        Directory.CreateDirectory(_folder);
        var file = PathFor(snapshot.RootPath);
        var temp = file + ".tmp";
        using (var stream = new GZipStream(File.Create(temp), CompressionLevel.SmallestSize))
        {
            JsonSerializer.Serialize(stream, snapshot, JsonOpts);
        }
        File.Move(temp, file, overwrite: true);
    }
}
