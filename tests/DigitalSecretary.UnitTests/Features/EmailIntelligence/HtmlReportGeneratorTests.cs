using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class HtmlReportGeneratorTests
{
    [Fact]
    public void Writes_a_self_contained_report_with_tabs_and_data()
    {
        using var dir = new TempDir();
        var report = new IntelligenceReport { OwnerAddress = "me@example.com" };
        var person = new Person { Id = "a@example.com", DisplayName = "Alice" };
        person.Addresses.Add("a@example.com");
        report.People.Add(person);

        new HtmlReportGenerator().Write(report, dir.Path);

        File.Exists(Path.Combine(dir.Path, "index.html")).Should().BeTrue();
        File.Exists(Path.Combine(dir.Path, "assets", "app.js")).Should().BeTrue();
        File.Exists(Path.Combine(dir.Path, "assets", "styles.css")).Should().BeTrue();

        File.ReadAllText(Path.Combine(dir.Path, "index.html")).Should().Contain("Email Intelligence");
        File.ReadAllText(Path.Combine(dir.Path, "data", "data.js")).Should().StartWith("window.__DATA__ =");
    }
}
