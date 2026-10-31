using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>
/// The picture-quality guard: PSNR (peak signal-to-noise ratio, in dB) of the copy's brightness against the
/// original's, pixel by pixel at full size. Above ~40 dB no difference is visible; JPEG at quality 90 of a
/// photo scores ~40-45. A copy below the limit for the chosen quality is thrown away. Only same-size
/// copies are compared (a shrunk or WebP/AVIF copy is checked by size and readability only).
/// </summary>
public static class ImageQualityCheck
{
    /// <summary>Lowest PSNR accepted for each quality.</summary>
    public static double MinimumPsnr(ImageQuality quality) => quality switch
    {
        ImageQuality.Balanced => 36,
        ImageQuality.Smallest => 34,
        _ => 38
    };

    /// <summary>PSNR of two equally long brightness arrays (0-255); +infinity when identical.</summary>
    public static double Psnr(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            throw new ArgumentException("The pictures differ in size.");
        }
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var d = a[i] - b[i];
            sum += d * d;
        }
        var mse = sum / a.Length;
        return mse == 0 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255.0 / mse);
    }

    /// <summary>PSNR between two picture files of the same size, or null when they cannot be compared.</summary>
    public static double? Compare(string original, string copy)
    {
        try
        {
            var a = Brightness(original);
            var b = Brightness(copy);
            return a.Width == b.Width && a.Height == b.Height ? Psnr(a.Values, b.Values) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException or IOException)
        {
            return null;
        }
    }

    /// <summary>Brightness (Rec. 601 luma) of every pixel, with transparent pixels shown over white as in a JPEG.</summary>
    public static (int Width, int Height, byte[] Values) Brightness(string path)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        using var image = Image.FromStream(stream);
        using var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.DrawImage(image, new Rectangle(0, 0, bmp.Width, bmp.Height));
        }
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[data.Stride];
            var values = new byte[bmp.Width * bmp.Height];
            for (var y = 0; y < bmp.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);
                for (var x = 0; x < bmp.Width; x++)
                {
                    var p = x * 3; // B, G, R
                    values[y * bmp.Width + x] = (byte)((row[p + 2] * 299 + row[p + 1] * 587 + row[p] * 114 + 500) / 1000);
                }
            }
            return (bmp.Width, bmp.Height, values);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
