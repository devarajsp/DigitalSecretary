using DigitalSecretary.Features.GmailDownloader;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class GmailFileNamingTests
{
    [Fact]
    public void Sanitize_replaces_invalid_characters()
    {
        var result = GmailFileNaming.Sanitize("a/b:c*d?e");

        result.Should().NotContainAny("/", ":", "*", "?");
        result.Should().Be("a_b_c_d_e");
    }

    [Fact]
    public void Sanitize_trims_trailing_dots_and_spaces()
        => GmailFileNaming.Sanitize("  report.  ").Should().Be("report");

    [Fact]
    public void Sanitize_empty_becomes_underscore()
        => GmailFileNaming.Sanitize("   ").Should().Be("_");

    [Fact]
    public void BaseName_uses_date_prefix_and_subject()
    {
        var date = new DateTimeOffset(2026, 6, 20, 14, 25, 30, TimeSpan.Zero).ToLocalTime();

        var result = GmailFileNaming.BaseNameFor(date, "Hello World");

        result.Should().EndWith("_Hello World");
        result.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}_\d{6}_");
    }

    [Fact]
    public void BaseName_blank_subject_becomes_no_subject()
        => GmailFileNaming.BaseNameFor(DateTimeOffset.Now, "  ").Should().EndWith("_no-subject");

    [Fact]
    public void BaseName_caps_long_subject()
    {
        var longSubject = new string('a', 200);

        var result = GmailFileNaming.BaseNameFor(DateTimeOffset.Now, longSubject);

        var subjectPart = result[(result.IndexOf('_', 11) + 1)..];
        subjectPart.Length.Should().BeLessThanOrEqualTo(GmailFileNaming.MaxBaseLength);
    }

    [Fact]
    public void GetUniquePath_returns_original_when_absent()
    {
        using var dir = new TempDir();

        var path = GmailFileNaming.GetUniquePath(dir.Path, "mail.txt");

        Path.GetFileName(path).Should().Be("mail.txt");
    }

    [Fact]
    public void GetUniquePath_appends_counter_on_collision()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "mail.txt"), "x");
        File.WriteAllText(Path.Combine(dir.Path, "mail_1.txt"), "x");

        var path = GmailFileNaming.GetUniquePath(dir.Path, "mail.txt");

        Path.GetFileName(path).Should().Be("mail_2.txt");
    }
}
