using System.Drawing;
using DigitalSecretary.Features.SimilarVideos;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>
/// Made-up video records for the tests: real fingerprints of generated frames (no codec involved), or
/// hand-set hashes when a test needs exact distances. No real video anywhere.
/// </summary>
internal static class Videos
{
    public static readonly DateTime Now = new(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>A record whose frames are <paramref name="frames"/> (disposed).</summary>
    public static VideoRecord From(string path, IReadOnlyList<Bitmap> frames, long durationMs = 60_000, int width = 1920, int height = 1080, long size = 100_000_000, DateTime? modified = null)
    {
        var sample = SampleVideoSet.Sample(frames, durationMs, width, height);
        foreach (var f in frames)
        {
            f.Dispose();
        }
        return VideoRecord.From(path, size, modified ?? Now, sample);
    }

    /// <summary>Video <paramref name="seed"/> painted at the given frame size.</summary>
    public static VideoRecord Seed(string path, int seed, int frameWidth = 320, int frameHeight = 180, long durationMs = 60_000, int width = 1920, int height = 1080, long size = 100_000_000)
        => From(path, SampleVideoSet.Frames(seed, frameWidth, frameHeight), durationMs, width, height, size);

    /// <summary>The frames of video <paramref name="seed"/>, each turned or flipped.</summary>
    public static IReadOnlyList<Bitmap> Turned(int seed, RotateFlipType how)
    {
        var frames = SampleVideoSet.Frames(seed);
        foreach (var f in frames)
        {
            f.RotateFlip(how);
        }
        return frames;
    }

    /// <summary>A record with hand-set hashes: every frame's pHash is <paramref name="p"/> and dHash <paramref name="d"/> in all orientations; null entries are flat frames.</summary>
    public static VideoRecord Hashed(string path, ulong?[] p, ulong d = 0, long durationMs = 60_000, long size = 100, int width = 1920, int height = 1080, DateTime? modified = null) => new()
    {
        Path = path,
        Size = size,
        ModifiedUtc = modified ?? Now,
        DurationMs = durationMs,
        Width = width,
        Height = height,
        Frames = [.. p.Select(x => x is { } v ? new FrameHash(Enumerable.Repeat(v, 8).ToArray(), Enumerable.Repeat(d, 8).ToArray()) : null)]
    };

    /// <summary>Ten frames, all with pHash <paramref name="p"/>.</summary>
    public static ulong?[] All(ulong p) => [.. Enumerable.Repeat<ulong?>(p, VideoDecoder.FrameCount)];

    /// <summary>A 64-bit value with the lowest <paramref name="n"/> bits set - Hamming distance n from 0.</summary>
    public static ulong Bits(int n) => n >= 64 ? ulong.MaxValue : (1UL << n) - 1;
}
