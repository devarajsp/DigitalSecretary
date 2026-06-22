using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class NetworkGraphBuilderTests
{
    [Fact]
    public void Links_two_people_who_share_an_email()
    {
        const string owner = "me@example.com";
        var email = MailFactory.Msg(owner, new[] { "a@example.com", "b@example.com" }, default, "team");
        var people = new IdentityResolver().Resolve(new[] { email }, owner).ToList();

        var edges = new NetworkGraphBuilder().Build(people, new[] { email }, owner);

        edges.Should().ContainSingle();
        edges[0].Weight.Should().Be(1);
    }
}
