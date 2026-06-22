using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class TimelineBuilderTests
{
    [Fact]
    public void Builds_ordered_entries_with_kind_and_source_file()
    {
        const string owner = "me@example.com";
        var e1 = MailFactory.Msg("a@example.com", new[] { owner }, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), "first");
        e1.SourceFile = @"C:\mail\1.eml";
        var e2 = MailFactory.Msg(owner, new[] { "a@example.com" }, new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero), "second");

        var people = new IdentityResolver().Resolve(new[] { e1, e2 }, owner).ToList();
        var timelines = new TimelineBuilder().Build(people, new[] { e1, e2 }, owner);

        var a = people.Single(p => p.Id == "a@example.com");
        timelines[a.Id].Should().HaveCount(2);
        timelines[a.Id][0].Kind.Should().Be("received");
        timelines[a.Id][0].SourceFile.Should().Be(@"C:\mail\1.eml");
        timelines[a.Id][1].Kind.Should().Be("sent");
    }
}
