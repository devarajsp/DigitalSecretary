using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DigitalSecretary.Features.SimilarPictures;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>
/// Generated test pictures and their variants (resized, re-compressed, turned, mirrored, brightened,
/// cropped, burst frames) - no real photos. Built on <see cref="SamplePictureSet.Scene"/>.
/// </summary>
internal static class Pictures
{
    public static readonly DateTime Now = new(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc);

    public static GrayImage Gray(Bitmap bmp)
    {
        using (bmp)
        {
            return GrayImage.FromBitmap(bmp);
        }
    }

    public static Fingerprint Print(Bitmap bmp) => ImageHasher.Compute(Gray(bmp));

    public static Bitmap Resized(Bitmap source, int width, int height)
    {
        using (source)
        {
            var result = new Bitmap(width, height);
            using var g = Graphics.FromImage(result);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, width, height);
            return result;
        }
    }

    public static Bitmap Jpeg(Bitmap source, long quality)
    {
        using (source)
        {
            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            var stream = new MemoryStream();
            source.Save(stream, codec, parameters);
            stream.Position = 0;
            return new Bitmap(stream);
        }
    }

    public static Bitmap Turned(Bitmap source, RotateFlipType how)
    {
        source.RotateFlip(how);
        return source;
    }

    public static Bitmap Brighter(Bitmap source, float factor)
    {
        using (source)
        {
            var result = new Bitmap(source.Width, source.Height);
            using var g = Graphics.FromImage(result);
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(new ColorMatrix(
            [
                [factor, 0, 0, 0, 0],
                [0, factor, 0, 0, 0],
                [0, 0, factor, 0, 0],
                [0, 0, 0, 1, 0],
                [0, 0, 0, 0, 1]
            ]));
            g.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
            return result;
        }
    }

    /// <summary>Removes <paramref name="percent"/>% from every edge.</summary>
    public static Bitmap Cropped(Bitmap source, int percent)
    {
        using (source)
        {
            var dx = source.Width * percent / 100;
            var dy = source.Height * percent / 100;
            return source.Clone(new Rectangle(dx, dy, source.Width - (2 * dx), source.Height - (2 * dy)), source.PixelFormat);
        }
    }

    /// <summary>A record with the fingerprint of <paramref name="bmp"/> and the given facts.</summary>
    public static PictureRecord Record(string path, Bitmap bmp, int width = 4000, int height = 3000, long size = 4_000_000, DateTime? taken = null)
        => PictureRecord.From(path, size, Now, new PictureSample(Gray(bmp), width, height, taken));
}
