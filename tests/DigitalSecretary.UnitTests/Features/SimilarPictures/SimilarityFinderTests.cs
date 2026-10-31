using System.Drawing;
using DigitalSecretary.Kit;
using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>Covers <see cref="SimilarityFinder"/>, <see cref="Thresholds"/> and <see cref="GroupEdits"/>.</summary>
public sealed class SimilarityFinderTests
{
    private static readonly FindOptions Defaults = new();

    private static PictureRecord Original(string name = "orig.jpg", DateTime? taken = null)
        => Pictures.Record(name, SamplePictureSet.Scene(11), taken: taken);

    /// <summary>A record with hand-made hashes: every orientation gets the same values.</summary>
    private static PictureRecord Hashed(string path, ulong p, ulong d, long size = 1000, int width = 100)
        => new() { Path = path, Size = size, Width = width, Height = width, ModifiedUtc = Pictures.Now, PHashes = Enumerable.Repeat(p, 8).ToArray(), DHashes = Enumerable.Repeat(d, 8).ToArray() };

    private static ulong Bits(int n) => n == 64 ? ulong.MaxValue : (1UL << n) - 1;

    [Fact]
    public void A_resized_copy_is_the_same_picture_and_pre_marked_with_the_original_kept()
    {
        var original = Original();
        var small = Pictures.Record("small.jpg", Pictures.Resized(SamplePictureSet.Scene(11), 160, 120), width: 1600, height: 1200, size: 300_000);

        var groups = SimilarityFinder.Find([small, original], Defaults);

        var g = groups.Should().ContainSingle().Subject;
        g.Keeper.Path.Should().Be("orig.jpg", "more pixels wins");
        g.Members.Should().ContainSingle().Which.Should().Match<GroupMember>(m => m.Kind == MatchKind.SamePicture && m.Suggested);
        g.SuggestedBytes.Should().Be(300_000);
    }

    [Fact]
    public void Byte_identical_files_are_exact_copies_when_confirmed()
    {
        var a = Original("a.jpg");
        var b = Original("b.jpg");

        SimilarityFinder.Find([a, b], Defaults, (_, _) => true).Single().Kind.Should().Be(MatchKind.Exact);
        SimilarityFinder.Find([a, b], Defaults, (_, _) => false).Single().Kind.Should().Be(MatchKind.SamePicture);
        SimilarityFinder.Find([a, b], Defaults).Single().Kind.Should().Be(MatchKind.SamePicture, "without a byte check nothing is called exact");
    }

    [Fact]
    public void Turned_copies_match_only_when_asked()
    {
        var turned = Pictures.Record("turned.jpg", Pictures.Turned(SamplePictureSet.Scene(11), RotateFlipType.Rotate90FlipNone));

        var with = SimilarityFinder.Find([Original(), turned], Defaults);
        var without = SimilarityFinder.Find([Original(), turned], Defaults with { IncludeRotated = false });

        with.Single().Members.Single().Orientation.Should().NotBe(PictureOrientation.AsIs);
        without.Should().BeEmpty();
    }

    [Fact]
    public void Burst_frames_taken_at_different_moments_are_similar_and_never_pre_marked()
    {
        var t = Pictures.Now;
        var a = Pictures.Record("b1.jpg", SamplePictureSet.Scene(37), taken: t);
        var b = Pictures.Record("b2.jpg", SamplePictureSet.Scene(37, shift: 7), taken: t.AddMilliseconds(120));

        var g = SimilarityFinder.Find([a, b], Defaults).Single();

        g.Kind.Should().Be(MatchKind.Similar);
        g.Members.Should().OnlyContain(m => !m.Suggested);
        g.SuggestedBytes.Should().Be(0);
        SimilarityFinder.Find([a, b], Defaults with { IncludeSimilar = false }).Should().BeEmpty();
    }

    [Fact]
    public void Copies_with_the_same_capture_time_or_none_are_not_different_moments()
    {
        var a = Original("a.jpg", Pictures.Now);
        SimilarityFinder.DifferentMoments(a, Original("b.jpg", Pictures.Now)).Should().BeFalse();
        SimilarityFinder.DifferentMoments(a, Original("c.jpg", null)).Should().BeFalse();
        SimilarityFinder.DifferentMoments(a, Original("d.jpg", Pictures.Now.AddSeconds(1))).Should().BeTrue();
    }

    [Fact]
    public void Different_pictures_do_not_group()
        => SimilarityFinder.Find([Original(), Pictures.Record("other.jpg", SamplePictureSet.Scene(24))], Defaults with { Sensitivity = Sensitivity.Loose })
            .Should().BeEmpty();

    [Fact]
    public void A_heavier_crop_is_found_as_similar_only_when_loose()
    {
        var crop = Pictures.Record("crop.jpg", Pictures.Cropped(SamplePictureSet.Scene(11), 8));
        SimilarityFinder.Find([Original(), crop], Defaults with { Sensitivity = Sensitivity.Loose }).Single().Kind.Should().Be(MatchKind.Similar);
        SimilarityFinder.Find([Original(), crop], Defaults).Should().BeEmpty();
        SimilarityFinder.Find([Original(), crop], Defaults with { Sensitivity = Sensitivity.Strict }).Should().BeEmpty();
    }

