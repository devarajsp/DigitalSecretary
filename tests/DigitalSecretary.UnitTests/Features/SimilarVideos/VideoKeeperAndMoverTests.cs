using DigitalSecretary.Features.SimilarVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;
using static DigitalSecretary.UnitTests.Features.SimilarVideos.Videos;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>Covers <see cref="VideoKeeperRanking"/>, <see cref="VideoReviewMover"/> and <see cref="VideoGroupEdits"/>.</summary>
public sealed class VideoKeeperAndMoverTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private static VideoRecord V(string path, int w = 1920, int h = 1080, long size = 100_000_000, long ms = 60_000, DateTime? modified = null)
        => Hashed(path, All(0), size: size, durationMs: ms, width: w, height: h, modified: modified);

    private static string Best(params VideoRecord[] videos) => videos.OrderBy(v => v, VideoKeeperRanking.Instance).First().Path;

    [Fact]
    public void More_pixels_wins_first() => Best(V("small", 1280, 720, size: 900_000_000), V("big", 3840, 2160)).Should().Be("big");

    [Fact]
    public void At_the_same_size_a_higher_bit_rate_wins() => Best(V("low", size: 50_000_000), V("high", size: 200_000_000)).Should().Be("high");

    [Fact]
    public void A_longer_video_wins_over_a_clipped_one_at_the_same_rate()
        => Best(V("clipped", size: 90_000_000, ms: 54_000), V("full", size: 100_000_000, ms: 60_000)).Should().Be("full");

    [Fact]
    public void A_name_that_looks_like_a_copy_loses_a_tie()
        => Best(V(@"x\clip (1).mp4"), V(@"x\clip.mp4"), V(@"x\clip - Copy.mp4")).Should().Be(@"x\clip.mp4");

    [Fact]
    public void The_older_file_wins_then_the_shorter_path()
    {
        Best(V("b", modified: Now), V("a", modified: Now.AddDays(-1))).Should().Be("a");
        Best(V(@"deep\folder\v.mp4"), V(@"v.mp4")).Should().Be("v.mp4");
        VideoKeeperRanking.Instance.Compare(null, V("a")).Should().Be(1);
        VideoKeeperRanking.Instance.Compare(V("a"), null).Should().Be(-1);
    }

    private static VideoGroup Group(VideoRecord keeper, params VideoRecord[] others)
        => new(1, keeper, [.. others.Select(o => new VideoMember(o, new VideoMatch(VideoMatchKind.SameVideo, PictureOrientation.Rotate90, 10, 10, 1), true))]);

    [Fact]
    public void The_plan_never_includes_a_keeper_and_lists_each_video_once()
    {
        var g1 = Group(V("k1"), V("a"), V("b"));
        var g2 = Group(V("a"), V("c"));
        var marked = new HashSet<string>(["k1", "a", "b", "c"], StringComparer.OrdinalIgnoreCase);

        VideoReviewMover.Plan([g1, g2], marked).Select(v => v.Path).Should().Equal("b", "c");
    }

    [Fact]
    public void Run_moves_marked_videos_into_the_review_folder_keeping_their_layout_and_undo_puts_them_back()
    {
        var root = _dir.Path;
        var keeper = Real(Path.Combine(root, "2024", "trip.mp4"));
        var copy = Real(Path.Combine(root, "backup", "trip (1).mp4"));
        var changed = Real(Path.Combine(root, "chat", "trip-small.mp4"));
        var g = Group(keeper, copy, changed);
        File.AppendAllText(changed.Path, "edited after the scan");

        var outcome = VideoReviewMover.Run(new VideoRemoveRequest { Root = root, Groups = [g], Marked = new HashSet<string>([copy.Path, changed.Path, keeper.Path]) });

        outcome.Removed.Should().Be(1);
        outcome.Skipped.Should().Be(1, "a file that changed since the scan is never moved");
        File.Exists(keeper.Path).Should().BeTrue();
        File.Exists(Path.Combine(VideoReviewMover.ReviewFolderFor(root), "backup", "trip (1).mp4")).Should().BeTrue();
        VideoReviewMover.ReviewFolderName.Should().Be("_Similar videos - review");

        ReviewFolder.Undo(outcome.Batch!).Restored.Should().Be(1);
        File.Exists(copy.Path).Should().BeTrue();
    }

    [Fact]
    public void Recycle_bin_removal_uses_the_recycler_and_leaves_no_undo_batch()
    {
        var copy = Real(Path.Combine(_dir.Path, "b.mp4"));
        var recycled = new List<string>();

        var outcome = VideoReviewMover.Run(new VideoRemoveRequest
        {
            Root = _dir.Path, Groups = [Group(V("a"), copy)], Marked = new HashSet<string>([copy.Path]),
            Action = RemoveAction.RecycleBin, Recycle = recycled.Add
        });

        recycled.Should().Equal(copy.Path);
        outcome.Batch.Should().BeNull();
    }

    [Fact]
    public void Choosing_another_keeper_swaps_roles_and_suggests_nothing_for_the_old_keeper()
    {
        var g = Group(V("k"), V("a"), V("b"));

        var edited = VideoGroupEdits.WithKeeper(g, "b");

        edited.Keeper.Path.Should().Be("b");
        edited.Members.Select(m => m.Video.Path).Should().Equal("k", "a");
        edited.Members[0].Suggested.Should().BeFalse();
        edited.Members[0].Match.Orientation.Should().Be(PictureOrientation.Rotate270, "turned back the other way");
        VideoGroupEdits.WithKeeper(g, "not-a-member").Should().BeSameAs(g);
    }

    private static VideoRecord Real(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "video bytes");
        var info = new FileInfo(path);
        return Hashed(path, All(0), size: info.Length, modified: info.LastWriteTimeUtc);
    }
}
