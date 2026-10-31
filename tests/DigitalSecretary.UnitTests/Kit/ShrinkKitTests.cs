using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Kit;

/// <summary>The kit pieces the Reduce Size features share: ffmpeg locate/run, the safe swap with undo, the savings history.</summary>
public sealed class ShrinkKitTests
{
    private static (string Original, string Temp, SwapRequest Request) Prepare(TempDir dir, string relative, string tempExt, RemoveAction action = RemoveAction.ReviewFolder, Action<string>? recycle = null)
    {
        var original = Path.Combine(dir.Path, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        File.WriteAllBytes(original, new byte[1000]);
        var when = new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetCreationTimeUtc(original, when);
        File.SetLastWriteTimeUtc(original, when);
        var temp = ShrinkSwap.TempPathFor(original, tempExt);
        File.WriteAllBytes(temp, new byte[300]);
        var request = new SwapRequest
        {
            Root = dir.Path,
            ReviewFolderName = "_Reduced - originals",
            Original = original,
            OriginalSize = 1000,
            OriginalModifiedUtc = when,
            Reduced = temp,
            FinalPath = ShrinkSwap.FinalPathFor(original, tempExt),
            Action = action,
            Recycle = recycle ?? (_ => throw new InvalidOperationException())
        };
        return (original, temp, request);
    }

    [Fact]
    public void The_smaller_file_takes_the_original_s_place_and_dates()
    {
        using var dir = new TempDir();
        var (original, temp, request) = Prepare(dir, @"a\clip.mp4", ".mp4");

        var entry = ShrinkSwap.Commit(request);

        new FileInfo(original).Length.Should().Be(300);
        File.GetLastWriteTimeUtc(original).Should().Be(request.OriginalModifiedUtc);
        File.GetCreationTimeUtc(original).Should().Be(request.OriginalModifiedUtc);
        File.Exists(temp).Should().BeFalse();
        entry.StoredAt.Should().Be(Path.Combine(dir.Path, "_Reduced - originals", "a", "clip.mp4"));
        new FileInfo(entry.StoredAt).Length.Should().Be(1000);
        (entry.OriginalSize, entry.ReducedSize, entry.Reduced).Should().Be((1000L, 300L, original));
    }

    [Fact]
    public void Undo_puts_the_original_back_and_removes_the_copy()
    {
        using var dir = new TempDir();
        var (original, _, request) = Prepare(dir, "clip.avi", ".mp4");
        var entry = ShrinkSwap.Commit(request);
        var log = new List<string>();

        var result = ShrinkSwap.Undo(new SwapBatch(DateTime.UtcNow, dir.Path, [entry]), new SyncLog(log));

        result.Should().Be((1, 0));
        new FileInfo(original).Length.Should().Be(1000);
        File.Exists(Path.ChangeExtension(original, ".mp4")).Should().BeFalse();
        log.Should().BeEmpty();
    }

    [Fact]
    public void Undo_never_deletes_a_copy_that_was_changed_or_overwrites_another_file()
    {
        using var dir = new TempDir();
        var (_, _, r1) = Prepare(dir, "one.mp4", ".mp4");
        var (o2, _, r2) = Prepare(dir, "two.avi", ".mp4");
        var e1 = ShrinkSwap.Commit(r1);
        var e2 = ShrinkSwap.Commit(r2);
        File.AppendAllText(e1.Reduced, "edited later");
        File.WriteAllText(o2, "a new file with the old name");
        var log = new List<string>();

        var result = ShrinkSwap.Undo(new SwapBatch(DateTime.UtcNow, dir.Path, [e1, e2, e2 with { StoredAt = "" }]), new SyncLog(log));

        result.Should().Be((0, 3));
        File.Exists(e1.StoredAt).Should().BeTrue();
        File.Exists(e2.Reduced).Should().BeTrue("the copy stays when its original cannot come back");
        log.Should().HaveCount(3);
    }

    [Fact]
    public void A_changed_original_is_never_replaced()
    {
        using var dir = new TempDir();
        var (original, temp, request) = Prepare(dir, "clip.mp4", ".mp4");
        File.AppendAllText(original, "x");

        var act = () => ShrinkSwap.Commit(request);

        act.Should().Throw<InvalidOperationException>();
        File.Exists(temp).Should().BeTrue();
        new FileInfo(original).Length.Should().Be(1001);
    }

    [Fact]
    public void If_the_rename_fails_the_original_is_put_back()
    {
        using var dir = new TempDir();
        var (original, temp, request) = Prepare(dir, "clip.mp4", ".mp4");
        File.Delete(temp); // the copy vanished between check and swap

        var act = () => ShrinkSwap.Commit(request);

        act.Should().Throw<IOException>();
        new FileInfo(original).Length.Should().Be(1000);
    }

    [Fact]
    public void Recycle_mode_recycles_and_cannot_be_undone_here()
    {
        using var dir = new TempDir();
        var recycled = new List<string>();
        var (original, _, request) = Prepare(dir, "clip.mp4", ".mp4", RemoveAction.RecycleBin, p => { recycled.Add(p); File.Delete(p); });

        var entry = ShrinkSwap.Commit(request);

        recycled.Should().Equal(original);
        entry.StoredAt.Should().BeEmpty();
        ShrinkSwap.Undo(new SwapBatch(DateTime.UtcNow, dir.Path, [entry])).Should().Be((0, 1));
    }

    [Fact]
    public void A_new_extension_never_overwrites_an_existing_file()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\v\a.mp4", @"C:\v\a (reduced).mp4" };

        ShrinkSwap.FinalPathFor(@"C:\v\a.avi", ".mp4", taken.Contains).Should().Be(@"C:\v\a (reduced 2).mp4");
        ShrinkSwap.FinalPathFor(@"C:\v\b.avi", ".mp4", taken.Contains).Should().Be(@"C:\v\b.mp4");
        ShrinkSwap.FinalPathFor(@"C:\v\a.MP4", ".mp4", taken.Contains).Should().Be(@"C:\v\a.MP4");
    }

