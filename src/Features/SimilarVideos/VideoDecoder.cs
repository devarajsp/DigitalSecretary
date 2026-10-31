using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>What decoding one video produced.</summary>
/// <param name="DurationMs">Length in milliseconds, or 0 when the file does not say.</param>
/// <param name="Width">Display width (after the video's rotation tag), or 0 when unknown.</param>
/// <param name="Height">Display height, or 0 when unknown.</param>
/// <param name="Frames">One small greyscale frame per sampling point (<see cref="VideoDecoder.FrameFractions"/>); null where no frame could be decoded.</param>
public sealed record VideoSample(long DurationMs, int Width, int Height, IReadOnlyList<GrayImage?> Frames);

/// <summary>One decoded frame: top-down rows of B, G, R, X bytes (no padding), as the decoder delivered it (before rotation).</summary>
internal sealed record RawFrame(int Width, int Height, byte[] Bgrx);

/// <summary>
/// Reads videos through <b>Windows Media Foundation</b> - every format Windows can play (MP4/MOV with
/// H.264 or HEVC, WMV, AVI, MKV/WebM where the codec is installed, ...). For fingerprinting it decodes
/// <see cref="FrameCount"/> frames at fixed fractions of the length, seeking to the exact time (not the
/// nearest key frame, which differs between two encodes of the same video), and applies the video's
/// rotation tag so frames are upright. Never throws for a bad file: it returns null and the scan skips it.
/// </summary>
public sealed class VideoDecoder
{
    /// <summary>Frames sampled per video.</summary>
    public const int FrameCount = 10;

    /// <summary>Long side of the greyscale frames handed to the hasher.</summary>
    public const int SampleSize = 160;

    /// <summary>Where in the video the frames are taken: 5%, 15%, ... 95% - skipping the very start and end, where intros, fades and black frames live.</summary>
    public static IReadOnlyList<double> FrameFractions { get; } = [.. Enumerable.Range(0, FrameCount).Select(i => 0.05 + (0.1 * i))];

    /// <summary>True when Media Foundation could be started on this machine (absent on some Windows Server / "N" editions).</summary>
    public static bool IsAvailable => MF.Started;

    /// <summary>Decodes the sampling frames of <paramref name="path"/>, or returns null when it cannot be opened as a video.</summary>
    public VideoSample? Read(string path)
    {
        using var session = ReaderSession.Open(path);
        if (session is null)
        {
            return null;
        }
        var (width, height) = session.DisplaySize;
        if (session.DurationTicks <= 0)
        {
            return new VideoSample(0, width, height, new GrayImage?[FrameCount]);
        }
        var frames = FrameFractions
            .Select(f => session.FrameAt((long)(session.DurationTicks * f)) is { } raw ? Upright(ToGray(raw, SampleSize), session.Rotation) : null)
            .ToList();
        return new VideoSample(session.DurationTicks / MF.TicksPerMs, width, height, frames);
    }

