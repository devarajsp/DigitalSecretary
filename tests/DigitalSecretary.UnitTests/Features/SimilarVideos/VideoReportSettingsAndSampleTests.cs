using DigitalSecretary.Features.SimilarVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;
using static DigitalSecretary.UnitTests.Features.SimilarVideos.Videos;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>Covers <see cref="VideoReportText"/>, <see cref="SimilarVideosSettingsStore"/> and <see cref="SampleVideoSet"/>.</summary>
public sealed class VideoReportSettingsAndSampleTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Theory]
    [InlineData(0L, "length unknown")]
    [InlineData(42_000L, "0:42")]
    [InlineData(725_400L, "12:05")]
    [InlineData(3_723_000L, "1:02:03")]
    public void Durations_read_like_a_player(long ms, string text) => VideoReportText.Duration(ms).Should().Be(text);

    [Theory]
    [InlineData(0L, "")]
    [InlineData(950L, "950 kbps")]
    [InlineData(8_200L, "8.2 Mbps")]
    public void Bit_rates_are_short(long kbps, string text) => VideoReportText.Bitrate(kbps).Should().Be(text);

    [Fact]
    public void Labels_and_match_text()
    {
        VideoReportText.Label(VideoMatchKind.Exact).Should().Be("Exact copy");
        VideoReportText.Label(VideoMatchKind.SameVideo).Should().Be("Same video");
        VideoReportText.Label(VideoMatchKind.Similar).Should().Be("Similar - check");
        VideoReportText.MatchText(new VideoMatch(VideoMatchKind.SameVideo, PictureOrientation.Rotate90, 9, 10, 1)).Should().Be("Same video, turned 90° - 9/10 frames");
        VideoReportText.MatchText(new VideoMatch(VideoMatchKind.Exact, PictureOrientation.AsIs, 0, 0, 0)).Should().Be("Exact copy");
        VideoReportText.Describe(PictureOrientation.Rotate180).Should().Be("turned 180°");
        VideoReportText.Describe(PictureOrientation.MirrorLeftRight).Should().Be("mirrored");
        VideoReportText.Describe(PictureOrientation.MirrorDiagonal).Should().Be("turned and mirrored");
        VideoReportText.Dimensions(Hashed("a", All(0), width: 0)).Should().Be("size unknown");
    }

    [Fact]
    public void The_report_lists_every_group_with_keep_and_move()
    {
        var keeper = Hashed(@"C:\Users\You\Videos\a.mp4", All(0), size: 2_000_000, durationMs: 61_000);
        var copy = Hashed(@"C:\Users\You\Videos\a (1).mp4", All(0), size: 2_000_000, durationMs: 61_000);
        var take = Hashed(@"C:\Users\You\Videos\b.mp4", All(0), size: 1_000_000, durationMs: 61_000);
        var g = new VideoGroup(1, keeper,
        [
            new VideoMember(copy, new VideoMatch(VideoMatchKind.Exact, PictureOrientation.AsIs, 10, 10, 0), true),
            new VideoMember(take, new VideoMatch(VideoMatchKind.Similar, PictureOrientation.AsIs, 7, 10, 9), false)
        ]);

        var text = VideoReportText.Render(@"C:\Users\You\Videos", Now, 3, [g], new HashSet<string>([copy.Path]));

        text.Should().Contain("Similar Videos report").And.Contain("1 groups (0 exact, 0 same video, 1 similar to check)")
            .And.Contain("1 marked to move, 1.91 MB").And.Contain("KEEP").And.Contain("MOVE").And.Contain("1:01").And.Contain("[Similar - check - 7/10 frames]");
        VideoReportText.Render("x", Now, 0, [], new HashSet<string>()).Should().Contain("No similar videos found.");
    }

    [Fact]
    public void Settings_have_safe_defaults_and_round_trip()
    {
        var store = new SimilarVideosSettingsStore(Path.Combine(_dir.Path, "data"));
        var fresh = store.Load();
        fresh.Action.Should().Be(RemoveAction.ReviewFolder);
        fresh.SkipLivePhotos.Should().BeTrue();
        fresh.ToFindOptions().Should().Be(new VideoFindOptions());

        store.Save(new SimilarVideosSettings { Root = @"C:\Users\You\Videos", Sensitivity = VideoSensitivity.Strict, IncludeRotated = false, Action = RemoveAction.RecycleBin });
        var loaded = store.Load();
        (loaded.Root, loaded.Sensitivity, loaded.IncludeRotated, loaded.Action).Should().Be((@"C:\Users\You\Videos", VideoSensitivity.Strict, false, RemoveAction.RecycleBin));
        File.ReadAllText(Path.Combine(_dir.Path, "data", "similar_videos_settings.json")).Should().Contain("\"Strict\"");

        File.WriteAllText(Path.Combine(_dir.Path, "data", "similar_videos_settings.json"), "{ not json");
        store.Load().Sensitivity.Should().Be(VideoSensitivity.Balanced);
    }

    [Fact]
    public void The_sample_set_shows_every_kind_of_group_under_a_placeholder_folder()
    {
        var (records, strips) = SampleVideoSet.Build(Now);
        try
        {
            records.Should().OnlyContain(r => r.Path.StartsWith(SampleVideoSet.RootPath, StringComparison.Ordinal));
            strips.Values.Should().OnlyContain(b => b.Height == SampleVideoSet.StripHeight);

            var groups = VideoMatcher.Find(records, new VideoFindOptions());
            groups.Should().HaveCount(2, "the holiday video with its copies, and the birthday video with its turned copy and another take");
            groups.SelectMany(g => g.Members).Should().Contain(m => m.Kind == VideoMatchKind.Similar && m.Video.Path.EndsWith("MVI_0421.mp4", StringComparison.Ordinal));
            groups.SelectMany(g => g.Members).Should().Contain(m => m.Match.Orientation != PictureOrientation.AsIs);
            groups.SelectMany(g => g.Members.Select(m => m.Video).Append(g.Keeper)).Should().NotContain(v => v.Path.EndsWith("VID_5512.mp4", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var b in strips.Values)
            {
                b.Dispose();
            }
        }
    }
}
