using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>
/// Makes the smaller picture. JPEG and PNG are written by GDI+ (built into Windows) and keep the camera
/// details (EXIF: date taken, camera, location, orientation) - when a picture is shrunk its stored pixel size
/// is updated too. WebP and AVIF are written by ffmpeg from an upright, lossless intermediate (those formats'
/// camera details are not carried over; the help text says so). Returns null on success, or the reason.
/// </summary>
public static class ImageEncoder
{
    private const int OrientationTag = 0x0112;
    private const int PixelXDimensionTag = 0xA002;
    private const int PixelYDimensionTag = 0xA003;

    public static string? Encode(ImageFacts facts, ImageAdvice advice, ImageQuality quality, string output, MediaToolPaths? tools, CancellationToken cancel)
    {
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(facts.Path));
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            return advice.Action switch
            {
                ImageAction.RecompressJpeg or ImageAction.PhotoToJpeg => SaveJpeg(source, advice, ImageAdvisor.TargetJpegQuality(quality), output),
                ImageAction.LosslessToPng => SavePng(source, advice, output),
                ImageAction.ToModern => SaveModern(source, advice, quality, output, tools, cancel),
                _ => "Nothing to do."
            };
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException or IOException or UnauthorizedAccessException)
        {
            return "Could not convert the picture: " + ex.Message;
        }
    }

    /// <summary>The ffmpeg arguments for a WebP or AVIF still at the chosen quality.</summary>
    public static IReadOnlyList<string> ModernArgs(string input, string output, ImageQuality quality)
    {
        var jpegLike = ImageAdvisor.TargetJpegQuality(quality);
        return Path.GetExtension(output).Equals(".webp", StringComparison.OrdinalIgnoreCase)
            ? ["-hide_banner", "-nostdin", "-y", "-i", input, "-c:v", "libwebp", "-quality", (jpegLike - 2).ToString(System.Globalization.CultureInfo.InvariantCulture), "-preset", "photo", output]
            : ["-hide_banner", "-nostdin", "-y", "-i", input, "-c:v", "libaom-av1", "-still-picture", "1", "-crf", (110 - jpegLike).ToString(System.Globalization.CultureInfo.InvariantCulture), "-b:v", "0", "-cpu-used", "6", "-pix_fmt", "yuv420p", output];
    }

    /// <summary>The GDI+ turn/flip that makes a picture with EXIF orientation 1-8 upright.</summary>
    public static RotateFlipType Upright(int orientation) => orientation switch
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

    private static string? SaveJpeg(Image source, ImageAdvice advice, int quality, string output)
    {
        using var pixels = Pixels(source, advice, opaque: true);
        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        pixels.Save(output, encoder, parameters);
        return null;
    }

    private static string? SavePng(Image source, ImageAdvice advice, string output)
    {
        using var pixels = Pixels(source, advice, opaque: false);
        pixels.Save(output, ImageFormat.Png);
        return null;
    }

    private static string? SaveModern(Image source, ImageAdvice advice, ImageQuality quality, string output, MediaToolPaths? tools, CancellationToken cancel)
    {
        if (tools is null)
        {
            return "WebP / AVIF need ffmpeg - install it once (" + FfmpegLocator.InstallCommand + ").";
        }
        var upright = Path.Combine(Path.GetTempPath(), "ds_reduce_" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var pixels = Pixels(source, advice, opaque: true))
            {
                pixels.RotateFlip(Upright(Orientation(source)));
                pixels.Save(upright, ImageFormat.Png);
            }
            var result = ToolRunner.Run(tools.Ffmpeg, ModernArgs(upright, output, quality), _ => { }, cancel);
            return result.Cancelled ? "Stopped." : result.Succeeded ? null : "ffmpeg failed: " + result.ErrorTail.Split('\n').LastOrDefault()?.Trim();
        }
        finally
        {
            File.Delete(upright);
        }
    }

    /// <summary>
    /// The pixels to save: the source itself when nothing changes size or transparency, otherwise a new bitmap
    /// (high-quality resampling, white behind transparent pixels for JPEG) carrying the source's camera details.
    /// The caller disposes the result; disposing the source itself is harmless because the caller owns it too.
    /// </summary>
    private static Bitmap Pixels(Image source, ImageAdvice advice, bool opaque)
    {
        var resize = advice.TargetWidth != source.Width || advice.TargetHeight != source.Height;
        if (!resize && source is Bitmap bitmap)
        {
            // Same size: an exact pixel copy (no resampling), so a PNG keeps every pixel bit for bit.
            var exact = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), opaque ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb);
            CopyDetails(source, exact, resized: false);
            return exact;
        }
        var copy = new Bitmap(advice.TargetWidth, advice.TargetHeight, opaque ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb);
        copy.SetResolution(source.HorizontalResolution, source.VerticalResolution);
        using (var g = Graphics.FromImage(copy))
        {
            if (opaque)
            {
                g.Clear(Color.White);
            }
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = resize ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.None;
            using var wrap = new ImageAttributes();
            wrap.SetWrapMode(WrapMode.TileFlipXY); // no dark fringe at the borders when resampling
            g.DrawImage(source, new Rectangle(0, 0, copy.Width, copy.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, wrap);
        }
        CopyDetails(source, copy, resize);
        return copy;
    }

    /// <summary>Copies every camera detail (EXIF / XMP items GDI+ exposes); a shrunk picture gets its new pixel size.</summary>
    private static void CopyDetails(Image source, Image target, bool resized)
    {
        foreach (var item in source.PropertyItems)
        {
            if (resized && item.Id is PixelXDimensionTag or PixelYDimensionTag)
            {
                item.Type = 4; // LONG
                item.Len = 4;
                item.Value = BitConverter.GetBytes(item.Id == PixelXDimensionTag ? target.Width : target.Height);
            }
            try
            {
                target.SetPropertyItem(item);
            }
            catch (ArgumentException)
            {
                // A detail GDI+ cannot write back; the rest are kept.
            }
        }
    }

    private static int Orientation(Image image)
    {
        try
        {
            var item = image.GetPropertyItem(OrientationTag);
            return item?.Value is { Length: >= 2 } v ? BitConverter.ToUInt16(v, 0) : 1;
        }
        catch (ArgumentException)
        {
            return 1;
        }
    }
}
