using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class ExportersTests
{
    private static IntelligenceReport SampleReport()
    {
        var report = new IntelligenceReport { OwnerAddress = "me@example.com", MessageCount = 2 };
        var person = new Person
        {
            Id = "a@example.com",
            DisplayName = "Alice",
            FromMe = 1,
            FromThem = 1,
            TotalMessages = 2,
            StrengthScore = 42.0,
            Organization = "Acme",
        };
        person.Addresses.Add("a@example.com");
        person.Phones.Add("+1 555 0000");
        person.Urls.Add("https://a.example");
        person.Topics.Add("project");
        report.People.Add(person);
        report.Edges.Add(new GraphEdge("a@example.com", "b@example.com", 2));
        return report;
    }

    [Fact]
    public void Json_contains_owner_and_person()
    {
        var json = new JsonExporter().Serialize(SampleReport());

        json.Should().Contain("me@example.com");
        json.Should().Contain("Alice");
    }

    [Fact]
    public void Csv_has_header_and_a_row()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "c.csv");

        new CsvExporter().WriteContacts(SampleReport(), path);

        var text = File.ReadAllText(path);
        text.Should().Contain("Name,Primary Address");
        text.Should().Contain("Alice");
    }

    [Fact]
    public void VCard_is_well_formed()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "c.vcf");

        new VCardExporter().Write(SampleReport(), path);

        var text = File.ReadAllText(path);
        text.Should().Contain("BEGIN:VCARD");
        text.Should().Contain("EMAIL;TYPE=INTERNET:a@example.com");
    }

    [Fact]
    public void GraphMl_has_nodes_and_edges()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "g.graphml");

        new GraphMlExporter().Write(SampleReport(), path);

        var text = File.ReadAllText(path);
        text.Should().Contain("<graphml");
        text.Should().Contain("<edge");
    }
}
