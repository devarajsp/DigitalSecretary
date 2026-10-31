using DigitalSecretary.Features.ReduceVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.ReduceVideos;

public sealed class ReduceVideosTextAndSettingsTests
{
    private static List<(VideoFacts, VideoAdvice)> SampleRows()
        => [.. SampleVideoFacts.Create().Select(f => (f, VideoAdvisor.Advise(f, new VideoAdviceOptions())))];

    [Fact]
    public void Settings_round_trip_and_bad_files_fall_back_to_safe_defaults()
    {
        using var dir = new TempDir();
        var store = new ReduceVideosSettingsStore(dir.Path);
        store.Save(new ReduceVideosSettings { Root = @"C:\v", Engine = VideoEngineKind.Windows, Quality = VideoQuality.Smallest, MinSavingPercent = 40, CheckQuality = false, SkipHdr = false, Action = RemoveAction.RecycleBin, FfmpegFolder = @"D:\ff" });

        var loaded = store.Load();

        (loaded.Root, loaded.Engine, loaded.Quality, loaded.MinSavingPercent, loaded.CheckQuality, loaded.SkipHdr, loaded.Action, loaded.FfmpegFolder)
            .Should().Be((@"C:\v", VideoEngineKind.Windows, VideoQuality.Smallest, 40, false, false, RemoveAction.RecycleBin, @"D:\ff"));
        File.WriteAllText(Path.Combine(dir.Path, "reduce_videos_settings.json"), "{nope");
        var fallback = store.Load();
        (fallback.Engine, fallback.Quality, fallback.Action, fallback.SkipHdr).Should().Be((VideoEngineKind.FfmpegX265, VideoQuality.VisuallyLossless, RemoveAction.ReviewFolder, true));
    }

    [Fact]
    public void The_minimum_saving_is_kept_within_sensible_bounds()
    {
        new ReduceVideosSettings { MinSavingPercent = 1 }.ToAdviceOptions(true).MinSavingPercent.Should().Be(5);
        new ReduceVideosSettings { MinSavingPercent = 99 }.ToAdviceOptions(false).Should().Match<VideoAdviceOptions>(o => o.MinSavingPercent == 90 && !o.WindowsHasHevc);
    }

    [Fact]
    public void Help_text_guides_the_choice_and_says_how_to_install_ffmpeg()
    {
        ReduceVideosText.EngineHelp(VideoEngineKind.FfmpegX265, false, GpuEncoder.None, true).Should().Contain(FfmpegLocator.InstallCommand);
        ReduceVideosText.EngineHelp(VideoEngineKind.FfmpegGpu, false, GpuEncoder.None, true).Should().Contain(FfmpegLocator.InstallCommand);
        ReduceVideosText.EngineHelp(VideoEngineKind.FfmpegX265, true, GpuEncoder.None, true).Should().Contain("smallest files");
        ReduceVideosText.EngineHelp(VideoEngineKind.FfmpegGpu, true, GpuEncoder.Amd, true).Should().Contain("AMD");
        ReduceVideosText.EngineHelp(VideoEngineKind.Windows, false, GpuEncoder.None, false).Should().Contain("H.264").And.Contain("nothing to install");
        foreach (var q in Enum.GetValues<VideoQuality>())
        {
            ReduceVideosText.QualityHelp(q).Should().NotBeNullOrWhiteSpace();
        }
        foreach (var g in Enum.GetValues<GpuEncoder>())
        {
            ReduceVideosText.GpuName(g).Should().NotBeNullOrWhiteSpace();
        }
        ReduceVideosText.EngineChoices.Should().HaveCount(3);
        ReduceVideosText.QualityChoices.Select(c => c.Value).Should().Equal(Enum.GetValues<VideoQuality>());
    }

