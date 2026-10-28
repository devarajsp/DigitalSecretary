namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// Lower-case hexadecimal text for checksums. Written by hand (rather than via
/// <c>Convert.ToHexString(...)</c> plus a lower-casing call) so no culture-sensitive casing is
/// involved and the checksum list matches the conventional <c>md5sum</c> presentation exactly.
/// </summary>
internal static class HexText
{
    private const string Digits = "0123456789abcdef";

    /// <summary>Renders bytes as lower-case hex, two characters per byte.</summary>
    public static string FromBytes(byte[] bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = Digits[bytes[i] >> 4];
            chars[(i * 2) + 1] = Digits[bytes[i] & 0xF];
        }
        return new string(chars);
    }

    /// <summary>
    /// Validates a 32-character MD5 string and returns it lower-cased, or <c>null</c> when the text
    /// is not 32 hex characters - so a hand-edited or corrupt list line is simply skipped.
    /// </summary>
    public static string? NormalizeMd5(string? text)
    {
        if (text is null || text.Length != 32)
        {
            return null;
        }

        var chars = new char[32];
        for (var i = 0; i < 32; i++)
        {
            var c = text[i];
            chars[i] = c switch
            {
                >= '0' and <= '9' => c,
                >= 'a' and <= 'f' => c,
                >= 'A' and <= 'F' => (char)(c + 32),
                _ => '\0'
            };
            if (chars[i] == '\0')
            {
                return null;
            }
        }
        return new string(chars);
    }
}
