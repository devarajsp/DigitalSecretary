using System.Drawing;
using System.Drawing.Drawing2D;

namespace DigitalSecretary.Kit;

/// <summary>
/// Made-up pictures for documentation screenshots and tests: a simple painted "landscape" (sky, sun,
/// hills, a house) generated from a seed. Deterministic for a given seed and size, so no real photo
/// ever has to appear in a screenshot or a test. The look-alike finders build their sample sets on it.
/// </summary>
public static class SampleScene
{
    /// <summary>Paints scene <paramref name="seed"/> at the given size; <paramref name="shift"/> nudges the sun (a "burst" frame).</summary>
    public static Bitmap Paint(int seed, int width = 320, int height = 240, int shift = 0)
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
}
