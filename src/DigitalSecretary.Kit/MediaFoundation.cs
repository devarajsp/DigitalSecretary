using System.Runtime.InteropServices;

namespace DigitalSecretary.Kit;

/// <summary>
/// The slice of Windows Media Foundation (mfplat / mfreadwrite) the video features need, declared by hand so
/// no NuGet package is required: the Source Reader (decode frames at a time position, converted to RGB32
/// by Windows) and - for the tests only - the Sink Writer (encode tiny test videos at run time).
/// Every method keeps its HRESULT (<c>PreserveSig</c>); callers check it. Interface members follow the
/// native vtable order exactly - do not reorder them.
/// </summary>
public static class MF
{
    public const int Version = 0x00020070;
    public const int FirstVideoStream = unchecked((int)0xFFFFFFFC);
    public const int AllStreams = unchecked((int)0xFFFFFFFE);
    public const int MediaSource = unchecked((int)0xFFFFFFFF);

    public const int FlagEndOfStream = 0x2;
    public const int FlagCurrentMediaTypeChanged = 0x20;
    public const int FlagStreamTick = 0x100;

    public const ushort VtI8 = 20;
    public const ushort VtUi8 = 21;

    /// <summary>100-nanosecond units per millisecond (Media Foundation time unit).</summary>
    public const long TicksPerMs = 10_000;

    public static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    public static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    public static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    public static readonly Guid PixelAspectRatio = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    public static readonly Guid InterlaceMode = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    public static readonly Guid AvgBitrate = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    public static readonly Guid MinimumDisplayAperture = new("d7388766-18fe-48c6-a177-ee894867c8c4");
    public static readonly Guid VideoRotation = new("c380465d-2271-428c-9b83-ecea3b4a85c1");
    public static readonly Guid MediaTypeVideo = new("73646976-0000-0010-8000-00aa00389b71"); // Media Foundation GUID, not a card number - pragma: allowlist secret
    public static readonly Guid FormatRgb32 = new("00000016-0000-0010-8000-00aa00389b71"); // Media Foundation GUID - pragma: allowlist secret
    public static readonly Guid FormatH264 = new("34363248-0000-0010-8000-00aa00389b71");
    public static readonly Guid PresentationDuration = new("6c990d33-bb8e-477a-8598-0d5d96fcd88a");
    public static readonly Guid EnableVideoProcessing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");

    // Used by the Windows video engine of Reduce Size - Videos (decode with the Source Reader, re-encode with the Sink Writer).
    public const int FirstAudioStream = unchecked((int)0xFFFFFFFD);
    public const int AnyStream = unchecked((int)0xFFFFFFFE);
    public static readonly Guid MediaTypeAudio = new("73647561-0000-0010-8000-00aa00389b71"); // Media Foundation GUID - pragma: allowlist secret
    public static readonly Guid FormatNv12 = new("3231564e-0000-0010-8000-00aa00389b71"); // Media Foundation GUID - pragma: allowlist secret
    public static readonly Guid FormatHevc = new("43564548-0000-0010-8000-00aa00389b71"); // Media Foundation GUID - pragma: allowlist secret
    public static readonly Guid AudioFormatPcm = new("00000001-0000-0010-8000-00aa00389b71"); // Media Foundation GUID - pragma: allowlist secret
    public static readonly Guid AudioFormatAac = new("00001610-0000-0010-8000-00aa00389b71"); // Media Foundation GUID - pragma: allowlist secret
    public static readonly Guid AudioChannels = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    public static readonly Guid AudioSamplesPerSecond = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    public static readonly Guid AudioBitsPerSample = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    public static readonly Guid AudioAvgBytesPerSecond = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    public static readonly Guid AudioBlockAlignment = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    public static readonly Guid AacPayloadType = new("bfbabe79-7434-4d1c-94f0-72a3b9e17188");
    public static readonly Guid TransferFunction = new("5fb0fce9-be5c-4935-a811-ec838f8eed93");
    public static readonly Guid EnableHardwareTransforms = new("a634a91c-822b-41b9-a494-4de4643612b0");
    public static readonly Guid DisableThrottling = new("08b845d8-2b74-4afe-9d53-be16d2d5ae4f");

    /// <summary>MFVideoTransFunc_2084 (PQ) and MFVideoTransFunc_HLG: the HDR transfer functions.</summary>
    public const int TransferPq = 15;
    public const int TransferHlg = 16;

    /// <summary>MFStartup has succeeded in this process (it is reference-counted and never shut down here).</summary>
    public static bool Started => LazyStart.Value;

    private static readonly Lazy<bool> LazyStart = new(() =>
    {
        try
        {
            return MFNative.Startup(Version, 0) >= 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    });

    /// <summary>Releases a COM object now instead of waiting for the garbage collector (decoded frames are large).</summary>
    public static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
        {
            Marshal.ReleaseComObject(com);
        }
    }

