using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="SpaceScanner"/> against real temporary folders.</summary>
public sealed class SpaceScannerTests : IDisposable
{
    private readonly string _root = SpaceTrees.TempFolder();

    public void Dispose() => SpaceTrees.Delete(_root);

    private sealed class Recorder<T> : IProgress<T>
    {
        public List<T> Items { get; } = [];

        public void Report(T value) => Items.Add(value);
    }

    [Fact]
    public void Builds_a_tree_with_sizes_and_counts_at_every_depth()
    {
        SpaceTrees.WriteFile(_root, "a.txt", 10);
        SpaceTrees.WriteFile(_root, Path.Combine("Videos", "v.mp4"), 300);
        SpaceTrees.WriteFile(_root, Path.Combine("Videos", "Deep", "Deeper", "w.mkv"), 200);
        Directory.CreateDirectory(Path.Combine(_root, "Empty"));

        var result = SpaceScanner.Run(new SpaceScanRequest { Root = _root });

        result.Cancelled.Should().BeFalse();
        result.Errors.Should().Be(0);
        result.Root.Size.Should().Be(510);
        result.Root.FileCount.Should().Be(3);
        result.Root.FolderCount.Should().Be(4);
        result.Root.Children[0].Name.Should().Be("Videos", "children are sorted largest first");
        result.Root.Children[0].Size.Should().Be(500);
        result.Root.CategorySizes[(int)FileCategory.Video].Should().Be(500);
    }

    [Fact]
    public void Records_each_files_modified_time()
    {
        var path = SpaceTrees.WriteFile(_root, "dated.txt", 1);
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);

        var result = SpaceScanner.Run(new SpaceScanRequest { Root = _root });

        result.Root.Children.Single().LastWriteUtc.Should().Be(stamp);
    }

    [Fact]
    public void Reports_progress_during_and_at_the_end()
    {
        for (var i = 0; i < 7; i++)
        {
            SpaceTrees.WriteFile(_root, "f" + i + ".bin", 1);
        }
        var progress = new Recorder<SpaceScanProgress>();

        SpaceScanner.Run(new SpaceScanRequest { Root = _root, Progress = progress, ProgressEvery = 3 });

        progress.Items.Should().HaveCount(3);   // after files 3 and 6, then the final report
        progress.Items[^1].Files.Should().Be(7);
        progress.Items[^1].Bytes.Should().Be(7);
    }

    [Fact]
    public void A_scan_cancelled_before_it_starts_returns_an_empty_cancelled_tree()
    {
        SpaceTrees.WriteFile(_root, "a.txt", 10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = SpaceScanner.Run(new SpaceScanRequest { Root = _root, Cancel = cts.Token });

        result.Cancelled.Should().BeTrue();
        result.Root.FileCount.Should().Be(0);
    }

    [Fact]
    public async Task Stop_while_paused_ends_the_scan_as_cancelled()
    {
        SpaceTrees.WriteFile(_root, "a.txt", 10);
        using var pause = new PauseToken();
        using var cts = new CancellationTokenSource();
        pause.Pause();

        var scan = Task.Run(() => SpaceScanner.Run(new SpaceScanRequest { Root = _root, Pause = pause, Cancel = cts.Token }));
        (await Task.WhenAny(scan, Task.Delay(150))).Should().NotBeSameAs(scan, "a paused scan waits");
        cts.Cancel();

        (await Task.WhenAny(scan, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(scan);
        (await scan).Cancelled.Should().BeTrue();
    }

    [Fact]
    public void A_missing_folder_is_reported_as_an_error_not_a_crash()
    {
        var log = new Recorder<string>();
        var result = SpaceScanner.Run(new SpaceScanRequest { Root = Path.Combine(_root, "nope"), Log = log });

        result.Errors.Should().Be(1);
        result.Root.Size.Should().Be(0);
        log.Items.Should().ContainSingle().Which.Should().StartWith("Folder vanished:");
    }

    [Fact]
    public void Junctions_are_not_followed()
    {
        SpaceTrees.WriteFile(_root, Path.Combine("real", "data.bin"), 100);
        var link = Path.Combine(_root, "link");
        try
        {
            Directory.CreateSymbolicLink(link, Path.Combine(_root, "real"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return; // Creating links needs Developer Mode or admin; nothing to assert without one.
        }

        var result = SpaceScanner.Run(new SpaceScanRequest { Root = _root });

        result.Root.Size.Should().Be(100, "the linked folder must not be counted twice");
        result.SkippedLinks.Should().Be(1);
    }
}