    [Fact]
    public void Temp_files_are_recognised_and_leftovers_cleaned()
    {
        using var dir = new TempDir();
        var temp = ShrinkSwap.TempPathFor(Path.Combine(dir.Path, "x.mov"), ".mov");
        var normal = Path.Combine(dir.Path, "x.mov");
        File.WriteAllText(temp, "half");
        File.WriteAllText(normal, "keep");

        ShrinkSwap.IsTemp(temp).Should().BeTrue();
        ShrinkSwap.IsTemp(normal).Should().BeFalse();
        ShrinkSwap.CleanLeftovers([temp, normal]).Should().Be(1);
        File.Exists(temp).Should().BeFalse();
        File.Exists(normal).Should().BeTrue();
    }

    [Fact]
    public void The_last_batch_survives_a_restart()
    {
        using var dir = new TempDir();
        var file = Path.Combine(dir.Path, "data", "last_run.json");
        var batch = new SwapBatch(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), dir.Path, [new SwapEntry("a", "b", "c", 10, 3)]);

        ShrinkSwap.SaveLastBatch(file, batch);

        ShrinkSwap.LoadLastBatch(file)!.Entries.Should().Equal(batch.Entries);
        File.WriteAllText(file, "{broken");
        ShrinkSwap.LoadLastBatch(file).Should().BeNull();
        ShrinkSwap.LoadLastBatch(Path.Combine(dir.Path, "none.json")).Should().BeNull();
    }

    [Fact]
    public void The_history_adds_up_and_undo_lines_cancel_savings()
    {
        using var dir = new TempDir();
        var ledger = new SavingsLedger(Path.Combine(dir.Path, "h", "history.jsonl"));
        var when = DateTime.UtcNow;

        ledger.Append(new SavingRecord(when, "a", 1000, 300, "x"));
        ledger.Append(new SavingRecord(when, "b", 500, 100, "x"));
        ledger.Append(new SavingRecord(when, "a", 300, 1000, "undo"));
        File.AppendAllText(ledger.File, "{torn line" + Environment.NewLine);

        var records = ledger.Load();
        records.Should().HaveCount(3);
        var total = SavingsLedger.Total(records);
        total.Files.Should().Be(1);
        total.Saved.Should().Be(400);
        new SavingsLedger(Path.Combine(dir.Path, "missing.jsonl")).Load().Should().BeEmpty();
    }

    [Fact]
    public void Ffmpeg_is_found_only_where_both_tools_are()
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"D:\half\ffmpeg.exe", @"E:\tools\ffmpeg.exe", @"E:\tools\ffprobe.exe" };

        var found = FfmpegLocator.Find([@"D:\half", "", @"E:\tools"], present.Contains)!;

        found.Should().Be(new MediaToolPaths(@"E:\tools\ffmpeg.exe", @"E:\tools\ffprobe.exe"));
        found.Folder.Should().Be(@"E:\tools");
        FfmpegLocator.Find([@"D:\half"], present.Contains).Should().BeNull();
        FfmpegLocator.Candidates(@"X:\mine").First().Should().Be(@"X:\mine");
        FfmpegLocator.Candidates(null).Should().Contain(c => c.EndsWith(@"WinGet\Links", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_tool_runs_without_a_window_and_reports_output_errors_and_stops()
    {
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var lines = new List<string>();

        var collected = ToolRunner.Run(cmd, ["/c", "echo hello & echo oops 1>&2 & exit 3"], null, CancellationToken.None);
        var streamed = ToolRunner.Run(cmd, ["/c", "echo one & echo two"], l => { lock (lines) { lines.Add(l.Trim()); } }, CancellationToken.None);
        using var cts = new CancellationTokenSource(300);
        var stopped = ToolRunner.Run(cmd, ["/c", "ping -n 30 127.0.0.1 > nul"], null, cts.Token);

        collected.ExitCode.Should().Be(3);
        collected.Succeeded.Should().BeFalse();
        collected.Output.Should().Contain("hello");
        collected.ErrorTail.Should().Contain("oops");
        streamed.Succeeded.Should().BeTrue();
        lines.Should().Contain(["one", "two"]);
        stopped.Cancelled.Should().BeTrue();
        ToolRunner.StartInfo("x.exe", ["a b", "c"]).ArgumentList.Should().Equal("a b", "c");
    }

    private sealed class SyncLog(List<string> lines) : IProgress<string>
    {
        public void Report(string value) => lines.Add(value);
    }
}