    [Fact]
    public void List_cells_summary_and_selection_read_naturally()
    {
        var rows = SampleRows();
        var (facts, advice) = rows.First(r => r.Item2.Verdict == Verdict.Recommended);
        var skip = rows.First(r => r.Item2.Verdict == Verdict.Skip);

        var cells = ReduceVideosText.Cells(facts, advice);

        cells.Should().HaveCount(10);
        cells[0].Should().Be(Path.GetFileName(facts.Path));
        cells[6].Should().Be("Recommended");
        cells[7].Should().StartWith("~");
        cells[8].Should().EndWith("%)");
        ReduceVideosText.Cells(skip.Item1, skip.Item2)[7].Should().BeEmpty();
        ReduceVideosText.Summary(rows).Should().StartWith("8 videos").And.Contain("recommended");
        ReduceVideosText.Selection([]).Should().Be("Nothing selected.");
        ReduceVideosText.Selection([(facts, advice)]).Should().StartWith("1 selected:").And.Contain("saves ~");
        ReduceVideosText.VerdictLabel(Verdict.Optional).Should().Be("Your call");
        ReduceVideosText.Duration(3_909_000).Should().Be("1:05:09");
        ReduceVideosText.Duration(247_000).Should().Be("4:07");
        ReduceVideosText.Percent(1, 0).Should().Be("0%");
        ReduceVideosText.SavedSoFar(new SavingsTotal(0, 0, 0)).Should().Be("Nothing reduced yet.");
        ReduceVideosText.SavedSoFar(new SavingsTotal(1, 2000, 1000)).Should().Be("Saved so far: 1 video, " + ByteSize.Format(1000));
    }

    [Fact]
    public void The_report_lists_every_video_with_its_reason()
    {
        var report = ReduceVideosText.Report(SampleVideoFacts.Root, SampleRows(), new VideoAdviceOptions(), new DateTime(2026, 11, 1, 9, 30, 0));

        report.Should().StartWith("Reduce Size - Videos report").And.Contain("2026-11-01 09:30").And.Contain("FfmpegX265");
        foreach (var f in SampleVideoFacts.Create())
        {
            report.Should().Contain(f.Path);
        }
        report.Should().Contain("HDR video");
    }

    [Fact]
    public void The_scan_finds_videos_skips_the_originals_folder_and_cleans_unfinished_copies()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "a.mp4"), "x");
        File.WriteAllText(Path.Combine(dir.Path, "notes.txt"), "x");
        File.WriteAllText(ShrinkSwap.TempPathFor(Path.Combine(dir.Path, "b.mp4"), ".mp4"), "half");
        Directory.CreateDirectory(Path.Combine(dir.Path, VideoReduceRunner.ReviewFolderName));
        File.WriteAllText(Path.Combine(dir.Path, VideoReduceRunner.ReviewFolderName, "old.mp4"), "x");
        var cache = new VideoFactsCache();
        var seen = new List<string>();

        var result = VideoFactsScanner.Run(new VideoFactsScanRequest
        {
            Root = dir.Path,
            Cache = cache,
            Read = f => { lock (seen) { seen.Add(f.Name); } return new VideoFacts { Path = f.FullName, Size = f.Length, ModifiedUtc = f.LastWriteTimeUtc, DurationMs = 1000, Width = 2, Height = 2 }; }
        });

        seen.Should().Equal("a.mp4");
        result.Items.Should().Be(1);
        Directory.GetFiles(dir.Path).Should().NotContain(f => ShrinkSwap.IsTemp(f));
        VideoFactsScanner.IsVideo("x.MTS").Should().BeTrue();
        VideoFactsScanner.IsVideo("x.jpg").Should().BeFalse();
    }

    [Fact]
    public void The_cache_keeps_only_usable_records()
    {
        using var dir = new TempDir();
        var file = VideoFactsCache.PathFor(dir.Path, @"C:\v");
        var cache = new VideoFactsCache();
        cache.Put(new VideoFacts { Path = "good", DurationMs = 1000, Width = 2, Height = 2 });
        cache.Put(new VideoFacts { Path = "bad", DurationMs = 0, Width = 2, Height = 2 });
        cache.Save(file);

        VideoFactsCache.Load(file).Records.Select(r => r.Path).Should().Equal("good");
        File.WriteAllText(file, "not gzip");
        VideoFactsCache.Load(file).Count.Should().Be(0);
    }
}
