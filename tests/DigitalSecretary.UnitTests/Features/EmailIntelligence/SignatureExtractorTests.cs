using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class SignatureExtractorTests
{
    [Fact]
    public void Extracts_phone_and_url_from_a_signature()
    {
        var signature = new SignatureExtractor().Extract(
            "Best regards,\nJohn\nPhone: +1 (555) 123-4567\nhttps://example.com/john");

        signature.Phones.Should().ContainMatch("*555*");
        signature.Urls.Should().Contain("https://example.com/john");
    }

    [Fact]
    public void Empty_body_yields_nothing()
    {
        var signature = new SignatureExtractor().Extract("");

        signature.Phones.Should().BeEmpty();
        signature.Urls.Should().BeEmpty();
    }
}
