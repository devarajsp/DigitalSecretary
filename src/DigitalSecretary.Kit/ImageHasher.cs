using System.Numerics;

namespace DigitalSecretary.Kit;

/// <summary>
/// A picture's visual fingerprint: a perceptual hash (pHash) and a difference hash (dHash) for each
/// of the eight <see cref="PictureOrientation"/>s, plus a sharpness score. Index 0 is the picture as it is;
/// comparing one picture's index 0 against another's eight variants finds turned or mirrored copies.
/// </summary>
public sealed record Fingerprint(ulong[] PHashes, ulong[] DHashes, double Sharpness)
{
    public ulong PHash => PHashes[0];
    public ulong DHash => DHashes[0];
}

/// <summary>
/// Pure perceptual hashing. Both hashes are 64-bit and compared by Hamming distance (how many bits
/// differ): small for the same picture resized, re-compressed or lightly edited, large for different
/// pictures. Using two independent hashes and requiring both to agree cuts false matches sharply.
/// <list type="bullet">
/// <item><b>pHash</b> - the low-frequency 8 x 8 corner of a 32 x 32 discrete cosine transform,
/// each coefficient compared with their median. Robust to scaling, compression and brightness.</item>
/// <item><b>dHash</b> - a 9 x 8 thumbnail, each pixel compared with its right-hand neighbour.
/// Cheap and good at telling apart pictures with the same overall tones.</item>
/// </list>
/// </summary>
public static class ImageHasher
{
    /// <summary>Side of the square the pHash DCT runs on.</summary>
    public const int DctSize = 32;

    private const int LowFrequencies = 8;
    private const int SharpnessSize = 128;

    private static readonly double[,] Cosines = BuildCosines();

    /// <summary>Number of bits that differ between two hashes (0 - 64).</summary>
    public static int Distance(ulong a, ulong b) => BitOperations.PopCount(a ^ b);

    /// <summary>
    /// Fingerprints <paramref name="image"/> (any size; typically a ~256 px thumbnail). Sharpness is
    /// measured on a 128 px version so it is comparable across source sizes.
    /// </summary>
    public static Fingerprint Compute(GrayImage image)
    {
        var square = image.Resize(DctSize, DctSize);
        var orientations = Enum.GetValues<PictureOrientation>();
        var pHashes = new ulong[orientations.Length];
        var dHashes = new ulong[orientations.Length];
        for (var i = 0; i < orientations.Length; i++)
        {
            var turned = square.Transform(orientations[i]);
            pHashes[i] = PerceptualHash(turned);
            dHashes[i] = DifferenceHash(turned);
        }

        var scale = Math.Min(1d, SharpnessSize / (double)Math.Max(image.Width, image.Height));
        var forSharpness = scale < 1
            ? image.Resize(Math.Max(3, (int)Math.Round(image.Width * scale)), Math.Max(3, (int)Math.Round(image.Height * scale)))
            : image;
        return new Fingerprint(pHashes, dHashes, forSharpness.LaplacianVariance());
    }

    /// <summary>pHash of a picture (resampled to 32 x 32 first if needed).</summary>
    public static ulong PerceptualHash(GrayImage image)
    {
        var square = image.Width == DctSize && image.Height == DctSize ? image : image.Resize(DctSize, DctSize);
        var coefficients = new double[LowFrequencies * LowFrequencies];
        for (var v = 0; v < LowFrequencies; v++)
        {
            for (var u = 0; u < LowFrequencies; u++)
            {
                coefficients[(v * LowFrequencies) + u] = Dct(square, u, v);
            }
        }

        // The DC term (0,0) is overall brightness, not structure: leave it out of the median and the bits.
        var median = coefficients.Skip(1).OrderBy(c => c).ElementAt((coefficients.Length - 1) / 2);
        ulong hash = 0;
        for (var i = 1; i < coefficients.Length; i++)
        {
            if (coefficients[i] > median)
            {
                hash |= 1UL << i;
            }
        }
        return hash;
    }

    /// <summary>dHash of a picture: 8 rows of 8 "is the next pixel brighter?" bits.</summary>
    public static ulong DifferenceHash(GrayImage image)
    {
        var small = image.Resize(9, 8);
        ulong hash = 0;
        var bit = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++, bit++)
            {
                if (small[x, y] < small[x + 1, y])
                {
                    hash |= 1UL << bit;
                }
            }
        }
        return hash;
    }

    private static double Dct(GrayImage square, int u, int v)
    {
        double sum = 0;
        for (var y = 0; y < DctSize; y++)
        {
            var cy = Cosines[y, v];
            for (var x = 0; x < DctSize; x++)
            {
                sum += square[x, y] * Cosines[x, u] * cy;
            }
        }
        return sum;
    }

    private static double[,] BuildCosines()
    {
        var table = new double[DctSize, LowFrequencies];
        for (var x = 0; x < DctSize; x++)
        {
            for (var u = 0; u < LowFrequencies; u++)
            {
                table[x, u] = Math.Cos((2 * x + 1) * u * Math.PI / (2 * DctSize));
            }
        }
        return table;
    }
}
