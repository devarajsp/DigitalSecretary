using System.Drawing;
using DigitalSecretary.Features.SimilarVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;
using static DigitalSecretary.UnitTests.Features.SimilarVideos.Videos;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>
/// Covers <see cref="VideoMatcher"/>: frame-by-frame matching, the tiers, turned copies, the length gate,
/// exact copies and star grouping. Generated frames for the realistic cases, hand-set hashes for exact limits.
/// </summary>
public sealed class VideoMatcherTests
{
    private static readonly VideoFindOptions Defaults = new();
    private static readonly VideoThresholds Balanced = VideoThresholds.For(VideoSensitivity.Balanced);

    [Fact]
    public void The_same_frames_are_the_same_video_on_every_frame()
    {
        var m = VideoMatcher.Compare(Seed("a.mp4", 1), Seed("b.mp4", 1), Defaults, Balanced);

        m.Should().NotBeNull();
        m!.Value.Kind.Should().Be(VideoMatchKind.SameVideo);
        (m.Value.MatchedFrames, m.Value.ComparedFrames).Should().Be((10, 10));
        m.Value.Orientation.Should().Be(PictureOrientation.AsIs);
    }

    [Fact]
    public void A_smaller_version_is_the_same_video()
        => VideoMatcher.Compare(Seed("a.mp4", 1), Seed("b.mp4", 1, 160, 90), Defaults, Balanced)!.Value.Kind.Should().Be(VideoMatchKind.SameVideo);

    [Theory]
    [InlineData(RotateFlipType.Rotate90FlipNone)]
    [InlineData(RotateFlipType.Rotate180FlipNone)]
    [InlineData(RotateFlipType.RotateNoneFlipX)]
    public void A_turned_or_mirrored_copy_matches_once_turned_back(RotateFlipType how)
    {
        var a = Seed("a.mp4", 2);
        var b = From("b.mp4", Turned(2, how));

        var m = VideoMatcher.Compare(a, b, Defaults, Balanced);

        m!.Value.Kind.Should().Be(VideoMatchKind.SameVideo);
        m.Value.Orientation.Should().NotBe(PictureOrientation.AsIs);
        VideoMatcher.Compare(a, b, Defaults with { IncludeRotated = false, IncludeSimilar = false }, Balanced)
            .Should().BeNull("without turned matching, a turned copy is a different picture");
    }

    [Fact]
    public void Another_take_filmed_a_little_closer_is_only_similar_and_can_be_switched_off()
    {
        var a = Seed("a.mp4", 2);
        var b = From("b.mp4", [.. SampleVideoSet.Frames(2).Select(f => SampleVideoSet.Zoomed(f, 0.06))]);

        VideoMatcher.Compare(a, b, Defaults, Balanced)!.Value.Kind.Should().Be(VideoMatchKind.Similar);
        VideoMatcher.Compare(a, b, Defaults with { IncludeSimilar = false }, Balanced).Should().BeNull();
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(1, 4)]
    [InlineData(3, 5)]
    public void Different_videos_do_not_match_even_loosely(int seedA, int seedB)
        => VideoMatcher.Compare(Seed("a.mp4", seedA), Seed("b.mp4", seedB), Defaults, VideoThresholds.For(VideoSensitivity.Loose)).Should().BeNull();

    [Fact]
    public void Too_few_comparable_frames_never_match()
    {
        // Three content frames in common (the rest flat in one or the other) is not enough evidence.
        ulong?[] a = [0, 0, 0, null, null, null, null, null, null, null];
        VideoMatcher.Compare(Hashed("a", a), Hashed("b", All(0)), Defaults, Balanced).Should().BeNull();

        ulong?[] four = [0, 0, 0, 0, null, null, null, null, null, null];
        VideoMatcher.Compare(Hashed("a", four), Hashed("b", All(0)), Defaults, Balanced)!.Value.ComparedFrames.Should().Be(4);
    }

    [Fact]
    public void The_share_of_matching_frames_decides_the_tier()
    {
        // 8 of 10 frames within the Balanced pHash limit (8 bits) -> Same video; 7 of 10 -> only Similar (<= 12 bits).
        ulong?[] eight = [0, 0, 0, 0, 0, 0, 0, 0, Bits(11), Bits(11)];
        ulong?[] seven = [0, 0, 0, 0, 0, 0, 0, Bits(11), Bits(11), Bits(11)];

        VideoMatcher.Compare(Hashed("a", All(0)), Hashed("b", eight), Defaults, Balanced)!.Value.Kind.Should().Be(VideoMatchKind.SameVideo);
        VideoMatcher.Compare(Hashed("a", All(0)), Hashed("b", seven), Defaults, Balanced)!.Value.Kind.Should().Be(VideoMatchKind.Similar);
    }

    [Fact]
    public void A_high_mean_dhash_distance_vetoes_a_pHash_match()
    {
        // pHash identical, but dHash differs by 17 bits on average - over the Balanced mean limit of 16 for
        // "Same" (and fine for "Similar", whose mean limit is 20).
        var a = Hashed("a", All(0), d: 0);
        var b = Hashed("b", All(0), d: Bits(17));

        VideoMatcher.Compare(a, b, Defaults, Balanced)!.Value.Kind.Should().Be(VideoMatchKind.Similar);
        VideoMatcher.Compare(a, Hashed("c", All(0), d: Bits(21)), Defaults, Balanced).Should().BeNull();
    }

