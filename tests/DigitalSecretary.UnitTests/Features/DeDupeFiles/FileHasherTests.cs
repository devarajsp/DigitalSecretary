using System.Globalization;
using System.Security.Cryptography;
using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>
/// Covers <see cref="Md5FileHasher"/>. MD5 here is a content fingerprint, never a security measure,
/// so the tests pin the well-known digests and the block-boundary behaviour of the streaming read.
/// </summary>
public sealed class FileHasherTests
{
    private const int OneMegabyte = 1024 * 1024;

    private static string Write(string dir, string name, byte[] content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>MD5 of the whole buffer in one shot - the independent answer the streaming read must match.</summary>
    private static string ExpectedHash(byte[] content)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        hash.AppendData(content);
        return string.Concat(hash.GetHashAndReset().Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static byte[] Pattern(int length, byte seed = 0)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 31) + seed);
        }
        return bytes;
    }

    [Fact]
    public void Compute_returns_the_known_md5_of_abc()
    {
        using var dir = new TempDir();
        var file = Write(dir.Path, "abc.bin", [0x61, 0x62, 0x63]);

        new Md5FileHasher().Compute(file, CancellationToken.None)
            .Should().Be("900150983cd24fb0d6963f7d28e17f72");
    }

    [Fact]
    public void Compute_returns_the_known_md5_of_an_empty_file()
    {
        using var dir = new TempDir();
        var file = Write(dir.Path, "empty.bin", []);

        new Md5FileHasher().Compute(file, CancellationToken.None)
            .Should().Be("d41d8cd98f00b204e9800998ecf8427e");
    }

    [Fact]
    public void Compute_gives_the_same_hash_for_two_files_with_identical_content()
    {
        using var dir = new TempDir();
        var content = Pattern(5000);
        var first = Write(dir.Path, "one.bin", content);
        var second = Write(dir.Path, "two.bin", content);
        var hasher = new Md5FileHasher();

        hasher.Compute(first, CancellationToken.None)
            .Should().Be(hasher.Compute(second, CancellationToken.None));
    }

    [Fact]
    public void Compute_gives_different_hashes_for_different_content()
    {
        using var dir = new TempDir();
        var first = Write(dir.Path, "one.bin", Pattern(5000));
        var second = Write(dir.Path, "two.bin", Pattern(5000, seed: 1));
        var hasher = new Md5FileHasher();

        hasher.Compute(first, CancellationToken.None)
            .Should().NotBe(hasher.Compute(second, CancellationToken.None));
    }

    [Fact]
    public void Compute_hashes_a_file_larger_than_one_read_block_correctly()
    {
        using var dir = new TempDir();
        var content = Pattern((OneMegabyte * 2) + 777);
        var file = Write(dir.Path, "big.bin", content);

        new Md5FileHasher().Compute(file, CancellationToken.None).Should().Be(ExpectedHash(content));
    }

    [Fact]
    public void Compute_notices_a_change_in_a_late_read_block()
    {
        using var dir = new TempDir();
        var content = Pattern(OneMegabyte + 2048);
        var first = Write(dir.Path, "one.bin", content);
        var altered = (byte[])content.Clone();
        altered[OneMegabyte + 1000] ^= 0xFF;
        var second = Write(dir.Path, "two.bin", altered);
        var hasher = new Md5FileHasher();

        hasher.Compute(first, CancellationToken.None)
            .Should().NotBe(hasher.Compute(second, CancellationToken.None));
    }

    [Fact]
    public void Compute_returns_null_when_the_token_is_already_cancelled()
    {
        using var dir = new TempDir();
        var file = Write(dir.Path, "big.bin", Pattern(OneMegabyte * 2));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        new Md5FileHasher().Compute(file, cancelled.Token).Should().BeNull();
    }
}
