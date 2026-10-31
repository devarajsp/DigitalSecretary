using DigitalSecretary.Kit;
using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>Covers <see cref="SimilarReportText"/>, the settings store, <see cref="FileBytes"/> and <see cref="SamplePictureSet"/>.</summary>
public sealed class ReportSettingsAndSampleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ds-sim-" + Guid.NewGuid().ToString("N"));

    public ReportSettingsAndSampleTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private static PictureRecord P(string path, long size = 1000, int w = 4000, int h = 3000)
        => new() { Path = path, Size = size, Width = w, Height = h, ModifiedUtc = Pictures.Now, PHashes = new ulong[8], DHashes = new ulong[8] };

    [Fact]
    public void Labels_and_descriptions()
    {
        SimilarReportText.Label(MatchKind.Exact).Should().Be("Exact copy");
        SimilarReportText.Label(MatchKind.SamePicture).Should().Be("Same picture");
        SimilarReportText.Label(MatchKind.Similar).Should().Be("Similar - check");
        SimilarReportText.Describe(PictureOrientation.AsIs).Should().BeEmpty();
        SimilarReportText.Describe(PictureOrientation.Rotate90).Should().Be("turned 90\u00b0");
        SimilarReportText.Describe(PictureOrientation.Rotate180).Should().Be("turned 180\u00b0");
        SimilarReportText.Describe(PictureOrientation.MirrorLeftRight).Should().Be("mirrored");
        SimilarReportText.Describe(PictureOrientation.MirrorDiagonal).Should().Be("turned and mirrored");
        SimilarReportText.Dimensions(P("a")).Should().Be("4000 x 3000");
        SimilarReportText.Dimensions(P("a", w: 0)).Should().Be("size unknown");
    }

    [Fact]
    public void The_report_lists_keepers_moves_and_matches()
    {
        var keeper = P(@"C:\Users\You\Pictures\a.jpg", 5000);
        var copy = P(@"C:\Users\You\Pictures\b.jpg", 2048);
        var burst = P(@"C:\Users\You\Pictures\c.jpg", 3000);
        var groups = new List<SimilarGroup>
        {
            new(1, keeper, [new GroupMember(copy, MatchKind.SamePicture, PictureOrientation.Rotate90, 0, 0, true), new GroupMember(burst, MatchKind.Similar, PictureOrientation.AsIs, 12, 9, false)])
        };
        var marked = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { copy.Path };

        var text = SimilarReportText.Render(@"C:\Users\You\Pictures", Pictures.Now, 10, groups, marked);

        text.Should().Contain("Folder:   C:\\Users\\You\\Pictures");
        text.Should().Contain("Created:  2026-10-30 09:00 UTC");
        text.Should().Contain("10 pictures - 1 groups (0 exact, 0 same picture, 1 similar to check) - 1 marked to move, 2 KB");
        text.Should().Contain("Group 1 - Similar - check - 3 pictures");
        text.Should().Contain("KEEP").And.Contain("MOVE").And.Contain("[Same picture, turned 90\u00b0]").And.Contain("[Similar - check]");
    }

    [Fact]
    public void An_empty_report_says_nothing_was_found()
        => SimilarReportText.Render("x", Pictures.Now, 3, [], new HashSet<string>()).Should().Contain("No similar pictures found.");

    [Fact]
    public void Settings_default_to_safe_choices_and_round_trip()
    {
        var store = new SimilarPicturesSettingsStore(_dir);
        var defaults = store.Load();
        defaults.Action.Should().Be(RemoveAction.ReviewFolder);
        defaults.Sensitivity.Should().Be(Sensitivity.Balanced);
        defaults.IncludeRotated.Should().BeTrue();
        defaults.IncludeSimilar.Should().BeTrue();

        store.Save(new SimilarPicturesSettings { Root = @"C:\Users\You\Pictures", Sensitivity = Sensitivity.Strict, IncludeSimilar = false, Action = RemoveAction.RecycleBin });
        var loaded = store.Load();
        loaded.Root.Should().Be(@"C:\Users\You\Pictures");
        loaded.ToFindOptions().Should().Be(new FindOptions { Sensitivity = Sensitivity.Strict, IncludeRotated = true, IncludeSimilar = false });
        loaded.Action.Should().Be(RemoveAction.RecycleBin);

        File.WriteAllText(Path.Combine(_dir, "similar_pictures_settings.json"), "{ broken");
        store.Load().Action.Should().Be(RemoveAction.ReviewFolder);
    }

    [Fact]
    public void Byte_comparison()
    {
        var a = Path.Combine(_dir, "a.bin");
        var b = Path.Combine(_dir, "b.bin");
        var c = Path.Combine(_dir, "c.bin");
        var big = Enumerable.Range(0, 200_000).Select(i => (byte)i).ToArray();
        File.WriteAllBytes(a, big);
        File.WriteAllBytes(b, big);
        big[150_000] ^= 1;
        File.WriteAllBytes(c, big);

        FileBytes.AreIdentical(a, b).Should().BeTrue();
        FileBytes.AreIdentical(a, c).Should().BeFalse("one bit differs in the third block");
        FileBytes.AreIdentical(a, Path.Combine(_dir, "missing")).Should().BeFalse();
        File.WriteAllBytes(c, [1, 2]);
        FileBytes.AreIdentical(a, c).Should().BeFalse();
    }

    [Fact]
    public void The_sample_set_uses_placeholder_paths_and_shows_every_kind_of_match()
    {
        var (records, thumbs) = SamplePictureSet.Build(Pictures.Now);
        try
        {
            records.Should().OnlyContain(r => r.Path.StartsWith(SamplePictureSet.RootPath + @"\", StringComparison.Ordinal));
            thumbs.Keys.Should().BeEquivalentTo(records.Select(r => r.Path));

            var groups = SimilarityFinder.Find(records, new FindOptions());
            groups.Select(g => g.Kind).Should().Contain([MatchKind.SamePicture, MatchKind.Similar]);
            groups.SelectMany(g => g.Members).Should().Contain(m => m.Orientation != PictureOrientation.AsIs, "the sample shows a turned copy");
            groups.Should().HaveCount(3, "the copies of scene 11, the turned/mirrored scene 23 and the burst of scene 37");
        }
        finally
        {
            foreach (var bmp in thumbs.Values)
            {
                bmp.Dispose();
            }
        }
    }
}
