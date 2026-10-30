using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>What reading one picture produced: a small greyscale copy plus what the file header says.</summary>
/// <param name="Gray">Greyscale version of the thumbnail, for hashing and sharpness.</param>
/// <param name="Width">Full picture width in pixels, or 0 when the format's header could not be read.</param>
/// <param name="Height">Full picture height in pixels, or 0 when unknown.</param>
/// <param name="TakenUtc">When the photo was taken (EXIF), or null when the file does not say.</param>
public sealed record PictureSample(GrayImage Gray, int Width, int Height, DateTime? TakenUtc);

/// <summary>Produces a thumbnail bitmap for a picture file, or null if it cannot.</summary>
public interface IThumbnailProvider
{
    /// <summary>A bitmap no larger than <paramref name="size"/> on its long side, upright; caller disposes.</summary>
    Bitmap? GetThumbnail(string path, int size);
}

/// <summary>
/// Thumbnails from the Windows Shell (<c>IShellItemImageFactory</c>) - the same engine Explorer uses.
/// It reads every format Windows has a codec for (JPEG, PNG, HEIC, WebP, camera RAW ...), honours EXIF
/// rotation, and is fast because Windows caches thumbnails. Asks for real thumbnails only, never a
/// file-type icon, so an unreadable file returns null instead of a misleading picture.
/// </summary>
public sealed class ShellThumbnailProvider : IThumbnailProvider
{
    private const int BiggerSizeOk = 0x1;
    private const int ThumbnailOnly = 0x8;
    private static readonly Guid ImageFactoryId = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    public Bitmap? GetThumbnail(string path, int size)
    {
        IntPtr hbitmap = IntPtr.Zero;
        try
        {
            var iid = ImageFactoryId;
            if (NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0 || factory is null)
            {
                return null;
            }
            try
            {
                if (factory.GetImage(new NativeSize(size, size), BiggerSizeOk | ThumbnailOnly, out hbitmap) != 0 || hbitmap == IntPtr.Zero)
                {
                    return null;
                }
            }
            finally
            {
                Marshal.ReleaseComObject(factory);
            }
            using var raw = Image.FromHbitmap(hbitmap);
            return PictureReader.FitWithin(raw, size);
        }
        catch (Exception ex) when (ex is COMException or ExternalException or ArgumentException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (hbitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(hbitmap);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize(int cx, int cy)
    {
        public readonly int Cx = cx;
        public readonly int Cy = cy;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, int flags, out IntPtr phbm);
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr bindContext, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr handle);
    }
}

/// <summary>
/// Thumbnails decoded with GDI+ (System.Drawing): JPEG, PNG, GIF, BMP, TIFF. The fallback when the
/// Shell cannot help (no thumbnail handler, or a headless session). Applies the EXIF orientation tag
/// so the result is upright, matching what the Shell returns.
/// </summary>
public sealed class GdiThumbnailProvider : IThumbnailProvider
{
    public Bitmap? GetThumbnail(string path, int size)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            var flip = PictureReader.ExifRotation(image);
            var thumb = PictureReader.FitWithin(image, size);
            if (flip != RotateFlipType.RotateNoneFlipNone)
            {
                thumb.RotateFlip(flip);
            }
            return thumb;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or OutOfMemoryException or ExternalException)
        {
            return null;
        }
    }
}

/// <summary>
/// Reads one picture for comparison: a thumbnail from the first provider that can make one (Shell,
/// then GDI+), turned into greyscale, plus the dimensions and EXIF date from the file header when
/// GDI+ understands the format. Never throws for a bad file - it returns null and the scan skips it.
/// </summary>
public sealed class PictureReader
{
    /// <summary>Long side of the thumbnail the fingerprint is computed from.</summary>
    public const int SampleSize = 256;

    private const int ExifOrientationTag = 0x0112;
    private const int ExifDateTakenTag = 0x9003;
    private const int ExifSubSecondsTag = 0x9291;

    private readonly IReadOnlyList<IThumbnailProvider> _providers;

    public PictureReader(params IThumbnailProvider[] providers) => _providers = providers;

