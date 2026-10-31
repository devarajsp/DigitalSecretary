using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DigitalSecretary.Features.SimilarVideos;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>
/// Encodes a tiny H.264 MP4 at test time with the Media Foundation Sink Writer, so the real decoder can be
/// tested end to end without any video file in the repository (machine rule §9: nothing large or binary
/// is committed). Each scene bitmap is held for <c>framesPerScene</c> frames.
/// </summary>
internal static class TestVideoWriter
{
    private const int Progressive = 2;

    public static void Write(string path, IReadOnlyList<Bitmap> scenes, int framesPerScene = 5, int fps = 10, int bitrate = 1_000_000)
    {
        var (w, h) = (scenes[0].Width, scenes[0].Height);
        if (!MF.Started)
        {
            throw new InvalidOperationException("Media Foundation is not available on this machine.");
        }
        MF.Check(NativeMethods.MFCreateSinkWriterFromURL(path, IntPtr.Zero, null, out var writer), "create sink writer");
        try
        {
            var stream = AddStream(writer, w, h, fps, bitrate);
            MF.Check(writer.BeginWriting(), "begin writing");
            var frameTicks = 10_000_000L / fps;
            var n = 0;
            foreach (var scene in scenes)
            {
                var bytes = Bgrx(scene);
                for (var k = 0; k < framesPerScene; k++, n++)
                {
                    WriteFrame(writer, stream, bytes, n * frameTicks, frameTicks);
                }
            }
            MF.Check(writer.Complete(), "finalize");
        }
        finally
        {
            MF.Release(writer);
        }
    }

    private static int AddStream(IMFSinkWriter writer, int w, int h, int fps, int bitrate)
    {
        MF.Check(NativeMethods.MFCreateMediaType(out var output), "create output type");
        MF.Check(NativeMethods.MFCreateMediaType(out var input), "create input type");
        try
        {
            Describe(output, MF.FormatH264, w, h, fps);
            output.SetUINT32(MF.AvgBitrate, bitrate);
            MF.Check(writer.AddStream(output, out var stream), "add stream");
            Describe(input, MF.FormatRgb32, w, h, fps);
            input.SetUINT32(MF.DefaultStride, w * 4);
            MF.Check(writer.SetInputMediaType(stream, input, null), "set input type");
            return stream;
        }
        finally
        {
            MF.Release(output);
            MF.Release(input);
        }
    }

    private static void Describe(IMFAttributes type, Guid subtype, int w, int h, int fps)
    {
        type.SetGUID(MF.MajorType, MF.MediaTypeVideo);
        type.SetGUID(MF.Subtype, subtype);
        type.SetUINT32(MF.InterlaceMode, Progressive);
        type.SetUINT64(MF.FrameSize, MF.Pack(w, h));
        type.SetUINT64(MF.FrameRate, MF.Pack(fps, 1));
        type.SetUINT64(MF.PixelAspectRatio, MF.Pack(1, 1));
    }

    private static void WriteFrame(IMFSinkWriter writer, int stream, byte[] bytes, long time, long duration)
    {
        MF.Check(NativeMethods.MFCreateMemoryBuffer(bytes.Length, out var buffer), "create buffer");
        MF.Check(NativeMethods.MFCreateSample(out var sample), "create sample");
        try
        {
            MF.Check(buffer.Lock(out var data, out _, out _), "lock buffer");
            Marshal.Copy(bytes, 0, data, bytes.Length);
            buffer.Unlock();
            buffer.SetCurrentLength(bytes.Length);
            sample.AddBuffer(buffer);
            sample.SetSampleTime(time);
            sample.SetSampleDuration(duration);
            MF.Check(writer.WriteSample(stream, sample), "write sample");
        }
        finally
        {
            MF.Release(sample);
            MF.Release(buffer);
        }
    }

    /// <summary>Top-down B, G, R, X rows with no padding.</summary>
    private static byte[] Bgrx(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            var row = bmp.Width * 4;
            var bytes = new byte[row * bmp.Height];
            for (var y = 0; y < bmp.Height; y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), bytes, y * row, row);
            }
            return bytes;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
