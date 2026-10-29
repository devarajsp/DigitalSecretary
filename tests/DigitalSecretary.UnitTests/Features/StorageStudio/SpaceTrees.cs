using DigitalSecretary.Features.StorageStudio;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Builds small in-memory space trees and temporary folders for the Storage Studio tests.</summary>
internal static class SpaceTrees
{
    /// <summary>A fixed "now" so age-based tests never depend on the clock.</summary>
    public static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A placeholder root path - never a real user folder.</summary>
    public const string RootPath = @"C:\Users\You\Data";

    public static DateTime YearsAgo(double years) => Now.AddDays(-365 * years);

    /// <summary>
    /// A small tree:
    /// <code>
    /// Data\
    ///   Videos\holiday.mp4   600
    ///   Videos\clip.mov      100
    ///   Photos\a.jpg         200
    ///   Photos\Old\b.jpg      50   (6 years old)
    ///   notes.txt             50   (2 years old)
    /// </code>
    /// Total 1000 bytes.
    /// </summary>
    public static SpaceNode Sample()
    {
        var root = SpaceNode.CreateRoot(RootPath);
        var videos = root.AddFolder("Videos");
        videos.AddFile("holiday.mp4", 600, YearsAgo(0.1));
        videos.AddFile("clip.mov", 100, YearsAgo(0.5));
        var photos = root.AddFolder("Photos");
        photos.AddFile("a.jpg", 200, YearsAgo(0.2));
        photos.AddFolder("Old").AddFile("b.jpg", 50, YearsAgo(6));
        root.AddFile("notes.txt", 50, YearsAgo(2));
        root.SortBySize();
        return root;
    }

    /// <summary>A fresh, empty temporary folder; delete it with <see cref="Delete"/>.</summary>
    public static string TempFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ds-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Writes a file of <paramref name="bytes"/> bytes at <paramref name="relative"/> under <paramref name="root"/>.</summary>
    public static string WriteFile(string root, string relative, int bytes)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    public static void Delete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort - a leftover temp folder is harmless.
        }
    }
}
