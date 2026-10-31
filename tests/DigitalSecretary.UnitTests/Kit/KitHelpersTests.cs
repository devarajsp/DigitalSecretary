using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Kit;

/// <summary>Covers the small shared kit helpers: copy-like names, the "clearly larger" ranking step, flatness, mapped progress, cache file names.</summary>
public sealed class KitHelpersTests
{
    [Theory]
    [InlineData(@"C:\v\IMG_1 (2).jpg", true)]
    [InlineData(@"C:\v\clip - Copy.mp4", true)]
    [InlineData(@"C:\v\clip_copy.mov", true)]
    [InlineData(@"C:\v\clip - Copy (3).mp4", true)]
    [InlineData(@"C:\v\Copy of trip.mp4", true)]
    [InlineData(@"C:\v\trip.mp4", false)]
    [InlineData(@"C:\v\copycat.mp4", false)]
    public void Names_that_look_like_copies(string path, bool copy) => FileNames.LooksLikeCopy(path).Should().Be(copy);

    [Theory]
    [InlineData(110, 100, 0.05, -1)]
    [InlineData(100, 110, 0.05, 1)]
    [InlineData(104, 100, 0.05, 0)]
    [InlineData(0, 0, 0.05, 0)]
    public void Prefer_larger_only_when_clearly_larger(double a, double b, double margin, int expected)
        => Ranking.PreferLarger(a, b, margin).Should().Be(expected);

    [Fact]
    public void A_flat_image_has_no_spread_and_a_two_tone_one_has_half_the_difference()
    {
        new GrayImage(2, 2, [50, 50, 50, 50]).StandardDeviation().Should().Be(0);
        new GrayImage(2, 1, [0, 100]).StandardDeviation().Should().BeApproximately(50, 1e-6);
    }

    [Fact]
    public void Mapped_progress_passes_each_report_on_converted()
    {
        var seen = new List<string>();
        var progress = new MappedProgress<int, string>(new Relay(seen), i => "#" + i);

        progress.Report(3);

        seen.Should().Equal("#3");
    }

    [Fact]
    public void One_cache_file_per_folder_whatever_the_spelling()
    {
        var a = CacheFiles.PathFor(@"C:\data", @"C:\Users\You\Videos");

        a.Should().Be(CacheFiles.PathFor(@"C:\data", @"c:\users\you\videos\"));
        a.Should().NotBe(CacheFiles.PathFor(@"C:\data", @"C:\Users\You\Pictures"));
        a.Should().StartWith(Path.Combine(@"C:\data", "fingerprints")).And.EndWith(".json.gz");
    }

    private sealed class Relay(List<string> seen) : IProgress<string>
    {
        public void Report(string value) => seen.Add(value);
    }
}
