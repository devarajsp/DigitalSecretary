using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>
/// Covers <see cref="SampleSpaceTree"/> - the made-up tree the documentation screenshots show. It
/// must use only the placeholder path and exercise every chart and card the manual describes.
/// </summary>
public sealed class SampleSpaceTreeTests
{
    private static readonly SpaceNode Tree = SampleSpaceTree.Build(SpaceTrees.Now);

    [Fact]
    public void Every_path_sits_under_the_placeholder_root()
    {
        SampleSpaceTree.RootPath.Should().Be(@"C:\Users\You\Documents");
        Tree.Descendants().Should().OnlyContain(n => n.FullPath.StartsWith(SampleSpaceTree.RootPath + @"\", StringComparison.Ordinal));
    }

    [Fact]
    public void It_is_deterministic()
    {
        var again = SampleSpaceTree.Build(SpaceTrees.Now);
        again.Size.Should().Be(Tree.Size);
        again.Descendants().Select(n => n.FullPath).Should().Equal(Tree.Descendants().Select(n => n.FullPath));
    }

    [Fact]
    public void It_shows_something_in_every_clean_up_card_and_several_types_and_ages()
    {
        CleanupFinder.Find(Tree, SpaceTrees.Now).Select(g => g.Kind).Should().BeEquivalentTo(Enum.GetValues<CleanupKind>());
        SpaceAnalyzer.ByType(Tree).Should().HaveCountGreaterThanOrEqualTo(6);
        SpaceAnalyzer.ByAge(Tree, SpaceTrees.Now).Should().OnlyContain(b => b.Bytes > 0);
    }
}
