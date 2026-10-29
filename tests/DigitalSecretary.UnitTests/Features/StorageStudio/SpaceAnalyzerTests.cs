using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="SpaceAnalyzer"/> - the by-type, by-age and largest-items questions.</summary>
public sealed class SpaceAnalyzerTests
{
    [Fact]
    public void By_type_lists_non_empty_categories_largest_first_with_shares_and_counts()
    {
        var items = SpaceAnalyzer.ByType(SpaceTrees.Sample());

        items.Select(i => i.Label).Should().Equal("Videos", "Pictures", "Documents");
        items[0].Bytes.Should().Be(700);
        items[0].Files.Should().Be(2);
        items[0].Share.Should().BeApproximately(0.7, 1e-9);
        items.Sum(i => i.Share).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void By_type_works_on_a_sub_folder()
    {
        var photos = SpaceTrees.Sample().Children.Single(c => c.Name == "Photos");
        var items = SpaceAnalyzer.ByType(photos);
        items.Should().ContainSingle().Which.Should().Be(new BreakdownItem("Pictures", 250, 2, 1.0));
    }

    [Fact]
    public void By_age_always_has_four_buckets_in_age_order()
    {
        var items = SpaceAnalyzer.ByAge(SpaceTrees.Sample(), SpaceTrees.Now);

        items.Select(i => i.Bytes).Should().Equal(900, 50, 0, 50);
        items.Select(i => i.Files).Should().Equal(3, 1, 0, 1);
        items[0].Label.Should().Be(SpaceAnalyzer.Label(AgeBucket.UnderOneYear));
    }

    [Theory]
    [InlineData(0.0, AgeBucket.UnderOneYear)]
    [InlineData(0.99, AgeBucket.UnderOneYear)]
    [InlineData(1.0, AgeBucket.OneToThreeYears)]
    [InlineData(2.99, AgeBucket.OneToThreeYears)]
    [InlineData(3.0, AgeBucket.ThreeToFiveYears)]
    [InlineData(5.0, AgeBucket.OverFiveYears)]
    [InlineData(-1.0, AgeBucket.UnderOneYear)]
    public void Bucket_boundaries(double yearsAgo, AgeBucket expected)
        => SpaceAnalyzer.BucketFor(SpaceTrees.YearsAgo(yearsAgo), SpaceTrees.Now).Should().Be(expected);

    [Fact]
    public void Largest_files_are_the_top_n_by_size()
    {
        var top = SpaceAnalyzer.LargestFiles(SpaceTrees.Sample(), 3);
        top.Select(f => f.Name).Should().Equal("holiday.mp4", "a.jpg", "clip.mov");
    }

    [Fact]
    public void Largest_folders_exclude_the_folder_itself_and_include_nested_ones()
    {
        var top = SpaceAnalyzer.LargestFolders(SpaceTrees.Sample(), 10);
        top.Select(f => f.Name).Should().Equal("Videos", "Photos", "Old");
    }

    [Fact]
    public void Asking_for_zero_or_more_than_exist_is_safe()
    {
        var root = SpaceTrees.Sample();
        SpaceAnalyzer.LargestFiles(root, 0).Should().BeEmpty();
        SpaceAnalyzer.LargestFiles(root, 100).Should().HaveCount(5);
    }

    [Fact]
    public void Ties_are_ordered_by_name_so_lists_are_stable()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        foreach (var name in new[] { "c.bin", "a.bin", "b.bin" })
        {
            root.AddFile(name, 5, SpaceTrees.Now);
        }
        SpaceAnalyzer.LargestFiles(root, 3).Select(f => f.Name).Should().Equal("a.bin", "b.bin", "c.bin");
    }

    [Fact]
    public void An_empty_folder_has_no_shares_and_no_division_by_zero()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        SpaceAnalyzer.ByType(root).Should().BeEmpty();
        SpaceAnalyzer.ByAge(root, SpaceTrees.Now).Should().OnlyContain(i => i.Bytes == 0 && i.Share == 0);
        SpaceAnalyzer.Share(5, 0).Should().Be(0);
    }

    [Fact]
    public void A_file_node_can_be_analysed_on_its_own()
    {
        var file = SpaceTrees.Sample().Descendants().Single(n => n.Name == "a.jpg");
        SpaceAnalyzer.ByType(file).Should().ContainSingle().Which.Bytes.Should().Be(200);
        SpaceAnalyzer.Files(file).Should().ContainSingle();
    }
}
