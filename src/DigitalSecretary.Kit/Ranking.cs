namespace DigitalSecretary.Kit;

/// <summary>Small comparison steps shared by the "which one do we keep" rankings.</summary>
public static class Ranking
{
    /// <summary>
    /// -1 when <paramref name="a"/> is larger than <paramref name="b"/> by more than <paramref name="margin"/>
    /// (a fraction of the larger value), 1 when smaller by more than that, else 0 ("about the same").
    /// Both zero or negative counts as "about the same".
    /// </summary>
    public static int PreferLarger(double a, double b, double margin)
    {
        var top = Math.Max(a, b);
        if (top <= 0 || Math.Abs(a - b) <= top * margin)
        {
            return 0;
        }
        return a > b ? -1 : 1;
    }
}
