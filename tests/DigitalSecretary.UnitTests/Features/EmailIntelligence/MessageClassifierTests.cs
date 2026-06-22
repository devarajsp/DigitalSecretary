using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class MessageClassifierTests
{
    private readonly MessageClassifier _classifier = new();

    private static ParsedEmail M(string from, string subject) =>
        MailFactory.Msg(from, new[] { "me@example.com" }, default, subject);

    [Fact]
    public void Bulk_sender_is_newsletter() =>
        _classifier.Classify(M("noreply@example.com", "Spring sale")).Should().Be(MessageCategory.Newsletter);

    [Fact]
    public void Receipt_is_transactional() =>
        _classifier.Classify(M("billing@example.com", "Your receipt #123")).Should().Be(MessageCategory.Transactional);

    [Fact]
    public void Verify_is_notification() =>
        _classifier.Classify(M("noreply@app.com", "Verify your email")).Should().Be(MessageCategory.Notification);

    [Fact]
    public void A_person_is_personal() =>
        _classifier.Classify(M("friend@example.com", "Dinner tonight?")).Should().Be(MessageCategory.Personal);
}
