using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>
/// Reads <see cref="ImageFacts"/> with GDI+ (built into Windows). The picture is decoded once, at a small
/// size, to tell photos from graphics (<see cref="Classify"/>) and to look for transparent pixels; the JPEG
/// quality comes from the file's header (<see cref="JpegQuality"/>).
/// </summary>
public static class ImageProbe
{
    /// <summary>Picture files this feature reads. HEIC / WebP / AVIF are already efficient and are not touched.</summary>
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".jpe", ".png", ".bmp", ".dib", ".tif", ".tiff"
    };

    /// <summary>Side of the small copy used for the photo/graphic check.</summary>
    private const int SampleSide = 96;

    public static bool IsPicture(string path) => Extensions.Contains(Path.GetExtension(path));

    /// <summary>The facts for one file, or null when it is not a readable picture.</summary>
    public static ImageFacts? Read(FileInfo file)
    {
        try
        {
            var bytes = File.ReadAllBytes(file.FullName);
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            var format = FormatOf(image.RawFormat);
            if (format.Length == 0)
            {
                return null;
            }
            var untouchable = Untouchable(image);
            var (kind, alpha) = untouchable.Length == 0 ? Inspect(image) : (PictureKind.Unknown, false);
            return new ImageFacts
            {
                Path = file.FullName,
                Size = file.Length,
                ModifiedUtc = file.LastWriteTimeUtc,
                Format = format,
                Width = image.Width,
                Height = image.Height,
                JpegQuality = format == "jpeg" ? JpegQuality.Estimate(bytes.AsSpan(0, Math.Min(bytes.Length, 65_536))) : 0,
                HasAlpha = alpha,
                Kind = kind,
                Untouchable = untouchable
            };
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>"jpeg", "png", "bmp", "tiff", or "" for anything else.</summary>
    public static string FormatOf(ImageFormat raw)
    {
        if (raw.Guid == ImageFormat.Jpeg.Guid)
        {
            return "jpeg";
        }
        if (raw.Guid == ImageFormat.Png.Guid)
        {
            return "png";
        }
        if (raw.Guid == ImageFormat.Bmp.Guid || raw.Guid == ImageFormat.MemoryBmp.Guid)
        {
            return "bmp";
        }
        return raw.Guid == ImageFormat.Tiff.Guid ? "tiff" : "";
    }

    /// <summary>Why a picture must be left alone: 16-bit colour, CMYK print files, several pages, or an animation.</summary>
    public static string Untouchable(Image image)
    {
        if (image.PixelFormat is PixelFormat.Format48bppRgb or PixelFormat.Format64bppArgb or PixelFormat.Format64bppPArgb or PixelFormat.Format16bppGrayScale)
        {
            return "16-bit colour - kept as it is so no detail is lost.";
        }
        if (((ImageFlags)image.Flags & ImageFlags.ColorSpaceCmyk) != 0 || ((ImageFlags)image.Flags & ImageFlags.ColorSpaceYcck) != 0)
        {
            return "CMYK print file - kept as it is.";
        }
        foreach (var dimension in image.FrameDimensionsList)
        {
            if (image.GetFrameCount(new FrameDimension(dimension)) > 1)
            {
                return "Several pages or frames - kept as it is.";
            }
        }
        return "";
    }

    /// <summary>Decodes a small copy and classifies it; also reports whether any pixel is see-through.</summary>
    private static (PictureKind Kind, bool Alpha) Inspect(Image image)
    {
        var (w, h) = SmallSize(image.Width, image.Height);
        using var small = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.NearestNeighbor; // keep edges crisp: blending would invent colours
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(image, new Rectangle(0, 0, w, h));
        }
        var pixels = new int[w * h];
        var data = small.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        }
        finally
        {
            small.UnlockBits(data);
        }
        var alpha = Image.IsAlphaPixelFormat(image.PixelFormat) && pixels.Any(p => (uint)p >> 24 < 250);
        return (Classify(pixels, w), alpha);
    }

    /// <summary>
    /// Photo or graphic, from a small copy's pixels (ARGB). Photos have many distinct colours and few
    /// identical neighbours; screenshots, drawings and text have few colours and long flat runs. A graphic
    /// saved as JPEG would show smudges around its edges, so only photos are ever turned into JPEGs.
    /// </summary>
    public static PictureKind Classify(int[] pixels, int width)
    {
        if (pixels.Length < 64 || width <= 0)
        {
            return PictureKind.Unknown;
        }
        var distinct = pixels.Select(p => p & 0xFFFFFF).Distinct().Count();
        var same = 0;
        for (var i = 1; i < pixels.Length; i++)
        {
            if (i % width != 0 && (pixels[i] & 0xFFFFFF) == (pixels[i - 1] & 0xFFFFFF))
            {
                same++;
            }
        }
        var colourShare = (double)distinct / pixels.Length;
        var flatShare = (double)same / pixels.Length;
        return colourShare > 0.25 && flatShare < 0.35 ? PictureKind.Photo : PictureKind.Graphic;
    }

    private static (int W, int H) SmallSize(int w, int h)
    {
        var scale = Math.Min(1.0, (double)SampleSide / Math.Max(w, h));
        return (Math.Max(1, (int)Math.Round(w * scale)), Math.Max(1, (int)Math.Round(h * scale)));
    }
}
