using System.Drawing;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// Colours for the size map and charts. One fixed colour per <see cref="FileCategory"/> (based on
/// the Okabe-Ito colour-blind-safe set, so categories stay distinguishable for most viewers), a
/// lighter tint per nesting level, and black or white label text chosen by contrast. Pure.
/// </summary>
public static class CategoryPalette
{
    private static readonly Color[] Colors =
    [
        Color.FromArgb(0xD5, 0x5E, 0x00), // Video     - vermilion
        Color.FromArgb(0x00, 0x72, 0xB2), // Picture   - blue
        Color.FromArgb(0xCC, 0x79, 0xA7), // Audio     - reddish purple
        Color.FromArgb(0x00, 0x9E, 0x73), // Document  - bluish green
        Color.FromArgb(0xE6, 0x9F, 0x00), // Archive   - orange
        Color.FromArgb(0x56, 0xB4, 0xE9), // Installer - sky blue
        Color.FromArgb(0x7A, 0x5C, 0xB8), // Code      - violet
        Color.FromArgb(0x8C, 0x8C, 0x8C)  // Other     - grey
    ];

    /// <summary>The base colour of a category.</summary>
    public static Color For(FileCategory category) => Colors[(int)category];

    /// <summary>
    /// <paramref name="color"/> mixed toward white by <paramref name="amount"/> (0 = unchanged,
    /// 1 = white). Used to lighten deeper levels so the nesting reads as layers.
    /// </summary>
    public static Color Tint(Color color, double amount)
    {
        var t = Math.Clamp(amount, 0d, 1d);
        static int Mix(int c, double t) => (int)Math.Round(c + ((255 - c) * t));
        return Color.FromArgb(color.A, Mix(color.R, t), Mix(color.G, t), Mix(color.B, t));
    }

    /// <summary>The fill for a map tile at nesting <paramref name="depth"/> (1 = top level).</summary>
    public static Color Fill(FileCategory category, int depth) => Tint(For(category), Math.Max(0, depth - 1) * 0.22);

    /// <summary>
    /// Black or white, whichever has the higher WCAG contrast on <paramref name="background"/>.
    /// The two contrasts are equal at a luminance of about 0.179, so that is the switch-over point.
    /// </summary>
    public static Color TextOn(Color background)
        => RelativeLuminance(background) > 0.179 ? Color.FromArgb(25, 25, 25) : Color.White;

    /// <summary>WCAG 2.x relative luminance of an sRGB colour, 0 (black) to 1 (white).</summary>
    public static double RelativeLuminance(Color c)
    {
        static double Channel(int v)
        {
            var s = v / 255d;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }
}
