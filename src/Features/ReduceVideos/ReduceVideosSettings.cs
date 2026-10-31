using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>Remembered, non-sensitive settings: the folder, the engine and quality choices, and where originals go.</summary>
public sealed class ReduceVideosSettings
{
    public string Root { get; set; } = "";
    public VideoEngineKind Engine { get; set; } = VideoEngineKind.FfmpegX265;
    public VideoQuality Quality { get; set; } = VideoQuality.VisuallyLossless;
    public int MinSavingPercent { get; set; } = 25;

    /// <summary>Measure the picture quality of each copy (SSIM, ffmpeg only). On by default.</summary>
    public bool CheckQuality { get; set; } = true;

    /// <summary>Leave HDR videos alone. On by default.</summary>
    public bool SkipHdr { get; set; } = true;

    /// <summary>Where originals go. The review folder by default, so every run can be undone.</summary>
    public RemoveAction Action { get; set; } = RemoveAction.ReviewFolder;

    /// <summary>A folder holding ffmpeg.exe + ffprobe.exe chosen by the user; empty = search the usual places.</summary>
    public string FfmpegFolder { get; set; } = "";

    public VideoAdviceOptions ToAdviceOptions(bool windowsHasHevc) => new()
    {
        Engine = Engine,
        Quality = Quality,
        MinSavingPercent = Math.Clamp(MinSavingPercent, 5, 90),
        SkipHdr = SkipHdr,
        WindowsHasHevc = windowsHasHevc
    };
}

/// <summary>Loads and saves <see cref="ReduceVideosSettings"/> in the feature's own data folder.</summary>
public sealed class ReduceVideosSettingsStore(string dataDirectory)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private readonly string _file = Path.Combine(dataDirectory, "reduce_videos_settings.json");

    public ReduceVideosSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                return JsonSerializer.Deserialize<ReduceVideosSettings>(File.ReadAllText(_file), JsonOpts) ?? new();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            // Corrupt or unreadable settings - fall back to the safe defaults.
        }
        return new();
    }

    public void Save(ReduceVideosSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
