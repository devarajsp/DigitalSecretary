using System.Drawing;
using DigitalSecretary.Features.SimilarVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>Covers the pure parts of <see cref="VideoDecoder"/> and <see cref="ReaderSession"/>: sampling plan, greyscale, rotation, codec padding.</summary>
public sealed class VideoDecoderTests
{
    private static RawFrame Frame(int w, int h, Func<int, int, byte> value)
    {
        var bytes = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = ((y * w) + x) * 4;
                bytes[i] = bytes[i + 1] = bytes[i + 2] = value(x, y);
            }
        }
        return new RawFrame(w, h, bytes);
    }

    [Fact]
    public void Ten_frames_are_sampled_between_5_and_95_percent()
    {
        VideoDecoder.FrameFractions.Should().HaveCount(VideoDecoder.FrameCount);
        VideoDecoder.FrameFractions[0].Should().BeApproximately(0.05, 1e-9);
        VideoDecoder.FrameFractions[^1].Should().BeApproximately(0.95, 1e-9);
    }

    [Fact]
    public void A_big_frame_becomes_a_small_greyscale_one_that_keeps_its_shape_and_brightness()
    {
        var raw = Frame(1920, 1080, (x, _) => x < 960 ? (byte)40 : (byte)200);

        var gray = VideoDecoder.ToGray(raw, VideoDecoder.SampleSize);

        gray.Width.Should().Be(VideoDecoder.SampleSize);
        gray.Height.Should().Be(90);
        gray[10, 45].Should().BeApproximately(40f, 0.5f);
        gray[150, 45].Should().BeApproximately(200f, 0.5f);
    }

    [Fact]
    public void A_small_frame_is_not_enlarged()
    {
        var gray = VideoDecoder.ToGray(Frame(64, 36, (_, _) => 128), VideoDecoder.SampleSize);

        (gray.Width, gray.Height).Should().Be((64, 36));
    }

    [Theory]
    [InlineData(0, 4, 2)]
    [InlineData(90, 2, 4)]
    [InlineData(180, 4, 2)]
    [InlineData(270, 2, 4)]
    public void The_rotation_tag_turns_frames_upright(int degrees, int width, int height)
    {
        var frame = new GrayImage(4, 2, [0, 1, 2, 3, 4, 5, 6, 7]);

        var upright = VideoDecoder.Upright(frame, degrees);

        (upright.Width, upright.Height).Should().Be((width, height));
        if (degrees == 90)
        {
            upright[0, 0].Should().Be(4f, "turned clockwise: the bottom-left pixel comes to the top-left");
        }
    }

    [Fact]
    public void Codec_padding_is_cut_off()
    {
        // 1080p is coded as 1088 rows; the decoder announces 1080 before the first frame.
        ReaderSession.ViewOf(new Size(1920, 1088), new Size(1920, 1080), null).Should().Be(new Rectangle(0, 0, 1920, 1080));
        // A display aperture wins when it is given and fits.
        ReaderSession.ViewOf(new Size(1920, 1088), new Size(1920, 1088), new Rectangle(0, 4, 1920, 1080)).Should().Be(new Rectangle(0, 4, 1920, 1080));
        // A nonsense aperture is ignored; nothing larger than the buffer is ever read.
        ReaderSession.ViewOf(new Size(640, 360), new Size(800, 600), new Rectangle(0, 0, 9999, 10)).Should().Be(new Rectangle(0, 0, 640, 360));
        ReaderSession.ViewOf(new Size(640, 360), Size.Empty, null).Should().Be(new Rectangle(0, 0, 640, 360));
    }

    [Fact]
    public void A_video_area_blob_is_read_as_offset_and_size()
    {
        var blob = new byte[16];
        BitConverter.GetBytes((short)2).CopyTo(blob, 2);
        BitConverter.GetBytes((short)4).CopyTo(blob, 6);
        BitConverter.GetBytes(1920).CopyTo(blob, 8);
        BitConverter.GetBytes(1080).CopyTo(blob, 12);

        ReaderSession.ParseVideoArea(blob).Should().Be(new Rectangle(2, 4, 1920, 1080));
        ReaderSession.ParseVideoArea(new byte[8]).Should().BeNull();
        ReaderSession.ParseVideoArea(null).Should().BeNull();
    }

    [Fact]
    public void A_film_strip_puts_frames_side_by_side()
    {
        using var a = new Bitmap(96, 54);
        using var b = new Bitmap(30, 54);

        using var strip = VideoDecoder.Join([a, b], 54);

        (strip.Width, strip.Height).Should().Be((96 + 2 + 30, 54));
    }

    [Fact]
    public void Frame_hashes_survive_the_decoder_path_for_a_painted_frame()
    {
        using var scene = SampleScene.Paint(42, 640, 360);
        var bytes = new byte[640 * 360 * 4];
        for (var y = 0; y < 360; y++)
        {
            for (var x = 0; x < 640; x++)
            {
                var c = scene.GetPixel(x, y);
                var i = ((y * 640) + x) * 4;
                (bytes[i], bytes[i + 1], bytes[i + 2]) = (c.B, c.G, c.R);
            }
        }

        var viaDecoder = ImageHasher.Compute(VideoDecoder.ToGray(new RawFrame(640, 360, bytes), VideoDecoder.SampleSize));
        var direct = ImageHasher.Compute(GrayImage.FromBitmap(scene));

        ImageHasher.Distance(viaDecoder.PHash, direct.PHash).Should().BeLessThanOrEqualTo(2);
    }
}