    /// <summary>
    /// A film strip of <paramref name="count"/> frames side by side (spread over the video), each
    /// <paramref name="frameHeight"/> pixels high, for the review cards. Null when nothing decodes.
    /// </summary>
    public Bitmap? Filmstrip(string path, int frameHeight, int count = 4)
    {
        using var session = ReaderSession.Open(path);
        if (session is null || session.DurationTicks <= 0)
        {
            return null;
        }
        var shots = new List<Bitmap>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var at = (long)(session.DurationTicks * (0.1 + (0.8 * i / Math.Max(1, count - 1))));
                if (session.FrameAt(at) is { } raw)
                {
                    shots.Add(ToBitmap(raw, frameHeight, session.Rotation));
                }
            }
            return shots.Count == 0 ? null : Join(shots, frameHeight);
        }
        finally
        {
            shots.ForEach(b => b.Dispose());
        }
    }

    /// <summary>Puts bitmaps side by side with a 2 px gap on a dark background (a film strip).</summary>
    public static Bitmap Join(IReadOnlyList<Bitmap> shots, int height)
    {
        const int Gap = 2;
        var width = shots.Sum(s => s.Width) + (Gap * Math.Max(0, shots.Count - 1));
        var strip = new Bitmap(Math.Max(1, width), height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(strip);
        g.Clear(Color.FromArgb(30, 30, 30));
        var x = 0;
        foreach (var shot in shots)
        {
            g.DrawImage(shot, x, (height - shot.Height) / 2, shot.Width, shot.Height);
            x += shot.Width + Gap;
        }
        return strip;
    }

    /// <summary>
    /// Greyscale (Rec. 601 luma) version of a frame, shrunk so its long side is at most
    /// <paramref name="maxSide"/>: first a cheap whole-pixel box average, then the exact area-average resize.
    /// </summary>
    internal static GrayImage ToGray(RawFrame raw, int maxSide)
    {
        var step = Math.Max(1, Math.Max(raw.Width, raw.Height) / (maxSide * 2));
        var w = Math.Max(1, raw.Width / step);
        var h = Math.Max(1, raw.Height / step);
        var pixels = new float[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                pixels[(y * w) + x] = BoxLuma(raw, x * step, y * step, step);
            }
        }
        var gray = new GrayImage(w, h, pixels);
        var scale = Math.Min(1d, maxSide / (double)Math.Max(w, h));
        return scale < 1 ? gray.Resize(Math.Max(1, (int)Math.Round(w * scale)), Math.Max(1, (int)Math.Round(h * scale))) : gray;
    }

    /// <summary>The frame turned upright for a video whose rotation tag is <paramref name="degrees"/> (0, 90, 180, 270).</summary>
    internal static GrayImage Upright(GrayImage frame, int degrees) => degrees switch
    {
        90 => frame.Transform(PictureOrientation.Rotate90),
        180 => frame.Transform(PictureOrientation.Rotate180),
        270 => frame.Transform(PictureOrientation.Rotate270),
        _ => frame
    };

    private static float BoxLuma(RawFrame raw, int x0, int y0, int step)
    {
        double sum = 0;
        var n = 0;
        for (var y = y0; y < Math.Min(raw.Height, y0 + step); y++)
        {
            var row = y * raw.Width * 4;
            for (var x = x0; x < Math.Min(raw.Width, x0 + step); x++)
            {
                var i = row + (x * 4);
                sum += (0.114 * raw.Bgrx[i]) + (0.587 * raw.Bgrx[i + 1]) + (0.299 * raw.Bgrx[i + 2]);
                n++;
            }
        }
        return n == 0 ? 0f : (float)(sum / n);
    }

    private static Bitmap ToBitmap(RawFrame raw, int height, int rotation)
    {
        using var full = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppRgb);
        var data = full.LockBits(new Rectangle(0, 0, raw.Width, raw.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
        try
        {
            for (var y = 0; y < raw.Height; y++)
            {
                Marshal.Copy(raw.Bgrx, y * raw.Width * 4, data.Scan0 + (y * data.Stride), raw.Width * 4);
            }
        }
        finally
        {
            full.UnlockBits(data);
        }
        full.RotateFlip(rotation switch
        {
            90 => RotateFlipType.Rotate90FlipNone,
            180 => RotateFlipType.Rotate180FlipNone,
            270 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        });
        var w = Math.Max(1, (int)Math.Round(full.Width * (height / (double)full.Height)));
        var shot = new Bitmap(w, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(shot);
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.DrawImage(full, new Rectangle(0, 0, w, height));
        return shot;
    }
}

/// <summary>
/// One open Media Foundation Source Reader, configured for the first video stream with RGB32 output
/// (Windows converts from whatever the codec produces). Knows the frame size, stride, length and rotation.
/// </summary>
internal sealed class ReaderSession : IDisposable
{
    /// <summary>Most samples read after a seek before giving up on reaching the target time.</summary>
    private const int MaxReadsPerFrame = 2000;

    private readonly IMFSourceReader _reader;

    // The decoded buffer's layout ("coded" size, often padded to a multiple of 16: 1080p is coded as 1088)...
    private int _codedWidth;
    private int _codedHeight;
    private int _stride;

    // ...and the part of it that is the picture (the display aperture, or the size first announced).
    private Rectangle _view;
    private Size _announced;

    private ReaderSession(IMFSourceReader reader) => _reader = reader;

    /// <summary>Length in 100 ns units, or 0 when unknown.</summary>
    public long DurationTicks { get; private set; }

    /// <summary>The rotation tag in degrees (0, 90, 180, 270).</summary>
    public int Rotation { get; private set; }

    /// <summary>Width and height as displayed (swapped for a 90 or 270 degree rotation tag).</summary>
    public (int Width, int Height) DisplaySize => Rotation is 90 or 270 ? (_view.Height, _view.Width) : (_view.Width, _view.Height);

    /// <summary>
    /// The picture area inside a <paramref name="coded"/>-size buffer: the display aperture when the format
    /// gives one, otherwise the top-left <paramref name="announced"/> size (decoders pad at the bottom/right),
    /// never larger than the buffer. Pure, so the padding rule is unit-tested.
    /// </summary>
    internal static Rectangle ViewOf(Size coded, Size announced, Rectangle? aperture)
    {
        var bounds = new Rectangle(Point.Empty, coded);
        if (aperture is { Width: > 0, Height: > 0 } a && bounds.Contains(a))
        {
            return a;
        }
        var w = announced.Width > 0 ? Math.Min(announced.Width, coded.Width) : coded.Width;
        var h = announced.Height > 0 ? Math.Min(announced.Height, coded.Height) : coded.Height;
        return new Rectangle(0, 0, w, h);
    }

    /// <summary>
    /// Reads an MFVideoArea blob (offset x/y as 16.16 fixed point, then width and height) into a rectangle;
    /// null when it is missing or malformed.
    /// </summary>
    internal static Rectangle? ParseVideoArea(byte[]? blob)
        => blob is { Length: >= 16 }
            ? new Rectangle(BitConverter.ToInt16(blob, 2), BitConverter.ToInt16(blob, 6), BitConverter.ToInt32(blob, 8), BitConverter.ToInt32(blob, 12))
            : null;

    /// <summary>Opens <paramref name="path"/> for frame reading, or returns null if it has no decodable video stream.</summary>
    public static ReaderSession? Open(string path)
    {
        if (!MF.Started || !File.Exists(path))
        {
            return null;
        }
        if (NativeMethods.MFCreateAttributes(out var attributes, 1) < 0)
        {
            return null;
        }
        IMFSourceReader? reader;
        try
        {
            attributes.SetUINT32(MF.EnableVideoProcessing, 1);
            if (NativeMethods.MFCreateSourceReaderFromURL(path, attributes, out reader) < 0)
            {
                return null;
            }
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            MF.Release(attributes);
        }
        var session = new ReaderSession(reader);
        if (session.Configure())
        {
            return session;
        }
        session.Dispose();
        return null;
    }

    /// <summary>
    /// The frame shown at <paramref name="ticks"/>: seeks (which lands on the key frame before it) and
    /// decodes forward until the frame that covers the target time. Falls back to the last frame
    /// decoded when the stream ends first. Null when nothing could be decoded.
    /// </summary>
    public RawFrame? FrameAt(long ticks)
    {
        var position = new PropVariant { Type = MF.VtI8, Value = ticks };
        _reader.SetCurrentPosition(Guid.Empty, ref position);
        IMFSample? held = null;
        try
        {
            for (var step = 0; step < MaxReadsPerFrame; step++)
            {
                if (_reader.ReadSample(MF.FirstVideoStream, 0, out _, out var flags, out var time, out var sample) < 0)
                {
                    break;
                }
                if ((flags & MF.FlagCurrentMediaTypeChanged) != 0)
                {
                    ReadFormat();
                }
                if (sample is not null)
                {
                    if (Reached(sample, time, ticks))
                    {
                        try
                        {
                            return Copy(sample);
                        }
                        finally
                        {
                            MF.Release(sample);
                        }
                    }
                    MF.Release(held);
                    held = sample;
                }
                if ((flags & MF.FlagEndOfStream) != 0)
                {
                    break;
                }
            }
            return held is null ? null : Copy(held);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            MF.Release(held);
        }
    }

    public void Dispose() => MF.Release(_reader);

    private static bool Reached(IMFSample sample, long time, long target)
    {
        var duration = sample.GetSampleDuration(out var d) >= 0 && d > 0 ? d : 0;
        return time + (duration / 2) >= target;
    }

    private bool Configure()
    {
        try
        {
            _reader.SetStreamSelection(MF.AllStreams, false);
            if (_reader.SetStreamSelection(MF.FirstVideoStream, true) < 0)
            {
                return false;
            }
            Rotation = ReadRotation();
            MF.Check(NativeMethods.MFCreateMediaType(out var wanted), "create media type");
            try
            {
                wanted.SetGUID(MF.MajorType, MF.MediaTypeVideo);
                wanted.SetGUID(MF.Subtype, MF.FormatRgb32);
                if (_reader.SetCurrentMediaType(MF.FirstVideoStream, IntPtr.Zero, wanted) < 0)
                {
                    return false;
                }
            }
            finally
            {
                MF.Release(wanted);
            }
            ReadFormat();
            DurationTicks = _reader.GetPresentationAttribute(MF.MediaSource, MF.PresentationDuration, out var duration) >= 0 ? duration.Value : 0;
            return _view.Width > 0 && _view.Height > 0;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private int ReadRotation()
    {
        if (_reader.GetNativeMediaType(MF.FirstVideoStream, 0, out var native) < 0)
        {
            return 0;
        }
        try
        {
            return native.GetUINT32(MF.VideoRotation, out var degrees) >= 0 && degrees is 90 or 180 or 270 ? degrees : 0;
        }
        finally
        {
            MF.Release(native);
        }
    }

    /// <summary>Reads the output layout; called once after configuring and again whenever the decoder changes it.</summary>
    private void ReadFormat()
    {
        MF.Check(_reader.GetCurrentMediaType(MF.FirstVideoStream, out var current), "read output format");
        try
        {
            MF.Check(current.GetUINT64(MF.FrameSize, out var size), "read frame size");
            (_codedWidth, _codedHeight) = MF.Unpack(size);
            if (_announced.IsEmpty)
            {
                _announced = new Size(_codedWidth, _codedHeight);
            }
            _stride = current.GetUINT32(MF.DefaultStride, out var stride) >= 0 && stride != 0 ? stride : _codedWidth * 4;
            _view = ViewOf(new Size(_codedWidth, _codedHeight), _announced, ParseVideoArea(ReadBlob(current, MF.MinimumDisplayAperture)));
        }
        finally
        {
            MF.Release(current);
        }
    }

    private static byte[]? ReadBlob(IMFAttributes attributes, Guid key)
    {
        if (attributes.GetAllocatedBlob(key, out var memory, out var size) < 0 || memory == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            var bytes = new byte[size];
            Marshal.Copy(memory, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Marshal.FreeCoTaskMem(memory);
        }
    }

    /// <summary>
    /// Copies the picture area of a decoded RGB32 sample into a top-down byte array, leaving out codec padding
    /// (a negative stride means the rows are stored bottom-up).
    /// </summary>
    private RawFrame? Copy(IMFSample sample)
    {
        if (sample.ConvertToContiguousBuffer(out var buffer) < 0)
        {
            return null;
        }
        try
        {
            if (buffer.Lock(out var data, out _, out var length) < 0)
            {
                return null;
            }
            try
            {
                var pitch = Math.Abs(_stride);
                if (length < (pitch * (_codedHeight - 1)) + (_codedWidth * 4))
                {
                    return null;
                }
                var rowBytes = _view.Width * 4;
                var pixels = new byte[rowBytes * _view.Height];
                for (var y = 0; y < _view.Height; y++)
                {
                    var row = _view.Y + y;
                    var source = _stride > 0 ? row : _codedHeight - 1 - row;
                    Marshal.Copy(data + (source * pitch) + (_view.X * 4), pixels, y * rowBytes, rowBytes);
                }
                return new RawFrame(_view.Width, _view.Height, pixels);
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            MF.Release(buffer);
        }
    }
}
