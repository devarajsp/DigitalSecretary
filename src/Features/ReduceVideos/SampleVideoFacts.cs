namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>
/// A made-up folder of videos (placeholder paths, no real files) covering every kind of advice: generous
/// phone H.264 (Recommended), a modest saving (Your call), an already-HEVC phone video, an HDR clip and a
/// short clip (Skip). Used by the documentation screenshots, QA and tests.
/// </summary>
public static class SampleVideoFacts
{
    public const string Root = @"C:\Users\You\Videos";

    public static IReadOnlyList<VideoFacts> Create()
    {
        var when = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        return
        [
            Make("2026-05 Birthday party.mp4", 1_380_000_000, 640_000, 1920, 1080, 30, "h264", when),
            Make("Beach day 4K.mov", 2_950_000_000, 380_000, 3840, 2160, 30, "h264", when),
            Make("School concert.mp4", 1_140_000_000, 1_210_000, 1920, 1080, 30, "h264", when),
            Make("Old camcorder tape.avi", 4_100_000_000, 2_700_000, 720, 576, 25, "dvvideo", when),
            Make("Screen recording.mp4", 160_000_000, 600_000, 1920, 1080, 30, "h264", when),
            Make("Phone clip (HEVC).mov", 420_000_000, 300_000, 1920, 1080, 30, "hevc", when),
            Make("Sunset HDR.mov", 610_000_000, 120_000, 3840, 2160, 30, "hevc", when) with { IsHdr = true, BitDepth = 10 },
            Make("Tiny clip.mp4", 2_400_000, 1_500, 1280, 720, 30, "h264", when)
        ];
    }

    private static VideoFacts Make(string name, long size, long ms, int w, int h, double fps, string codec, DateTime when) => new()
    {
        Path = Path.Combine(Root, name),
        Size = size,
        ModifiedUtc = when,
        DurationMs = ms,
        Width = w,
        Height = h,
        Fps = fps,
        Codec = codec,
        HasAudio = true,
        AudioCodec = "aac",
        ReadBy = "sample"
    };
}
