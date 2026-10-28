using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>Covers the in-memory checksum index: lookups, updates, pruning and hash grouping.</summary>
public sealed class ChecksumIndexTests
{
    private const string HashA = "900150983cd24fb0d6963f7d28e17f72";
    private const string HashB = "d41d8cd98f00b204e9800998ecf8427e";

    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static ChecksumEntry Entry(string path, string hash = HashA, long length = 100)
        => new(hash, length, Stamp, path);

    private static HashSet<string> Live(params string[] paths)
        => new(paths, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Put_adds_a_new_entry()
    {
        var index = new ChecksumIndex();

        index.Put(Entry(@"C:\a.txt"));

        index.Count.Should().Be(1);
        index.Find(@"C:\a.txt").Should().NotBeNull();
    }

    [Fact]
    public void Put_replaces_the_entry_for_the_same_path_ignoring_case()
    {
        var index = new ChecksumIndex();
        index.Put(Entry(@"C:\a.txt", HashA, 100));

        index.Put(Entry(@"C:\A.TXT", HashB, 250));

        index.Count.Should().Be(1);
        index.Find(@"C:\a.txt")!.Md5.Should().Be(HashB);
        index.Find(@"C:\a.txt")!.Length.Should().Be(250);
    }

    [Fact]
    public void Find_returns_null_for_an_unknown_path()
    {
        var index = new ChecksumIndex();
        index.Put(Entry(@"C:\a.txt"));

        index.Find(@"C:\somewhere-else.txt").Should().BeNull();
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        var index = new ChecksumIndex();
        index.Put(Entry(@"C:\Pics\A.jpg"));

        index.Find(@"c:\pics\a.jpg").Should().NotBeNull();
    }

    [Fact]
    public void Remove_drops_a_known_path_and_reports_an_unknown_one()
    {
        var index = new ChecksumIndex();
        index.Put(Entry(@"C:\a.txt"));

        index.Remove(@"C:\a.txt").Should().BeTrue();
        index.Remove(@"C:\a.txt").Should().BeFalse();
        index.Count.Should().Be(0);
    }

    [Fact]
    public void Count_tracks_the_number_of_distinct_paths()
    {
        var index = new ChecksumIndex();

        index.Count.Should().Be(0);
        index.Put(Entry(@"C:\a.txt"));
        index.Put(Entry(@"C:\b.txt"));
        index.Count.Should().Be(2);
    }

    [Fact]
    public void Total_bytes_sums_the_recorded_sizes()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(@"C:\a.txt", HashA, 100),
            Entry(@"C:\b.txt", HashB, 250),
        });

        index.TotalBytes.Should().Be(350);
    }

    [Fact]
    public void Total_bytes_ignores_an_unrecorded_or_negative_length()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(@"C:\a.txt", HashA, 100),
            Entry(@"C:\legacy.txt", HashB, -1),
        });

        index.TotalBytes.Should().Be(100);
    }

    [Fact]
    public void From_entries_keeps_one_entry_per_repeated_path()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(@"C:\a.txt", HashA, 100),
            Entry(@"C:\a.txt", HashB, 900),
        });

        index.Count.Should().Be(1);
        index.Find(@"C:\a.txt")!.Md5.Should().Be(HashB);
    }

    [Fact]
    public void Retain_only_prunes_paths_that_are_no_longer_live_and_returns_them()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(@"C:\keep.txt"),
            Entry(@"C:\gone.txt"),
            Entry(@"C:\also-gone.txt"),
        });

        var removed = index.RetainOnly(Live(@"C:\keep.txt"));

        removed.Should().BeEquivalentTo(new[] { @"C:\gone.txt", @"C:\also-gone.txt" });
        index.Count.Should().Be(1);
        index.Find(@"C:\keep.txt").Should().NotBeNull();
    }

    [Fact]
    public void Retain_only_removes_nothing_when_every_path_is_live()
    {
        var index = ChecksumIndex.FromEntries(new[] { Entry(@"C:\a.txt"), Entry(@"C:\b.txt") });

        index.RetainOnly(Live(@"C:\a.txt", @"C:\b.txt")).Should().BeEmpty();
        index.Count.Should().Be(2);
    }

    [Fact]
    public void Group_by_hash_groups_identical_checksums_and_orders_each_group_by_path()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(@"C:\z\third.txt", HashA),
            Entry(@"C:\a\first.txt", HashA),
            Entry(@"C:\m\second.txt", HashA),
        });

        var groups = index.GroupByHash();

        groups.Should().ContainKey(HashA);
        groups[HashA].Select(e => e.FullPath).Should()
            .Equal(@"C:\a\first.txt", @"C:\m\second.txt", @"C:\z\third.txt");
    }

    [Fact]
    public void Group_by_hash_keeps_a_single_file_in_its_own_one_item_group()
    {
        var index = ChecksumIndex.FromEntries(new[]
        {
            Entry(@"C:\a.txt", HashA),
            Entry(@"C:\b.txt", HashA),
            Entry(@"C:\lonely.txt", HashB),
        });

        var groups = index.GroupByHash();

        groups.Should().HaveCount(2);
        groups[HashA].Should().HaveCount(2);
        groups[HashB].Should().HaveCount(1);
        groups[HashB][0].FullPath.Should().Be(@"C:\lonely.txt");
    }

    [Fact]
    public void Entries_exposes_everything_in_the_index()
    {
        var index = ChecksumIndex.FromEntries(new[] { Entry(@"C:\a.txt"), Entry(@"C:\b.txt") });

        index.Entries.Select(e => e.FullPath).Should().BeEquivalentTo(new[] { @"C:\a.txt", @"C:\b.txt" });
    }
}
