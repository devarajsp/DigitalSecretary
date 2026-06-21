using DigitalSecretary.Features.Calculator;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class CalculatorEngineTests
{
    [Theory]
    [InlineData("2+3", "5")]
    [InlineData("10-4", "6")]
    [InlineData("6*7", "42")]
    [InlineData("8/2", "4")]
    [InlineData("(12+5)*2", "34")]
    [InlineData("7.0/2", "3.5")]
    public void Evaluates_ascii_expressions(string input, string expected)
        => CalculatorEngine.Evaluate(input).Should().Be(expected);

    [Theory]
    [InlineData("2×3", "6")]
    [InlineData("6÷2", "3")]
    [InlineData("9−4", "5")]
    public void Evaluates_display_operators(string input, string expected)
        => CalculatorEngine.Evaluate(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_input_returns_empty(string input)
        => CalculatorEngine.Evaluate(input).Should().BeEmpty();

    [Theory]
    [InlineData("2+")]
    [InlineData("abc")]
    [InlineData("5/0")]
    public void Invalid_input_returns_error(string input)
        => CalculatorEngine.Evaluate(input).Should().Be("Error");
}
