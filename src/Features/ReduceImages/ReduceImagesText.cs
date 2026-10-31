using System.Globalization;
using System.Text;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>
/// All words and numbers the Reduce Size - Images screen shows, as pure functions: the help for each option,
/// list cells, the selection summary, the savings line and the saved report.
/// </summary>
public static class ReduceImagesText
{
    public static readonly (ImageQuality Value, string Text)[] QualityChoices =
    [
        (ImageQuality.VisuallyLossless, "Visually lossless - JPEG 90 (recommended)"),
        (ImageQuality.Balanced, "Balanced - JPEG 85"),
        (ImageQuality.Smallest, "Smallest - JPEG 80")
    ];

    public static readonly (ModernFormat Value, string Text)[] ModernChoices =
    [
        (ModernFormat.None, "Keep JPEG / PNG (works everywhere)"),
        (ModernFormat.WebP, "Convert photos to WebP"),
        (ModernFormat.Avif, "Convert photos to AVIF")
    ];

    /// <summary>Plain-words guidance for the current choices: what is safe, what trades something.</summary>
    public static string Help(ImageAdviceOptions o, bool ffmpegFound)
    {
        var text = new StringBuilder("Safe and recommended: re-compress big JPEGs and turn photos stored as PNG/BMP/TIFF into JPEGs - no visible change, camera details and dates kept; "
            + "BMP/TIFF graphics become PNG with no loss at all. Screenshots and see-through pictures are never made JPEG. ");
        if (o.Downscale)
        {
            text.Append("Downscaling is ON: pictures larger than ").Append(o.MaxLongSide).Append(" px are shrunk - fine for screens, but it removes detail for big prints or crops. ");
        }
        if (o.Modern != ModernFormat.None)
        {
            text.Append(o.Modern == ModernFormat.WebP ? "WebP" : "AVIF")
                .Append(" is ON: smaller files, but camera details (date taken, location) are not kept and some programs cannot open them")
                .Append(ffmpegFound ? ". " : " - and it needs ffmpeg (" + FfmpegLocator.InstallCommand + "). ");
        }
        text.Append(o.Quality switch
        {
            ImageQuality.Balanced => "Balanced: differences only when zooming in a lot.",
            ImageQuality.Smallest => "Smallest: fine on screens; fine detail may soften in large prints.",
            _ => "Visually lossless: no visible difference even zoomed in."
        });
        return text.ToString();
    }

    /// <summary>List cells: name, size, pixels, format, advice, action, new size, saving, why.</summary>
    public static string[] Cells(ImageFacts f, ImageAdvice a) =>
    [
        Path.GetFileName(f.Path),
        ByteSize.Format(f.Size),
        f.Width + "x" + f.Height,
        Format(f),
        VerdictLabel(a.Verdict),
        ActionLabel(a),
        a.Verdict == Verdict.Skip ? "" : "~" + ByteSize.Format(a.EstimatedBytes),
        a.Verdict == Verdict.Skip ? "" : "~" + ByteSize.Format(a.SavingFrom(f.Size)) + " (" + Percent(a.SavingFrom(f.Size), f.Size) + ")",
        a.Reason
    ];

    /// <summary>"JPEG q95", "PNG photo", "BMP graphic".</summary>
    public static string Format(ImageFacts f)
        => f.Format.ToUpperInvariant() + (f.IsJpeg
            ? (f.JpegQuality > 0 ? " q" + f.JpegQuality : "")
            : f.Kind switch { PictureKind.Photo => " photo", PictureKind.Graphic => " graphic", _ => "" });

    public static string VerdictLabel(Verdict v) => v switch
    {
        Verdict.Recommended => "Recommended",
        Verdict.Optional => "Your call",
        _ => "Skip"
    };

    public static string ActionLabel(ImageAdvice a) => a.Action switch
    {
        ImageAction.RecompressJpeg => a.TargetWidth > 0 && a.Reason.Contains("shrunk", StringComparison.Ordinal) ? "Shrink + JPEG" : "Re-save JPEG",
        ImageAction.PhotoToJpeg => "To JPEG",
        ImageAction.LosslessToPng => "To PNG (no loss)",
        ImageAction.ToModern => "To " + a.Extension.TrimStart('.').ToUpperInvariant(),
        _ => ""
    };

