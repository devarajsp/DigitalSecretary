using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class EmailIntelligenceSettingsTests
{
    [Fact]
    public void Round_trips_settings_to_file()
    {
        using var dir = new TempDir();
        var store = new EmailIntelligenceSettingsStore(dir.Path);

        store.Save(new EmailIntelligenceSettings
        {
            InputDir = "in",
            OutputDir = "out",
            OwnerAddress = "me@example.com",
        });
        var loaded = store.Load();

        loaded.InputDir.Should().Be("in");
        loaded.OutputDir.Should().Be("out");
        loaded.OwnerAddress.Should().Be("me@example.com");
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        using var dir = new TempDir();
        new EmailIntelligenceSettingsStore(dir.Path).Load().InputDir.Should().BeEmpty();
    }
}
