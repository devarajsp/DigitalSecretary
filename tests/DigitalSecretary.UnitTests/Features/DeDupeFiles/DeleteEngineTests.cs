using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>
/// Covers <see cref="DeleteEngine"/> with a fake deleter over real temp files: the keeper must never
/// be touched, every guard must skip rather than delete, and one failure must not stop the run.
/// </summary>
public sealed class DeleteEngineTests
{
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>Records what it was asked to remove; an optional hook lets a test throw or cancel.</summary>
    private sealed class FakeDeleter : IFileDeleter
    {
        private readonly Action<string>? _onDelete;

        public FakeDeleter(Action<string>? onDelete = null) => _onDelete = onDelete;

        public string Description => "removed by the test deleter";

        public List<string> Deleted { get; } = [];

        public void Delete(string path)
        {
            _onDelete?.Invoke(path);
            Deleted.Add(path);
            File.Delete(path);
        }
    }

    private sealed class Recorder<T> : IProgress<T>
    {
        public List<T> Items { get; } = [];

        public void Report(T value) => Items.Add(value);
    }

    private static DuplicateFile Make(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        var info = new FileInfo(path);
        return new DuplicateFile(info.FullName, info.Length, info.LastWriteTimeUtc);
    }

    private static DuplicateGroup Group(DuplicateFile keeper, params DuplicateFile[] duplicates)
        => new(HashA, keeper.Length, keeper, duplicates);

    private static ChecksumIndex IndexFor(params DuplicateFile[] files)
        => ChecksumIndex.FromEntries(files.Select(f => new ChecksumEntry(HashA, f.Length, f.ModifiedUtc, f.Path)));

    private static DeleteRequest Request(
        IReadOnlyList<DuplicateGroup> groups, bool dryRun, ChecksumIndex? index = null,
        IProgress<DeleteProgress>? progress = null, CancellationToken cancel = default)
        => new()
        {
            Groups = groups,
            DryRun = dryRun,
            Index = index,
            Progress = progress,
            Cancel = cancel,
        };

    [Fact]
    public void A_dry_run_deletes_nothing_but_reports_what_it_would_delete()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var first = Make(dir.Path, "copy1.txt", "same content");
        var second = Make(dir.Path, "copy2.txt", "same content");
        var deleter = new FakeDeleter();

        var outcome = new DeleteEngine(deleter, null)
            .Run(Request([Group(keeper, first, second)], dryRun: true));

