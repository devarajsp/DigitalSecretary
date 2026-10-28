using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>
/// Covers <see cref="ScanEngine"/> with a fake hasher, so the incremental logic (reuse, re-hash,
/// prune, cancel) is asserted without depending on real MD5 timings.
/// </summary>
public sealed class ScanEngineTests
{
    /// <summary>A hasher that hands back a canned answer per path and records what it was asked for.</summary>
    private sealed class FakeHasher : IFileHasher
    {
        private readonly Func<string, string?> _answer;

        public FakeHasher(Func<string, string?>? answer = null)
            => _answer = answer ?? (path => HashFor(path));

        public List<string> Calls { get; } = [];

        public string? Compute(string path, CancellationToken cancel)
        {
            Calls.Add(path);
            return _answer(path);
        }
    }

    private sealed class Recorder<T> : IProgress<T>
    {
        public List<T> Items { get; } = [];

        public void Report(T value) => Items.Add(value);
    }

    /// <summary>A stable, valid-looking 32-character checksum derived from the file name.</summary>
    private static string HashFor(string path)
    {
        var seed = 0;
        foreach (var c in Path.GetFileName(path))
        {
            seed = unchecked((seed * 31) + c) & 0xFFFFFFF;
        }
        var hex = seed.ToString("x7", System.Globalization.CultureInfo.InvariantCulture);
        return hex + new string('a', 32 - hex.Length);
    }

    private static string Write(string dir, string relative, string content)
    {
        var path = Path.Combine(dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static ScanRequest Request(
        string root, ChecksumIndex index, bool rehash = false, CancellationToken cancel = default)
        => new() { Root = root, Index = index, Cancel = cancel, Rehash = rehash };

    [Fact]
    public void Run_finds_every_file_across_nested_subfolders()
    {
        using var dir = new TempDir();
        Write(dir.Path, "top.txt", "one");
        Write(dir.Path, @"sub\middle.txt", "two");
        Write(dir.Path, @"sub\deeper\bottom.txt", "three");
        var index = new ChecksumIndex();
        var hasher = new FakeHasher();

        var result = new ScanEngine(hasher).Run(Request(dir.Path, index));

        result.FilesSeen.Should().Be(3);
        result.Hashed.Should().Be(3);
        result.Reused.Should().Be(0);
        result.Cancelled.Should().BeFalse();
        index.Count.Should().Be(3);
        hasher.Calls.Should().HaveCount(3);
    }

    [Fact]
    public void Run_never_hashes_the_checksum_list_itself()
    {
        using var dir = new TempDir();
        Write(dir.Path, "top.txt", "one");
        Write(dir.Path, ChecksumListFile.FileName, "# a list");
        Write(dir.Path, ChecksumListFile.FileName + ".tmp", "# a half-written list");
        var hasher = new FakeHasher();

        var result = new ScanEngine(hasher).Run(Request(dir.Path, new ChecksumIndex()));

        result.FilesSeen.Should().Be(1);
        hasher.Calls.Should().HaveCount(1);
        hasher.Calls.Should().NotContain(ChecksumListFile.PathIn(dir.Path));
    }

    [Fact]
    public void Second_run_over_an_unchanged_tree_hashes_nothing_and_reuses_everything()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        Write(dir.Path, @"sub\b.txt", "two");
        var index = new ChecksumIndex();
        var engine = new ScanEngine(new FakeHasher());
        engine.Run(Request(dir.Path, index));

        var second = new FakeHasher();
        var result = new ScanEngine(second).Run(Request(dir.Path, index));

        result.Reused.Should().Be(2);
        result.Hashed.Should().Be(0);
        result.Removed.Should().Be(0);
        second.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Changing_one_file_re_hashes_only_that_file()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        var changing = Write(dir.Path, "b.txt", "two");
        var index = new ChecksumIndex();
        new ScanEngine(new FakeHasher()).Run(Request(dir.Path, index));

        File.WriteAllText(changing, "two, but longer now");
        var second = new FakeHasher();
        var result = new ScanEngine(second).Run(Request(dir.Path, index));

        result.Hashed.Should().Be(1);
        result.Reused.Should().Be(1);
        second.Calls.Should().Equal(changing);
    }

    [Fact]
    public void Rehash_re_hashes_every_file_even_when_nothing_changed()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        Write(dir.Path, "b.txt", "two");
        var index = new ChecksumIndex();
        new ScanEngine(new FakeHasher()).Run(Request(dir.Path, index));

        var second = new FakeHasher();
        var result = new ScanEngine(second).Run(Request(dir.Path, index, rehash: true));

        result.Hashed.Should().Be(2);
        result.Reused.Should().Be(0);
        second.Calls.Should().HaveCount(2);
    }

