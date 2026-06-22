using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class LifeDataExtractorTests
{
    private static ParsedEmail Msg(string subject, string body = "") =>
        MailFactory.Msg("x@example.com", new[] { "me@example.com" }, default, subject, body);

    [Fact]
    public void Detects_purchase() => LifeDataExtractor.Categorize(Msg("Your order #123", "Total $42.50")).Should().Be("Purchase");

    [Fact]
    public void Detects_subscription() => LifeDataExtractor.Categorize(Msg("Your subscription renews soon")).Should().Be("Subscription");

    [Fact]
    public void Detects_travel() => LifeDataExtractor.Categorize(Msg("Your flight is confirmed")).Should().Be("Travel");

    [Fact]
    public void Detects_account() => LifeDataExtractor.Categorize(Msg("Welcome to Acme, verify your email")).Should().Be("Account");

    [Fact]
    public void Non_matching_returns_null() => LifeDataExtractor.Categorize(Msg("Lunch tomorrow?")).Should().BeNull();

    [Theory]
    [InlineData("Total: $42.50", 42.50, "USD")]
    [InlineData("Amount ₹1,200", 1200, "INR")]
    public void FindAmount_parses_money(string text, double expected, string currency)
    {
        var (amount, cur) = LifeDataExtractor.FindAmount(text);
        amount.Should().Be((decimal)expected);
        cur.Should().Be(currency);
    }

    [Fact]
    public void FindAmount_returns_null_when_absent()
        => LifeDataExtractor.FindAmount("no money here").Amount.Should().BeNull();

    [Fact]
    public void Extract_emits_one_item_per_matching_message()
    {
        var emails = new[]
        {
            MailFactory.Msg("shop@example.com", new[] { "me@example.com" }, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), "Your order #9", "Total $10.00"),
            MailFactory.Msg("friend@example.com", new[] { "me@example.com" }, default, "Lunch?", "see you"),
        };

        var items = new LifeDataExtractor().Extract(emails);

        items.Should().ContainSingle();
        items[0].Category.Should().Be("Purchase");
        items[0].Amount.Should().Be(10.00m);
    }
}
