using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class MessageStoreTests
{
    [Fact]
    public void Round_trips_a_message_with_participants_and_attachments()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "messages.json");
        var email = MailFactory.Msg(
            "a@example.com", new[] { "me@example.com" },
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            "Hi", "body", fromName: "Alice", messageId: "<m1>");
        email.SourceFile = @"C:\mail\1.eml";
        email.Attachments.Add(new ParsedAttachment("a.pdf", "application/pdf", 123, "hash"));

        var store = new MessageStore();
        store.Save(new[] { email }, path);
        var loaded = store.Load(path);

        loaded.Should().ContainSingle();
        var r = loaded[0];
        r.From!.Address.Should().Be("a@example.com");
        r.From!.Name.Should().Be("Alice");
        r.To.Should().ContainSingle(p => p.Address == "me@example.com");
        r.MessageId.Should().Be("<m1>");
        r.Subject.Should().Be("Hi");
        r.SourceFile.Should().Be(@"C:\mail\1.eml");
        r.Attachments.Should().ContainSingle(a => a.FileName == "a.pdf");
    }

    [Fact]
    public void Missing_file_loads_as_empty()
    {
        using var dir = new TempDir();
        new MessageStore().Load(Path.Combine(dir.Path, "none.json")).Should().BeEmpty();
    }
}