    /// <summary>The usual chain: the Windows Shell first, GDI+ second.</summary>
    public static PictureReader Default() => new(new ShellThumbnailProvider(), new GdiThumbnailProvider());

    /// <summary>Reads <paramref name="path"/>, or returns null when no provider can decode it.</summary>
    public PictureSample? Read(string path)
    {
        Bitmap? thumb = null;
        foreach (var provider in _providers)
        {
            thumb = provider.GetThumbnail(path, SampleSize);
            if (thumb is not null)
            {
                break;
            }
        }
        if (thumb is null)
        {
            return null;
        }

        using (thumb)
        {
            var (width, height, taken) = ReadHeader(path);
            return new PictureSample(GrayImage.FromBitmap(thumb), width, height, taken);
        }
    }

    /// <summary>
    /// Width, height and EXIF date from the header, without decoding the pixels. Zeros and null for
    /// formats GDI+ does not know (HEIC, RAW, WebP); those are still compared, just without size data.
    /// The EXIF time has no zone, so it is taken as local time and converted to UTC.
    /// </summary>
    public static (int Width, int Height, DateTime? TakenUtc) ReadHeader(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            var swap = ExifRotation(image) is RotateFlipType.Rotate90FlipNone or RotateFlipType.Rotate270FlipNone
                or RotateFlipType.Rotate90FlipX or RotateFlipType.Rotate270FlipX;
            var ids = image.PropertyIdList;
            var taken = ids.Contains(ExifDateTakenTag) ? ParseExifDate(image.GetPropertyItem(ExifDateTakenTag)?.Value) : null;
            if (taken is { } t && ids.Contains(ExifSubSecondsTag))
            {
                taken = t + ParseSubSeconds(image.GetPropertyItem(ExifSubSecondsTag)?.Value);
            }
            return swap ? (image.Height, image.Width, taken) : (image.Width, image.Height, taken);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or OutOfMemoryException or ExternalException)
        {
            return (0, 0, null);
        }
    }

    /// <summary>Parses an EXIF "yyyy:MM:dd HH:mm:ss" value; null if absent or malformed.</summary>
    public static DateTime? ParseExifDate(byte[]? value)
    {
        if (value is null || value.Length < 19)
        {
            return null;
        }
        var text = System.Text.Encoding.ASCII.GetString(value, 0, 19);
        return DateTime.TryParseExact(text, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local)
            ? local.ToUniversalTime()
            : null;
    }

    /// <summary>
    /// The EXIF sub-second field: ASCII digits that are the fraction after the decimal point
    /// ("45" = 0.45 s, "045" = 0.045 s). Zero when absent or not digits.
    /// </summary>
    public static TimeSpan ParseSubSeconds(byte[]? value)
    {
        if (value is null)
        {
            return TimeSpan.Zero;
        }
        var digits = new string(System.Text.Encoding.ASCII.GetString(value).TakeWhile(char.IsAsciiDigit).ToArray());
        return digits.Length == 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(double.Parse("0." + digits, CultureInfo.InvariantCulture));
    }

    /// <summary>The rotate/flip that makes an image with an EXIF orientation tag upright.</summary>
    public static RotateFlipType ExifRotation(Image image)
    {
        if (!image.PropertyIdList.Contains(ExifOrientationTag) || image.GetPropertyItem(ExifOrientationTag)?.Value is not { Length: > 0 } value)
        {
            return RotateFlipType.RotateNoneFlipNone;
        }
        return value[0] switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
    }

    /// <summary>A new bitmap of <paramref name="image"/> scaled to fit <paramref name="size"/> (never enlarged).</summary>
    public static Bitmap FitWithin(Image image, int size)
    {
        var scale = Math.Min(1d, size / (double)Math.Max(image.Width, image.Height));
        var w = Math.Max(1, (int)Math.Round(image.Width * scale));
        var h = Math.Max(1, (int)Math.Round(image.Height * scale));
        var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.White);
        g.DrawImage(image, new Rectangle(0, 0, w, h));
        return result;
    }
}
