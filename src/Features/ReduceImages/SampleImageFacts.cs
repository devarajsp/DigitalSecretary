namespace DigitalSecretary.Features.ReduceImages;

/// <summary>
/// A made-up picture folder (placeholder paths, no real files) covering every kind of advice: camera JPEGs at
/// high quality (Recommended), photos kept as PNG / BMP, a BMP screenshot (lossless PNG), an already
/// compressed JPEG, a transparent logo, a 16-bit scan and a small icon (Skip). Used by the documentation
/// screenshots, QA and tests.
/// </summary>
public static class SampleImageFacts
{
    public const string Root = @"C:\Users\You\Pictures";

    public static IReadOnlyList<ImageFacts> Create() =>
    [
        Make(@"2026\IMG_4012.jpg", 7_800_000, 4032, 3024, "jpeg", 96, PictureKind.Photo),
        Make(@"2026\IMG_4013.jpg", 6_900_000, 4032, 3024, "jpeg", 95, PictureKind.Photo),
        Make(@"Camera\DSC_0815.JPG", 11_400_000, 6000, 4000, "jpeg", 98, PictureKind.Photo),
        Make(@"Scans\Grandma 1962.png", 21_500_000, 3500, 2480, "png", 0, PictureKind.Photo),
        Make(@"Scans\Wedding.bmp", 26_000_000, 3600, 2400, "bmp", 0, PictureKind.Photo),
        Make(@"Screens\Bank statement.bmp", 6_200_000, 1920, 1080, "bmp", 0, PictureKind.Graphic),
        Make(@"WhatsApp\IMG-20260101-WA0007.jpg", 210_000, 1600, 1200, "jpeg", 75, PictureKind.Photo),
        Make(@"Design\Logo.png", 380_000, 1200, 1200, "png", 0, PictureKind.Graphic) with { HasAlpha = true },
        Make(@"Scans\Negative 16bit.tif", 92_000_000, 5000, 3200, "tiff", 0, PictureKind.Unknown) with { Untouchable = "16-bit colour - kept as it is so no detail is lost." },
        Make(@"Icons\star.png", 9_000, 64, 64, "png", 0, PictureKind.Graphic)
    ];

    private static ImageFacts Make(string relative, long size, int w, int h, string format, int quality, PictureKind kind) => new()
    {
        Path = Path.Combine(Root, relative),
        Size = size,
        ModifiedUtc = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
        Format = format,
        Width = w,
        Height = h,
        JpegQuality = quality,
        Kind = kind
    };
}