    [Fact]
    public void Shots_close_in_time_get_extra_tolerance()
    {
        var limits = Thresholds.For(Sensitivity.Balanced);
        var a = Hashed("a.jpg", 0, 0) with { TakenUtc = Pictures.Now };
        var near = Hashed("b.jpg", Bits(limits.SimilarPHash + 2), Bits(limits.SimilarDHash + 2)) with { TakenUtc = Pictures.Now.AddSeconds(5) };
        var far = near with { Path = "c.jpg", TakenUtc = Pictures.Now.AddHours(3) };

        SimilarityFinder.Compare(a, near, Defaults, limits).Should().NotBeNull();
        SimilarityFinder.Compare(a, far, Defaults, limits).Should().BeNull();
    }

    [Fact]
    public void Grouping_is_by_keeper_so_a_chain_cannot_pull_in_an_unlike_picture()
    {
        // A~B and B~C are "similar", but A and C are too far apart. A is the best, so it keeps B; C stays alone.
        var limits = Thresholds.For(Sensitivity.Balanced);
        var step = limits.SimilarPHash - 2;
        var a = Hashed("a.jpg", 0, 0, size: 3000, width: 300);
        var b = Hashed("b.jpg", Bits(step), 0, size: 2000, width: 200);
        var c = Hashed("c.jpg", Bits(step) | (Bits(step) << step), 0, size: 1000, width: 100);   // step from B, 2 x step from A

        var groups = SimilarityFinder.Find([c, b, a], Defaults);

        groups.Should().ContainSingle();
        groups[0].Keeper.Path.Should().Be("a.jpg");
        groups[0].Members.Select(m => m.Picture.Path).Should().Equal("b.jpg");
    }

    [Fact]
    public void Groups_are_numbered_with_the_most_space_to_gain_first()
    {
        var big = Hashed("big1.jpg", 0, 0, size: 9000);
        var big2 = Hashed("big2.jpg", 0, 0, size: 9000);
        var small = Hashed("s1.jpg", ulong.MaxValue, ulong.MaxValue, size: 10);
        var small2 = Hashed("s2.jpg", ulong.MaxValue, ulong.MaxValue, size: 10);

        var groups = SimilarityFinder.Find([small, big, small2, big2], Defaults);

        groups.Select(g => g.Id).Should().Equal(1, 2);
        groups[0].SuggestedBytes.Should().Be(9000);
        groups[0].Count.Should().Be(2);
        groups[0].AllOthersBytes.Should().Be(9000);
    }

    [Fact]
    public void No_pictures_means_no_groups()
        => SimilarityFinder.Find([], Defaults).Should().BeEmpty();

    [Fact]
    public void Thresholds_loosen_from_strict_to_loose()
    {
        var s = Thresholds.For(Sensitivity.Strict);
        var b = Thresholds.For(Sensitivity.Balanced);
        var l = Thresholds.For(Sensitivity.Loose);
        new[] { s.SamePHash, b.SamePHash, l.SamePHash }.Should().BeInAscendingOrder();
        new[] { s.SimilarPHash, b.SimilarPHash, l.SimilarPHash }.Should().BeInAscendingOrder();
        new[] { s.SimilarDHash, b.SimilarDHash, l.SimilarDHash }.Should().BeInAscendingOrder();
        b.SimilarPHash.Should().BeGreaterThan(b.SamePHash);
    }

    [Theory]
    [InlineData(PictureOrientation.Rotate90, PictureOrientation.Rotate270)]
    [InlineData(PictureOrientation.Rotate270, PictureOrientation.Rotate90)]
    [InlineData(PictureOrientation.Rotate180, PictureOrientation.Rotate180)]
    [InlineData(PictureOrientation.MirrorLeftRight, PictureOrientation.MirrorLeftRight)]
    [InlineData(PictureOrientation.AsIs, PictureOrientation.AsIs)]
    public void Inverse_orientations(PictureOrientation o, PictureOrientation expected)
        => SimilarityFinder.Inverse(o).Should().Be(expected);

    [Fact]
    public void Choosing_another_keeper_swaps_it_with_the_old_one_unmarked()
    {
        var a = Hashed("a.jpg", 0, 0, size: 3000, width: 300);
        var b = Hashed("b.jpg", 1, 0, size: 2000, width: 200);
        var c = Hashed("c.jpg", 2, 0, size: 1000, width: 100);
        var g = SimilarityFinder.Find([a, b, c], Defaults).Single();

        var edited = GroupEdits.WithKeeper(g, "c.jpg");

        edited.Keeper.Path.Should().Be("c.jpg");
        edited.Members.Select(m => m.Picture.Path).Should().BeEquivalentTo(["a.jpg", "b.jpg"]);
        edited.Members.Single(m => m.Picture.Path == "a.jpg").Suggested.Should().BeFalse();
        GroupEdits.WithKeeper(g, "nope.jpg").Should().BeSameAs(g);
    }
}
