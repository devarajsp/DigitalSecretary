using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>
/// Covers the remembered settings. The defaults matter as much as the round-trip: the first run of a
/// destructive feature has to be the safe one.
/// </summary>
public sealed class DeDupeSettingsTests
{
    private const string FileName = "dedupe_settings.json";

    [Fact]
    public void Defaults_are_the_safe_ones()
    {
        var settings = new DeDupeSettings();

        settings.DryRun.Should().BeTrue();
        settings.RecycleBin.Should().BeTrue();
        settings.VerifyBeforeDelete.Should().BeTrue();
        settings.IgnoreEmptyFiles.Should().BeTrue();
        settings.OnExistingList.Should().Be(ExistingListAction.Ask);
        settings.Keeper.Should().Be(KeeperPolicy.ShortestPath);
        settings.Root.Should().BeEmpty();
    }

    [Fact]
    public void Load_returns_the_defaults_when_no_file_exists()
    {
        using var dir = new TempDir();

        var settings = new DeDupeSettingsStore(dir.Path).Load();

        settings.Root.Should().BeEmpty();
        settings.DryRun.Should().BeTrue();
        settings.RecycleBin.Should().BeTrue();
        settings.VerifyBeforeDelete.Should().BeTrue();
        settings.IgnoreEmptyFiles.Should().BeTrue();
        settings.OnExistingList.Should().Be(ExistingListAction.Ask);
        settings.Keeper.Should().Be(KeeperPolicy.ShortestPath);
    }

    [Fact]
    public void Save_then_load_round_trips_every_property()
    {
        using var dir = new TempDir();
        var store = new DeDupeSettingsStore(dir.Path);

        store.Save(new DeDupeSettings
        {
            Root = @"D:\Pictures",
            OnExistingList = ExistingListAction.UseAsIs,
            Keeper = KeeperPolicy.NewestModified,
            IgnoreEmptyFiles = false,
            VerifyBeforeDelete = false,
            RecycleBin = false,
            DryRun = false,
        });

        var loaded = store.Load();
        loaded.Root.Should().Be(@"D:\Pictures");
        loaded.OnExistingList.Should().Be(ExistingListAction.UseAsIs);
        loaded.Keeper.Should().Be(KeeperPolicy.NewestModified);
        loaded.IgnoreEmptyFiles.Should().BeFalse();
        loaded.VerifyBeforeDelete.Should().BeFalse();
        loaded.RecycleBin.Should().BeFalse();
        loaded.DryRun.Should().BeFalse();
    }

    [Fact]
    public void Save_writes_the_enums_as_readable_strings()
    {
        using var dir = new TempDir();
        var store = new DeDupeSettingsStore(dir.Path);

        store.Save(new DeDupeSettings { Keeper = KeeperPolicy.ShortestPath, OnExistingList = ExistingListAction.Ask });

        var json = File.ReadAllText(Path.Combine(dir.Path, FileName));
        json.Should().Contain("\"ShortestPath\"");
        json.Should().Contain("\"Ask\"");
        json.Should().NotContain("\"Keeper\": 0");
    }

    [Fact]
    public void Save_writes_a_non_default_enum_as_its_own_name()
    {
        using var dir = new TempDir();
        var store = new DeDupeSettingsStore(dir.Path);

        store.Save(new DeDupeSettings
        {
            Keeper = KeeperPolicy.FirstAlphabetical,
            OnExistingList = ExistingListAction.Update,
        });

        var json = File.ReadAllText(Path.Combine(dir.Path, FileName));
        json.Should().Contain("\"FirstAlphabetical\"");
        json.Should().Contain("\"Update\"");
    }

    [Fact]
    public void Load_returns_the_defaults_for_a_corrupt_file_instead_of_throwing()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, FileName), "{ this is not json at all");

        var settings = new DeDupeSettingsStore(dir.Path).Load();

        settings.DryRun.Should().BeTrue();
        settings.Keeper.Should().Be(KeeperPolicy.ShortestPath);
        settings.Root.Should().BeEmpty();
    }

    [Fact]
    public void Load_returns_the_defaults_for_a_file_holding_json_null()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, FileName), "null");

        var settings = new DeDupeSettingsStore(dir.Path).Load();

        settings.DryRun.Should().BeTrue();
        settings.OnExistingList.Should().Be(ExistingListAction.Ask);
    }

    [Fact]
    public void Save_overwrites_a_previously_saved_file()
    {
        using var dir = new TempDir();
        var store = new DeDupeSettingsStore(dir.Path);
        store.Save(new DeDupeSettings { Root = @"D:\First" });

        store.Save(new DeDupeSettings { Root = @"D:\Second" });

        store.Load().Root.Should().Be(@"D:\Second");
    }
}
