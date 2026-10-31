using DigitalSecretary.Features.ReduceVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.ReduceVideos;

/// <summary>The run loop with a fake engine: every safety rule, with no codec involved.</summary>
public sealed class VideoRunnerTests
{
    /// <summary>"Encodes" by writing <see cref="Share"/> of the original's bytes; probes return what the test says.</summary>
    private sealed class FakeEngine : IVideoEngine
    {
        public double Share { get; init; } = 0.3;
        public Func<string, VideoFacts?>? ProbeAs { get; init; }
        public EncodeResult? Result { get; init; }
        public double? Score { get; init; }
        public List<string> Encoded { get; } = [];

        public string Name => "fake";

        public string OutputExtension(VideoFacts facts) => FfmpegVideoArgs.OutputExtension(facts.Path);

        public EncodeResult Encode(EncodeJob job, IProgress<int>? percent, CancellationToken cancel)
        {
            Encoded.Add(job.Source.Path);
            File.WriteAllBytes(job.Output, new byte[(int)(job.Source.Size * Share)]);
            percent?.Report(100);
            return Result ?? EncodeResult.Success;
        }

        public VideoFacts? Probe(string path) => ProbeAs is null ? new VideoFacts { Path = path, Width = 2, Height = 2, DurationMs = 60_000, HasAudio = true } : ProbeAs(path);

        public double? Ssim(string reduced, string original, CancellationToken cancel) => Score;
    }

    private static VideoFacts Make(TempDir dir, string name, int size = 10_000)
    {
        var path = Path.Combine(dir.Path, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Range(0, size).Select(i => (byte)i).ToArray());
        var info = new FileInfo(path);
        return new VideoFacts { Path = path, Size = size, ModifiedUtc = info.LastWriteTimeUtc, DurationMs = 60_000, Width = 2, Height = 2, Fps = 30, HasAudio = true };
    }

    private static VideoReduceJob Job(TempDir dir, IVideoEngine engine, params VideoFacts[] videos) => new()
    {
        Root = dir.Path,
        Candidates = [.. videos.Select(v => new VideoCandidate(v, new VideoAdvice(Verdict.Recommended, "", v.Size / 3, 1000)))],
        Engine = engine,
        FreeSpace = _ => long.MaxValue,
        Recycle = _ => throw new InvalidOperationException("no recycling in this test")
    };

    [Fact]
    public void Each_video_is_replaced_and_its_original_kept_in_the_review_folder_with_sub_folders()
    {
        using var dir = new TempDir();
        var a = Make(dir, @"2025\a.mp4");
        var b = Make(dir, "b.avi");
        var logged = new List<string>();

        var outcome = VideoReduceRunner.Run(Job(dir, new FakeEngine(), a, b) with { });

        outcome.Reduced.Should().Be(2);
        outcome.Saved.Should().Be(2 * (10_000 - 3_000));
        new FileInfo(a.Path).Length.Should().Be(3_000);
        File.Exists(Path.Combine(dir.Path, VideoReduceRunner.ReviewFolderName, "2025", "a.mp4")).Should().BeTrue();
        File.Exists(b.Path).Should().BeFalse("an AVI becomes an MP4");
        File.Exists(Path.ChangeExtension(b.Path, ".mp4")).Should().BeTrue();
        outcome.Batch!.Entries.Should().HaveCount(2);
        _ = logged;
    }

    [Fact]
    public void A_copy_that_saves_too_little_is_thrown_away()
    {
        using var dir = new TempDir();
        var a = Make(dir, "a.mp4");

        var outcome = VideoReduceRunner.Run(Job(dir, new FakeEngine { Share = 0.9 }, a));

        outcome.Skipped.Should().Be(1);
        outcome.Files[0].Note.Should().StartWith("Saved only 10%");
        new FileInfo(a.Path).Length.Should().Be(10_000);
        Directory.GetFiles(dir.Path).Should().ContainSingle();
        outcome.Batch.Should().BeNull();
    }

    [Fact]
    public void A_copy_with_a_different_length_or_without_sound_is_thrown_away()
    {
        using var dir = new TempDir();
        var a = Make(dir, "a.mp4");
        var b = Make(dir, "b.mp4");
        var engine = new FakeEngine
        {
            ProbeAs = p => p.Contains("a.ds", StringComparison.Ordinal)
                ? new VideoFacts { Path = p, Width = 2, Height = 2, DurationMs = 50_000, HasAudio = true }
                : new VideoFacts { Path = p, Width = 2, Height = 2, DurationMs = 60_000, HasAudio = false }
        };

        var outcome = VideoReduceRunner.Run(Job(dir, engine, a, b));

        outcome.Files.Select(f => f.Note).Should().SatisfyRespectively(
            n => n.Should().StartWith("The copy's length differs"),
            n => n.Should().StartWith("The copy lost the sound"));
        new FileInfo(a.Path).Length.Should().Be(10_000);
        new FileInfo(b.Path).Length.Should().Be(10_000);
    }

