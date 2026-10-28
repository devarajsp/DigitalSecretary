using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>
/// Covers the hex/checksum text rules. The helper itself is <c>internal</c> to the feature, so it is
/// exercised through its two public doorways - <see cref="Md5FileHasher"/> (rendering bytes as hex)
/// and <see cref="ChecksumListFile.ParseLine"/> (validating and lower-casing a stored checksum).
/// </summary>
public sealed class HexTextTests
{
    private const string Hash = "900150983cd24fb0d6963f7d28e17f72";

    [Fact]
    public void Computed_hash_is_32_lower_case_hex_characters()
    {
        using var dir = new TempDir();
        var file = Path.Combine(dir.Path, "abc.bin");
        File.WriteAllBytes(file, [0x61, 0x62, 0x63]);

        var result = new Md5FileHasher().Compute(file, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().HaveLength(32);
        result.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void Parse_line_lower_cases_an_upper_case_checksum()
    {
        var line = Hash.ToUpperInvariant() + "\t3\t2026-01-02T03:04:05.0000000Z\tC:\\pics\\a.jpg";

        var entry = ChecksumListFile.ParseLine(line);

        entry.Should().NotBeNull();
        entry!.Md5.Should().Be(Hash);
    }

    [Fact]
    public void Parse_line_accepts_mixed_case_hex()
    {
        var entry = ChecksumListFile.ParseLine("900150983CD24fb0D6963f7d28E17F72\tC:\\pics\\a.jpg");

        entry.Should().NotBeNull();
        entry!.Md5.Should().Be(Hash);
    }

    [Theory]
    [InlineData("900150983cd24fb0d6963f7d28e17f7")]
    [InlineData("900150983cd24fb0d6963f7d28e17f722")]
    [InlineData("900150983cd24fb0d6963f7d28e17fzz")]
    [InlineData("900150983cd24fb0d6963f7d28e17f7 ")]
    [InlineData("not-a-checksum-at-all-but-32-chrs")]
    public void Parse_line_rejects_a_checksum_that_is_not_32_hex_characters(string checksum)
    {
        ChecksumListFile.ParseLine(checksum + "\tC:\\pics\\a.jpg").Should().BeNull();
    }
}
