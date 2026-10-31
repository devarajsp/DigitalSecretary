namespace DigitalSecretary.Features.ReduceImages;

/// <summary>
/// Reads the quality a JPEG was saved at from its own luminance quantisation table (DQT). Nearly every
/// encoder (cameras, phones, Photoshop, GDI+, libjpeg) scales the standard JPEG table by the IJG formula, so
/// the quality that reproduces the file's table most closely is the quality it was saved at. This is what
/// tells "a camera JPEG at 95" (worth re-saving at 88) from "already re-saved at 80" (leave it alone).
/// </summary>
public static class JpegQuality
{
    /// <summary>The standard JPEG luminance table (ITU T.81 Annex K), in zig-zag order as stored in files.</summary>
    private static readonly int[] StandardLuma =
    [
        16, 11, 12, 14, 12, 10, 16, 14, 13, 14, 18, 17, 16, 19, 24, 40,
        26, 24, 22, 22, 24, 49, 35, 37, 29, 40, 58, 51, 61, 60, 57, 51,
        56, 55, 64, 72, 92, 78, 64, 68, 87, 69, 55, 56, 80, 109, 81, 87,
        95, 98, 103, 104, 103, 62, 77, 113, 121, 112, 100, 120, 92, 101, 103, 99
    ];

    /// <summary>The estimated quality (1-100), or 0 when the bytes hold no readable luminance table.</summary>
    public static int Estimate(ReadOnlySpan<byte> jpeg)
    {
        var table = LumaTable(jpeg);
        return table is null ? 0 : BestQuality(table);
    }

    /// <summary>Reads the first 64 KB of a file and estimates its quality.</summary>
    public static int Estimate(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[Math.Min(65_536, stream.Length)];
        var read = stream.Read(buffer, 0, buffer.Length);
        return Estimate(buffer.AsSpan(0, read));
    }

    /// <summary>The IJG table for a quality: the standard table scaled, each value clamped to 1-255.</summary>
    public static int[] TableFor(int quality)
    {
        quality = Math.Clamp(quality, 1, 100);
        var scale = quality < 50 ? 5000 / quality : 200 - quality * 2;
        return [.. StandardLuma.Select(v => Math.Clamp((v * scale + 50) / 100, 1, 255))];
    }

    /// <summary>The quality whose table is closest to <paramref name="table"/> (sum of absolute differences; the higher one on a tie).</summary>
    public static int BestQuality(int[] table)
    {
        var best = 0;
        var bestError = long.MaxValue;
        for (var q = 1; q <= 100; q++)
        {
            var expected = TableFor(q);
            long error = 0;
            for (var i = 0; i < 64; i++)
            {
                error += Math.Abs(expected[i] - table[i]);
            }
            if (error <= bestError) // ties (identical tables near 100) go to the higher quality
            {
                (best, bestError) = (q, error);
            }
        }
        return best;
    }

    /// <summary>The 64 values of quantisation table 0 (luminance), or null. Walks the markers up to the image data.</summary>
    public static int[]? LumaTable(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return null;
        }
        var pos = 2;
        while (pos + 4 <= jpeg.Length)
        {
            if (jpeg[pos] != 0xFF)
            {
                return null;
            }
            var marker = jpeg[pos + 1];
            var length = (jpeg[pos + 2] << 8) | jpeg[pos + 3];
            if (marker == 0xDA || length < 2)
            {
                return null; // start of image data: no table found before it
            }
            if (marker == 0xDB && ReadTable(jpeg.Slice(pos + 4, Math.Min(length - 2, jpeg.Length - pos - 4))) is { } table)
            {
                return table;
            }
            pos += 2 + length;
        }
        return null;
    }

    /// <summary>Table 0 from one DQT segment (which may hold several tables, 8- or 16-bit).</summary>
    private static int[]? ReadTable(ReadOnlySpan<byte> segment)
    {
        var pos = 0;
        while (pos < segment.Length)
        {
            var precision = segment[pos] >> 4;
            var id = segment[pos] & 0x0F;
            var size = precision == 0 ? 64 : 128;
            if (pos + 1 + size > segment.Length)
            {
                return null;
            }
            if (id == 0)
            {
                var values = new int[64];
                for (var i = 0; i < 64; i++)
                {
                    values[i] = precision == 0 ? segment[pos + 1 + i] : (segment[pos + 1 + 2 * i] << 8) | segment[pos + 2 + 2 * i];
                }
                return values;
            }
            pos += 1 + size;
        }
        return null;
    }
}
