using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Kit;

/// <summary>Covers <see cref="ByteSize"/> - the human-readable byte formatting used everywhere.</summary>
public sealed class ByteSizeTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1L, "1 B")]
    [InlineData(999L, "999 B")]
    [InlineData(1023L, "1023 B")]
    public void Format_shows_plain_bytes_whole(long bytes, string expected)
    {
        ByteSize.Format(bytes).Should().Be(expected);
    }

    [Theory]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    [InlineData(1099511627776L, "1 TB")]
    public void Format_scales_to_the_largest_readable_unit(long bytes, string expected)
    {
        ByteSize.Format(bytes).Should().Be(expected);
    }

    [Fact]
    public void Format_keeps_at_most_two_decimals()
    {
        ByteSize.Format(1234L).Should().Be("1.21 KB");
        ByteSize.Format(1500000L).Should().Be("1.43 MB");
    }

    [Fact]
    public void Format_trims_trailing_zeros()
    {
        ByteSize.Format(2048L).Should().Be("2 KB");
        ByteSize.Format(2560L).Should().Be("2.5 KB");
    }

    [Theory]
    [InlineData(-500L, "-500 B")]
    [InlineData(-1024L, "-1 KB")]
    [InlineData(-1572864L, "-1.5 MB")]
    public void Format_keeps_the_sign_of_a_negative_count(long bytes, string expected)
    {
        ByteSize.Format(bytes).Should().Be(expected);
    }

    [Fact]
    public void Format_handles_long_MaxValue_without_throwing()
    {
        var text = ByteSize.Format(long.MaxValue);

        text.Should().EndWith(" PB");
        text.Should().NotStartWith("-");
    }

    [Fact]
    public void Format_handles_long_MinValue_without_throwing()
    {
        var text = ByteSize.Format(long.MinValue);

        text.Should().StartWith("-");
        text.Should().EndWith(" PB");
    }

    [Fact]
    public void Format_never_runs_past_the_largest_unit()
    {
        ByteSize.Format(1024L * 1024 * 1024 * 1024 * 1024).Should().Be("1 PB");
        ByteSize.Format(1024L * 1024 * 1024 * 1024 * 1024 * 4).Should().Be("4 PB");
    }
}
