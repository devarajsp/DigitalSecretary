using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class RelationshipAnalyzerTests
{
    private static readonly DateTimeOffset Now = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Counts_each_direction_and_first_last_contact()
    {
        const string owner = "me@example.com";
        var emails = new[]
        {
            MailFactory.Msg("a@example.com", new[] { owner }, Now.AddDays(-10), "hi"),
            MailFactory.Msg(owner, new[] { "a@example.com" }, Now.AddDays(-5), "re"),
            MailFactory.Msg(owner, new[] { "a@example.com" }, Now.AddDays(-1), "re2"),
        };
        var people = new IdentityResolver().Resolve(emails, owner).ToList();

        new RelationshipAnalyzer().Analyze(people, emails, owner, Now);

        var a = people.Single(p => p.Id == "a@example.com");
        a.FromThem.Should().Be(1);
        a.FromMe.Should().Be(2);
        a.TotalMessages.Should().Be(3);
        a.FirstContact!.Value.Should().Be(Now.AddDays(-10));
        a.LastContact!.Value.Should().Be(Now.AddDays(-1));
        a.StrengthScore.Should().BeGreaterThan(0);
        a.Dormant.Should().BeFalse();
    }

    [Fact]
    public void Flags_a_long_silent_contact_as_dormant()
    {
        const string owner = "me@example.com";
        var old = Now.AddMonths(-20);
        var emails = new[]
        {
            MailFactory.Msg("b@example.com", new[] { owner }, old, "1"),
            MailFactory.Msg(owner, new[] { "b@example.com" }, old.AddDays(1), "2"),
            MailFactory.Msg("b@example.com", new[] { owner }, old.AddDays(2), "3"),
        };
        var people = new IdentityResolver().Resolve(emails, owner).ToList();

        new RelationshipAnalyzer().Analyze(people, emails, owner, Now);

        people.Single(p => p.Id == "b@example.com").Dormant.Should().BeTrue();
    }
}
