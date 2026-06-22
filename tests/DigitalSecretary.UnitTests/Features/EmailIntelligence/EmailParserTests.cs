using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class EmailParserTests
{
    [Fact]
    public void ParseEml_reads_headers_body_and_message_id()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "m.eml");
        File.WriteAllText(path,
            "From: Alice <Alice@Example.com>\r\n" +
            "To: Bob <bob@example.com>\r\n" +
            "Subject: Hello there\r\n" +
            "Date: Wed, 01 Jan 2024 10:00:00 +0000\r\n" +
            "Message-Id: <m1@example.com>\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            "Hello world body\r\n");

        var email = new EmailParser().ParseEml(path);

        email.From!.Address.Should().Be("alice@example.com");
        email.To.Should().ContainSingle(p => p.Address == "bob@example.com");
        email.Subject.Should().Be("Hello there");
        email.MessageId.Should().Be("m1@example.com");
        email.Body.Should().Contain("Hello world body");
    }

    [Fact]
    public void ParseTxt_reads_the_downloader_format()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "m.txt");
        File.WriteAllText(path,
            "From:    Carol <carol@example.com>\r\n" +
            "To:      me@example.com\r\n" +
            "Date:    Wed, 01 Jan 2024 10:00:00 +0000\r\n" +
            "Subject: Lunch plans\r\n" +
            new string('=', 70) + "\r\n\r\n" +
            "Are we still on for lunch?\r\n");

        var email = new EmailParser().ParseTxt(path);

        email.From!.Address.Should().Be("carol@example.com");
        email.Subject.Should().Be("Lunch plans");
        email.Body.Should().Contain("lunch");
    }

    [Fact]
    public void ParseFile_returns_null_when_unreadable()
        => new EmailParser()
            .ParseFile(Path.Combine(Path.GetTempPath(), "ds_missing_zzz.eml"))
            .Should().BeNull();
}
