using System.Drawing;
using DigitalSecretary.Features.ReduceVideos;
using DigitalSecretary.Kit;
using DigitalSecretary.UnitTests.Features.SimilarVideos;

namespace DigitalSecretary.UnitTests.Features.ReduceVideos;

/// <summary>
/// Real video files made at test time (nothing binary is committed, machine rule §9): a silent H.264 MP4
/// written by Windows' own Sink Writer, and - when ffmpeg is installed on the machine - a noisy H.264 MP4
/// with sound made by ffmpeg's test sources. Tests needing ffmpeg return early (pass) on machines without
/// it, such as CI; the Windows-only tests always run.
/// </summary>
internal static class RealVideos
{
    /// <summary>ffmpeg on this machine, or null.</summary>
    public static readonly MediaToolPaths? Tools = FfmpegLocator.Find();

    /// <summary>A 4-second 640x360 30 fps H.264 MP4 at a generous 6 Mbit/s, no sound (Windows Sink Writer).</summary>
    public static string WriteSilentH264(string folder, string name = "silent.mp4")
    {
        var path = Path.Combine(folder, name);
        var scenes = Enumerable.Range(0, 8).Select(Scene).ToList();
        try
        {
            TestVideoWriter.Write(path, scenes, framesPerScene: 15, fps: 30, bitrate: 6_000_000);
        }
        finally
        {
            scenes.ForEach(s => s.Dispose());
        }
        return path;
    }

    /// <summary>
    /// A 3-second 1280x720 30 fps H.264 MP4 with a 48 kHz stereo AAC tone, encoded at a wasteful 20 Mbit/s
    /// (like a phone does) by ffmpeg. Null when ffmpeg is not installed.
    /// </summary>
    public static string? WriteNoisyWithSound(string folder, string name = "phone.mp4")
    {
        if (Tools is null)
        {
            return null;
        }
        var path = Path.Combine(folder, name);
        var result = ToolRunner.Run(Tools.Ffmpeg,
        [
            "-hide_banner", "-nostdin", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=1280x720:r=30:d=3",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=3",
            "-ac", "2", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "20M", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-shortest", path
        ], null, CancellationToken.None);
        return result.Succeeded ? path : throw new InvalidOperationException("ffmpeg could not make the test video: " + result.ErrorTail);
    }

    /// <summary>A busy, high-detail scene so the encoder has real work to do.</summary>
    private static Bitmap Scene(int seed)
    {
        var bmp = new Bitmap(640, 360);
        var random = new Random(seed);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
        for (var i = 0; i < 300; i++)
        {
            using var brush = new SolidBrush(Color.FromArgb(random.Next(256), random.Next(256), random.Next(256)));
            g.FillEllipse(brush, random.Next(-20, 640), random.Next(-20, 360), random.Next(4, 60), random.Next(4, 60));
        }
        return bmp;
    }
}
