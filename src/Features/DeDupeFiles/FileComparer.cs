using System.Buffers;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// Byte-for-byte file comparison - the safety net in front of every delete. Two different files can
/// share an MD5, so a duplicate is only removed once its contents are proven identical to the copy
/// being kept.
/// </summary>
public static class FileComparer
{
    private const int BlockSize = 64 * 1024;

    /// <summary>
    /// True when both files exist and hold exactly the same bytes. The same path twice is identical
    /// by definition; a missing file, a different length, or an I/O error gives <c>false</c>, which
    /// callers treat as "do not delete".
    /// </summary>
    public static bool AreIdentical(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var a = new FileInfo(first);
        var b = new FileInfo(second);
        if (!a.Exists || !b.Exists || a.Length != b.Length)
        {
            return false;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(BlockSize * 2);
        try
        {
            return SameContents(a.FullName, b.FullName, buffer);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool SameContents(string first, string second, byte[] buffer)
    {
        using var sa = Open(first);
        using var sb = Open(second);
        var left = buffer.AsMemory(0, BlockSize);
        var right = buffer.AsMemory(BlockSize, BlockSize);

        while (true)
        {
            var readA = sa.ReadAtLeast(left.Span, BlockSize, throwOnEndOfStream: false);
            var readB = sb.ReadAtLeast(right.Span, BlockSize, throwOnEndOfStream: false);
            if (readA != readB)
            {
                return false;
            }
            if (readA == 0)
            {
                return true;
            }
            if (!left.Span[..readA].SequenceEqual(right.Span[..readB]))
            {
                return false;
            }
        }
    }

    private static FileStream Open(string path)
        => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BlockSize, FileOptions.SequentialScan);
}
