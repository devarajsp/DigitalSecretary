using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class IntelligencePipelineTests
{
    private static readonly DateTimeOffset Now = new(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void End_to_end_run_writes_all_outputs()
    {
        using var src = new TempDir();
        using var outp = new TempDir();
        WriteEml(src.Path, "1.eml", "alice@example.com", "me@example.com", "Hi", "Hello from Alice");
        WriteEml(src.Path, "2.eml", "me@example.com", "alice@example.com", "Re: Hi", "Reply body");
        WriteEml(src.Path, "3.eml", "bob@example.com", "me@example.com", "Project", "Project update budget");

        var options = new EmailIntelligenceOptions
        {
            InputDir = src.Path,
            OutputDir = outp.Path,
            OwnerAddress = "me@example.com",
            Now = Now,
        };

        var outDir = new IntelligencePipeline().RunAndExport(options);

        File.Exists(Path.Combine(outDir, "index.html")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "Contacts.csv")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "Contacts.vcf")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "network.graphml")).Should().BeTrue();
        File.Exists(Path.Combine(outDir, "data", "report.json")).Should().BeTrue();
    }

    [Fact]
    public void Analyze_resolves_people_and_excludes_the_owner()
    {
        using var src = new TempDir();
        WriteEml(src.Path, "1.eml", "alice@example.com", "me@example.com", "Hi", "body");

        var report = new IntelligencePipeline().Analyze(new EmailIntelligenceOptions
        {
            InputDir = src.Path,
            OwnerAddress = "me@example.com",
            Now = Now,
        });

        report.MessageCount.Should().Be(1);
        report.People.Should().Contain(p => p.Id == "alice@example.com");
        report.People.Should().NotContain(p => p.Id == "me@example.com");
    }

    [Theory]
    [InlineData("test@example.com", "Example")]
    [InlineData("me@gmail.com", null)]
    public void OrganizationFor_uses_company_domains_only(string address, string? expected)
        => IntelligencePipeline.OrganizationFor(address).Should().Be(expected);

    [Fact]
    public void Append_mode_consolidates_archives_and_dedupes_across_them()
    {
        using var archiveA = new TempDir();
        using var archiveB = new TempDir();
        using var outp = new TempDir();

        // Archive A: a message from Alice + one more.
        WriteEml(archiveA.Path, "1.eml", "alice@example.com", "me@example.com", "Hi", "from alice", "<shared@x>");
        WriteEml(archiveA.Path, "2.eml", "alice@example.com", "me@example.com", "More", "more", "<a2@x>");
        // Archive B: the SAME shared message (same Message-Id) + a new one from Bob.
        WriteEml(archiveB.Path, "1.eml", "alice@example.com", "me@example.com", "Hi", "from alice", "<shared@x>");
        WriteEml(archiveB.Path, "3.eml", "bob@example.com", "me@example.com", "Project", "budget", "<b3@x>");

        var pipeline = new IntelligencePipeline();
        pipeline.RunAndExport(new EmailIntelligenceOptions
        {
            InputDir = archiveA.Path, OutputDir = outp.Path, OwnerAddress = "me@example.com", Now = Now,
        });
        var outDir = pipeline.RunAndExport(new EmailIntelligenceOptions
        {
            InputDir = archiveB.Path, OutputDir = outp.Path, OwnerAddress = "me@example.com",
            Mode = AnalysisMode.Append, Now = Now,
        });

        var json = File.ReadAllText(Path.Combine(outDir, "data", "report.json"));
        json.Should().Contain("alice@example.com");
        json.Should().Contain("bob@example.com");
        // shared + a2 + b3 == 3 unique messages after cross-archive de-duplication.
        json.Should().Contain("\"messageCount\": 3");
    }

    [Fact]
    public void Overwrite_mode_replaces_with_only_the_current_input()
    {
        using var archiveA = new TempDir();
        using var archiveB = new TempDir();
        using var outp = new TempDir();
        WriteEml(archiveA.Path, "1.eml", "alice@example.com", "me@example.com", "Hi", "body", "<a1@x>");
        WriteEml(archiveB.Path, "1.eml", "bob@example.com", "me@example.com", "Hi", "body", "<b1@x>");

        var pipeline = new IntelligencePipeline();
        pipeline.RunAndExport(new EmailIntelligenceOptions
        {
            InputDir = archiveA.Path, OutputDir = outp.Path, OwnerAddress = "me@example.com", Now = Now,
        });
        var outDir = pipeline.RunAndExport(new EmailIntelligenceOptions
        {
            InputDir = archiveB.Path, OutputDir = outp.Path, OwnerAddress = "me@example.com", Now = Now,
        });

        var json = File.ReadAllText(Path.Combine(outDir, "data", "report.json"));
        json.Should().Contain("bob@example.com");
        json.Should().NotContain("alice@example.com");
    }

    private static void WriteEml(string dir, string name, string from, string to, string subject, string body, string? messageId = null)
        => File.WriteAllText(Path.Combine(dir, name),
            $"From: {from}\r\nTo: {to}\r\nSubject: {subject}\r\n" +
            "Date: Mon, 01 Jan 2024 10:00:00 +0000\r\n" +
            $"Message-Id: <{(messageId is null ? Guid.NewGuid().ToString("N") + "@x" : messageId.Trim('<', '>'))}>\r\n" +
            $"Content-Type: text/plain; charset=utf-8\r\n\r\n{body}\r\n");
}
