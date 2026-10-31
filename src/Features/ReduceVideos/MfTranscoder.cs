using System.Runtime.InteropServices;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>
/// The Windows engine: re-encodes a video with Windows' own Media Foundation encoders, no ffmpeg needed.
/// The Source Reader decodes the picture to NV12 (cropped to the visible picture by the video processor)
/// and the sound to 16-bit PCM; the Sink Writer encodes HEVC - or H.264 when this PC has no HEVC encoder -
/// at the advisor's target bit rate, and AAC 192 kbit/s, into an MP4. Hardware encoders (graphics card)
/// are allowed, so it is fast. The two streams are pumped in time order so the MP4 stays interleaved.
/// Limits (shown in the help text): sound must be 44.1 / 48 kHz with 1, 2 or 6 channels, 8-bit pictures
/// only, and camera metadata (e.g. GPS) is not carried over - use ffmpeg for those.
/// </summary>
public static class MfTranscoder
{
    private const int Progressive = 2;
    private const int AacBytesPerSecond = 24_000; // 192 kbit/s
    private static readonly Guid AdvancedVideoProcessing = new("0f81da2c-b537-4672-a8b2-a681b17307a3");
    private static readonly Lazy<bool> LazyHevc = new(() => CanEncode(MF.FormatHevc));

    /// <summary>True when Windows has an HEVC encoder (usually the graphics card's) the Sink Writer can use.</summary>
    public static bool HasHevcEncoder => LazyHevc.Value;

    /// <summary>Makes the copy. Never throws for a bad file; a failed or stopped encode deletes the partial output.</summary>
    public static EncodeResult Transcode(EncodeJob job, IProgress<int>? percent, CancellationToken cancel)
    {
        if (!MF.Started)
        {
            return EncodeResult.Failed("Windows Media Foundation is not available on this PC.");
        }
        IMFSourceReader? reader = null;
        IMFSinkWriter? writer = null;
        try
        {
            reader = OpenReader(job.Source.Path);
            var plan = ConfigureReader(reader, job.Source.HasAudio);
            if (plan.Error is not null)
            {
                return EncodeResult.Failed(plan.Error);
            }
            writer = OpenWriter(job.Output);
            var streams = ConfigureWriter(writer, reader, plan, job);
            MF.Check(writer.BeginWriting(), "start writing");
            var result = Pump(reader, writer, streams, job.Source.DurationMs, percent, cancel);
            if (result.Ok)
            {
                MF.Check(writer.Complete(), "finish the file");
            }
            return result;
        }
        catch (COMException ex)
        {
            return EncodeResult.Failed("Windows could not re-encode this video (" + ex.Message + "). Try the ffmpeg engine.");
        }
        finally
        {
            MF.Release(writer);
            MF.Release(reader);
        }
    }

    /// <summary>What the reader delivers: picture size and sound format (Error set when the sound cannot be kept).</summary>
    private sealed record ReaderPlan(int Width, int Height, long FrameRate, bool Audio, string? Error);

    private sealed record WriterStreams(int Video, int Audio);

    private static IMFSourceReader OpenReader(string path)
    {
        MF.Check(MFNative.MFCreateAttributes(out var attributes, 1), "create reader settings");
        try
        {
            attributes.SetUINT32(AdvancedVideoProcessing, 1);
            MF.Check(MFNative.MFCreateSourceReaderFromURL(path, attributes, out var reader), "open the video");
            return reader;
        }
        finally
        {
            MF.Release(attributes);
        }
    }

    private static ReaderPlan ConfigureReader(IMFSourceReader reader, bool wantAudio)
    {
        reader.SetStreamSelection(MF.AllStreams, false);
        MF.Check(reader.SetStreamSelection(MF.FirstVideoStream, true), "select the picture");
        MF.Check(reader.GetNativeMediaType(MF.FirstVideoStream, 0, out var native), "read the picture format");
        int width, height;
        long rate;
        try
        {
            MF.Check(native.GetUINT64(MF.FrameSize, out var size), "read the picture size");
            (width, height) = VisibleSize(native, MF.Unpack(size));
            rate = native.GetUINT64(MF.FrameRate, out var r) >= 0 ? r : MF.Pack(30, 1);
        }
        finally
        {
            MF.Release(native);
        }
        SetReaderType(reader, MF.FirstVideoStream, MF.MediaTypeVideo, MF.FormatNv12, t =>
        {
            t.SetUINT64(MF.FrameSize, MF.Pack(width, height));
            t.SetUINT64(MF.FrameRate, rate);
        });
        if (!wantAudio)
        {
            return new ReaderPlan(width, height, rate, false, null);
        }
        MF.Check(reader.SetStreamSelection(MF.FirstAudioStream, true), "select the sound");
        SetReaderType(reader, MF.FirstAudioStream, MF.MediaTypeAudio, MF.AudioFormatPcm, t => t.SetUINT32(MF.AudioBitsPerSample, 16));
        return new ReaderPlan(width, height, rate, true, AudioProblem(reader));
    }

