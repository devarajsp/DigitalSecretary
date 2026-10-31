using System.Drawing.Imaging;
using DigitalSecretary.Kit;
using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>Covers <see cref="PictureCache"/> and <see cref="PictureScanner"/> against real temporary folders.</summary>
public sealed class PictureCacheAndScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ds-scan-" + Guid.NewGuid().ToString("N"));
    private readonly string _data = Path.Combine(Path.GetTempPath(), "ds-data-" + Guid.NewGuid().ToString("N"));

    public PictureCacheAndScannerTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_data);
    }

    public void Dispose()
    {
        foreach (var d in new[] { _root, _data })
        {
            try
            {
                Directory.Delete(d, recursive: true);
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }

    private string Picture(string relative, int seed = 11)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bmp = SamplePictureSet.Scene(seed, 120, 90);
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    /// <summary>Reads with GDI+ only (fast, no Shell) and counts the calls.</summary>
    private sealed class CountingReader
    {
        private readonly PictureReader _reader = new(new GdiThumbnailProvider());
        public List<string> Calls { get; } = [];

        public PictureSample? Read(string path)
        {
            lock (Calls)
            {
                Calls.Add(path);
            }
            return _reader.Read(path);
        }
    }

    private PictureScanRequest Request(PictureCache cache, CountingReader reader, PauseToken? pause = null, CancellationToken cancel = default)
        => new() { Root = _root, Cache = cache, Read = reader.Read, ExcludeFolder = ReviewMover.ReviewFolderFor(_root), Cancel = cancel, Pause = pause };

    [Theory]
    [InlineData("a.JPG", true)]
    [InlineData("b.heic", true)]
    [InlineData("c.cr3", true)]
    [InlineData("d.txt", false)]
    [InlineData("e.mp4", false)]
    public void Picture_extensions(string name, bool expected) => PictureScanner.IsPicture(name).Should().Be(expected);

    [Fact]
    public void Collect_finds_pictures_at_every_depth_but_not_in_the_review_folder()
    {
        Picture("a.png");
        Picture(Path.Combine("x", "y", "b.png"));
        Picture(Path.Combine(ReviewMover.ReviewFolderName, "moved.png"));
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "hi");

        var files = PictureScanner.Collect(_root, ReviewMover.ReviewFolderFor(_root), null, default);

        files.Select(f => f.Name).Should().BeEquivalentTo(["a.png", "b.png"]);
    }

    [Fact]
    public void A_scan_fingerprints_every_picture_and_a_rescan_reads_nothing_again()
    {
        Picture("a.png");
        Picture(Path.Combine("sub", "b.png"), 12);
        var cache = new PictureCache();
        var reader = new CountingReader();

        var first = PictureScanner.Run(Request(cache, reader));
        var second = PictureScanner.Run(Request(cache, reader));

        first.Should().Be(new PictureScanResult(2, 2, 0, 0, 0, false));
        second.Should().Be(new PictureScanResult(2, 0, 2, 0, 0, false));
        reader.Calls.Should().HaveCount(2);
        cache.Records.Should().OnlyContain(r => r.PHashes.Length == 8 && r.Width == 120 && r.Height == 90);
    }

    [Fact]
    public void A_changed_picture_is_read_again_and_a_deleted_one_is_pruned()
    {
        var a = Picture("a.png");
        var b = Picture("b.png", 12);
        var cache = new PictureCache();
        var reader = new CountingReader();
        PictureScanner.Run(Request(cache, reader));

        File.SetLastWriteTimeUtc(a, DateTime.UtcNow.AddMinutes(5));
        File.Delete(b);
        var result = PictureScanner.Run(Request(cache, reader));

        result.Read.Should().Be(1);
        result.Removed.Should().Be(1);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void Unreadable_pictures_are_counted_not_cached()
    {
        File.WriteAllText(Path.Combine(_root, "broken.jpg"), "not really");
        var cache = new PictureCache();

        var result = PictureScanner.Run(Request(cache, new CountingReader()));

        result.Unreadable.Should().Be(1);
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void A_scan_cancelled_before_it_starts_prunes_nothing()
    {
        Picture("a.png");
        var cache = new PictureCache();
        PictureScanner.Run(Request(cache, new CountingReader()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = PictureScanner.Run(Request(cache, new CountingReader(), cancel: cts.Token));

        result.Cancelled.Should().BeTrue();
        result.Removed.Should().Be(0);
        cache.Count.Should().Be(1, "a stopped scan has not seen every file, so it must not forget any");
    }

    [Fact]
    public async Task Stop_while_paused_ends_the_scan_as_cancelled()
    {
        Picture("a.png");
        using var pause = new PauseToken();
        using var cts = new CancellationTokenSource();
        pause.Pause();
        var cache = new PictureCache();

        var scan = Task.Run(() => PictureScanner.Run(Request(cache, new CountingReader(), pause, cts.Token)));
        (await Task.WhenAny(scan, Task.Delay(150))).Should().NotBeSameAs(scan);
        cts.Cancel();

        (await Task.WhenAny(scan, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(scan);
        (await scan).Cancelled.Should().BeTrue();
    }

    [Fact]
    public void Autosave_runs_while_scanning()
    {
        for (var i = 0; i < 5; i++)
        {
            Picture("p" + i + ".png", 11 + i);
        }
        var saves = 0;
        var request = new PictureScanRequest
        {
            Root = _root, Cache = new PictureCache(), Read = new CountingReader().Read, AutoSaveEvery = 2, Parallelism = 1,
            AutoSave = _ => saves++
        };
        PictureScanner.Run(request);
        saves.Should().Be(2);
    }

    [Fact]
    public void The_cache_round_trips_compressed_and_survives_corruption()
    {
        Picture("a.png");
        var cache = new PictureCache();
        PictureScanner.Run(Request(cache, new CountingReader()));
        var file = PictureCache.PathFor(_data, _root);

        cache.Save(file);
        var loaded = PictureCache.Load(file);

        loaded.Count.Should().Be(1);
        loaded.Records.Single().Should().BeEquivalentTo(cache.Records.Single());
        File.ReadAllBytes(file).Take(2).Should().Equal(new byte[] { 0x1F, 0x8B });

        File.WriteAllText(file, "garbage");
        PictureCache.Load(file).Count.Should().Be(0);
        PictureCache.Load(Path.Combine(_data, "missing.json.gz")).Count.Should().Be(0);
    }

    [Fact]
    public void Cache_file_names_are_per_folder_and_contain_no_path_text()
    {
        var a = PictureCache.PathFor(_data, @"C:\Users\You\Pictures");
        a.Should().Be(PictureCache.PathFor(_data, @"c:\users\you\pictures\"));
        a.Should().NotBe(PictureCache.PathFor(_data, @"C:\Users\You\Other"));
        Path.GetFileName(a).Should().NotContain("Pictures");
    }

    [Fact]
    public void Find_valid_needs_the_same_size_and_time()
    {
        var cache = new PictureCache();
        var t = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        cache.Put(new PictureRecord { Path = "p.jpg", Size = 10, ModifiedUtc = t, PHashes = new ulong[8], DHashes = new ulong[8] });

        cache.FindValid("P.JPG", 10, t).Should().NotBeNull();
        cache.FindValid("p.jpg", 11, t).Should().BeNull();
        cache.FindValid("p.jpg", 10, t.AddSeconds(1)).Should().BeNull();
        cache.Remove("p.jpg").Should().BeTrue();
    }
}