    [Theory]
    [InlineData(95_000, 96_800, true)]
    [InlineData(95_000, 97_000, false)]
    [InlineData(5_000, 5_900, true)]
    [InlineData(5_000, 6_100, false)]
    [InlineData(0, 0, true)]
    public void Lengths_must_agree_within_2_percent_or_one_second(long a, long b, bool agree)
        => VideoMatcher.DurationsAgree(Hashed("a", All(0), durationMs: a), Hashed("b", All(0), durationMs: b)).Should().Be(agree);

    [Fact]
    public void Videos_of_different_lengths_are_never_grouped_even_with_the_same_frames()
        => VideoMatcher.Find([Hashed("a", All(0), durationMs: 60_000), Hashed("b", All(0), durationMs: 90_000)], Defaults).Should().BeEmpty();

    [Fact]
    public void An_identical_file_is_an_exact_copy_even_without_usable_frames()
    {
        var flat = new ulong?[VideoDecoder.FrameCount];
        var groups = VideoMatcher.Find([Hashed("a.mp4", flat, size: 500), Hashed("a (1).mp4", flat, size: 500)], Defaults, (_, _) => true);

        var g = groups.Should().ContainSingle().Subject;
        g.Keeper.Path.Should().Be("a.mp4", "the other one is named like a copy");
        g.Members.Single().Kind.Should().Be(VideoMatchKind.Exact);
        g.Members.Single().Suggested.Should().BeTrue();
    }

    [Fact]
    public void The_byte_check_is_only_asked_for_files_of_the_same_size()
    {
        var asked = 0;
        VideoMatcher.Find([Hashed("a", All(0), size: 100), Hashed("b", All(0), size: 200)], Defaults, (_, _) => { asked++; return true; });

        asked.Should().Be(0);
    }

    [Fact]
    public void Every_member_matches_the_keeper_itself_so_chains_cannot_form()
    {
        // a ~ b (6 bits) and b ~ c (6 bits), but a and c are 12 bits apart. "a" is the best (most pixels),
        // so it keeps b; c is not dragged in through b.
        var a = Hashed("a", All(0), width: 3840, height: 2160);
        var b = Hashed("b", All(Bits(6)));
        var c = Hashed("c", All(Bits(12)), width: 1280, height: 720);

        var groups = VideoMatcher.Find([a, b, c], Defaults with { IncludeSimilar = false });

        var g = groups.Should().ContainSingle().Subject;
        g.Keeper.Path.Should().Be("a");
        g.Members.Select(m => m.Video.Path).Should().Equal("b");
    }

    [Fact]
    public void Similar_members_are_never_suggested_and_groups_come_most_space_first()
    {
        ulong?[] similar = [0, 0, 0, 0, 0, 0, 0, Bits(11), Bits(11), Bits(11)];
        var groups = VideoMatcher.Find(
        [
            Hashed("small-a", All(0), size: 10, durationMs: 10_000),
            Hashed("small-b", All(0), size: 10, durationMs: 10_000),
            Hashed("big-a", All(Bits(40)), size: 1_000, durationMs: 50_000),
            Hashed("big-b", All(Bits(40)), size: 1_000, durationMs: 50_000),
            Hashed("take-a", similar, size: 5_000, durationMs: 90_000, width: 3840, height: 2160),
            Hashed("take-b", All(0), size: 5_000, durationMs: 90_000)
        ], Defaults);

        groups.Select(g => g.Id).Should().Equal(1, 2, 3);
        groups[0].Keeper.Path.Should().StartWith("big");
        var take = groups.Single(g => g.Kind == VideoMatchKind.Similar);
        take.Members.Should().OnlyContain(m => !m.Suggested);
        take.SuggestedBytes.Should().Be(0);
        take.AllOthersBytes.Should().Be(5_000);
    }

    [Fact]
    public void Each_direction_reports_the_turn_that_undoes_the_other()
    {
        var a = Seed("a.mp4", 2, durationMs: 1000);
        var b = From("b.mp4", Turned(2, RotateFlipType.Rotate90FlipNone), durationMs: 1000, width: 1080, height: 1920);
        var ab = VideoMatcher.Compare(a, b, Defaults, Balanced)!.Value.Orientation;
        var ba = VideoMatcher.Compare(b, a, Defaults, Balanced)!.Value.Orientation;

        ab.Should().Be(VideoMatcher.Inverse(ba));
        VideoMatcher.Find([a, b], Defaults).Single().Members.Single().Match.Orientation.Should().BeOneOf(ab, ba);
    }

    [Fact]
    public void A_stricter_setting_never_finds_more_than_a_looser_one()
    {
        var records = new[] { Seed("a", 1), Seed("b", 1, 160, 90), From("c", [.. SampleVideoSet.Frames(1).Select(f => SampleVideoSet.Zoomed(f, 0.06))]), Seed("d", 2) };
        int Found(VideoSensitivity s) => VideoMatcher.Find(records, Defaults with { Sensitivity = s }).Sum(g => g.Members.Count);

        Found(VideoSensitivity.Strict).Should().BeLessThanOrEqualTo(Found(VideoSensitivity.Balanced));
        Found(VideoSensitivity.Balanced).Should().BeLessThanOrEqualTo(Found(VideoSensitivity.Loose));
    }
}
