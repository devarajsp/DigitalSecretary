using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="SpaceReport"/> and <see cref="SpaceReportText"/> - the saved text report.</summary>
public sealed class SpaceReportTextTests
{
    private static string Render(IReadOnlyList<GrowthEntry>? growth = null, DateTime? previous = null)
        => SpaceReportText.Render(SpaceReport.For(SpaceTrees.Sample(), SpaceTrees.Now, growth, previous));

    [Fact]
    public void The_report_has_every_section_with_totals()
    {
        var text = Render();

        text.Should().Contain("Folder:    " + SpaceTrees.RootPath);
        text.Should().Contain("Created:   2026-10-29 12:00 UTC");
        text.Should().Contain("Total:     1000 B in 5 files, 3 folders");
        foreach (var section in new[] { "By type", "By age (last modified)", "Largest files", "Largest folders", "Clean-up candidates", "Changes since the previous scan" })
        {
            text.Should().Contain(section);
        }
        text.Should().Contain("Videos").And.Contain("70.0%");
        text.Should().Contain(Path.Combine(SpaceTrees.RootPath, "Videos", "holiday.mp4"));
    }

    [Fact]
    public void Without_a_previous_scan_the_report_says_so()
    {
        Render().Should().Contain("No previous scan of this folder to compare with.");
        Render().Should().Contain("None found.");
    }

    [Fact]
    public void With_a_previous_scan_changes_are_listed_with_signs()
    {
        var text = Render([new GrowthEntry("Videos", 100, 700), new GrowthEntry("Photos", 400, 250)], SpaceTrees.Now.AddDays(-7));

        text.Should().Contain("Previous scan: 2026-10-22 12:00 UTC");
        text.Should().Contain("+600 B  Videos");
        text.Should().Contain("-150 B  Photos");
    }

    [Fact]
    public void An_unchanged_folder_reports_no_noticeable_change()
        => Render([], SpaceTrees.Now.AddDays(-1)).Should().Contain("No folder changed noticeably.");

    [Fact]
    public void Clean_up_lists_are_capped_with_a_count_of_the_rest()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        for (var i = 0; i < 25; i++)
        {
            root.AddFile("t" + i + ".tmp", 10, SpaceTrees.Now);
        }
        var text = SpaceReportText.Render(SpaceReport.For(root, SpaceTrees.Now, null, null));

        text.Should().Contain("Temporary & partial files: 250 B in 25 item(s)");
        text.Should().Contain("... and 5 more");
    }

    [Theory]
    [InlineData(0L, "+0 B")]
    [InlineData(2048L, "+2 KB")]
    [InlineData(-2048L, "-2 KB")]
    public void Changes_carry_their_sign(long delta, string expected)
        => SpaceReportText.WithSign(delta).Should().Be(expected);

    [Theory]
    [InlineData(0.0, "0.0%")]
    [InlineData(0.425, "42.5%")]
    [InlineData(1.0, "100.0%")]
    public void Shares_render_as_percentages(double share, string expected)
        => SpaceReportText.Percent(share).Should().Be(expected);
}
