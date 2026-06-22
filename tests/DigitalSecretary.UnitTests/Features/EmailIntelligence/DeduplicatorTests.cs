using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class DeduplicatorTests
{
    private static readonly DateTimeOffset T = new(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Dedupes_by_message_id()
    {
        var a = MailFactory.Msg("x@example.com", new[] { "me@example.com" }, T, "Hi", "body", messageId: "<id1>");
        var b = MailFactory.Msg("x@example.com", new[] { "me@example.com" }, T.AddDays(1), "Other", "diff", messageId: "<id1>");

        var result = new Deduplicator().Deduplicate(new[] { a, b });

        result.Unique.Should().HaveCount(1);
        result.DuplicatesRemoved.Should().Be(1);
    }

    [Fact]
    public void Dedupes_by_content_hash_when_no_message_id()
    {
        var a = MailFactory.Msg("x@example.com", new[] { "me@example.com" }, T, "Hi", "Same body");
        var b = MailFactory.Msg("x@example.com", new[] { "me@example.com" }, T, "Hi", "Same body");

        new Deduplicator().Deduplicate(new[] { a, b }).Unique.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("Re: Lunch", "subj:lunch")]
    [InlineData("FWD: Lunch", "subj:lunch")]
    [InlineData("Lunch", "subj:lunch")]
    public void ThreadKey_strips_reply_prefix(string subject, string expected)
    {
        var email = MailFactory.Msg("x@example.com", new[] { "me@example.com" }, T, subject);
        Deduplicator.ThreadKey(email).Should().Be(expected);
    }
}