    /// <summary>Throws a <see cref="MediaFoundationException"/> (a <see cref="COMException"/>) for a failed HRESULT.</summary>
    public static void Check(int hr, string what)
    {
        if (hr < 0)
        {
            throw new MediaFoundationException("Media Foundation: " + what + " failed", hr);
        }
    }

    /// <summary>Two 32-bit numbers packed the Media Foundation way (frame size, frame rate, aspect ratio).</summary>
    public static long Pack(int high, int low) => ((long)high << 32) | (uint)low;

    public static (int High, int Low) Unpack(long value) => ((int)(value >> 32), (int)(value & 0xFFFFFFFF));
}

/// <summary>A failed Media Foundation call; callers catch it as a <see cref="COMException"/>.</summary>
public sealed class MediaFoundationException(string message, int hr) : COMException(message, hr);

/// <summary>A PROPVARIANT holding a 64-bit integer (the only kind used here: time positions and durations).</summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct PropVariant
{
    [FieldOffset(0)]
    public ushort Type;

    [FieldOffset(8)]
    public long Value;
}

[ComImport]
[Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFAttributes
{
    [PreserveSig] int GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr value);
    [PreserveSig] int GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int type);
    [PreserveSig] int CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [PreserveSig] int Compare(IntPtr theirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [PreserveSig] int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int value);
    [PreserveSig] int GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out long value);
    [PreserveSig] int GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out double value);
    [PreserveSig] int GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out Guid value);
    [PreserveSig] int GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int length);
    [PreserveSig] int GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr buffer, int size, IntPtr length);
    [PreserveSig] int GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr value, out int length);
    [PreserveSig] int GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int size);
    [PreserveSig] int GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr buffer, int size, IntPtr blobSize);
    [PreserveSig] int GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr buffer, out int size);
    [PreserveSig] int GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr value);
    [PreserveSig] int SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr value);
    [PreserveSig] int DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key);
    [PreserveSig] int DeleteAllItems();
    [PreserveSig] int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, int value);
    [PreserveSig] int SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, long value);
    [PreserveSig] int SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, double value);
    [PreserveSig] int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid value);
    [PreserveSig] int SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    [PreserveSig] int SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr buffer, int size);
    [PreserveSig] int SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr unknown);
    [PreserveSig] int LockStore();
    [PreserveSig] int UnlockStore();
    [PreserveSig] int GetCount(out int items);
    [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
    [PreserveSig] int CopyAllItems(IntPtr destination);
}

/// <summary>IMFSample: the 30 IMFAttributes methods first (vtable order), then the sample's own.</summary>
[ComImport]
[Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFSample
{
    [PreserveSig] int GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr value);
    [PreserveSig] int GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int type);
    [PreserveSig] int CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [PreserveSig] int Compare(IntPtr theirs, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [PreserveSig] int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int value);
    [PreserveSig] int GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out long value);
    [PreserveSig] int GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out double value);
    [PreserveSig] int GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out Guid value);
    [PreserveSig] int GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int length);
    [PreserveSig] int GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr buffer, int size, IntPtr length);
    [PreserveSig] int GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr value, out int length);
    [PreserveSig] int GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int size);
    [PreserveSig] int GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr buffer, int size, IntPtr blobSize);
    [PreserveSig] int GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out IntPtr buffer, out int size);
    [PreserveSig] int GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr value);
    [PreserveSig] int SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr value);
    [PreserveSig] int DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid key);
    [PreserveSig] int DeleteAllItems();
    [PreserveSig] int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, int value);
    [PreserveSig] int SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, long value);
    [PreserveSig] int SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, double value);
    [PreserveSig] int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid value);
    [PreserveSig] int SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    [PreserveSig] int SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr buffer, int size);
    [PreserveSig] int SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, IntPtr unknown);
    [PreserveSig] int LockStore();
    [PreserveSig] int UnlockStore();
    [PreserveSig] int GetCount(out int items);
    [PreserveSig] int GetItemByIndex(int index, out Guid key, IntPtr value);
    [PreserveSig] int CopyAllItems(IntPtr destination);

    [PreserveSig] int GetSampleFlags(out int flags);
    [PreserveSig] int SetSampleFlags(int flags);
    [PreserveSig] int GetSampleTime(out long time);
    [PreserveSig] int SetSampleTime(long time);
    [PreserveSig] int GetSampleDuration(out long duration);
    [PreserveSig] int SetSampleDuration(long duration);
    [PreserveSig] int GetBufferCount(out int count);
    [PreserveSig] int GetBufferByIndex(int index, out IMFMediaBuffer buffer);
    [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
    [PreserveSig] int AddBuffer(IMFMediaBuffer buffer);
    [PreserveSig] int RemoveBufferByIndex(int index);
    [PreserveSig] int RemoveAllBuffers();
    [PreserveSig] int GetTotalLength(out int length);
    [PreserveSig] int CopyToBuffer(IMFMediaBuffer buffer);
}

