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
    public static Bitmap Scene(int seed, int width = 320, int height = 240, int shift = 0)
    {
        var rnd = new Random(seed);
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var sky = Color.FromArgb(rnd.Next(40, 120), rnd.Next(110, 190), rnd.Next(180, 255));
        using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), sky, Color.White, 90f))
        {
            g.FillRectangle(brush, 0, 0, width, height);
        }

        float sx = width / 320f, sy = height / 240f;
        using (var sun = new SolidBrush(Color.FromArgb(255, 220, rnd.Next(60, 140))))
        {
            g.FillEllipse(sun, (rnd.Next(30, 250) + shift) * sx, rnd.Next(15, 70) * sy, 46 * sx, 46 * sy);
        }
        for (var i = 0; i < 3; i++)
        {
            using var hill = new SolidBrush(Color.FromArgb(rnd.Next(20, 90), rnd.Next(90, 170), rnd.Next(20, 80)));
            var cx = rnd.Next(-40, 300);
            g.FillEllipse(hill, cx * sx, rnd.Next(130, 180) * sy, rnd.Next(160, 300) * sx, 220 * sy);
        }
        using (var wall = new SolidBrush(Color.FromArgb(rnd.Next(150, 230), rnd.Next(80, 160), rnd.Next(40, 120))))
        using (var roof = new SolidBrush(Color.FromArgb(120, 30, 30)))
        {
            var hx = rnd.Next(40, 220) * sx;
            var hy = rnd.Next(140, 170) * sy;
            g.FillRectangle(wall, hx, hy, 60 * sx, 45 * sy);
            g.FillPolygon(roof, [new PointF(hx - (6 * sx), hy), new PointF(hx + (30 * sx), hy - (28 * sy)), new PointF(hx + (66 * sx), hy)]);
        }
        return bmp;
    }

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
