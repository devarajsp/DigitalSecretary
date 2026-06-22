using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class DocumentLibraryTests
{
    [Fact]
    public void Dedupes_by_content_hash_and_counts_occurrences()
    {
        var e1 = MailFactory.Msg("a@example.com", new[] { "me@example.com" }, default, "doc");
        e1.Attachments.Add(new ParsedAttachment("report.pdf", "application/pdf", 100, "hash1"));
        var e2 = MailFactory.Msg("b@example.com", new[] { "me@example.com" }, default, "doc again");
        e2.Attachments.Add(new ParsedAttachment("report-copy.pdf", "application/pdf", 100, "hash1"));
        e2.Attachments.Add(new ParsedAttachment("photo.png", "image/png", 50, "hash2"));

        var docs = new DocumentLibrary().Build(new[] { e1, e2 });

        docs.Should().HaveCount(2);
        var pdf = docs.Single(d => d.Sha256 == "hash1");
        pdf.Count.Should().Be(2);
        pdf.Type.Should().Be("PDF");
        docs.Single(d => d.Sha256 == "hash2").Type.Should().Be("Image");
    }

    [Theory]
    [InlineData("a.pdf", "application/pdf", "PDF")]
    [InlineData("a.png", "image/png", "Image")]
    [InlineData("a.xlsx", "application/octet-stream", "Spreadsheet")]
    [InlineData("a.docx", "application/octet-stream", "Document")]
    [InlineData("a.unknown", "application/octet-stream", "Other")]
    public void Classifies_by_extension(string name, string mime, string expected)
        => DocumentLibrary.Classify(new ParsedAttachment(name, mime, 1, "h")).Should().Be(expected);
}