    [Fact]
    public void A_low_picture_quality_score_keeps_the_original_and_a_good_one_is_noted()
    {
        using var dir = new TempDir();
        var a = Make(dir, "a.mp4");

        var bad = VideoReduceRunner.Run(Job(dir, new FakeEngine { Score = 0.90 }, a) with { CheckQuality = true });
        var good = VideoReduceRunner.Run(Job(dir, new FakeEngine { Score = 0.991 }, a) with { CheckQuality = true });

        bad.Files[0].Note.Should().StartWith("Picture quality check failed (SSIM 0.900)");
        good.Files[0].Kind.Should().Be(FileOutcomeKind.Reduced);
        good.Files[0].Note.Should().Be("SSIM 0.991");
    }

    [Fact]
    public void An_encoder_failure_leaves_the_original_and_no_temp_file()
    {
        using var dir = new TempDir();
        var a = Make(dir, "a.mp4");

        var outcome = VideoReduceRunner.Run(Job(dir, new FakeEngine { Result = EncodeResult.Failed("boom") }, a));

        outcome.Failed.Should().Be(1);
        outcome.Files[0].Note.Should().Be("boom");
        Directory.GetFiles(dir.Path).Should().Equal(a.Path);
    }

    [Fact]
    public void A_video_changed_since_the_scan_is_left_alone()
    {
        using var dir = new TempDir();
        var a = Make(dir, "a.mp4");
        File.AppendAllText(a.Path, "edited");

        var outcome = VideoReduceRunner.Run(Job(dir, new FakeEngine(), a));

        outcome.Failed.Should().Be(1);
        outcome.Files[0].Note.Should().Contain("changed or vanished");
        Directory.GetFiles(dir.Path).Should().Equal(a.Path);
    }

    [Fact]
    public void Not_enough_free_space_skips_before_encoding()
    {
        using var dir = new TempDir();
        var engine = new FakeEngine();

        var outcome = VideoReduceRunner.Run(Job(dir, engine, Make(dir, "a.mp4")) with { FreeSpace = _ => 1000 });

        outcome.Files[0].Note.Should().StartWith("Not enough free space");
        engine.Encoded.Should().BeEmpty();
    }

    [Fact]
    public void Stop_ends_the_run_and_a_stopped_encode_counts_as_stopped()
    {
        using var dir = new TempDir();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = new FakeEngine();

        var before = VideoReduceRunner.Run(Job(dir, engine, Make(dir, "a.mp4")) with { Cancel = cts.Token });
        var during = VideoReduceRunner.Run(Job(dir, new FakeEngine { Result = EncodeResult.Stopped }, Make(dir, "b.mp4"), Make(dir, "c.mp4")));

        before.Cancelled.Should().BeTrue();
        engine.Encoded.Should().BeEmpty();
        during.Cancelled.Should().BeTrue();
        during.Files.Should().ContainSingle();
    }

    [Fact]
    public void Recycle_mode_recycles_the_original_and_leaves_no_undo_batch_but_records_the_saving()
    {
        using var dir = new TempDir();
        var a = Make(dir, "a.mp4");
        var recycled = new List<string>();
        var ledger = new SavingsLedger(Path.Combine(dir.Path, "data", "h.jsonl"));

        var outcome = VideoReduceRunner.Run(Job(dir, new FakeEngine(), a) with
        {
            Action = RemoveAction.RecycleBin,
            Recycle = p => { recycled.Add(p); File.Delete(p); },
            Ledger = ledger
        });

        recycled.Should().Equal(a.Path);
        outcome.Batch.Should().BeNull();
        new FileInfo(a.Path).Length.Should().Be(3_000);
        ledger.Load().Should().ContainSingle().Which.How.Should().Be("fake, VisuallyLossless");
    }

    [Fact]
    public void Run_summaries_explain_where_originals_are()
    {
        var reduced = new VideoReduceOutcome([new FileOutcome("a", FileOutcomeKind.Reduced, 100, 30, ""), new FileOutcome("b", FileOutcomeKind.Skipped, 5, 5, "x"), new FileOutcome("c", FileOutcomeKind.Failed, 5, 5, "y")], null, true);

        ReduceVideosText.RunSummary(reduced, RemoveAction.ReviewFolder).Should()
            .Contain("Reduced 1, saved 70 B").And.Contain("1 kept as they were").And.Contain("1 failed").And.Contain("Stopped").And.Contain(VideoReduceRunner.ReviewFolderName);
        ReduceVideosText.RunSummary(reduced, RemoveAction.RecycleBin).Should().Contain("Recycle Bin");
        VideoReduceRunner.Describe(reduced.Files[0]).Should().StartWith("Reduced a:");
        VideoReduceRunner.Describe(reduced.Files[1]).Should().Be("Skipped b: x");
        VideoReduceRunner.Describe(reduced.Files[2]).Should().Be("Failed c: y");
        VideoReduceRunner.FreeBytes(Path.GetTempPath()).Should().BeGreaterThan(0);
    }
}
