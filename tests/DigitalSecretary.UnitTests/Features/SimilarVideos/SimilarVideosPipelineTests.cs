using DigitalSecretary.Features.SimilarVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>
/// Integration: the path the window takes, on real files - scan a folder, group, move the pre-marked
/// videos to the review folder, rescan (the review folder is ignored), then undo. The decoder is a fake
/// keyed by file name so the test is fast and needs no codec; <see cref="MediaFoundationRoundTripTests"/>
/// covers the real decoder.
/// </summary>
public sealed class SimilarVideosPipelineTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string Root => _dir.Path;

    private string File_(string relative, int bytes)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Repeat((byte)7, bytes).ToArray());
        return path;
    }

    /// <summary>"trip*" files are video 1 (the "-small" one painted smaller); "garden" is video 3.</summary>
    private static VideoSample? Decode(string path)
    {
        var name = Path.GetFileName(path);
        var frames = name.StartsWith("garden", StringComparison.Ordinal)
            ? SampleVideoSet.Frames(3)
            : name.Contains("small", StringComparison.Ordinal) ? SampleVideoSet.Frames(1, 160, 90) : SampleVideoSet.Frames(1);
        var big = !name.Contains("small", StringComparison.Ordinal);
        return SampleVideoSet.Sample(frames, 95_000, big ? 1920 : 640, big ? 1080 : 360);
    }

    [Fact]
    public void Scan_group_move_rescan_and_undo()
    {
        var original = File_(Path.Combine("2024", "trip.mp4"), 4000);
        var copy = Path.Combine(Root, "backup", "trip (1).mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(original, copy);
        var small = File_(Path.Combine("chat", "trip-small.mp4"), 500);
        File_(Path.Combine("2024", "garden.mp4"), 3000);

        var cache = new VideoCache();
        var request = new VideoScanRequest { Root = Root, Cache = cache, Read = Decode, ExcludeFolder = VideoReviewMover.ReviewFolderFor(Root) };
        VideoScanner.Run(request).Items.Should().Be(4);

        var groups = VideoMatcher.Find([.. cache.Records], new VideoFindOptions(), FileBytes.AreIdentical);
        var g = groups.Should().ContainSingle().Subject;
        g.Keeper.Path.Should().Be(original, "full size, and not named like a copy");
        g.Members.Single(m => m.Video.Path == copy).Kind.Should().Be(VideoMatchKind.Exact);
        g.Members.Single(m => m.Video.Path == small).Kind.Should().Be(VideoMatchKind.SameVideo);
        g.Members.Should().OnlyContain(m => m.Suggested);

        var marked = g.Members.Select(m => m.Video.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var moved = VideoReviewMover.Run(new VideoRemoveRequest { Root = Root, Groups = groups, Marked = marked });
        moved.Removed.Should().Be(2);
        moved.Bytes.Should().Be(4500);
        File.Exists(original).Should().BeTrue("the keeper never moves");
        File.Exists(Path.Combine(VideoReviewMover.ReviewFolderFor(Root), "chat", "trip-small.mp4")).Should().BeTrue();

        var rescan = VideoScanner.Run(request);
        rescan.Items.Should().Be(2, "moved videos are gone and the review folder is not scanned");
        rescan.Removed.Should().Be(2);

        ReviewFolder.Undo(moved.Batch!).Restored.Should().Be(2);
        File.Exists(copy).Should().BeTrue();
        File.Exists(small).Should().BeTrue();
    }
}
