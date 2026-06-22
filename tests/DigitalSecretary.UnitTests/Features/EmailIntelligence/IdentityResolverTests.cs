using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class IdentityResolverTests
{
    [Fact]
    public void Merges_two_addresses_with_the_same_display_name()
    {
        var e1 = MailFactory.Msg("owner@example.com", new[] { "john@example.com" }, default);
        e1.To[0] = new EmailParticipant("john@example.com", "John Smith");
        var e2 = MailFactory.Msg("john@example.org", new[] { "owner@example.com" }, default, fromName: "John Smith");

        var people = new IdentityResolver().Resolve(new[] { e1, e2 }, "owner@example.com");

        people.Should().ContainSingle();
        people[0].Addresses.Should().Contain(new[] { "john@example.com", "john@example.org" });
    }

    [Fact]
    public void Excludes_the_owner()
    {
        var email = MailFactory.Msg("owner@example.com", new[] { "a@example.com" }, default);

        var people = new IdentityResolver().Resolve(new[] { email }, "owner@example.com");

        people.Should().ContainSingle(p => p.Id == "a@example.com");
    }

    [Theory]
    [InlineData("Dr. John Smith", "john smith")]
    [InlineData("  Mary   Jane ", "mary jane")]
    public void NormalizeName_strips_titles_and_collapses_space(string input, string expected)
        => IdentityResolver.NormalizeName(input).Should().Be(expected);
}
