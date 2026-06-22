using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class ToneAnalyzerTests
{
    private readonly ToneAnalyzer _tone = new();

    [Fact]
    public void Positive_text_scores_above_zero()
        => _tone.ScoreText("Thanks so much, this is great and I love it!").Should().BeGreaterThan(0);

    [Fact]
    public void Negative_text_scores_below_zero()
        => _tone.ScoreText("This is terrible and I hate the awful delay").Should().BeLessThan(0);

    [Fact]
    public void Neutral_text_scores_zero()
        => _tone.ScoreText("The meeting is at noon in room four").Should().Be(0);

    [Theory]
    [InlineData(2.0, "Positive")]
    [InlineData(0.0, "Neutral")]
    [InlineData(-2.0, "Negative")]
    public void Label_reflects_score(double score, string expected)
        => ToneAnalyzer.LabelFor(score).Should().Be(expected);

    [Fact]
    public void Analyze_sets_person_tone()
    {
        const string owner = "me@example.com";
        var emails = new[]
        {
            MailFactory.Msg("a@example.com", new[] { owner }, default, "thanks", "thank you, this is great, love it"),
        };
        var people = new IdentityResolver().Resolve(emails, owner).ToList();

        _tone.Analyze(people, emails, owner);

        var a = people.Single(p => p.Id == "a@example.com");
        a.ToneScore.Should().BeGreaterThan(0);
        a.ToneLabel.Should().Be("Positive");
    }
}