        outcome.DryRun.Should().BeTrue();
        outcome.Deleted.Should().Be(2);
        outcome.BytesFreed.Should().Be(first.Length + second.Length);
        deleter.Deleted.Should().BeEmpty();
        File.Exists(first.Path).Should().BeTrue();
        File.Exists(second.Path).Should().BeTrue();
        File.Exists(keeper.Path).Should().BeTrue();
    }

    [Fact]
    public void A_real_run_deletes_only_the_duplicates_and_never_the_keeper()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var copy = Make(dir.Path, "copy.txt", "same content");
        var deleter = new FakeDeleter();

        var outcome = new DeleteEngine(deleter, null)
            .Run(Request([Group(keeper, copy)], dryRun: false));

        outcome.DryRun.Should().BeFalse();
        outcome.Deleted.Should().Be(1);
        outcome.Skipped.Should().Be(0);
        outcome.Failed.Should().Be(0);
        deleter.Deleted.Should().Equal(copy.Path);
        File.Exists(copy.Path).Should().BeFalse();
        File.Exists(keeper.Path).Should().BeTrue();
    }

    [Fact]
    public void Bytes_freed_sums_the_sizes_of_the_files_actually_deleted()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "0123456789");
        var first = Make(dir.Path, "copy1.txt", "0123456789");
        var second = Make(dir.Path, "copy2.txt", "0123456789");

        var outcome = new DeleteEngine(new FakeDeleter(), null)
            .Run(Request([Group(keeper, first, second)], dryRun: false));

        outcome.BytesFreed.Should().Be(20);
    }

    [Fact]
    public void A_real_run_takes_the_deleted_paths_out_of_the_index_and_keeps_the_keeper()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var copy = Make(dir.Path, "copy.txt", "same content");
        var index = IndexFor(keeper, copy);

        new DeleteEngine(new FakeDeleter(), null)
            .Run(Request([Group(keeper, copy)], dryRun: false, index));

        index.Find(copy.Path).Should().BeNull();
        index.Find(keeper.Path).Should().NotBeNull();
        index.Count.Should().Be(1);
    }

    [Fact]
    public void A_dry_run_leaves_the_index_untouched()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var copy = Make(dir.Path, "copy.txt", "same content");
        var index = IndexFor(keeper, copy);

        new DeleteEngine(new FakeDeleter(), null)
            .Run(Request([Group(keeper, copy)], dryRun: true, index));

        index.Count.Should().Be(2);
    }

    [Fact]
    public void A_duplicate_that_fails_verification_is_skipped_rather_than_deleted()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var copy = Make(dir.Path, "copy.txt", "same content");
        var deleter = new FakeDeleter();

        var outcome = new DeleteEngine(deleter, (_, _) => false)
            .Run(Request([Group(keeper, copy)], dryRun: false));

        outcome.Deleted.Should().Be(0);
        outcome.Skipped.Should().Be(1);
        deleter.Deleted.Should().BeEmpty();
        File.Exists(copy.Path).Should().BeTrue();
    }

    [Fact]
    public void Byte_for_byte_verification_protects_a_file_that_is_not_really_identical()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "aaaaaaaaaa");
        var impostor = Make(dir.Path, "impostor.txt", "bbbbbbbbbb");
        var deleter = new FakeDeleter();

        var outcome = new DeleteEngine(deleter, FileComparer.AreIdentical)
            .Run(Request([Group(keeper, impostor)], dryRun: false));

        outcome.Skipped.Should().Be(1);
        File.Exists(impostor.Path).Should().BeTrue();
    }

    [Fact]
    public void Byte_for_byte_verification_lets_a_genuine_duplicate_through()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "identical bytes");
        var copy = Make(dir.Path, "copy.txt", "identical bytes");

        var outcome = new DeleteEngine(new FakeDeleter(), FileComparer.AreIdentical)
            .Run(Request([Group(keeper, copy)], dryRun: false));

        outcome.Deleted.Should().Be(1);
        File.Exists(copy.Path).Should().BeFalse();
    }

    [Fact]
    public void A_missing_keeper_leaves_the_whole_set_alone()
    {
        using var dir = new TempDir();
        var copy1 = Make(dir.Path, "copy1.txt", "same content");
        var copy2 = Make(dir.Path, "copy2.txt", "same content");
        var ghostKeeper = new DuplicateFile(Path.Combine(dir.Path, "gone.txt"), 12, DateTime.UtcNow);
        var deleter = new FakeDeleter();

        var outcome = new DeleteEngine(deleter, null)
            .Run(Request([Group(ghostKeeper, copy1, copy2)], dryRun: false));

        outcome.Deleted.Should().Be(0);
        outcome.Skipped.Should().Be(2);
        deleter.Deleted.Should().BeEmpty();
        File.Exists(copy1.Path).Should().BeTrue();
        File.Exists(copy2.Path).Should().BeTrue();
    }

    [Fact]
    public void A_duplicate_that_has_already_gone_is_skipped()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var vanished = Make(dir.Path, "vanished.txt", "same content");
        File.Delete(vanished.Path);

        var outcome = new DeleteEngine(new FakeDeleter(), null)
            .Run(Request([Group(keeper, vanished)], dryRun: false));

        outcome.Deleted.Should().Be(0);
        outcome.Skipped.Should().Be(1);
        outcome.Failed.Should().Be(0);
    }

    [Fact]
    public void A_deleter_that_throws_counts_a_failure_and_the_run_continues()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var awkward = Make(dir.Path, "awkward.txt", "same content");
        var fine = Make(dir.Path, "fine.txt", "same content");
        var deleter = new FakeDeleter(path =>
        {
            if (path.EndsWith("awkward.txt", StringComparison.Ordinal))
            {
                throw new IOException("the file is in use");
            }
        });

        var outcome = new DeleteEngine(deleter, null)
            .Run(Request([Group(keeper, awkward, fine)], dryRun: false));

        outcome.Failed.Should().Be(1);
        outcome.Deleted.Should().Be(1);
        outcome.BytesFreed.Should().Be(fine.Length);
        deleter.Deleted.Should().Equal(fine.Path);
        File.Exists(awkward.Path).Should().BeTrue();
        File.Exists(fine.Path).Should().BeFalse();
    }

    [Fact]
    public void Cancelling_stops_the_run_and_reports_it()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var first = Make(dir.Path, "copy1.txt", "same content");
        var second = Make(dir.Path, "copy2.txt", "same content");
        var third = Make(dir.Path, "copy3.txt", "same content");
        using var cancel = new CancellationTokenSource();
        var deleter = new FakeDeleter(_ => cancel.Cancel());

        var outcome = new DeleteEngine(deleter, null)
            .Run(Request([Group(keeper, first, second, third)], dryRun: false, cancel: cancel.Token));

        outcome.Cancelled.Should().BeTrue();
        outcome.Deleted.Should().Be(1);
        deleter.Deleted.Should().HaveCount(1);
        File.Exists(keeper.Path).Should().BeTrue();
    }

    [Fact]
    public void Progress_is_reported_for_every_duplicate_considered()
    {
        using var dir = new TempDir();
        var keeper = Make(dir.Path, "keep.txt", "same content");
        var first = Make(dir.Path, "copy1.txt", "same content");
        var second = Make(dir.Path, "copy2.txt", "same content");
        var progress = new Recorder<DeleteProgress>();

        new DeleteEngine(new FakeDeleter(), null)
            .Run(Request([Group(keeper, first, second)], dryRun: false, progress: progress));

        progress.Items.Should().HaveCount(2);
        progress.Items[^1].Processed.Should().Be(2);
        progress.Items[^1].Total.Should().Be(2);
        progress.Items[^1].Deleted.Should().Be(2);
        progress.Items[^1].Percent.Should().Be(100);
    }

    [Fact]
    public void An_empty_run_reports_nothing_done()
    {
        var outcome = new DeleteEngine(new FakeDeleter(), null)
            .Run(Request(Array.Empty<DuplicateGroup>(), dryRun: false));

        outcome.Deleted.Should().Be(0);
        outcome.Skipped.Should().Be(0);
        outcome.Failed.Should().Be(0);
        outcome.BytesFreed.Should().Be(0);
        outcome.Cancelled.Should().BeFalse();
    }

    [Fact]
    public void Permanent_file_deleter_really_removes_the_file()
    {
        using var dir = new TempDir();
        var file = Path.Combine(dir.Path, "doomed.txt");
        File.WriteAllText(file, "goodbye");

        new PermanentFileDeleter().Delete(file);

        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public void Permanent_file_deleter_describes_itself()
    {
        new PermanentFileDeleter().Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Recycle_bin_deleter_describes_itself()
    {
        // Deliberately does not call Delete: that would put files in the user's Recycle Bin.
        new RecycleBinFileDeleter().Description.Should().NotBeNullOrWhiteSpace();
    }
}
