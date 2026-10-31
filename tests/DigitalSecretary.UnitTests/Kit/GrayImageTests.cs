using System.Drawing;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Kit;

/// <summary>Covers <see cref="GrayImage"/> - greyscale conversion, resampling, turning and sharpness.</summary>
public sealed class GrayImageTests
{
    private static GrayImage Numbered(int w, int h)
        => new(w, h, Enumerable.Range(0, w * h).Select(i => (float)i).ToArray());

    [Fact]
    public void Pixel_count_must_match_the_size()
    {
        var act = () => new GrayImage(2, 2, new float[3]);
        act.Should().Throw<ArgumentException>();
        var zero = () => new GrayImage(0, 1, []);
        zero.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void From_bitmap_uses_luma_and_composites_transparency_over_white()
    {
        using var bmp = new Bitmap(3, 1);
        bmp.SetPixel(0, 0, Color.White);
        bmp.SetPixel(1, 0, Color.Black);
        bmp.SetPixel(2, 0, Color.FromArgb(0, 0, 0, 0));

        var gray = GrayImage.FromBitmap(bmp);

        gray[0, 0].Should().BeApproximately(255f, 0.5f);
        gray[1, 0].Should().BeApproximately(0f, 0.5f);
        gray[2, 0].Should().BeApproximately(255f, 0.5f, "a transparent pixel reads as white paper");
    }

    [Fact]
    public void Green_counts_more_than_blue()
    {
        using var bmp = new Bitmap(2, 1);
        bmp.SetPixel(0, 0, Color.FromArgb(0, 255, 0));
        bmp.SetPixel(1, 0, Color.FromArgb(0, 0, 255));
        var gray = GrayImage.FromBitmap(bmp);
        gray[0, 0].Should().BeGreaterThan(gray[1, 0]);
    }

    [Fact]
    public void Shrinking_averages_the_area_each_pixel_covers()
    {
        var img = new GrayImage(2, 2, [0f, 100f, 200f, 100f]);
        img.Resize(1, 1)[0, 0].Should().BeApproximately(100f, 0.01f);

        var row = new GrayImage(4, 1, [0f, 0f, 100f, 100f]).Resize(2, 1);
        row[0, 0].Should().BeApproximately(0f, 0.01f);
        row[1, 0].Should().BeApproximately(100f, 0.01f);
    }

    [Fact]
    public void Non_integer_shrink_weights_partly_covered_pixels()
    {
        var img = new GrayImage(3, 1, [0f, 90f, 180f]).Resize(2, 1);
        img[0, 0].Should().BeApproximately(30f, 0.01f);   // 1 x 0 + 0.5 x 90 over 1.5
        img[1, 0].Should().BeApproximately(150f, 0.01f);  // 0.5 x 90 + 1 x 180 over 1.5
    }

    [Fact]
    public void Growing_repeats_the_nearest_pixel()
    {
        var img = new GrayImage(1, 1, [42f]).Resize(3, 2);
        img.Width.Should().Be(3);
        img.Height.Should().Be(2);
        Enumerable.Range(0, 3).Should().OnlyContain(x => Math.Abs(img[x, 1] - 42f) < 0.01f);
    }

    [Fact]
    public void Turning_a_quarter_moves_the_top_left_corner_to_the_top_right()
    {
        var img = Numbered(3, 2);   // 0 1 2 / 3 4 5
        var turned = img.Transform(PictureOrientation.Rotate90);

        turned.Width.Should().Be(2);
        turned.Height.Should().Be(3);
        turned[1, 0].Should().Be(0f);   // old top-left
        turned[0, 0].Should().Be(3f);   // old bottom-left
    }

    [Theory]
    [InlineData(PictureOrientation.Rotate90, 4)]
    [InlineData(PictureOrientation.Rotate180, 2)]
    [InlineData(PictureOrientation.Rotate270, 4)]
    [InlineData(PictureOrientation.MirrorLeftRight, 2)]
    [InlineData(PictureOrientation.MirrorTopBottom, 2)]
    [InlineData(PictureOrientation.MirrorDiagonal, 2)]
    [InlineData(PictureOrientation.MirrorAntiDiagonal, 2)]
    public void Repeating_a_transform_enough_times_gives_the_original(PictureOrientation o, int times)
    {
        var img = Numbered(4, 3);
        var result = img;
        for (var i = 0; i < times; i++)
        {
            result = result.Transform(o);
        }
        Pixels(result).Should().Equal(Pixels(img));
    }

    [Fact]
    public void Every_transform_is_undone_by_its_inverse()
    {
        var img = Numbered(4, 3);
        foreach (var o in Enum.GetValues<PictureOrientation>())
        {
            Pixels(img.Transform(o).Transform(Orientations.Inverse(o))).Should().Equal(Pixels(img), "for {0}", o);
        }
    }

    [Fact]
    public void Mirror_left_right_reverses_each_row()
    {
        var m = Numbered(3, 1).Transform(PictureOrientation.MirrorLeftRight);
        Pixels(m).Should().Equal(2f, 1f, 0f);
    }

    [Fact]
    public void Sharpness_is_zero_for_flat_and_tiny_pictures_and_higher_for_edges()
    {
        new GrayImage(5, 5, Enumerable.Repeat(128f, 25).ToArray()).LaplacianVariance().Should().Be(0);
        new GrayImage(2, 2, [0f, 255f, 255f, 0f]).LaplacianVariance().Should().Be(0);
        var checker = new GrayImage(6, 6, Enumerable.Range(0, 36).Select(i => ((i % 6) + (i / 6)) % 2 == 0 ? 0f : 255f).ToArray());
        checker.LaplacianVariance().Should().BeGreaterThan(1000);
    }

    [Fact]
    public void Mean_is_the_average_brightness()
        => new GrayImage(2, 1, [10f, 30f]).Mean().Should().Be(20);

    private static float[] Pixels(GrayImage img)
    {
        var list = new List<float>();
        for (var y = 0; y < img.Height; y++)
        {
            for (var x = 0; x < img.Width; x++)
            {
                list.Add(img[x, y]);
            }
        }
        return [.. list];
    }
}
