using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>Remembered, non-sensitive settings: the folder, which changes are allowed, quality, and where originals go.</summary>
public sealed class ReduceImagesSettings
{
    public string Root { get; set; } = "";
    public ImageQuality Quality { get; set; } = ImageQuality.VisuallyLossless;
    public bool RecompressJpegs { get; set; } = true;
    public bool PhotosToJpeg { get; set; } = true;
    public bool LosslessToPng { get; set; } = true;
    public bool Downscale { get; set; }
    public int MaxLongSide { get; set; } = 3840;
    public ModernFormat Modern { get; set; } = ModernFormat.None;
    public int MinSavingPercent { get; set; } = 20;
    public RemoveAction Action { get; set; } = RemoveAction.ReviewFolder;

    public ImageAdviceOptions ToAdviceOptions() => new()
    {
        Quality = Quality,
        RecompressJpegs = RecompressJpegs,
        PhotosToJpeg = PhotosToJpeg,
        LosslessToPng = LosslessToPng,
        Downscale = Downscale,
        MaxLongSide = Math.Clamp(MaxLongSide, 640, 20_000),
        Modern = Modern,
        MinSavingPercent = Math.Clamp(MinSavingPercent, 5, 90)
    };
}

/// <summary>Loads and saves <see cref="ReduceImagesSettings"/> in the feature's own data folder.</summary>
public sealed class ReduceImagesSettingsStore(string dataDirectory)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private readonly string _file = Path.Combine(dataDirectory, "reduce_images_settings.json");

    public ReduceImagesSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                return JsonSerializer.Deserialize<ReduceImagesSettings>(File.ReadAllText(_file), JsonOpts) ?? new();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            // Corrupt or unreadable settings - fall back to the safe defaults.
        }
        return new();
    }

    public void Save(ReduceImagesSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
