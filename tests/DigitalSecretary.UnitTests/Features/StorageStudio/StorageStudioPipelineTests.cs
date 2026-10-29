using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>
/// Integration: the path the window takes - scan a real folder, analyse it, snapshot it, change the
/// folder, scan again and compare - asserted end to end across the engine classes.
/// </summary>
public sealed class StorageStudioPipelineTests : IDisposable
{
    private readonly string _root = SpaceTrees.TempFolder();
    private readonly string _data = SpaceTrees.TempFolder();

    public void Dispose()
    {
        SpaceTrees.Delete(_root);
        SpaceTrees.Delete(_data);
    }

    [Fact]
    public void Scan_analyse_snapshot_rescan_and_see_what_grew()
    {
        SpaceTrees.WriteFile(_root, Path.Combine("Videos", "a.mp4"), 4000);
        SpaceTrees.WriteFile(_root, Path.Combine("Docs", "b.pdf"), 1000);
        SpaceTrees.WriteFile(_root, Path.Combine("web", "node_modules", "lib.js"), 500);
        var store = new SnapshotStore(_data);

        // First scan: no history yet.
        var first = SpaceScanner.Run(new SpaceScanRequest { Root = _root });
        store.Load(_root).Should().BeNull();
        store.Save(SpaceSnapshot.From(first.Root, SpaceTrees.Now));

        var report = SpaceReport.For(first.Root, SpaceTrees.Now, null, null);
        report.ByType[0].Label.Should().Be("Videos");
        report.LargestFolders[0].Name.Should().Be("Videos");
        report.Cleanup.Should().ContainSingle(g => g.Kind == CleanupKind.BuildOutput).Which.Bytes.Should().Be(500);

        // The folder changes, then a second scan compares against the first.
        SpaceTrees.WriteFile(_root, Path.Combine("Videos", "b.mp4"), 6000);
        File.Delete(Path.Combine(_root, "Docs", "b.pdf"));
        var second = SpaceScanner.Run(new SpaceScanRequest { Root = _root });
        var previous = store.Load(_root);
        previous.Should().NotBeNull();

        var growth = GrowthComparer.Compare(previous!, SpaceSnapshot.From(second.Root, SpaceTrees.Now.AddDays(1)), 1, 10);

        growth.Select(g => (g.RelativePath, g.Delta)).Should().Equal(("Videos", 6000L), ("Docs", -1000L));
        var text = SpaceReportText.Render(SpaceReport.For(second.Root, SpaceTrees.Now.AddDays(1), growth, previous!.TakenUtc));
        text.Should().Contain("+5.86 KB  Videos");
    }

    [Fact]
    public void Drilling_into_a_folder_re_asks_every_question_of_that_folder_only()
    {
        SpaceTrees.WriteFile(_root, Path.Combine("Videos", "a.mp4"), 4000);
        SpaceTrees.WriteFile(_root, Path.Combine("Photos", "p.jpg"), 3000);
        SpaceTrees.WriteFile(_root, Path.Combine("Photos", "Raw", "p.cr2"), 2000);

        var root = SpaceScanner.Run(new SpaceScanRequest { Root = _root }).Root;
        var photos = root.Children.Single(c => c.Name == "Photos");
        var report = SpaceReport.For(photos, SpaceTrees.Now, null, null);

        report.ByType.Should().ContainSingle().Which.Bytes.Should().Be(5000);
        report.LargestFiles.Select(f => f.Name).Should().Equal("p.jpg", "p.cr2");
        report.LargestFolders.Select(f => f.Name).Should().Equal("Raw");
        TreemapLayout.Build(photos, new System.Drawing.RectangleF(0, 0, 600, 400)).Should().NotContain(c => c.Node != null && c.Node.Name == "a.mp4");
    }
}
