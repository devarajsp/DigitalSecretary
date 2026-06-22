using DigitalSecretary.Features.EmailDownloader;
using DigitalSecretary.Features.GmailDownloader;
using DigitalSecretary.Features.GoogleDriveDownloader;
using DigitalSecretary.Features.Launcher;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class StoreTests
{
    [Fact]
    public void Launcher_load_returns_empty_when_no_file()
    {
        using var dir = new TempDir();
        new LauncherStore(dir.Path).Load().Should().BeEmpty();
    }

    [Fact]
    public void Launcher_round_trips_items()
    {
        using var dir = new TempDir();
        var store = new LauncherStore(dir.Path);

        store.Save(new List<LauncherItem>
        {
            new() { Name = "Notepad", Target = @"C:\Windows\notepad.exe" },
            new() { Name = "Site", Target = "https://example.com" }
        });

        var loaded = store.Load();
        loaded.Should().HaveCount(2);
        loaded[0].Name.Should().Be("Notepad");
        loaded[1].Target.Should().Be("https://example.com");
    }

    [Fact]
    public void Launcher_corrupt_file_returns_empty()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "shortcuts.json"), "{ not valid json");

        new LauncherStore(dir.Path).Load().Should().BeEmpty();
    }

    [Fact]
    public void Email_settings_round_trip_excludes_nothing_remembered()
    {
        using var dir = new TempDir();
        var store = new EmailSettingsStore(dir.Path);

        store.Save(new EmailSettings { Email = "me@yahoo.com", DownloadDir = @"D:\Mail" });

        var loaded = store.Load();
        loaded.Email.Should().Be("me@yahoo.com");
        loaded.DownloadDir.Should().Be(@"D:\Mail");
    }

    [Fact]
    public void Email_settings_default_when_absent()
    {
        using var dir = new TempDir();
        var loaded = new EmailSettingsStore(dir.Path).Load();

        loaded.Email.Should().BeEmpty();
        loaded.DownloadDir.Should().BeEmpty();
    }

    [Fact]
    public void Gmail_settings_round_trip()
    {
        using var dir = new TempDir();
        var store = new GmailSettingsStore(dir.Path);

        store.Save(new GmailSettings { Email = "me@gmail.com", DownloadDir = @"D:\Gmail" });

        var loaded = store.Load();
        loaded.Email.Should().Be("me@gmail.com");
        loaded.DownloadDir.Should().Be(@"D:\Gmail");
    }

    [Fact]
    public void Gmail_settings_default_when_absent()
    {
        using var dir = new TempDir();
        var loaded = new GmailSettingsStore(dir.Path).Load();

        loaded.Email.Should().BeEmpty();
        loaded.DownloadDir.Should().BeEmpty();
    }

    [Fact]
    public void Drive_settings_round_trip_remembers_path_not_token()
    {
        using var dir = new TempDir();
        var store = new DriveSettingsStore(dir.Path);

        store.Save(new DriveSettings { CredentialsPath = @"C:\creds\client.json", DownloadDir = @"D:\Drive" });

        var loaded = store.Load();
        loaded.CredentialsPath.Should().Be(@"C:\creds\client.json");
        loaded.DownloadDir.Should().Be(@"D:\Drive");
    }

    [Fact]
    public void Drive_settings_default_when_absent()
    {
        using var dir = new TempDir();
        var loaded = new DriveSettingsStore(dir.Path).Load();

        loaded.CredentialsPath.Should().BeEmpty();
        loaded.DownloadDir.Should().BeEmpty();
    }
}
