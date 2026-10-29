using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="CleanupFinder"/> - the conservative, report-only clean-up rules.</summary>
public sealed class CleanupFinderTests
{
    private static SpaceNode Tree()
    {
        var root = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        var web = root.AddFolder("web");
        var modules = web.AddFolder("node_modules");
        modules.AddFile("lib.js", 300, SpaceTrees.Now);
        modules.AddFolder("node_modules").AddFile("nested.js", 100, SpaceTrees.Now);   // counted once, via the outer folder

        var app = root.AddFolder("app");
        app.AddFolder("obj").AddFile("x.obj", 40, SpaceTrees.Now);
        app.AddFolder("bin").AddFile("app.dll", 60, SpaceTrees.Now);                   // bin counts: obj is beside it
        root.AddFolder("tools").AddFolder("bin").AddFile("tool.exe", 70, SpaceTrees.Now); // bin alone does not

        var downloads = root.AddFolder("Downloads");
        downloads.AddFile("setup.exe", 80, SpaceTrees.Now);
        downloads.AddFile("movie.mp4.crdownload", 30, SpaceTrees.Now);
        root.AddFile("disk.iso", 90, SpaceTrees.Now);
        root.AddFile("~$draft.docx", 1, SpaceTrees.Now);
        root.AddFile("archive.mkv", 2000, SpaceTrees.YearsAgo(3));
        root.AddFile("recent.mkv", 2000, SpaceTrees.YearsAgo(0.5));
        root.SortBySize();
        return root;
    }

    private static readonly CleanupOptions SmallThreshold = new() { LargeFileBytes = 1000 };

    private static CleanupGroup Group(IReadOnlyList<CleanupGroup> groups, CleanupKind kind)
        => groups.Single(g => g.Kind == kind);

    [Fact]
    public void Build_folders_are_found_once_and_not_searched_further()
    {
        var build = Group(CleanupFinder.Find(Tree(), SpaceTrees.Now, SmallThreshold), CleanupKind.BuildOutput);

        build.Items.Select(i => i.RelativeTo(i.Parent!.Parent!) ).Should().BeEquivalentTo(
            [Path.Combine("web", "node_modules"), Path.Combine("app", "obj"), Path.Combine("app", "bin")]);
        build.Bytes.Should().Be(400 + 40 + 60);
    }

    [Fact]
    public void A_bin_folder_without_an_obj_beside_it_is_not_build_output()
    {
        var tools = Tree().Children.Single(c => c.Name == "tools");
        CleanupFinder.ClassifyFolder(tools.Children.Single()).Should().BeNull();
    }

    [Fact]
    public void Temporary_and_partial_files_are_found()
    {
        var temp = Group(CleanupFinder.Find(Tree(), SpaceTrees.Now, SmallThreshold), CleanupKind.TemporaryFiles);
        temp.Items.Select(i => i.Name).Should().BeEquivalentTo(["movie.mp4.crdownload", "~$draft.docx"]);
    }

    [Fact]
    public void Exe_counts_as_an_installer_only_inside_downloads_but_disk_images_count_anywhere()
    {
        var installers = Group(CleanupFinder.Find(Tree(), SpaceTrees.Now, SmallThreshold), CleanupKind.Installers);
        installers.Items.Select(i => i.Name).Should().BeEquivalentTo(["setup.exe", "disk.iso"]);
    }

    [Fact]
    public void A_root_that_is_itself_inside_downloads_still_counts_exes()
    {
        var root = SpaceNode.CreateRoot(@"C:\Users\You\Downloads\Apps");
        var exe = root.AddFile("setup.exe", 10, SpaceTrees.Now);
        CleanupFinder.ClassifyFile(exe, SpaceTrees.Now, new CleanupOptions()).Should().Be(CleanupKind.Installers);
    }

    [Fact]
    public void Large_files_count_only_when_also_old()
    {
        var old = Group(CleanupFinder.Find(Tree(), SpaceTrees.Now, SmallThreshold), CleanupKind.LargeAndOld);
        old.Items.Should().ContainSingle().Which.Name.Should().Be("archive.mkv");
    }

    [Fact]
    public void Groups_are_largest_first_and_empty_groups_are_left_out()
    {
        var groups = CleanupFinder.Find(Tree(), SpaceTrees.Now, SmallThreshold);
        groups.Select(g => g.Bytes).Should().BeInDescendingOrder();

        var plain = SpaceNode.CreateRoot(SpaceTrees.RootPath);
        plain.AddFile("photo.jpg", 10, SpaceTrees.Now);
        CleanupFinder.Find(plain, SpaceTrees.Now).Should().BeEmpty();
    }

    [Fact]
    public void Every_kind_has_a_title_and_a_reason()
    {
        foreach (var kind in Enum.GetValues<CleanupKind>())
        {
            CleanupFinder.Title(kind).Should().NotBeNullOrWhiteSpace();
            CleanupFinder.Why(kind).Should().NotBeNullOrWhiteSpace();
        }
        Enum.GetValues<CleanupKind>().Select(CleanupFinder.Title).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Default_threshold_is_one_gigabyte_and_two_years()
    {
        var options = new CleanupOptions();
        options.LargeFileBytes.Should().Be(1L << 30);
        options.OldAfter.Should().Be(TimeSpan.FromDays(730));
    }
}