    [Fact]
    public void Run_prunes_an_entry_whose_file_no_longer_exists()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        var ghost = Path.Combine(dir.Path, "deleted-last-week.txt");
        var index = ChecksumIndex.FromEntries(new[]
        {
            new ChecksumEntry(HashFor(ghost), 10, DateTime.UtcNow, ghost),
        });

        var result = new ScanEngine(new FakeHasher()).Run(Request(dir.Path, index));

        result.Removed.Should().Be(1);
        index.Find(ghost).Should().BeNull();
        index.Count.Should().Be(1);
    }

    [Fact]
    public void A_cancelled_scan_reports_cancelled_and_prunes_nothing()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        Write(dir.Path, "b.txt", "two");
        Write(dir.Path, "c.txt", "three");
        var ghost = Path.Combine(dir.Path, "deleted-last-week.txt");
        var index = ChecksumIndex.FromEntries(new[]
        {
            new ChecksumEntry(HashFor(ghost), 10, DateTime.UtcNow, ghost),
        });
        using var cancel = new CancellationTokenSource();
        var hasher = new FakeHasher(path =>
        {
            cancel.Cancel();
            return HashFor(path);
        });

        var result = new ScanEngine(hasher).Run(Request(dir.Path, index, cancel: cancel.Token));

        result.Cancelled.Should().BeTrue();
        result.Removed.Should().Be(0);
        index.Find(ghost).Should().NotBeNull("a stopped scan has not seen every file, so it must not prune");
    }

    /// <summary>
    /// Regression: a scan entered with an already-cancelled token used to walk no files at all, so
    /// nothing marked the run as cancelled and the prune step ran against an empty "live" set -
    /// silently throwing away the entire checksum list and forcing a full re-hash next time.
    /// </summary>
    [Fact]
    public void A_scan_cancelled_before_it_starts_prunes_nothing()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        Write(dir.Path, "b.txt", "two");
        var first = Path.Combine(dir.Path, "a.txt");
        var second = Path.Combine(dir.Path, "b.txt");
        var index = ChecksumIndex.FromEntries(new[]
        {
            new ChecksumEntry(HashFor(first), 3, DateTime.UtcNow, first),
            new ChecksumEntry(HashFor(second), 3, DateTime.UtcNow, second),
        });
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        var result = new ScanEngine(new FakeHasher()).Run(Request(dir.Path, index, cancel: cancel.Token));

        result.Cancelled.Should().BeTrue("the token was already cancelled when the scan began");
        result.Removed.Should().Be(0);
        index.Count.Should().Be(2, "a scan that saw no files must not conclude every file is gone");
    }

    [Fact]
    public void Auto_save_is_invoked_once_per_batch_of_files()
    {
        using var dir = new TempDir();
        for (var i = 0; i < 5; i++)
        {
            Write(dir.Path, "f" + i + ".txt", "content " + i);
        }
        var saves = 0;
        var request = new ScanRequest
        {
            Root = dir.Path,
            Index = new ChecksumIndex(),
            AutoSave = _ => saves++,
            AutoSaveEvery = 2,
        };

        new ScanEngine(new FakeHasher()).Run(request);

        saves.Should().Be(2);
    }

    [Fact]
    public void Auto_save_is_not_invoked_when_the_batch_size_is_never_reached()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        var saves = 0;
        var request = new ScanRequest
        {
            Root = dir.Path,
            Index = new ChecksumIndex(),
            AutoSave = _ => saves++,
            AutoSaveEvery = 500,
        };

        new ScanEngine(new FakeHasher()).Run(request);

        saves.Should().Be(0);
    }

    [Fact]
    public void A_hasher_that_returns_null_counts_the_file_as_skipped()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        Write(dir.Path, "b.txt", "two");
        var index = new ChecksumIndex();
        var hasher = new FakeHasher(path => Path.GetFileName(path) == "a.txt" ? null : HashFor(path));

        var result = new ScanEngine(hasher).Run(Request(dir.Path, index));

        result.Skipped.Should().Be(1);
        result.Hashed.Should().Be(1);
        index.Count.Should().Be(1);
    }

    [Fact]
    public void A_hasher_that_throws_counts_an_error_and_the_scan_carries_on()
    {
        using var dir = new TempDir();
        Write(dir.Path, "locked.txt", "one");
        Write(dir.Path, "fine.txt", "two");
        var log = new Recorder<string>();
        var hasher = new FakeHasher(path => Path.GetFileName(path) == "locked.txt"
            ? throw new IOException("file is in use")
            : HashFor(path));
        var request = new ScanRequest { Root = dir.Path, Index = new ChecksumIndex(), Log = log };

        var result = new ScanEngine(hasher).Run(request);

        result.Errors.Should().Be(1);
        result.Hashed.Should().Be(1);
        result.FilesSeen.Should().Be(2);
        log.Items.Should().Contain(line => line.Contains("file is in use", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_reports_progress_and_the_total_size_of_the_index()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "0123456789");
        var progress = new Recorder<ScanProgress>();
        var request = new ScanRequest { Root = dir.Path, Index = new ChecksumIndex(), Progress = progress };

        var result = new ScanEngine(new FakeHasher()).Run(request);

        progress.Items.Should().NotBeEmpty();
        progress.Items[^1].Processed.Should().Be(1);
        progress.Items[^1].Percent.Should().Be(100);
        result.TotalBytes.Should().Be(10);
    }

    [Fact]
    public void Collect_returns_every_file_under_a_folder()
    {
        using var dir = new TempDir();
        var top = Write(dir.Path, "top.txt", "one");
        var nested = Write(dir.Path, @"sub\deeper\bottom.txt", "two");

        var files = ScanEngine.Collect(dir.Path, null, CancellationToken.None);

        files.Select(f => f.FullName).Should().BeEquivalentTo(new[] { top, nested });
    }

    [Fact]
    public void Collect_returns_an_empty_list_for_an_empty_folder()
    {
        using var dir = new TempDir();

        ScanEngine.Collect(dir.Path, null, CancellationToken.None).Should().BeEmpty();
    }

    [Fact]
    public void Run_on_an_empty_folder_reports_nothing_seen()
    {
        using var dir = new TempDir();

        var result = new ScanEngine(new FakeHasher()).Run(Request(dir.Path, new ChecksumIndex()));

        result.FilesSeen.Should().Be(0);
        result.Hashed.Should().Be(0);
        result.Cancelled.Should().BeFalse();
    }

    [Fact]
    public async Task A_scan_waiting_on_a_pause_stops_when_it_is_cancelled_and_prunes_nothing()
    {
        using var dir = new TempDir();
        Write(dir.Path, "a.txt", "one");
        var ghost = Path.Combine(dir.Path, "gone.txt");
        var index = ChecksumIndex.FromEntries(new[]
        {
            new ChecksumEntry(HashFor(ghost), 10, DateTime.UtcNow, ghost),
        });
        using var pause = new PauseToken();
        using var cancel = new CancellationTokenSource();
        using var collected = new ManualResetEventSlim(false);
        var log = new Recorder<string>();
        pause.Pause();
        var request = new ScanRequest
        {
            Root = dir.Path,
            Index = index,
            Pause = pause,
            Cancel = cancel.Token,
            Log = new Relay(line =>
            {
                log.Report(line);
                if (line.StartsWith("Found ", StringComparison.Ordinal))
                {
                    collected.Set();
                }
            }),
        };

        var run = Task.Run(() => new ScanEngine(new FakeHasher()).Run(request));
        collected.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the walk should finish and then pause");
        cancel.Cancel();

        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().BeSameAs(run, "cancelling releases the paused scan");
        var result = await run;

        result.Cancelled.Should().BeTrue();
        result.Removed.Should().Be(0);
        index.Find(ghost).Should().NotBeNull();
        log.Items.Should().NotBeEmpty();
    }

    /// <summary>Passes each reported line to a callback so a test can react to scan progress.</summary>
    private sealed class Relay : IProgress<string>
    {
        private readonly Action<string> _onReport;

        public Relay(Action<string> onReport) => _onReport = onReport;

        public void Report(string value) => _onReport(value);
    }
}
