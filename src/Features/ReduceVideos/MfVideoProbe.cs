namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>
/// Reads <see cref="VideoFacts"/> through Windows Media Foundation (the kit's Source Reader interop), so the
/// feature can scan and advise with no ffmpeg installed. It sees what Windows can decode: codec, picture
/// size, frame rate, length, rotation, HDR transfer and whether there is a sound track.
/// </summary>
public static class MfVideoProbe
{
    public static bool IsAvailable => MF.Started;

    /// <summary>The facts for one file, or null when Windows cannot open it as a video.</summary>
    public static VideoFacts? Read(FileInfo file)
    {
        if (!MF.Started || !file.Exists || MFNative.MFCreateSourceReaderFromURL(file.FullName, null, out var reader) < 0)
        {
            return null;
        }
        try
        {
            return Describe(reader, file);
        }
        finally
        {
            MF.Release(reader);
        }
    }

    private static VideoFacts? Describe(IMFSourceReader reader, FileInfo file)
    {
        if (reader.GetNativeMediaType(MF.FirstVideoStream, 0, out var video) < 0)
        {
            return null;
        }
        try
        {
            var durationMs = reader.GetPresentationAttribute(MF.MediaSource, MF.PresentationDuration, out var d) >= 0 ? d.Value / MF.TicksPerMs : 0;
            if (durationMs <= 0 || video.GetUINT64(MF.FrameSize, out var size) < 0)
            {
                return null;
            }
            var (w, h) = MfTranscoder.VisibleSize(video, MF.Unpack(size));
            var fps = video.GetUINT64(MF.FrameRate, out var rate) >= 0 ? Fps(MF.Unpack(rate)) : 0;
            var codec = video.GetGUID(MF.Subtype, out var subtype) >= 0 ? Codecs.FromMediaFoundation(subtype) : "";
            var transfer = video.GetUINT32(MF.TransferFunction, out var t) >= 0 ? t : 0;
            var rotation = video.GetUINT32(MF.VideoRotation, out var r) >= 0 ? r : 0;
            var hasAudio = HasAudio(reader);
            return new VideoFacts
            {
                Path = file.FullName,
                Size = file.Length,
                ModifiedUtc = file.LastWriteTimeUtc,
                DurationMs = durationMs,
                Width = w,
                Height = h,
                Fps = Math.Round(fps, 3),
                Codec = codec,
                BitDepth = transfer is MF.TransferPq or MF.TransferHlg ? 10 : 8,
                IsHdr = transfer is MF.TransferPq or MF.TransferHlg,
                HasAudio = hasAudio,
                AudioCodec = hasAudio ? "audio" : "",
                Rotation = rotation is 90 or 180 or 270 ? rotation : 0,
                ReadBy = "windows"
            };
        }
        finally
        {
            MF.Release(video);
        }
    }

    /// <summary>Frames per second from a packed numerator/denominator.</summary>
    public static double Fps((int Numerator, int Denominator) rate)
        => rate.Denominator > 0 ? (double)rate.Numerator / rate.Denominator : 0;

    private static bool HasAudio(IMFSourceReader reader)
    {
        if (reader.GetNativeMediaType(MF.FirstAudioStream, 0, out var audio) < 0)
        {
            return false;
        }
        MF.Release(audio);
        return true;
    }
}
