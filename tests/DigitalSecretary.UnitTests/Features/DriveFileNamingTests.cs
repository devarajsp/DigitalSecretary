using DigitalSecretary.Features.GoogleDriveDownloader;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class DriveFileNamingTests
{
    [Fact]
    public void Sanitize_replaces_invalid_characters()
        => DriveFileNaming.Sanitize("a/b:c*d?e").Should().Be("a_b_c_d_e");

    [Fact]
    public void Sanitize_empty_becomes_underscore()
        => DriveFileNaming.Sanitize("   ").Should().Be("_");

    [Fact]
    public void Sanitize_caps_very_long_names()
        => DriveFileNaming.Sanitize(new string('a', 300)).Length
            .Should().BeLessThanOrEqualTo(DriveFileNaming.MaxBaseLength);

    [Fact]
    public void GetUniquePath_appends_counter_on_collision()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "doc.pdf"), "x");

        var path = DriveFileNaming.GetUniquePath(dir.Path, "doc.pdf");

        Path.GetFileName(path).Should().Be("doc_1.pdf");
    }

    [Fact]
    public void ResolveFolderPath_walks_parents_top_down()
    {
        var folders = new Dictionary<string, DriveFolderNode>
        {
            ["root"] = new("root", "Projects", null),
            ["child"] = new("child", "2026", "root")
        };

        var segments = DriveFileNaming.ResolveFolderPath(folders, "child");

        segments.Should().ContainInOrder("Projects", "2026");
    }

    [Fact]
    public void ResolveFolderPath_stops_at_unknown_parent()
    {
        var folders = new Dictionary<string, DriveFolderNode>
        {
            ["child"] = new("child", "Sub", "missing-root")
        };

        var segments = DriveFileNaming.ResolveFolderPath(folders, "child");

        segments.Should().Equal("Sub");
    }

    [Fact]
    public void ResolveFolderPath_empty_when_no_parent()
        => DriveFileNaming.ResolveFolderPath(new Dictionary<string, DriveFolderNode>(), null)
            .Should().BeEmpty();

    [Fact]
    public void ResolveFolderPath_guards_against_cycles()
    {
        var folders = new Dictionary<string, DriveFolderNode>
        {
            ["a"] = new("a", "A", "b"),
            ["b"] = new("b", "B", "a")
        };

        // Must terminate (no infinite loop) and include each folder at most once.
        var segments = DriveFileNaming.ResolveFolderPath(folders, "a");

        segments.Should().HaveCount(2);
        segments.Should().Contain("A").And.Contain("B");
    }
}
