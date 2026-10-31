using System.Drawing;

namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>
/// Made-up videos for documentation screenshots and tests. A "video" here is just its ten sampled frames,
/// painted with the kit's <see cref="SampleScene"/> (a scene per frame, so the content changes over time),
/// plus look-alike variants: a copy, a small re-encode, a turned copy and another take of the same scene
/// (filmed a little closer - a 6% zoom, which measures as "Similar - check").
/// Paths are placeholders under <see cref="RootPath"/>, so no real video, name or folder ever appears.
/// </summary>
public static class SampleVideoSet
{
    /// <summary>The placeholder folder the sample pretends to have scanned.</summary>
    public const string RootPath = @"C:\Users\You\Videos";

    /// <summary>Height of one frame in the sample film strips.</summary>
    public const int StripHeight = 54;

    /// <summary>Which of the ten sampled frames the film strip shows (spread over the video, like the real strips).</summary>
    private static readonly int[] StripFrames = [1, 4, 6, 8];

    /// <summary>The sampled frames of made-up video <paramref name="seed"/>; <paramref name="shift"/> nudges the sun.</summary>
    public static IReadOnlyList<Bitmap> Frames(int seed, int width = 320, int height = 180, int shift = 0)
        => [.. Enumerable.Range(0, VideoDecoder.FrameCount).Select(i => SampleScene.Paint((seed * 100) + i, width, height, shift))];

    /// <summary>A sample as the decoder would return it for <paramref name="frames"/> (the bitmaps are disposed).</summary>
    public static VideoSample Sample(IReadOnlyList<Bitmap> frames, long durationMs, int width, int height)
    {
        var grays = frames.Select(f => (GrayImage?)GrayImage.FromBitmap(f)).ToList();
        return new VideoSample(durationMs, width, height, grays);
    }

    /// <summary>
    /// A ready-made result: records with real fingerprints of generated frames (placeholder paths) and a film
    /// strip per video for the window. The caller disposes the bitmaps via the returned dictionary.
    /// </summary>
    public static (IReadOnlyList<VideoRecord> Records, Dictionary<string, Bitmap> Strips) Build(DateTime nowUtc)
    {
        var strips = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        var records = new List<VideoRecord>();
        void Add(string relative, IReadOnlyList<Bitmap> frames, long durationMs, int width, int height, long size, double savedDaysAgo)
        {
            var path = Path.Combine(RootPath, relative);
            strips[path] = Strip(frames);
            records.Add(VideoRecord.From(path, size, nowUtc.AddDays(-savedDaysAgo), Sample(frames, durationMs, width, height)));
            foreach (var f in frames)
            {
                f.Dispose();
            }
        }

        Add(@"2024\Holiday\VID_20240611_101502.mp4", Frames(1), 95_400, 1920, 1080, 245_300_000, 500);
        Add(@"Phone backup\VID_20240611_101502 (1).mp4", Frames(1), 95_400, 1920, 1080, 245_300_000, 300);
        Add(@"WhatsApp\VID-20240611-WA0003.mp4", Frames(1, 160, 90), 95_350, 640, 360, 11_800_000, 290);
        Add(@"2025\Birthday\MVI_0420.mp4", Frames(2), 182_000, 3840, 2160, 1_104_000_000, 120);
        Add(@"Edited\MVI_0420_portrait.mp4", Turned(Frames(2)), 182_100, 2160, 3840, 382_000_000, 60);
        Add(@"2025\Birthday\MVI_0421.mp4", [.. Frames(2).Select(f => Zoomed(f, 0.06))], 180_500, 3840, 2160, 1_092_000_000, 120);
        Add(@"2025\Garden\VID_5512.mp4", Frames(3), 61_000, 1920, 1080, 98_000_000, 40);
        return (records, strips);
    }

    /// <summary>Four of the frames side by side, as the review cards show real videos.</summary>
    public static Bitmap Strip(IReadOnlyList<Bitmap> frames)
    {
        var picks = StripFrames.Where(i => i < frames.Count).Select(i => Shrink(frames[i])).ToList();
        try
        {
            return VideoDecoder.Join(picks, StripHeight);
        }
        finally
        {
            picks.ForEach(p => p.Dispose());
        }
    }

    private static Bitmap Shrink(Bitmap frame)
    {
        var w = Math.Max(1, frame.Width * StripHeight / frame.Height);
        return new Bitmap(frame, new Size(w, StripHeight));
    }

    /// <summary>
    /// The middle of <paramref name="frame"/> enlarged back to full size, as if filmed closer: <paramref name="crop"/>
    /// of each side is cut away (0.1 = 10%). Disposes the input and returns a new bitmap.
    /// </summary>
    public static Bitmap Zoomed(Bitmap frame, double crop)
    {
        using (frame)
        {
            var dx = (int)(frame.Width * crop);
            var dy = (int)(frame.Height * crop);
            var result = new Bitmap(frame.Width, frame.Height);
            using var g = Graphics.FromImage(result);
            g.DrawImage(frame, new Rectangle(0, 0, frame.Width, frame.Height), new Rectangle(dx, dy, frame.Width - (2 * dx), frame.Height - (2 * dy)), GraphicsUnit.Pixel);
            return result;
        }
    }

    private static IReadOnlyList<Bitmap> Turned(IReadOnlyList<Bitmap> frames)
    {
        foreach (var f in frames)
        {
            f.RotateFlip(RotateFlipType.Rotate90FlipNone);
        }
        return frames;
    }
}