    public static string Percent(long part, long whole)
        => whole > 0 ? ((int)Math.Round(100.0 * part / whole)).ToString(CultureInfo.InvariantCulture) + "%" : "0%";

    public static string Summary(IReadOnlyList<(ImageFacts Facts, ImageAdvice Advice)> rows)
    {
        var recommended = rows.Count(r => r.Advice.Verdict == Verdict.Recommended);
        var optional = rows.Count(r => r.Advice.Verdict == Verdict.Optional);
        return rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " pictures, " + ByteSize.Format(rows.Sum(r => r.Facts.Size)) + ". "
            + recommended.ToString("N0", CultureInfo.InvariantCulture) + " recommended, " + optional.ToString("N0", CultureInfo.InvariantCulture)
            + " your call - up to ~" + ByteSize.Format(rows.Sum(r => r.Advice.SavingFrom(r.Facts.Size))) + " could be saved.";
    }

    public static string Selection(IReadOnlyList<(ImageFacts Facts, ImageAdvice Advice)> selected)
    {
        if (selected.Count == 0)
        {
            return "Nothing selected.";
        }
        var before = selected.Sum(r => r.Facts.Size);
        var after = selected.Sum(r => r.Advice.EstimatedBytes);
        return selected.Count.ToString("N0", CultureInfo.InvariantCulture) + " selected: " + ByteSize.Format(before) + " -> ~" + ByteSize.Format(after) + ", saves ~" + ByteSize.Format(before - after);
    }

    public static string SavedSoFar(SavingsTotal total)
        => total.Files == 0 ? "Nothing reduced yet." : "Saved so far: " + total.Files.ToString("N0", CultureInfo.InvariantCulture) + " picture" + (total.Files == 1 ? "" : "s") + ", " + ByteSize.Format(Math.Max(0, total.Saved));

    public static string RunSummary(ImageReduceOutcome o, RemoveAction action)
    {
        var text = new StringBuilder();
        text.Append("Reduced ").Append(o.Reduced).Append(", saved ").Append(ByteSize.Format(o.Saved)).Append('.');
        if (o.Skipped > 0)
        {
            text.Append(' ').Append(o.Skipped).Append(" kept as they were (see the log).");
        }
        if (o.Failed > 0)
        {
            text.Append(' ').Append(o.Failed).Append(" failed.");
        }
        if (o.Cancelled)
        {
            text.Append(" Stopped.");
        }
        if (o.Reduced > 0)
        {
            text.Append(action == RemoveAction.ReviewFolder
                ? " Originals are in \"" + ImageReduceRunner.ReviewFolderName + "\" - check a few, then delete that folder to free the space."
                : " Originals are in the Recycle Bin - empty it to free the space.");
        }
        return text.ToString();
    }

    public static string Report(string root, IReadOnlyList<(ImageFacts Facts, ImageAdvice Advice)> rows, ImageAdviceOptions o, DateTime now)
    {
        var text = new StringBuilder();
        text.AppendLine("Reduce Size - Images report");
        text.AppendLine("Folder:  " + root);
        text.AppendLine("Made:    " + now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        text.AppendLine("Options: quality " + o.Quality + ", JPEGs " + o.RecompressJpegs + ", photos to JPEG " + o.PhotosToJpeg + ", BMP/TIFF to PNG " + o.LosslessToPng
            + ", downscale " + (o.Downscale ? o.MaxLongSide + " px" : "off") + ", format " + o.Modern + ", minimum saving " + o.MinSavingPercent + "%");
        text.AppendLine(Summary(rows));
        text.AppendLine();
        foreach (var (f, a) in rows.OrderBy(r => r.Advice.Verdict).ThenByDescending(r => r.Advice.SavingFrom(r.Facts.Size)))
        {
            var c = Cells(f, a);
            text.AppendLine(c[4].PadRight(12) + c[1].PadLeft(10) + "  " + (c[6].Length > 0 ? c[6] : "-").PadLeft(10) + "  " + f.Path);
            text.AppendLine(new string(' ', 12) + a.Reason);
        }
        return text.ToString();
    }
}