[ComImport]
[Guid("045fa593-8799-42b8-bc8d-8968c6453507")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaBuffer
{
    [PreserveSig] int Lock(out IntPtr buffer, out int maxLength, out int currentLength);
    [PreserveSig] int Unlock();
    [PreserveSig] int GetCurrentLength(out int length);
    [PreserveSig] int SetCurrentLength(int length);
    [PreserveSig] int GetMaxLength(out int length);
}

/// <summary>IMFSourceReader. Media types are handled through their IMFAttributes base (single inheritance, same pointer).</summary>
[ComImport]
[Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFSourceReader
{
    [PreserveSig] int GetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] out bool selected);
    [PreserveSig] int SetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] bool selected);
    [PreserveSig] int GetNativeMediaType(int streamIndex, int typeIndex, out IMFAttributes mediaType);
    [PreserveSig] int GetCurrentMediaType(int streamIndex, out IMFAttributes mediaType);
    [PreserveSig] int SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFAttributes mediaType);
    [PreserveSig] int SetCurrentPosition([In, MarshalAs(UnmanagedType.LPStruct)] Guid timeFormat, ref PropVariant position);
    [PreserveSig] int ReadSample(int streamIndex, int controlFlags, out int actualStreamIndex, out int streamFlags, out long timestamp, out IMFSample? sample);
    [PreserveSig] int Flush(int streamIndex);
    [PreserveSig] int GetServiceForStream(int streamIndex, [In, MarshalAs(UnmanagedType.LPStruct)] Guid service, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr value);
    [PreserveSig] int GetPresentationAttribute(int streamIndex, [In, MarshalAs(UnmanagedType.LPStruct)] Guid attribute, out PropVariant value);
}

/// <summary>IMFSinkWriter - used only by the tests to encode small videos at run time.</summary>
[ComImport]
[Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFSinkWriter
{
    [PreserveSig] int AddStream(IMFAttributes targetMediaType, out int streamIndex);
    [PreserveSig] int SetInputMediaType(int streamIndex, IMFAttributes inputMediaType, IMFAttributes? encodingParameters);
    [PreserveSig] int BeginWriting();
    [PreserveSig] int WriteSample(int streamIndex, IMFSample sample);
    [PreserveSig] int SendStreamTick(int streamIndex, long timestamp);
    [PreserveSig] int PlaceMarker(int streamIndex, IntPtr context);
    [PreserveSig] int NotifyEndOfSegment(int streamIndex);
    [PreserveSig] int Flush(int streamIndex);
    [PreserveSig] int Complete();
    [PreserveSig] int GetServiceForStream(int streamIndex, [In, MarshalAs(UnmanagedType.LPStruct)] Guid service, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr value);
    [PreserveSig] int GetStatistics(int streamIndex, IntPtr stats);
}

/// <summary>
/// The Media Foundation entry points, as public wrappers over private P/Invokes (CA1401). Named MFNative so it
/// never clashes with a feature's own NativeMethods. Each returns the HRESULT; callers check it.
/// </summary>
public static class MFNative
{
    public static int Startup(int version, int flags) => Native.MFStartup(version, flags);

    public static int MFCreateAttributes(out IMFAttributes attributes, int initialSize) => Native.MFCreateAttributes(out attributes, initialSize);

    public static int MFCreateMediaType(out IMFAttributes mediaType) => Native.MFCreateMediaType(out mediaType);

    public static int MFCreateSample(out IMFSample sample) => Native.MFCreateSample(out sample);

    public static int MFCreateMemoryBuffer(int maxLength, out IMFMediaBuffer buffer) => Native.MFCreateMemoryBuffer(maxLength, out buffer);

    public static int MFCreateSourceReaderFromURL(string url, IMFAttributes? attributes, out IMFSourceReader reader)
        => Native.MFCreateSourceReaderFromURL(url, attributes, out reader);

    public static int MFCreateSinkWriterFromURL(string url, IntPtr byteStream, IMFAttributes? attributes, out IMFSinkWriter writer)
        => Native.MFCreateSinkWriterFromURL(url, byteStream, attributes, out writer);

    private static class Native
    {
        [DllImport("mfplat.dll", ExactSpelling = true)]
        public static extern int MFStartup(int version, int flags);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        public static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        public static extern int MFCreateMediaType(out IMFAttributes mediaType);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        public static extern int MFCreateSample(out IMFSample sample);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        public static extern int MFCreateMemoryBuffer(int maxLength, out IMFMediaBuffer buffer);

        [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int MFCreateSourceReaderFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IMFAttributes? attributes, out IMFSourceReader reader);

        [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int MFCreateSinkWriterFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IntPtr byteStream, IMFAttributes? attributes, out IMFSinkWriter writer);
    }
}
