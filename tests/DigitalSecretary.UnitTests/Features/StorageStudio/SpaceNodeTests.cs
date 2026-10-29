using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="SpaceNode"/> - the running totals that every view reads.</summary>
public sealed class SpaceNodeTests
{
    [Fact]
    public void Adding_a_file_rolls_size_count_and_category_up_to_every_ancestor()
    {
        var root = SpaceTrees.Sample();
        var photos = root.Children.Single(c => c.Name == "Photos");

        root.Size.Should().Be(1000);
        root.FileCount.Should().Be(5);
        root.FolderCount.Should().Be(3);
        photos.Size.Should().Be(250);
        photos.FileCount.Should().Be(2);
        photos.FolderCount.Should().Be(1);
        root.CategorySizes[(int)FileCategory.Video].Should().Be(700);
        root.CategorySizes[(int)FileCategory.Picture].Should().Be(250);
        root.CategorySizes[(int)FileCategory.Document].Should().Be(50);
    }

    [Fact]
    public void A_folder_remembers_the_newest_file_time_beneath_it()
    {
        var root = SpaceTrees.Sample();
        root.LastWriteUtc.Should().Be(SpaceTrees.YearsAgo(0.1));
        root.Children.Single(c => c.Name == "Photos").LastWriteUtc.Should().Be(SpaceTrees.YearsAgo(0.2));
    }

    [Fact]
    public void Dominant_category_is_the_one_holding_the_most_bytes()
    {
        var root = SpaceTrees.Sample();
        root.DominantCategory.Should().Be(FileCategory.Video);
        root.Children.Single(c => c.Name == "Photos").DominantCategory.Should().Be(FileCategory.Picture);
        root.Children.Single(c => c.Name == "notes.txt").DominantCategory.Should().Be(FileCategory.Document);
    }

    [Fact]
    public void Sort_by_size_orders_children_largest_first_with_names_breaking_ties()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        root.AddFile("b.bin", 10, SpaceTrees.Now);
        root.AddFile("a.bin", 10, SpaceTrees.Now);
        root.AddFile("big.bin", 99, SpaceTrees.Now);

        root.SortBySize();

        root.Children.Select(c => c.Name).Should().Equal("big.bin", "a.bin", "b.bin");
    }

    [Fact]
    public void Full_and_relative_paths_are_rebuilt_from_the_chain_of_parents()
    {
        var root = SpaceTrees.Sample();
        var old = root.Descendants().Single(n => n.Name == "Old");

        old.FullPath.Should().Be(Path.Combine(SpaceTrees.RootPath, "Photos", "Old"));
        old.RelativeTo(root).Should().Be(Path.Combine("Photos", "Old"));
        old.Depth.Should().Be(2);
        root.RelativeTo(root).Should().BeEmpty();
    }

    [Fact]
    public void Descendants_lists_every_node_once()
    {
        SpaceTrees.Sample().Descendants().Should().HaveCount(8);
    }

    [Fact]
    public void A_negative_size_counts_as_zero()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        root.AddFile("odd.bin", -5, SpaceTrees.Now).Size.Should().Be(0);
        root.Size.Should().Be(0);
    }

    [Fact]
    public void A_file_cannot_have_children()
    {
        var file = SpaceNode.CreateRoot(SpaceTrees.RootPath).AddFile("x.txt", 1, SpaceTrees.Now);
        var act = () => file.AddFolder("nope");
        act.Should().Throw<InvalidOperationException>();
        file.CategorySizes.Should().BeEmpty();
        file.Children.Should().BeEmpty();
    }
}
