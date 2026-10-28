namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// Formats byte counts for people ("1.44 GB"). Pure and culture-free so the text in the UI,
/// the saved report and the tests always match.
/// </summary>
public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Renders <paramref name="bytes"/> in the largest unit that keeps the number readable.
    /// Bytes are shown whole; larger units keep up to two decimals. Negative input keeps its sign.
    /// </summary>
    public static string Format(long bytes)
    {
        var negative = bytes < 0;
        var value = Math.Abs((double)bytes);
        var unit = 0;

        while (value >= 1024d && unit < Units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        var text = unit == 0
            ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0} {1}", value, Units[0])
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} {1}", value, Units[unit]);

        return negative ? "-" + text : text;
    }
}
