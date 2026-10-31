using System.Drawing;
using System.Drawing.Drawing2D;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// Made-up pictures for documentation screenshots and tests: simple painted "landscapes" (sky, sun,
/// hills, a house) generated from a seed, plus look-alike variants - a resized copy, a turned copy, a
/// mirrored copy and burst shots. Paths are placeholders under <see cref="RootPath"/>, so no real
/// photo, name or folder ever appears in an image. Deterministic for a given seed.
/// </summary>
public static class SamplePictureSet
{
    /// <summary>The placeholder folder the sample pretends to have scanned.</summary>
    public const string RootPath = @"C:\Users\You\Pictures";

    /// <summary>Paints scene <paramref name="seed"/> at the given size; <paramref name="shift"/> nudges the sun (a "burst" frame).</summary>
    public static Bitmap Scene(int seed, int width = 320, int height = 240, int shift = 0) => SampleScene.Paint(seed, width, height, shift);

    /// <summary>
    /// A ready-made result: records with real fingerprints of generated pictures (placeholder paths),
    /// the groups <see cref="SimilarityFinder"/> makes of them, and a thumbnail source for the window.
    /// The caller disposes the bitmaps via the returned dictionary.
    /// </summary>
    public static (IReadOnlyList<PictureRecord> Records, Dictionary<string, Bitmap> Thumbnails) Build(DateTime nowUtc)
    {
        var thumbs = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        var records = new List<PictureRecord>();
        // takenMinutesAgo: the EXIF capture time (copies keep the original's; null = stripped, e.g. by a chat app).
        void Add(string relative, Bitmap bmp, int width, int height, long size, double savedMinutesAgo, double? takenMinutesAgo)
        {
            var path = Path.Combine(RootPath, relative);
            thumbs[path] = bmp;
            DateTime? taken = takenMinutesAgo is { } t ? nowUtc.AddMinutes(-t) : null;
            var sample = new PictureSample(GrayImage.FromBitmap(bmp), width, height, taken);
            records.Add(PictureRecord.From(path, size, nowUtc.AddMinutes(-savedMinutesAgo), sample));
        }

        Add(@"2024\Beach\IMG_2041.jpg", Scene(11), 4032, 3024, 4_812_000, 900_000, 900_000);
        Add(@"Phone backup\IMG_2041 (1).jpg", Scene(11), 4032, 3024, 4_812_000, 600_000, 900_000);
        Add(@"WhatsApp\IMG-20240611-WA0007.jpg", Scene(11, 160, 120), 1600, 1200, 312_000, 500_000, null);
        Add(@"2024\Hills\DSC_0310.jpg", Scene(23), 6000, 4000, 9_420_000, 800_000, 800_000);
        Add(@"Edited\DSC_0310_rotated.jpg", Rotated(Scene(23)), 4000, 6000, 8_960_000, 400_000, 800_000);
        Add(@"Edited\DSC_0310_mirror.jpg", Mirrored(Scene(23)), 6000, 4000, 9_100_000, 400_001, 800_000);
        Add(@"2025\Trip\burst_01.jpg", Scene(37, shift: 0), 4032, 3024, 5_120_000, 100_000, 100_000.0);
        Add(@"2025\Trip\burst_02.jpg", Scene(37, shift: 7), 4032, 3024, 5_090_000, 100_000, 100_000.0 - 0.02);
        Add(@"2025\Trip\burst_03.jpg", Scene(37, shift: 14), 4032, 3024, 5_060_000, 100_000, 100_000.0 - 0.04);
        Add(@"2025\Garden\IMG_5512.jpg", Scene(51), 4032, 3024, 4_400_000, 50_000, 50_000);
        Add(@"2025\City\IMG_7001.jpg", Scene(64), 4032, 3024, 4_700_000, 20_000, 20_000);
        return (records, thumbs);
    }

    private static Bitmap Rotated(Bitmap bmp)
    {
        bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
        return bmp;
    }

    private static Bitmap Mirrored(Bitmap bmp)
    {
        bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        return bmp;
    }
}

/// <summary>Pure edits of a <see cref="SimilarGroup"/> made from the review screen.</summary>
public static class GroupEdits
{
    /// <summary>
    /// The group with <paramref name="path"/> (a member) as its keeper. The previous keeper becomes a
    /// member with the same match kind and distances, and is not suggested for moving - the user picked
    /// the keeper by hand, so nothing else is pre-decided for them.
    /// </summary>
    public static SimilarGroup WithKeeper(SimilarGroup group, string path)
    {
        var chosen = group.Members.FirstOrDefault(m => string.Equals(m.Picture.Path, path, StringComparison.OrdinalIgnoreCase));
        if (chosen is null)
        {
            return group;
        }
        var oldKeeper = new GroupMember(group.Keeper, chosen.Kind, SimilarityFinder.Inverse(chosen.Orientation), chosen.PDistance, chosen.DDistance, false);
        var members = group.Members.Where(m => m != chosen).Prepend(oldKeeper).ToList();
        return group with { Keeper = chosen.Picture, Members = members };
    }
}
