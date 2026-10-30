using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>Covers <see cref="PictureReader"/>, the GDI+ and Shell thumbnail providers and the EXIF helpers.</summary>
public sealed class PictureReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ds-pics-" + Guid.NewGuid().ToString("N"));

    public PictureReaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private string Save(string name, Bitmap bmp, ImageFormat format)
    {
        var path = Path.Combine(_dir, name);
        using (bmp)
        {
            bmp.Save(path, format);
        }
        return path;
    }

    private sealed class FakeProvider(Func<string, Bitmap?> make) : IThumbnailProvider
    {
        public int Calls { get; private set; }

        public Bitmap? GetThumbnail(string path, int size)
        {
            Calls++;
            return make(path);
        }
    }

    [Fact]
    public void Gdi_reads_jpeg_and_png_scaled_to_fit()
    {
        var jpg = Save("a.jpg", SamplePictureSet.Scene(11, 640, 480), ImageFormat.Jpeg);
        var png = Save("b.png", SamplePictureSet.Scene(12, 100, 300), ImageFormat.Png);
        var gdi = new GdiThumbnailProvider();

        using var a = gdi.GetThumbnail(jpg, 256);
        using var b = gdi.GetThumbnail(png, 256);

        a!.Width.Should().Be(256);
        a.Height.Should().Be(192);
        b!.Height.Should().Be(256, "a tall picture fits by its height");
        b.Width.Should().Be(85);
    }

    [Fact]
    public void Gdi_returns_null_for_a_file_that_is_not_a_picture()
    {
        var bad = Path.Combine(_dir, "fake.jpg");
        File.WriteAllText(bad, "not a picture");
        new GdiThumbnailProvider().GetThumbnail(bad, 256).Should().BeNull();
        new GdiThumbnailProvider().GetThumbnail(Path.Combine(_dir, "missing.jpg"), 256).Should().BeNull();
    }

    [Fact]
    public void Shell_never_throws_and_respects_the_size_when_it_answers()
    {
        var jpg = Save("shell.jpg", SamplePictureSet.Scene(11, 640, 480), ImageFormat.Jpeg);
        using var thumb = new ShellThumbnailProvider().GetThumbnail(jpg, 128);
        if (thumb is not null)
        {
            Math.Max(thumb.Width, thumb.Height).Should().BeLessThanOrEqualTo(128);
        }
        new ShellThumbnailProvider().GetThumbnail(Path.Combine(_dir, "missing.jpg"), 128).Should().BeNull();
    }

    [Fact]
    public void The_reader_falls_back_to_the_next_provider()
    {
        var first = new FakeProvider(_ => null);
        var second = new FakeProvider(_ => SamplePictureSet.Scene(11, 64, 48));
        var reader = new PictureReader(first, second);

        var sample = reader.Read(Path.Combine(_dir, "anything.heic"));

        sample.Should().NotBeNull();
        sample!.Gray.Width.Should().Be(64);
        sample.Width.Should().Be(0, "the header of a file GDI+ cannot read is unknown");
        first.Calls.Should().Be(1);
        second.Calls.Should().Be(1);
    }

    [Fact]
    public void The_reader_returns_null_when_nothing_can_decode_the_file()
        => new PictureReader(new FakeProvider(_ => null)).Read("x.raw").Should().BeNull();

    [Fact]
    public void The_default_reader_reads_a_real_jpeg_with_its_dimensions()
    {
        var jpg = Save("real.jpg", SamplePictureSet.Scene(11, 640, 480), ImageFormat.Jpeg);
        var sample = PictureReader.Default().Read(jpg);

        sample.Should().NotBeNull();
        sample!.Width.Should().Be(640);
        sample.Height.Should().Be(480);
        Math.Max(sample.Gray.Width, sample.Gray.Height).Should().BeLessThanOrEqualTo(PictureReader.SampleSize);
    }

    [Fact]
    public void Header_of_an_unreadable_file_is_zero_and_no_date()
    {
        var bad = Path.Combine(_dir, "bad.png");
        File.WriteAllText(bad, "nope");
        PictureReader.ReadHeader(bad).Should().Be((0, 0, (DateTime?)null));
    }

    [Fact]
    public void Exif_dates_parse_as_local_time_and_bad_ones_are_ignored()
    {
        var value = Encoding.ASCII.GetBytes("2024:06:11 14:05:09\0");
        PictureReader.ParseExifDate(value).Should().Be(new DateTime(2024, 6, 11, 14, 5, 9, DateTimeKind.Local).ToUniversalTime());
        PictureReader.ParseExifDate(Encoding.ASCII.GetBytes("0000:00:00 00:00:00")).Should().BeNull();
        PictureReader.ParseExifDate(null).Should().BeNull();
        PictureReader.ParseExifDate([1, 2]).Should().BeNull();
    }

    [Theory]
    [InlineData("45", 450)]
    [InlineData("045", 45)]
    [InlineData("5\0\0", 500)]
    [InlineData("  ", 0)]
    public void Sub_seconds_are_the_digits_after_the_decimal_point(string text, int expectedMs)
        => PictureReader.ParseSubSeconds(Encoding.ASCII.GetBytes(text)).TotalMilliseconds.Should().BeApproximately(expectedMs, 0.5);

    [Fact]
    public void No_sub_second_field_is_zero()
        => PictureReader.ParseSubSeconds(null).Should().Be(TimeSpan.Zero);

    [Fact]
    public void A_picture_without_an_orientation_tag_needs_no_turning()
    {
        using var bmp = SamplePictureSet.Scene(11, 40, 30);
        PictureReader.ExifRotation(bmp).Should().Be(RotateFlipType.RotateNoneFlipNone);
    }

    [Fact]
    public void Fit_within_never_enlarges()
    {
        using var small = SamplePictureSet.Scene(11, 50, 40);
        using var fitted = PictureReader.FitWithin(small, 256);
        fitted.Size.Should().Be(new Size(50, 40));
    }
}
