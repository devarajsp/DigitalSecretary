using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Serialization;
using System.Text;

namespace DigitalSecretary.UnitTests.Features.ReduceImages;

/// <summary>
/// Real picture files made at test time (nothing binary is committed): a photo-like scene (gradients, soft
/// shapes, fine noise - like a camera picture), a graphic (flat colours and text, like a screenshot), saved
/// as JPEG at any quality with a "date taken" detail, or as PNG / BMP.
/// </summary>
internal static class TestPictures
{
    public const int DateTakenTag = 0x9003;
    public const string DateTaken = "2019:07:14 16:20:00";

    public static Bitmap Photo(int w = 1200, int h = 900, int seed = 1)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var random = new Random(seed);
        using (var g = Graphics.FromImage(bmp))
        {
            using var sky = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(70, 120, 200), Color.FromArgb(230, 190, 140), 70f);
            g.FillRectangle(sky, 0, 0, w, h);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (var i = 0; i < 60; i++)
            {
                using var brush = new SolidBrush(Color.FromArgb(120, random.Next(256), random.Next(256), random.Next(256)));
                g.FillEllipse(brush, random.Next(-100, w), random.Next(-100, h), random.Next(40, 300), random.Next(40, 300));
            }
        }
        AddGrain(bmp, random);
        return bmp;
    }

    public static Bitmap Graphic(int w = 1200, int h = 900)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.FillRectangle(Brushes.SteelBlue, 0, 0, w, 60);
        using var font = new Font("Segoe UI", 12f);
        for (var y = 80; y < h - 20; y += 24)
        {
            g.DrawString("Statement line " + y + "   1,234.56   Paid", font, Brushes.Black, 20, y);
        }
        return bmp;
    }

    /// <summary>Saves <paramref name="bmp"/> as a JPEG at <paramref name="quality"/> with a "date taken" detail.</summary>
    public static string SaveJpeg(Bitmap bmp, string path, int quality, int orientation = 0)
    {
        SetAscii(bmp, DateTakenTag, DateTaken);
        if (orientation > 0)
        {
            SetShort(bmp, 0x0112, (ushort)orientation);
        }
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var p = new EncoderParameters(1);
        p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        bmp.Save(path, codec, p);
        return path;
    }

    public static string Save(Bitmap bmp, string path, ImageFormat format)
    {
        bmp.Save(path, format);
        return path;
    }

    /// <summary>The "date taken" a file carries, or null.</summary>
    public static string? DateTakenOf(string path)
    {
        using var image = Image.FromFile(path);
        return image.PropertyIdList.Contains(DateTakenTag) ? Encoding.ASCII.GetString(image.GetPropertyItem(DateTakenTag)!.Value!).TrimEnd('\0') : null;
    }

    private static void AddGrain(Bitmap bmp, Random random)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        try
        {
            var bytes = new byte[data.Stride * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)Math.Clamp(bytes[i] + random.Next(-6, 7), 0, 255);
            }
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static PropertyItem NewItem()
#pragma warning disable SYSLIB0050 // PropertyItem has no public constructor; this is the documented way to make one.
        => (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
#pragma warning restore SYSLIB0050

    private static void SetAscii(Image image, int id, string value)
    {
        var item = NewItem();
        item.Id = id;
        item.Type = 2;
        item.Value = Encoding.ASCII.GetBytes(value + "\0");
        item.Len = item.Value.Length;
        image.SetPropertyItem(item);
    }

    private static void SetShort(Image image, int id, ushort value)
    {
        var item = NewItem();
        item.Id = id;
        item.Type = 3;
        item.Value = BitConverter.GetBytes(value);
        item.Len = 2;
        image.SetPropertyItem(item);
    }
}
