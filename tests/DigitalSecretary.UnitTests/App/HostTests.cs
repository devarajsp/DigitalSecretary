using DigitalSecretary.App.Hosting;
using DigitalSecretary.App.Settings;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.App;

public sealed class HostTests
{
    [Fact]
    public void AppSettings_defaults_are_empty()
    {
        var settings = new AppSettings();
        settings.HiddenOnDashboard.Should().BeEmpty();
        settings.HiddenOnMenu.Should().BeEmpty();
    }

    [Fact]
    public void Settings_round_trip_to_file()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "app-settings.json");
        var settings = new AppSettings
        {
            HiddenOnDashboard = { "calculator" },
            HiddenOnMenu = { "clipboard-history", "launcher" }
        };

        SettingsStore.Save(settings, path);
        var loaded = SettingsStore.Load(path);

        loaded.HiddenOnDashboard.Should().ContainSingle().Which.Should().Be("calculator");
        loaded.HiddenOnMenu.Should().BeEquivalentTo(new[] { "clipboard-history", "launcher" });
    }

    [Fact]
    public void Settings_load_missing_file_returns_defaults()
    {
        using var dir = new TempDir();
        var loaded = SettingsStore.Load(Path.Combine(dir.Path, "nope.json"));
        loaded.HiddenOnDashboard.Should().BeEmpty();
    }

    [Fact]
    public void FeatureContext_creates_and_exposes_private_data_dir()
    {
        using var dir = new TempDir();
        var logged = new List<string>();

        var context = new FeatureContext("notes", dir.Path, logged.Add);

        context.FeatureId.Should().Be("notes");
        context.DataDirectory.Should().Be(Path.Combine(dir.Path, "notes"));
        Directory.Exists(context.DataDirectory).Should().BeTrue();

        context.Log("hi");
        logged.Should().ContainSingle().Which.Should().Contain("notes").And.Contain("hi");
    }

    [Fact]
    public void PluginManifest_has_sensible_defaults()
    {
        var manifest = new PluginManifest();
        manifest.Category.Should().Be("General");
        manifest.Order.Should().Be(100);
        manifest.Id.Should().BeEmpty();
    }
}
