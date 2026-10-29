using System.Drawing;
using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>
/// Covers <see cref="TreemapLayout"/> by its geometric promises: tiles stay inside the bounds, do
/// not overlap, fill the area, keep area proportional to weight, and stay reasonably square.
/// </summary>
public sealed class TreemapLayoutTests
{
    private static readonly RectangleF Bounds = new(10, 20, 800, 500);
    private const float Tolerance = 0.5f;

    private static double Area(RectangleF r) => r.Width * (double)r.Height;

    [Theory]
    [InlineData(new double[] { 6, 6, 4, 3, 2, 2, 1 })]
    [InlineData(new double[] { 1 })]
    [InlineData(new double[] { 100, 1, 1, 1, 1 })]
    [InlineData(new double[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 })]
    public void Tiles_fill_the_bounds_in_proportion_without_overlapping(double[] weights)
    {
        var rects = TreemapLayout.Squarify(weights, Bounds);

        rects.Sum(Area).Should().BeApproximately(Area(Bounds), Area(Bounds) * 1e-3);
        var total = weights.Sum();
        for (var i = 0; i < weights.Length; i++)
        {
            Area(rects[i]).Should().BeApproximately(Area(Bounds) * weights[i] / total, Area(Bounds) * 1e-3);
            rects[i].Left.Should().BeGreaterThanOrEqualTo(Bounds.Left - Tolerance);
            rects[i].Top.Should().BeGreaterThanOrEqualTo(Bounds.Top - Tolerance);
            rects[i].Right.Should().BeLessThanOrEqualTo(Bounds.Right + Tolerance);
            rects[i].Bottom.Should().BeLessThanOrEqualTo(Bounds.Bottom + Tolerance);
            for (var j = i + 1; j < weights.Length; j++)
            {
                var overlap = RectangleF.Intersect(rects[i], rects[j]);
                Area(overlap).Should().BeLessThan(1.0, "tiles {0} and {1} must not overlap", i, j);
            }
        }
    }

    [Fact]
    public void Equal_weights_give_near_square_tiles()
    {
        var rects = TreemapLayout.Squarify(Enumerable.Repeat(1d, 16).ToList(), new RectangleF(0, 0, 400, 400));
        foreach (var r in rects)
        {
            (Math.Max(r.Width, r.Height) / Math.Min(r.Width, r.Height)).Should().BeLessThan(2.0f);
        }
    }

    [Fact]
    public void Zero_and_negative_weights_get_no_tile_and_take_no_space()
    {
        var rects = TreemapLayout.Squarify([3, 0, -2, 1], Bounds);
        rects[1].Should().Be(RectangleF.Empty);
        rects[2].Should().Be(RectangleF.Empty);
        (Area(rects[0]) + Area(rects[3])).Should().BeApproximately(Area(Bounds), 1.0);
    }

    [Fact]
    public void Nothing_to_lay_out_returns_empty_tiles()
    {
        TreemapLayout.Squarify([], Bounds).Should().BeEmpty();
        TreemapLayout.Squarify([0, 0], Bounds).Should().OnlyContain(r => r == RectangleF.Empty);
        TreemapLayout.Squarify([1], RectangleF.Empty).Should().OnlyContain(r => r == RectangleF.Empty);
    }

    [Fact]
    public void Build_nests_big_folders_and_lists_parents_before_children()
    {
        var cells = TreemapLayout.Build(SpaceTrees.Sample(), new RectangleF(0, 0, 900, 600), maxDepth: 2);

        var videos = cells.Single(c => c.Node?.Name == "Videos");
        videos.Expanded.Should().BeTrue();
        videos.Depth.Should().Be(1);

        var holiday = cells.Single(c => c.Node?.Name == "holiday.mp4");
        holiday.Depth.Should().Be(2);
        cells.ToList().IndexOf(holiday).Should().BeGreaterThan(cells.ToList().IndexOf(videos));
        videos.Rect.Contains(holiday.Rect).Should().BeTrue();
        holiday.Rect.Top.Should().BeGreaterThanOrEqualTo(videos.Rect.Top + TreemapLayout.HeaderHeight - Tolerance);
    }

    [Fact]
    public void Build_stops_at_max_depth()
    {
        var cells = TreemapLayout.Build(SpaceTrees.Sample(), new RectangleF(0, 0, 900, 600), maxDepth: 1);
        cells.Should().OnlyContain(c => c.Depth == 1 && !c.Expanded);
        cells.Should().HaveCount(3);
    }

    [Fact]
    public void Small_tiles_are_not_expanded()
    {
        var cells = TreemapLayout.Build(SpaceTrees.Sample(), new RectangleF(0, 0, 120, 80), maxDepth: 3);
        cells.Where(c => c.Node?.Name == "Photos").Should().OnlyContain(c => !c.Expanded);
    }

    [Fact]
    public void A_long_tail_is_folded_into_one_smaller_items_tile()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        for (var i = 0; i < TreemapLayout.MaxTilesPerFolder + 25; i++)
        {
            root.AddFile("f" + i + ".bin", 1000 - i, SpaceTrees.Now);
        }

        var cells = TreemapLayout.Build(root, new RectangleF(0, 0, 1000, 1000), maxDepth: 1);

        cells.Should().HaveCount(TreemapLayout.MaxTilesPerFolder + 1);
        var tail = cells.Single(c => c.Node is null);
        tail.Label.Should().Be("25 smaller items");
        tail.Bytes.Should().Be(Enumerable.Range(TreemapLayout.MaxTilesPerFolder, 25).Sum(i => 1000L - i));
    }

    [Fact]
    public void Empty_children_are_not_drawn()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        root.AddFolder("empty");
        root.AddFile("zero.txt", 0, SpaceTrees.Now);
        TreemapLayout.Build(root, new RectangleF(0, 0, 100, 100)).Should().BeEmpty();
    }
}
