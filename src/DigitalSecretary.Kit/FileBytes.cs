namespace DigitalSecretary.Kit;

/// <summary>Byte-for-byte file comparison, used to confirm that a zero-distance pair is truly an exact copy.</summary>
public static class FileBytes
{
    private const int BlockSize = 81920;

    /// <summary>
    /// True when both files exist, have the same length and identical contents. Reads block by block
    /// and stops at the first difference; any I/O problem counts as "not identical".
    /// </summary>
    public static bool AreIdentical(string a, string b)
    {
        try
        {
            using var fa = File.OpenRead(a);
            using var fb = File.OpenRead(b);
            if (fa.Length != fb.Length)
            {
                return false;
            }
            var bufA = new byte[BlockSize];
            var bufB = new byte[BlockSize];
            while (true)
            {
                var na = fa.ReadAtLeast(bufA, BlockSize, throwOnEndOfStream: false);
                var nb = fb.ReadAtLeast(bufB, BlockSize, throwOnEndOfStream: false);
                if (na != nb || !bufA.AsSpan(0, na).SequenceEqual(bufB.AsSpan(0, nb)))
                {
                    return false;
                }
                if (na == 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
