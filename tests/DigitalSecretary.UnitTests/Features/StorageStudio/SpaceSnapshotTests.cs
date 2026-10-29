using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="SpaceSnapshot"/>, <see cref="GrowthComparer"/> and <see cref="SnapshotStore"/>.</summary>
public sealed class SpaceSnapshotTests : IDisposable
{
    private readonly string _data = SpaceTrees.TempFolder();

    public void Dispose() => SpaceTrees.Delete(_data);

    private static SpaceSnapshot Snap(params (string Path, long Bytes)[] folders) => new()
    {
        RootPath = SpaceTrees.RootPath,
        TakenUtc = SpaceTrees.Now,
        Folders = folders.ToDictionary(f => f.Path, f => f.Bytes, StringComparer.OrdinalIgnoreCase)
    };

    [Fact]
    public void A_snapshot_records_totals_and_folder_sizes_by_relative_path()
    {
        var snap = SpaceSnapshot.From(SpaceTrees.Sample(), SpaceTrees.Now);

        snap.RootPath.Should().Be(SpaceTrees.RootPath);
        snap.TotalBytes.Should().Be(1000);
        snap.TotalFiles.Should().Be(5);
        snap.Folders.Should().BeEquivalentTo(new Dictionary<string, long>
        {
            ["Videos"] = 700,
            ["Photos"] = 250,
            [Path.Combine("Photos", "Old")] = 50
        });
    }

    [Fact]
    public void Folders_deeper_than_the_limit_are_not_stored()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        var node = root;
        for (var i = 1; i <= SpaceSnapshot.MaxDepth + 2; i++)
        {
            node = node.AddFolder("d" + i);
        }
        node.AddFile("x.bin", 10, SpaceTrees.Now);

        SpaceSnapshot.From(root, SpaceTrees.Now).Folders.Should().HaveCount(SpaceSnapshot.MaxDepth);
    }

    [Fact]
    public void Compare_lists_grown_shrunk_new_and_removed_folders_biggest_change_first()
    {
        var before = Snap(("Videos", 1000), ("Photos", 500), ("Gone", 300), ("Same", 50));
        var after = Snap(("Videos", 5000), ("Photos", 100), ("New", 700), ("Same", 50));

        var changes = GrowthComparer.Compare(before, after, minDelta: 1, count: 10);

        changes.Select(c => (c.RelativePath, c.Delta)).Should().Equal(
            ("Videos", 4000L), ("New", 700L), ("Photos", -400L), ("Gone", -300L));
    }

    [Fact]
    public void Compare_ignores_small_changes_and_respects_the_count()
    {
        var before = Snap(("A", 100), ("B", 100), ("C", 100));
        var after = Snap(("A", 150), ("B", 900), ("C", 101));

        GrowthComparer.Compare(before, after, minDelta: 10, count: 10).Select(c => c.RelativePath).Should().Equal("B", "A");
        GrowthComparer.Compare(before, after, minDelta: 10, count: 1).Should().ContainSingle();
        GrowthComparer.Compare(before, after, minDelta: 10, count: 0).Should().BeEmpty();
    }

    [Fact]
    public void The_store_round_trips_a_snapshot_compressed()
    {
        var store = new SnapshotStore(_data);
        var snap = SpaceSnapshot.From(SpaceTrees.Sample(), SpaceTrees.Now);

        store.Save(snap);
        var loaded = store.Load(SpaceTrees.RootPath);

        loaded.Should().NotBeNull();
        loaded!.TotalBytes.Should().Be(1000);
        loaded.TakenUtc.Should().Be(SpaceTrees.Now);
        loaded.Folders.Should().BeEquivalentTo(snap.Folders);
        var file = store.PathFor(SpaceTrees.RootPath);
        file.Should().EndWith(".json.gz");
        File.ReadAllBytes(file).Take(2).Should().Equal(new byte[] { 0x1F, 0x8B }, "the file is gzip");
    }

    [Fact]
    public void Each_root_has_its_own_file_and_case_or_trailing_slash_do_not_matter()
    {
        var store = new SnapshotStore(_data);
        store.PathFor(@"C:\Users\You\Data").Should().Be(store.PathFor(@"c:\users\you\data\"));
        store.PathFor(@"C:\Users\You\Data").Should().NotBe(store.PathFor(@"C:\Users\You\Other"));
        Path.GetFileName(store.PathFor(@"C:\Users\You\Data")).Should().NotContain("You", "no path text leaks into the file name");
    }

    [Fact]
    public void Missing_or_corrupt_snapshots_load_as_null()
    {
        var store = new SnapshotStore(_data);
        store.Load(SpaceTrees.RootPath).Should().BeNull();

        Directory.CreateDirectory(Path.GetDirectoryName(store.PathFor(SpaceTrees.RootPath))!);
        File.WriteAllText(store.PathFor(SpaceTrees.RootPath), "not gzip");
        store.Load(SpaceTrees.RootPath).Should().BeNull();
    }

    [Fact]
    public void Saving_again_replaces_the_previous_snapshot()
    {
        var store = new SnapshotStore(_data);
        store.Save(Snap(("A", 1)));
        store.Save(Snap(("A", 2)));
        store.Load(SpaceTrees.RootPath)!.Folders["A"].Should().Be(2);
        Directory.GetFiles(Path.GetDirectoryName(store.PathFor(SpaceTrees.RootPath))!).Should().ContainSingle();
    }
}
