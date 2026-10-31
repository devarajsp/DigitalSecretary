using System.Globalization;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>How hard to compress (the JPEG / WebP / AVIF quality used).</summary>
public enum ImageQuality
{
    /// <summary>JPEG quality 90: no visible difference, even zoomed in. The default.</summary>
    VisuallyLossless,

    /// <summary>JPEG quality 85: differences only when zooming in a lot.</summary>
    Balanced,

    /// <summary>JPEG quality 80: fine on screens; fine detail may soften in large prints.</summary>
    Smallest
}

/// <summary>A modern format to convert to instead of JPEG / PNG.</summary>
public enum ModernFormat
{
    /// <summary>Keep JPEG / PNG (works everywhere). The default.</summary>
    None,

    /// <summary>WebP: ~30% smaller than JPEG; opens in browsers and Windows, not in every old program.</summary>
    WebP,

    /// <summary>AVIF: ~50% smaller than JPEG; newest, fewer programs open it.</summary>
    Avif
}

/// <summary>What would be done to a picture.</summary>
public enum ImageAction
{
    None,

    /// <summary>Re-save a JPEG at a lower (still invisible) quality, camera details kept.</summary>
    RecompressJpeg,

    /// <summary>A photo stored without compression (PNG / BMP / TIFF) becomes a JPEG.</summary>
    PhotoToJpeg,

    /// <summary>A BMP / TIFF becomes a PNG: no loss at all.</summary>
    LosslessToPng,

    /// <summary>Convert to WebP or AVIF (needs ffmpeg).</summary>
    ToModern
}

/// <summary>The choices that change the advice. Changing them re-advises instantly; no rescan.</summary>
public sealed record ImageAdviceOptions
{
    public ImageQuality Quality { get; init; } = ImageQuality.VisuallyLossless;
    public bool RecompressJpegs { get; init; } = true;
    public bool PhotosToJpeg { get; init; } = true;
    public bool LosslessToPng { get; init; } = true;

    /// <summary>Shrink pictures whose longer side exceeds <see cref="MaxLongSide"/>. Off by default: it does remove detail.</summary>
    public bool Downscale { get; init; }

    public int MaxLongSide { get; init; } = 3840;
    public ModernFormat Modern { get; init; } = ModernFormat.None;
    public int MinSavingPercent { get; init; } = 20;

    /// <summary>Pictures smaller than this are not worth the trouble.</summary>
    public long MinBytes { get; init; } = 150 * 1024;
}

/// <summary>One picture's advice: verdict, action, output extension, estimated size and the reason in plain words.</summary>
public sealed record ImageAdvice(Verdict Verdict, ImageAction Action, string Extension, long EstimatedBytes, string Reason, int TargetWidth, int TargetHeight)
{
    public long SavingFrom(long size) => Verdict == Verdict.Skip ? 0 : Math.Max(0, size - EstimatedBytes);
}

/// <summary>
/// Decides, per picture, whether and how it can be made smaller without visible loss - the "help me choose"
/// part. JPEGs are judged by the quality they were saved at (read from the file): a camera's 92-98 can be
/// re-saved at 90 with no visible change and a large saving, but a JPEG already at 85 is left alone because
/// saving it again would only add loss. Pictures stored without compression are judged by what they show:
/// photos may become JPEGs, graphics (screenshots, text, transparency) only ever lose-lessly become PNG.
/// Downscaling and modern formats are never pre-selected ("Your call") because they trade detail or
/// compatibility. Estimates use typical bits per pixel; the real result is measured and a copy that does not
/// save enough is thrown away.
/// </summary>
public static class ImageAdvisor
{
    /// <summary>From this share saved a change is "Recommended" rather than "Your call".</summary>
    public const int RecommendedPercent = 40;

    /// <summary>A JPEG re-save is quality-checked and keeps everything, so a smaller saving is already worth recommending.</summary>
    public const int RecommendedJpegPercent = 30;

    public static ImageAdvice Advise(ImageFacts f, ImageAdviceOptions o)
    {
        if (Unsuitable(f, o) is { } why)
        {
            return Skip(f, why);
        }
        var (w, h) = TargetSize(f, o);
        var shrinking = w < f.Width;
        var pixelShare = (double)w * h / Math.Max(1, f.Pixels);
        var advice = o.Modern != ModernFormat.None && (f.IsJpeg || f.Kind == PictureKind.Photo) && !f.HasAlpha
            ? Modern(f, o, w, h)
            : f.IsJpeg ? Jpeg(f, o, w, h, shrinking, pixelShare) : Lossless(f, o, w, h);
        return Finish(f, o, advice, shrinking);
    }

    /// <summary>The JPEG quality used for each choice.</summary>
    public static int TargetJpegQuality(ImageQuality q) => q switch
    {
        ImageQuality.Balanced => 85,
        ImageQuality.Smallest => 80,
        _ => 90
    };

    /// <summary>
    /// Typical bits per pixel of a photo JPEG at a quality (camera-like content, 4:2:0). Used only to
    /// estimate; the real size is measured after encoding.
    /// </summary>
    public static double JpegBitsPerPixel(int quality) => quality switch
    {
        >= 98 => 5.0,
        >= 95 => 3.6,
        >= 92 => 2.8,
        >= 90 => 2.4,
        >= 88 => 2.1,
        >= 85 => 1.8,
        >= 80 => 1.45,
        >= 75 => 1.2,
        _ => 1.0
    };

