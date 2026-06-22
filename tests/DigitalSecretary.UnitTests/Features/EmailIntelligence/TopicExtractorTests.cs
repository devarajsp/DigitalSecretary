using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class TopicExtractorTests
{
    [Fact]
    public void GlobalTopics_returns_frequent_words_minus_stopwords()
    {
        var emails = new[]
        {
            MailFactory.Msg("a@example.com", new[] { "me@example.com" }, default, "project alpha", "project alpha deadline budget"),
            MailFactory.Msg("a@example.com", new[] { "me@example.com" }, default, "project beta", "project budget review"),
        };

        var topics = new TopicExtractor().GlobalTopics(emails);

        topics.Should().Contain("project");
        topics.Should().NotContain("the");
    }

    [Fact]
    public void AssignTopics_populates_the_correspondent()
    {
        const string owner = "me@example.com";
        var emails = new[]
        {
            MailFactory.Msg("a@example.com", new[] { owner }, default, "invoice", "invoice payment overdue invoice"),
        };
        var people = new IdentityResolver().Resolve(emails, owner).ToList();

        new TopicExtractor().AssignTopics(people, emails, owner);

        people.Single().Topics.Should().Contain("invoice");
    }
}
