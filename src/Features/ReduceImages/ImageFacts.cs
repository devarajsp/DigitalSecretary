namespace DigitalSecretary.Features.ReduceImages;

/// <summary>What a picture looks like inside: a photo (smooth, many colours) or a graphic (screenshot, drawing, text).</summary>
public enum PictureKind
{
    Unknown,
    Photo,
    Graphic
}

/// <summary>
/// What a scan learns about one picture: format, size in pixels, JPEG quality (read from the file's own
/// quantisation tables), transparency, photo-or-graphic, and anything that makes it unsafe to touch
/// (16-bit colour, CMYK, several pages). Everything the advisor needs.
/// </summary>
public sealed record ImageFacts : IFileRecord
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public DateTime ModifiedUtc { get; init; }

    /// <summary>"jpeg", "png", "bmp" or "tiff".</summary>
    public string Format { get; init; } = "";

    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>The JPEG quality the file was saved at (1-100), 0 when unknown or not a JPEG.</summary>
    public int JpegQuality { get; init; }

    /// <summary>Some pixels are see-through: such a picture can never become a JPEG.</summary>
    public bool HasAlpha { get; init; }

    public PictureKind Kind { get; init; }

    /// <summary>Why the picture must not be touched (e.g. "16-bit colour"), or empty.</summary>
    public string Untouchable { get; init; } = "";

    public long Pixels => (long)Width * Height;

    /// <summary>Bits stored per pixel - a camera JPEG is ~2-4, a well-compressed one ~1.5, an uncompressed BMP 24.</summary>
    public double BitsPerPixel => Pixels > 0 ? Size * 8.0 / Pixels : 0;

    public bool IsJpeg => Format == "jpeg";

    public bool IsLossless => Format is "png" or "bmp" or "tiff";

    public bool Matches(long size, DateTime modifiedUtc) => Size == size && ModifiedUtc == modifiedUtc;
}

/// <summary>Per-folder cache of <see cref="ImageFacts"/> (gzip, under the feature's data folder).</summary>
public sealed class ImageFactsCache : FileRecordCache<ImageFacts>
{
    public static string PathFor(string dataDirectory, string root) => CacheFiles.PathFor(dataDirectory, root);

    public static ImageFactsCache Load(string file)
    {
        var cache = new ImageFactsCache();
        return TryLoadInto(cache, file, f => f.Width > 0 && f.Format.Length > 0) ? cache : new ImageFactsCache();
    }
}
