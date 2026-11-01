using DigitalSecretary.Features.ReduceVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.ReduceVideos;

/// <summary>
/// Integration: the real engines on real files made at test time - Windows' probe and encoder always, ffprobe /
/// ffmpeg / SSIM when ffmpeg is installed - and the full run (encode, verify, swap, undo) end to end.
/// </summary>
public sealed class VideoEnginesIntegrationTests
{
    [Fact]
    public void Windows_reads_the_facts_of_a_real_mp4()
    {
        using var dir = new TempDir();
        var path = RealVideos.WriteSilentH264(dir.Path);

        var facts = MfVideoProbe.Read(new FileInfo(path))!;

        facts.Should().NotBeNull();
        facts.Codec.Should().Be("h264");
        (facts.Width, facts.Height).Should().Be((640, 360));
        facts.Fps.Should().BeApproximately(30, 0.1);
        facts.DurationMs.Should().BeInRange(3900, 4100);
        facts.HasAudio.Should().BeFalse();
        facts.ReadBy.Should().Be("windows");
    }

    [Fact]
    public void Windows_probe_rejects_a_file_that_is_not_a_video()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "notes.mp4");
        File.WriteAllText(path, "not a video");

        MfVideoProbe.Read(new FileInfo(path)).Should().BeNull();
    }

    [Fact]
    public void Windows_engine_makes_a_smaller_mp4_of_the_same_length()
    {
        using var dir = new TempDir();
        var source = MfVideoProbe.Read(new FileInfo(RealVideos.WriteSilentH264(dir.Path)))!;
        var output = Path.Combine(dir.Path, "out.mp4");
        var engine = new WindowsVideoEngine();

        var result = engine.Encode(new EncodeJob(source, output, VideoQuality.VisuallyLossless, 600), null, CancellationToken.None);

        result.Ok.Should().BeTrue(result.Error);
        var copy = engine.Probe(output)!;
        copy.Codec.Should().Be(MfTranscoder.HasHevcEncoder ? "hevc" : "h264");
        (copy.Width, copy.Height).Should().Be((640, 360));
        Math.Abs(copy.DurationMs - source.DurationMs).Should().BeLessThan(500);
        new FileInfo(output).Length.Should().BeLessThan(source.Size);
        if (RealVideos.Tools is { } tools)
        {
            var seen = FfprobeReader.Read(tools.Ffprobe, new FileInfo(output))!;
            (seen.Width, seen.Height).Should().Be((640, 360), "players must see the visible picture, not the encoder's padding");
        }
    }

    [Fact]
    public void Windows_engine_stops_when_asked()
    {
        using var dir = new TempDir();
        var source = MfVideoProbe.Read(new FileInfo(RealVideos.WriteSilentH264(dir.Path)))!;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = new WindowsVideoEngine().Encode(new EncodeJob(source, Path.Combine(dir.Path, "out.mp4"), VideoQuality.Balanced, 600), null, cts.Token);

        result.Cancelled.Should().BeTrue();
    }

    [Fact]
    public void Windows_engine_keeps_the_sound()
    {
        using var dir = new TempDir();
        if (RealVideos.WriteNoisyWithSound(dir.Path) is not { } path)
        {
            return; // needs ffmpeg to make a test video with sound
        }
        var source = MfVideoProbe.Read(new FileInfo(path))!;
        var output = Path.Combine(dir.Path, "out.mp4");

        var result = new WindowsVideoEngine().Encode(new EncodeJob(source, output, VideoQuality.VisuallyLossless, 3000), null, CancellationToken.None);

        result.Ok.Should().BeTrue(result.Error);
        var copy = FfprobeReader.Read(RealVideos.Tools!.Ffprobe, new FileInfo(output))!;
        copy.HasAudio.Should().BeTrue();
        copy.AudioCodec.Should().Be("aac");
        VideoReduceRunner.Verify(source, copy, new FileInfo(output).Length, 25).Should().BeNull();
    }

    [Fact]
    public void Ffprobe_reads_a_real_video_with_sound()
    {
        using var dir = new TempDir();
        if (RealVideos.WriteNoisyWithSound(dir.Path) is not { } path)
        {
            return;
        }

        var facts = FfprobeReader.Read(RealVideos.Tools!.Ffprobe, new FileInfo(path))!;

        facts.Codec.Should().Be("h264");
        (facts.Width, facts.Height).Should().Be((1280, 720));
        facts.Fps.Should().BeApproximately(30, 0.01);
        facts.DurationMs.Should().BeInRange(2900, 3100);
        facts.HasAudio.Should().BeTrue();
        facts.AudioCodec.Should().Be("aac");
        facts.BitDepth.Should().Be(8);
        facts.IsHdr.Should().BeFalse();
        VideoAdvisor.Advise(facts, new VideoAdviceOptions()).Verdict.Should().Be(Verdict.Recommended);
    }

    [Fact]
    public void Ffmpeg_x265_makes_a_much_smaller_copy_that_passes_the_quality_check()
    {
        using var dir = new TempDir();
        if (RealVideos.WriteNoisyWithSound(dir.Path) is not { } path)
        {
            return;
        }
        var engine = new FfmpegVideoEngine(RealVideos.Tools!, VideoEngineKind.FfmpegX265, GpuEncoder.None);
        var source = engine.Probe(path)!;
        var output = Path.Combine(dir.Path, "out" + engine.OutputExtension(source));
        var seen = new List<int>();

        var result = engine.Encode(new EncodeJob(source, output, VideoQuality.VisuallyLossless, 0), new SyncProgress<int>(seen.Add), CancellationToken.None);

        result.Ok.Should().BeTrue(result.Error);
        var copy = engine.Probe(output)!;
        copy.Codec.Should().Be("hevc");
        copy.AudioCodec.Should().Be("aac", "the sound is copied untouched");
        VideoReduceRunner.Verify(source, copy, new FileInfo(output).Length, 25).Should().BeNull();
        engine.Ssim(output, path, CancellationToken.None).Should().BeGreaterThan(FfmpegVideoArgs.MinimumSsim(VideoQuality.VisuallyLossless));
        seen.Should().NotBeEmpty();
        seen.Max().Should().BeGreaterThan(50);
    }

    [Fact]
    public void A_full_run_swaps_in_the_copy_keeps_the_original_and_undo_restores_it()
    {
        using var dir = new TempDir();
        var path = RealVideos.WriteSilentH264(dir.Path, "holiday.mp4");
        var original = File.ReadAllBytes(path);
        var modified = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, modified);
        var facts = MfVideoProbe.Read(new FileInfo(path))!;
        var ledger = new SavingsLedger(Path.Combine(dir.Path, "data", "history.jsonl"));
        var advice = new VideoAdvice(Verdict.Recommended, "", facts.Size / 4, 600);

        var outcome = VideoReduceRunner.Run(new VideoReduceJob
        {
            Root = dir.Path,
            Candidates = [new VideoCandidate(facts, advice)],
            Engine = new WindowsVideoEngine(),
            // Only "smaller" is required here: how much Windows' encoder saves varies by machine (14% on the CI runner).
            MinSavingPercent = 1,
            Ledger = ledger
        });

        outcome.Reduced.Should().Be(1, outcome.Files[0].Note);
        new FileInfo(path).Length.Should().BeLessThan(original.Length);
        File.GetLastWriteTimeUtc(path).Should().Be(modified, "the copy keeps the original's dates");
        var kept = Path.Combine(dir.Path, VideoReduceRunner.ReviewFolderName, "holiday.mp4");
        File.ReadAllBytes(kept).Should().Equal(original);
        Directory.GetFiles(dir.Path).Should().NotContain(f => ShrinkSwap.IsTemp(f));
        SavingsLedger.Total(ledger.Load()).Files.Should().Be(1);

        var (restored, skipped) = ShrinkSwap.Undo(outcome.Batch!);

        (restored, skipped).Should().Be((1, 0));
        File.ReadAllBytes(path).Should().Equal(original);
        File.Exists(kept).Should().BeFalse();
    }
}
