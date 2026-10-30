using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>Covers <see cref="KeeperRanking"/> - which picture of a group is kept.</summary>
public sealed class KeeperRankingTests
{
    private static PictureRecord P(string path, int width = 100, double sharp = 100, long size = 1000, DateTime? taken = null)
        => new() { Path = path, Width = width, Height = width, Sharpness = sharp, Size = size, TakenUtc = taken, ModifiedUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), PHashes = new ulong[8], DHashes = new ulong[8] };

    private static string Best(params PictureRecord[] pictures) => pictures.OrderBy(p => p, KeeperRanking.Instance).First().Path;

    [Fact]
    public void More_pixels_wins()
        => Best(P("small.jpg", width: 100, size: 9999), P("big.jpg", width: 200)).Should().Be("big.jpg");

    [Fact]
    public void Nearly_equal_pixels_fall_through_to_sharpness()
        => Best(P("soft.jpg", width: 100, sharp: 50), P("crisp.jpg", width: 98, sharp: 200)).Should().Be("crisp.jpg");

    [Fact]
    public void Then_the_bigger_file()
        => Best(P("a.jpg", size: 1000), P("b.jpg", size: 2000)).Should().Be("b.jpg");

    [Fact]
    public void Then_a_name_that_does_not_look_like_a_copy()
        => Best(P("IMG_1 (1).jpg"), P("IMG_1.jpg")).Should().Be("IMG_1.jpg");

    [Fact]
    public void Then_the_earlier_picture()
        => Best(P("late.jpg", taken: new DateTime(2025, 6, 1)), P("early.jpg", taken: new DateTime(2020, 6, 1))).Should().Be("early.jpg");

    [Fact]
    public void Then_the_shorter_path_then_alphabetical()
    {
        Best(P(@"C:\a\b\c\x.jpg"), P(@"C:\x.jpg")).Should().Be(@"C:\x.jpg");
        Best(P("b.jpg"), P("a.jpg")).Should().Be("a.jpg");
    }

    [Theory]
    [InlineData("IMG_1 (2)", true)]
    [InlineData("holiday - Copy", true)]
    [InlineData("holiday_copy", true)]
    [InlineData("Copy of holiday", true)]
    [InlineData("holiday - Copy (3)", true)]
    [InlineData("IMG_1", false)]
    [InlineData("copyright notice", false)]
    [InlineData("2024 (1999 trip)", false)]
    public void Copy_names(string stem, bool expected) => KeeperRanking.LooksLikeCopy(stem + ".jpg").Should().Be(expected);

    [Fact]
    public void Null_and_identity_compare_safely()
    {
        var p = P("a.jpg");
        KeeperRanking.Instance.Compare(p, p).Should().Be(0);
        KeeperRanking.Instance.Compare(null, p).Should().BePositive();
        KeeperRanking.Instance.Compare(p, null).Should().BeNegative();
    }
}
