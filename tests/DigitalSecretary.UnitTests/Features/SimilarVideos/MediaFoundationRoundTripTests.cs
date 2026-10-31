using System.Drawing;
using DigitalSecretary.Features.SimilarVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>
/// Integration: real videos through real Windows Media Foundation. Tiny MP4s are encoded at run time
/// (nothing binary in the repo), then decoded by <see cref="VideoDecoder"/> and grouped by
/// <see cref="VideoMatcher"/>. This is the seam unit tests cannot reach: seeking, RGB32 conversion,
/// stride/orientation of the decoded rows, length, and that a re-encode still matches.
/// When Media Foundation is absent (some Windows Server / "N" editions) the tests return early - there is
/// nothing to decode with, and the feature then reports every video as unreadable.
/// </summary>
public sealed class MediaFoundationRoundTripTests : IDisposable
{
    private const int W = 320;
    private const int H = 176;

    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string Make(string name, int seed, int width = W, int height = H, int bitrate = 1_000_000)
    {
        var path = Path.Combine(_dir.Path, name);
        var scenes = SampleVideoSet.Frames(seed, width, height);
        try
        {
            TestVideoWriter.Write(path, scenes, bitrate: bitrate);
        }
        finally
        {
            foreach (var s in scenes)
            {
                s.Dispose();
            }
        }
        return path;
    }

    private static VideoRecord Record(string path)
    {
        var sample = new VideoDecoder().Read(path);
        sample.Should().NotBeNull("Media Foundation decodes the MP4 it just wrote: " + path);
        var info = new FileInfo(path);
        return VideoRecord.From(path, info.Length, info.LastWriteTimeUtc, sample!);
    }

    [Fact]
    public void A_decoded_video_has_its_length_size_and_upright_frames()
    {
        if (!VideoDecoder.IsAvailable)
        {
            return;
        }
        var path = Make("original.mp4", 5);

        var sample = new VideoDecoder().Read(path)!;

        sample.DurationMs.Should().BeInRange(4800, 5200, "10 scenes x 5 frames at 10 fps = 5 s");
        (sample.Width, sample.Height).Should().Be((W, H));
        sample.Frames.Should().HaveCount(VideoDecoder.FrameCount).And.OnlyContain(f => f != null);
        for (var i = 0; i < VideoDecoder.FrameCount; i++)
        {
            using var scene = SampleScene.Paint(500 + i, W, H);
            var expected = ImageHasher.Compute(GrayImage.FromBitmap(scene));
            var got = ImageHasher.Compute(sample.Frames[i]!);
            ImageHasher.Distance(expected.PHash, got.PHash).Should().BeLessThanOrEqualTo(8,
                "frame {0} is the scene shown at that time, the right way up (not flipped by a stride mix-up)", i);
        }
    }

    [Fact]
    public void A_copy_is_exact_a_smaller_re_encode_is_the_same_video_and_another_video_is_left_alone()
    {
        if (!VideoDecoder.IsAvailable)
        {
            return;
        }
        var original = Make("original.mp4", 5);
        var copy = Path.Combine(_dir.Path, "original - Copy.mp4");
        File.Copy(original, copy);
        var small = Make("small.mp4", 5, 160, 96, 250_000);
        var other = Make("other.mp4", 6);

        var records = new[] { original, copy, small, other }.Select(Record).ToList();
        var groups = VideoMatcher.Find(records, new VideoFindOptions(), FileBytes.AreIdentical);

        groups.Should().ContainSingle();
        var g = groups[0];
        g.Keeper.Path.Should().Be(original, "same pixels as the copy, but the copy's name says it is one");
        g.Members.Select(m => (Path.GetFileName(m.Video.Path), m.Kind)).Should().BeEquivalentTo(new[]
        {
            ("original - Copy.mp4", VideoMatchKind.Exact),
            ("small.mp4", VideoMatchKind.SameVideo)
        });
    }

    [Fact]
    public void A_film_strip_shows_four_frames_at_the_requested_height()
    {
        if (!VideoDecoder.IsAvailable)
        {
            return;
        }
        var path = Make("strip.mp4", 7);

        using var strip = new VideoDecoder().Filmstrip(path, 54);

        strip.Should().NotBeNull();
        strip!.Height.Should().Be(54);
        strip.Width.Should().BeGreaterThan(4 * 90, "four 16:9-ish frames of 54 px height side by side");
    }

    [Fact]
    public void A_file_that_is_not_a_video_is_unreadable_not_an_error()
    {
        var path = Path.Combine(_dir.Path, "fake.mp4");
        File.WriteAllText(path, "this is not a video");

        new VideoDecoder().Read(path).Should().BeNull();
        new VideoDecoder().Filmstrip(path, 54).Should().BeNull();
        new VideoDecoder().Read(Path.Combine(_dir.Path, "missing.mp4")).Should().BeNull();
    }
}
