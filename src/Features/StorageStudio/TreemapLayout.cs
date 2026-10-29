using System.Drawing;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// One rectangle of the size map. <see cref="Node"/> is null for the "smaller items" tile that
/// stands in for a folder's long tail. <see cref="Expanded"/> is true when the cell's own children
/// are drawn inside it (below a header strip of <see cref="TreemapLayout.HeaderHeight"/>).
/// </summary>
public sealed record TreemapCell(SpaceNode? Node, string Label, long Bytes, RectangleF Rect, int Depth, bool Expanded);

/// <summary>
/// Pure treemap geometry. <see cref="Squarify"/> is the squarified algorithm (Bruls, Huizing and van
/// Wijk, 2000): it lays weighted items into a rectangle so each item's area is proportional to its
/// weight while keeping tiles as close to square as possible, which makes sizes easy to compare by eye.
/// <see cref="Build"/> applies it to a folder, nesting one more level inside tiles big enough to read.
/// </summary>
public static class TreemapLayout
{
    /// <summary>Height of the name strip at the top of an expanded folder tile.</summary>
    public const float HeaderHeight = 18f;

    /// <summary>A folder tile must be at least this big before its contents are drawn inside it.</summary>
    public static readonly SizeF MinExpandSize = new(90f, 60f);

    /// <summary>At most this many children get their own tile; the rest share one "smaller items" tile.</summary>
    public const int MaxTilesPerFolder = 200;

    /// <summary>
    /// Lays <paramref name="weights"/> into <paramref name="bounds"/>. Returns one rectangle per
    /// weight, in input order; zero or negative weights get <see cref="RectangleF.Empty"/>. Input in
    /// any order is accepted, but descending order gives the squarest result.
    /// </summary>
    public static RectangleF[] Squarify(IReadOnlyList<double> weights, RectangleF bounds)
    {
        var result = new RectangleF[weights.Count];
        var order = Enumerable.Range(0, weights.Count).Where(i => weights[i] > 0).ToList();
        var total = order.Sum(i => weights[i]);
        if (total <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return result;
        }

        var scale = bounds.Width * (double)bounds.Height / total;
        var remaining = bounds;
        var row = new List<int>();
        var next = 0;

        while (next < order.Count)
        {
            var side = Math.Min(remaining.Width, remaining.Height);
            var candidate = order[next];
            if (row.Count == 0 || Worst(row, candidate, weights, scale, side) <= Worst(row, null, weights, scale, side))
            {
                row.Add(candidate);
                next++;
                continue;
            }
            remaining = PlaceRow(row, weights, scale, remaining, result);
            row.Clear();
        }
        if (row.Count > 0)
        {
            PlaceRow(row, weights, scale, remaining, result);
        }
        return result;
    }

    /// <summary>
    /// The cells for <paramref name="focus"/>'s children inside <paramref name="bounds"/>, nesting up
    /// to <paramref name="maxDepth"/> levels. Parents come before their children, so drawing in list
    /// order paints children on top.
    /// </summary>
    public static IReadOnlyList<TreemapCell> Build(SpaceNode focus, RectangleF bounds, int maxDepth = 2)
    {
        var cells = new List<TreemapCell>();
        LayoutChildren(focus, bounds, 1, Math.Max(1, maxDepth), cells);
        return cells;
    }

    private static void LayoutChildren(SpaceNode folder, RectangleF bounds, int depth, int maxDepth, List<TreemapCell> cells)
    {
        var shown = folder.Children.Where(c => c.Size > 0).OrderByDescending(c => c.Size).ToList();
        var tail = shown.Count > MaxTilesPerFolder ? shown.Skip(MaxTilesPerFolder).ToList() : [];
        if (tail.Count > 0)
        {
            shown = shown.Take(MaxTilesPerFolder).ToList();
        }

        var weights = shown.Select(c => (double)c.Size).ToList();
        var tailBytes = tail.Sum(c => c.Size);
        if (tailBytes > 0)
        {
            weights.Add(tailBytes);
        }

        var rects = Squarify(weights, bounds);
        for (var i = 0; i < shown.Count; i++)
        {
            AddCell(shown[i], rects[i], depth, maxDepth, cells);
        }
        if (tailBytes > 0)
        {
            cells.Add(new TreemapCell(null, tail.Count + " smaller items", tailBytes, rects[^1], depth, false));
        }
    }

    private static void AddCell(SpaceNode node, RectangleF rect, int depth, int maxDepth, List<TreemapCell> cells)
    {
        var expand = node.IsFolder && depth < maxDepth && node.Children.Count > 0
            && rect.Width >= MinExpandSize.Width && rect.Height >= MinExpandSize.Height;
        cells.Add(new TreemapCell(node, node.Name, node.Size, rect, depth, expand));
        if (expand)
        {
            var inner = RectangleF.FromLTRB(rect.Left + 2, rect.Top + HeaderHeight, rect.Right - 2, rect.Bottom - 2);
            LayoutChildren(node, inner, depth + 1, maxDepth, cells);
        }
    }

    /// <summary>Worst aspect ratio of <paramref name="row"/> (plus <paramref name="extra"/>) laid along <paramref name="side"/>.</summary>
    private static double Worst(List<int> row, int? extra, IReadOnlyList<double> weights, double scale, double side)
    {
        double sum = 0, min = double.MaxValue, max = 0;
        foreach (var area in row.Select(i => weights[i] * scale).Concat(extra is { } e ? [weights[e] * scale] : []))
        {
            sum += area;
            min = Math.Min(min, area);
            max = Math.Max(max, area);
        }
        var s2 = side * side;
        var sum2 = sum * sum;
        return Math.Max(s2 * max / sum2, sum2 / (s2 * min));
    }

    /// <summary>Places a finished row along the shorter side and returns the space left over.</summary>
    private static RectangleF PlaceRow(List<int> row, IReadOnlyList<double> weights, double scale, RectangleF r, RectangleF[] result)
    {
        var rowArea = row.Sum(i => weights[i] * scale);
        if (r.Width >= r.Height)
        {
            // Column on the left, items stacked top to bottom.
            var width = (float)(rowArea / r.Height);
            var y = r.Top;
            foreach (var i in row)
            {
                var h = (float)(weights[i] * scale / width);
                result[i] = new RectangleF(r.Left, y, width, h);
                y += h;
            }
            return RectangleF.FromLTRB(Math.Min(r.Left + width, r.Right), r.Top, r.Right, r.Bottom);
        }

        // Row along the top, items side by side.
        var height = (float)(rowArea / r.Width);
        var x = r.Left;
        foreach (var i in row)
        {
            var w = (float)(weights[i] * scale / height);
            result[i] = new RectangleF(x, r.Top, w, height);
            x += w;
        }
        return RectangleF.FromLTRB(r.Left, Math.Min(r.Top + height, r.Bottom), r.Right, r.Bottom);
    }
}
