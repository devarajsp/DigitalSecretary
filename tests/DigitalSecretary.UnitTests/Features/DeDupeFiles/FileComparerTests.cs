using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>
/// Covers <see cref="FileComparer"/> - the byte-for-byte safety net that stands in front of every
/// delete. Anything it cannot prove identical must come back <c>false</c>.
/// </summary>
public sealed class FileComparerTests
{
    private const int BlockSize = 64 * 1024;

    private static string Write(string dir, string name, byte[] content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] Pattern(int length, byte seed = 0)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 17) + seed);
        }
        return bytes;
    }

    [Fact]
    public void Are_identical_is_true_for_two_files_with_the_same_content()
    {
        using var dir = new TempDir();
        var content = Pattern(2048);
        var first = Write(dir.Path, "one.bin", content);
        var second = Write(dir.Path, "two.bin", content);

        FileComparer.AreIdentical(first, second).Should().BeTrue();
    }

    [Fact]
    public void Are_identical_is_false_for_the_same_length_but_different_bytes()
    {
        using var dir = new TempDir();
        var content = Pattern(2048);
        var altered = (byte[])content.Clone();
        altered[1000] ^= 0xFF;
        var first = Write(dir.Path, "one.bin", content);
        var second = Write(dir.Path, "two.bin", altered);

        FileComparer.AreIdentical(first, second).Should().BeFalse();
    }

    [Fact]
    public void Are_identical_is_false_for_different_lengths()
    {
        using var dir = new TempDir();
        var first = Write(dir.Path, "one.bin", Pattern(2048));
        var second = Write(dir.Path, "two.bin", Pattern(2049));

        FileComparer.AreIdentical(first, second).Should().BeFalse();
    }

    [Fact]
    public void Are_identical_is_true_for_the_same_path_twice()
    {
        using var dir = new TempDir();
        var file = Write(dir.Path, "one.bin", Pattern(64));

        FileComparer.AreIdentical(file, file).Should().BeTrue();
        FileComparer.AreIdentical(file, file.ToUpperInvariant()).Should().BeTrue();
    }

    [Fact]
    public void Are_identical_is_false_when_a_file_is_missing()
    {
        using var dir = new TempDir();
        var file = Write(dir.Path, "one.bin", Pattern(64));
        var missing = Path.Combine(dir.Path, "not-there.bin");

        FileComparer.AreIdentical(file, missing).Should().BeFalse();
        FileComparer.AreIdentical(missing, file).Should().BeFalse();
    }

    [Fact]
    public void Are_identical_is_false_when_both_files_are_missing()
    {
        using var dir = new TempDir();

        FileComparer.AreIdentical(
            Path.Combine(dir.Path, "a.bin"), Path.Combine(dir.Path, "b.bin")).Should().BeFalse();
    }

    [Fact]
    public void Are_identical_is_true_for_two_empty_files()
    {
        using var dir = new TempDir();
        var first = Write(dir.Path, "one.bin", []);
        var second = Write(dir.Path, "two.bin", []);

        FileComparer.AreIdentical(first, second).Should().BeTrue();
    }

    [Fact]
    public void Are_identical_compares_files_larger_than_one_block()
    {
        using var dir = new TempDir();
        var content = Pattern((BlockSize * 2) + 999);
        var first = Write(dir.Path, "one.bin", content);
        var second = Write(dir.Path, "two.bin", content);

        FileComparer.AreIdentical(first, second).Should().BeTrue();
    }

    [Fact]
    public void Are_identical_notices_a_difference_in_a_late_block()
    {
        using var dir = new TempDir();
        var content = Pattern((BlockSize * 2) + 999);
        var altered = (byte[])content.Clone();
        altered[(BlockSize * 2) + 500] ^= 0xFF;
        var first = Write(dir.Path, "one.bin", content);
        var second = Write(dir.Path, "two.bin", altered);

        FileComparer.AreIdentical(first, second).Should().BeFalse();
    }
}
