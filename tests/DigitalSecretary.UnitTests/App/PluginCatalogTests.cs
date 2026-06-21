using DigitalSecretary.App.Hosting;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.App;

public sealed class PluginCatalogTests
{
    private static void WriteManifest(string root, string folder, string json)
    {
        var dir = Path.Combine(root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), json);
    }

    [Fact]
    public void Missing_plugins_folder_yields_empty()
    {
        var catalog = new PluginCatalog(Path.Combine(Path.GetTempPath(), "does_not_exist_" + Guid.NewGuid()));
        catalog.Discover().Should().BeEmpty();
    }

    [Fact]
    public void Discovers_valid_manifests_in_order()
    {
        using var dir = new TempDir();
        WriteManifest(dir.Path, "b", """{ "id":"bbb","title":"Bbb","order":20,"entryAssembly":"b.dll","entryType":"B" }""");
        WriteManifest(dir.Path, "a", """{ "id":"aaa","title":"Aaa","order":10,"entryAssembly":"a.dll","entryType":"A" }""");

        var found = new PluginCatalog(dir.Path).Discover();

        found.Select(m => m.Id).Should().ContainInOrder("aaa", "bbb");
        found[0].Directory.Should().Be(Path.Combine(dir.Path, "a"));
    }

    [Fact]
    public void Skips_manifest_missing_required_fields()
    {
        using var dir = new TempDir();
        WriteManifest(dir.Path, "good", """{ "id":"good","title":"Good","entryAssembly":"g.dll","entryType":"G" }""");
        WriteManifest(dir.Path, "noid", """{ "title":"No id","entryAssembly":"n.dll","entryType":"N" }""");
        WriteManifest(dir.Path, "notype", """{ "id":"notype","title":"No type" }""");

        var found = new PluginCatalog(dir.Path).Discover();

        found.Select(m => m.Id).Should().BeEquivalentTo(new[] { "good" });
    }

    [Fact]
    public void Skips_malformed_json_without_throwing()
    {
        using var dir = new TempDir();
        WriteManifest(dir.Path, "bad", "{ not json");
        WriteManifest(dir.Path, "ok", """{ "id":"ok","title":"Ok","entryAssembly":"o.dll","entryType":"O" }""");

        var found = new PluginCatalog(dir.Path).Discover();

        found.Select(m => m.Id).Should().BeEquivalentTo(new[] { "ok" });
    }
}