    /// <summary>The visible picture size: the display aperture when the file has one (1080p is coded as 1088), else the frame size.</summary>
    internal static (int W, int H) VisibleSize(IMFAttributes type, (int W, int H) coded)
    {
        var buffer = Marshal.AllocCoTaskMem(16);
        try
        {
            if (type.GetBlob(MF.MinimumDisplayAperture, buffer, 16, IntPtr.Zero) >= 0)
            {
                var (w, h) = (Marshal.ReadInt32(buffer, 8), Marshal.ReadInt32(buffer, 12));
                if (w > 0 && h > 0 && w <= coded.W && h <= coded.H)
                {
                    return (w & ~1, h & ~1);
                }
            }
            return (coded.W & ~1, coded.H & ~1);
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    private static void SetReaderType(IMFSourceReader reader, int stream, Guid major, Guid subtype, Action<IMFAttributes> extra)
    {
        MF.Check(MFNative.MFCreateMediaType(out var type), "create a format");
        try
        {
            type.SetGUID(MF.MajorType, major);
            type.SetGUID(MF.Subtype, subtype);
            extra(type);
            MF.Check(reader.SetCurrentMediaType(stream, IntPtr.Zero, type), "set the decoded format");
        }
        finally
        {
            MF.Release(type);
        }
    }

    /// <summary>Null when Windows' AAC encoder accepts the decoded sound; otherwise the reason.</summary>
    private static string? AudioProblem(IMFSourceReader reader)
    {
        MF.Check(reader.GetCurrentMediaType(MF.FirstAudioStream, out var pcm), "read the sound format");
        try
        {
            pcm.GetUINT32(MF.AudioSamplesPerSecond, out var rate);
            pcm.GetUINT32(MF.AudioChannels, out var channels);
            return rate is 44_100 or 48_000 && channels is 1 or 2 or 6
                ? null
                : "The Windows engine cannot keep this sound track (" + rate + " Hz, " + channels + " channels). Use the ffmpeg engine for this video.";
        }
        finally
        {
            MF.Release(pcm);
        }
    }

    private static IMFSinkWriter OpenWriter(string output)
    {
        MF.Check(MFNative.MFCreateAttributes(out var attributes, 2), "create writer settings");
        try
        {
            attributes.SetUINT32(MF.EnableHardwareTransforms, 1);
            attributes.SetUINT32(MF.DisableThrottling, 1);
            MF.Check(MFNative.MFCreateSinkWriterFromURL(output, IntPtr.Zero, attributes, out var writer), "create the output file");
            return writer;
        }
        finally
        {
            MF.Release(attributes);
        }
    }

    private static WriterStreams ConfigureWriter(IMFSinkWriter writer, IMFSourceReader reader, ReaderPlan plan, EncodeJob job)
    {
        var codec = HasHevcEncoder ? MF.FormatHevc : MF.FormatH264;
        var video = AddStream(writer, reader, MF.FirstVideoStream, t =>
        {
            t.SetGUID(MF.MajorType, MF.MediaTypeVideo);
            t.SetGUID(MF.Subtype, codec);
            t.SetUINT32(MF.AvgBitrate, Math.Max(200, job.TargetVideoKbps) * 1000);
            t.SetUINT32(MF.InterlaceMode, Progressive);
            t.SetUINT64(MF.FrameSize, MF.Pack(plan.Width, plan.Height));
            t.SetUINT64(MF.FrameRate, plan.FrameRate);
            t.SetUINT64(MF.PixelAspectRatio, MF.Pack(1, 1));
            if (job.Source.Rotation != 0)
            {
                t.SetUINT32(MF.VideoRotation, job.Source.Rotation);
            }
        });
        if (!plan.Audio)
        {
            return new WriterStreams(video, -1);
        }
        MF.Check(reader.GetCurrentMediaType(MF.FirstAudioStream, out var pcm), "read the sound format");
        pcm.GetUINT32(MF.AudioSamplesPerSecond, out var rate);
        pcm.GetUINT32(MF.AudioChannels, out var channels);
        MF.Release(pcm);
        var audio = AddStream(writer, reader, MF.FirstAudioStream, t =>
        {
            t.SetGUID(MF.MajorType, MF.MediaTypeAudio);
            t.SetGUID(MF.Subtype, MF.AudioFormatAac);
            t.SetUINT32(MF.AudioBitsPerSample, 16);
            t.SetUINT32(MF.AudioSamplesPerSecond, rate);
            t.SetUINT32(MF.AudioChannels, channels);
            t.SetUINT32(MF.AudioAvgBytesPerSecond, AacBytesPerSecond);
            t.SetUINT32(MF.AacPayloadType, 0);
        });
        return new WriterStreams(video, audio);
    }

    /// <summary>Adds an encoded output stream and connects it to the reader's decoded format of the same stream.</summary>
    private static int AddStream(IMFSinkWriter writer, IMFSourceReader reader, int readerStream, Action<IMFAttributes> describe)
    {
        MF.Check(MFNative.MFCreateMediaType(out var output), "create the output format");
        try
        {
            describe(output);
            MF.Check(writer.AddStream(output, out var index), "add an output stream (no suitable encoder?)");
            MF.Check(reader.GetCurrentMediaType(readerStream, out var input), "read the decoded format");
            try
            {
                MF.Check(writer.SetInputMediaType(index, input, null), "connect the encoder");
            }
            finally
            {
                MF.Release(input);
            }
            return index;
        }
        finally
        {
            MF.Release(output);
        }
    }

    /// <summary>Copies samples from reader to writer, always taking the stream that is behind in time.</summary>
    private static EncodeResult Pump(IMFSourceReader reader, IMFSinkWriter writer, WriterStreams streams, long durationMs, IProgress<int>? percent, CancellationToken cancel)
    {
        var state = new PumpState { AudioDone = streams.Audio < 0 };
        var lastPercent = -1;
        while (!(state.VideoDone && state.AudioDone))
        {
            if (cancel.IsCancellationRequested)
            {
                return EncodeResult.Stopped;
            }
            var takeVideo = !state.VideoDone && (state.AudioDone || state.VideoTime <= state.AudioTime);
            var error = takeVideo
                ? Step(reader, writer, MF.FirstVideoStream, streams.Video, ref state.VideoDone, ref state.VideoTime)
                : Step(reader, writer, MF.FirstAudioStream, streams.Audio, ref state.AudioDone, ref state.AudioTime);
            if (error is not null)
            {
                return EncodeResult.Failed(error);
            }
            var now = durationMs > 0 ? (int)Math.Clamp(state.VideoTime / MF.TicksPerMs * 100 / durationMs, 0, 100) : 0;
            if (now != lastPercent)
            {
                percent?.Report(now);
                lastPercent = now;
            }
        }
        return EncodeResult.Success;
    }

    private sealed class PumpState
    {
        public bool VideoDone;
        public bool AudioDone;
        public long VideoTime;
        public long AudioTime;
    }

    private static string? Step(IMFSourceReader reader, IMFSinkWriter writer, int readerStream, int writerStream, ref bool done, ref long time)
    {
        if (reader.ReadSample(readerStream, 0, out _, out var flags, out var timestamp, out var sample) < 0)
        {
            return "Windows could not decode part of this video.";
        }
        try
        {
            if ((flags & MF.FlagEndOfStream) != 0)
            {
                done = true;
            }
            if ((flags & MF.FlagStreamTick) != 0)
            {
                writer.SendStreamTick(writerStream, timestamp);
            }
            if (sample is not null)
            {
                time = Math.Max(time, timestamp);
                if (writer.WriteSample(writerStream, sample) < 0)
                {
                    return "Windows could not encode part of this video.";
                }
            }
            else if (!done && (flags & MF.FlagStreamTick) == 0)
            {
                time = Math.Max(time, timestamp);
            }
            return null;
        }
        finally
        {
            MF.Release(sample);
        }
    }

    /// <summary>True when the Sink Writer can encode <paramref name="subtype"/> (tries a tiny writer in the temp folder).</summary>
    private static bool CanEncode(Guid subtype)
    {
        if (!MF.Started)
        {
            return false;
        }
        var file = Path.Combine(Path.GetTempPath(), "ds_encoder_probe_" + Guid.NewGuid().ToString("N") + ".mp4");
        IMFSinkWriter? writer = null;
        try
        {
            writer = OpenWriter(file);
            MF.Check(MFNative.MFCreateMediaType(out var output), "create type");
            MF.Check(MFNative.MFCreateMediaType(out var input), "create type");
            try
            {
                foreach (var (t, s) in new[] { (output, subtype), (input, MF.FormatNv12) })
                {
                    t.SetGUID(MF.MajorType, MF.MediaTypeVideo);
                    t.SetGUID(MF.Subtype, s);
                    t.SetUINT32(MF.InterlaceMode, Progressive);
                    t.SetUINT64(MF.FrameSize, MF.Pack(640, 360));
                    t.SetUINT64(MF.FrameRate, MF.Pack(30, 1));
                    t.SetUINT64(MF.PixelAspectRatio, MF.Pack(1, 1));
                }
                output.SetUINT32(MF.AvgBitrate, 1_000_000);
                return writer.AddStream(output, out var index) >= 0 && writer.SetInputMediaType(index, input, null) >= 0;
            }
            finally
            {
                MF.Release(output);
                MF.Release(input);
            }
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            MF.Release(writer);
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A probe file the writer still held; it is in the temp folder and harmless.
            }
        }
    }
}