    /// <summary>The size after downscaling (aspect kept), or the picture's own size.</summary>
    public static (int W, int H) TargetSize(ImageFacts f, ImageAdviceOptions o)
    {
        var longSide = Math.Max(f.Width, f.Height);
        if (!o.Downscale || o.MaxLongSide <= 0 || longSide <= o.MaxLongSide)
        {
            return (f.Width, f.Height);
        }
        var scale = (double)o.MaxLongSide / longSide;
        return (Math.Max(1, (int)Math.Round(f.Width * scale)), Math.Max(1, (int)Math.Round(f.Height * scale)));
    }

    public static string? Unsuitable(ImageFacts f, ImageAdviceOptions o)
    {
        if (f.Untouchable.Length > 0)
        {
            return f.Untouchable;
        }
        if (f.Pixels == 0)
        {
            return "Could not read the picture.";
        }
        return f.Size < o.MinBytes ? "Already small (" + ByteSize.Format(f.Size) + ")." : null;
    }

    private static ImageAdvice Jpeg(ImageFacts f, ImageAdviceOptions o, int w, int h, bool shrinking, double pixelShare)
    {
        var target = TargetJpegQuality(o.Quality);
        var saved = f.JpegQuality > 0 ? f.JpegQuality : 92;
        if (!o.RecompressJpegs && !shrinking)
        {
            return Skip(f, "Re-compressing JPEGs is switched off.");
        }
        if (saved <= target + 2 && !shrinking)
        {
            return Skip(f, "Already saved at quality ~" + saved + " - saving it again would add loss for little gain.");
        }
        var quality = Math.Min(saved, target);
        var estimate = (long)(f.Size * pixelShare * JpegBitsPerPixel(quality) / JpegBitsPerPixel(saved));
        var reason = "JPEG at quality ~" + saved + " (" + Bpp(f) + "); quality " + quality + " looks the same";
        return new ImageAdvice(Verdict.Recommended, ImageAction.RecompressJpeg, ".jpg", estimate, reason, w, h);
    }

    private static ImageAdvice Lossless(ImageFacts f, ImageAdviceOptions o, int w, int h)
    {
        var name = f.Format.ToUpperInvariant();
        if (f.Kind == PictureKind.Photo && !f.HasAlpha && o.PhotosToJpeg)
        {
            var quality = TargetJpegQuality(o.Quality);
            var estimate = (long)((double)w * h * JpegBitsPerPixel(quality) / 8);
            return new ImageAdvice(Verdict.Recommended, ImageAction.PhotoToJpeg, ".jpg", estimate, "A photo stored as " + name + " (" + Bpp(f) + "); as a quality-" + quality + " JPEG it looks the same", w, h);
        }
        if (f.Format is "bmp" or "tiff" && o.LosslessToPng)
        {
            var share = f.Kind == PictureKind.Photo ? 0.55 : 0.2;
            return new ImageAdvice(Verdict.Recommended, ImageAction.LosslessToPng, ".png", (long)(f.Size * share * w * h / Math.Max(1, f.Pixels)), name + " stored uncompressed; PNG keeps every pixel exactly", w, h);
        }
        var what = f.HasAlpha ? "has see-through parts" : f.Kind == PictureKind.Graphic ? "is a graphic or screenshot" : "is not a photo";
        return Skip(f, name + " that " + what + " - kept lossless so text and edges stay sharp.");
    }

    private static ImageAdvice Modern(ImageFacts f, ImageAdviceOptions o, int w, int h)
    {
        var quality = TargetJpegQuality(o.Quality);
        var share = o.Modern == ModernFormat.WebP ? 0.7 : 0.5;
        var estimate = (long)((double)w * h * JpegBitsPerPixel(quality) * share / 8);
        if (f.IsJpeg && f.JpegQuality is > 0 and < 88)
        {
            estimate = (long)Math.Min(estimate, f.Size * share * w * h / Math.Max(1, f.Pixels));
        }
        var ext = o.Modern == ModernFormat.WebP ? ".webp" : ".avif";
        return new ImageAdvice(Verdict.Optional, ImageAction.ToModern, ext, estimate,
            ext.TrimStart('.').ToUpperInvariant() + " is smaller for the same look, but camera details are not kept and some programs cannot open it", w, h);
    }

    /// <summary>Applies the minimum saving, the downscale note and the Recommended / Your call split.</summary>
    private static ImageAdvice Finish(ImageFacts f, ImageAdviceOptions o, ImageAdvice a, bool shrinking)
    {
        if (a.Verdict == Verdict.Skip)
        {
            return a;
        }
        var percent = 100.0 * (f.Size - a.EstimatedBytes) / Math.Max(1, f.Size);
        if (percent < o.MinSavingPercent)
        {
            return Skip(f, "Would save only ~" + (int)Math.Max(0, Math.Round(percent)) + "% (" + Bpp(f) + ").");
        }
        var reason = a.Reason + (shrinking ? "; shrunk to " + a.TargetWidth + "x" + a.TargetHeight + " (removes fine detail)" : "") + ".";
        var recommendAt = a.Action == ImageAction.RecompressJpeg ? RecommendedJpegPercent : RecommendedPercent;
        var verdict = a.Verdict == Verdict.Optional || shrinking || percent < recommendAt ? Verdict.Optional : Verdict.Recommended;
        return a with { Verdict = verdict, Reason = reason };
    }

    private static ImageAdvice Skip(ImageFacts f, string why) => new(Verdict.Skip, ImageAction.None, "", f.Size, why, f.Width, f.Height);

    private static string Bpp(ImageFacts f) => f.BitsPerPixel.ToString("0.0", CultureInfo.InvariantCulture) + " bits/pixel";
}
