namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// A realistic but entirely made-up space tree under a placeholder path. It lets the documentation
/// screenshots (tools/DocShots, via <see cref="StorageStudioControl.ShowTree"/>) show a populated
/// map without scanning a real folder - so no real user name, path or file ever appears in an image.
/// Deterministic: the same tree every time, dated relative to the <paramref name="nowUtc"/> given.
/// </summary>
public static class SampleSpaceTree
{
    /// <summary>The placeholder root shown in screenshots.</summary>
    public const string RootPath = @"C:\Users\You\Documents";

    private const long MB = 1L << 20;
    private const long GB = 1L << 30;

    public static SpaceNode Build(DateTime nowUtc)
    {
        DateTime Ago(double years) => nowUtc.AddDays(-365 * years);

        var root = SpaceNode.CreateRoot(RootPath);
        var videos = root.AddFolder("Videos");
        videos.AddFile("Wedding 2019.mp4", 9 * GB / 2, Ago(6));
        videos.AddFile("Trip to the hills.mov", 2 * GB, Ago(2));
        var phone = videos.AddFolder("Phone clips");
        for (var i = 1; i <= 14; i++)
        {
            phone.AddFile("VID_" + i.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".mp4", (90 + (i * 23)) * MB, Ago(0.3 + (i * 0.1)));
        }

        var photos = root.AddFolder("Photos");
        foreach (var year in new[] { 2021, 2022, 2023, 2024, 2025 })
        {
            var folder = photos.AddFolder(year.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (var i = 1; i <= 12; i++)
            {
                folder.AddFile("IMG_" + year + "_" + i + ".jpg", (30 + ((year - 2020) * 9) + i) * MB, Ago(2026 - year));
            }
        }

        var downloads = root.AddFolder("Downloads");
        downloads.AddFile("setup-office.exe", 3 * GB / 2, Ago(1.5));
        downloads.AddFile("ubuntu-24.04.iso", 6 * GB, Ago(1.2));
        downloads.AddFile("movie.mkv.crdownload", 700 * MB, Ago(0.4));
        downloads.AddFile("statements.zip", 420 * MB, Ago(0.8));

        var projects = root.AddFolder("Projects");
        var web = projects.AddFolder("website");
        web.AddFolder("node_modules").AddFile("bundle-cache.bin", 1800 * MB, Ago(0.2));
        web.AddFile("index.html", MB / 4, Ago(0.2));
        var app = projects.AddFolder("app");
        app.AddFolder("obj").AddFile("app.pdb", 300 * MB, Ago(0.1));
        app.AddFolder("bin").AddFile("app.dll", 450 * MB, Ago(0.1));

        var docs = root.AddFolder("Papers");
        docs.AddFile("Tax returns.pdf", 80 * MB, Ago(3.5));
        docs.AddFile("Mail archive.pst", 5 * GB / 2, Ago(4));
        var music = root.AddFolder("Music");
        music.AddFile("Playlist backup.flac", 1200 * MB, Ago(5.5));
        root.AddFile("old-laptop-backup.7z", 3 * GB, Ago(3.2));

        root.SortBySize();
        return root;
    }
}
