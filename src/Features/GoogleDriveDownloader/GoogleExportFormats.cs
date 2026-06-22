namespace DigitalSecretary.Features.GoogleDriveDownloader;

/// <summary>One way to save a Google-native file: a file extension and the export MIME type.</summary>
public sealed record ExportTarget(string Extension, string MimeType);

/// <summary>
/// Pure mapping rules (no Google dependency, so they're unit-testable) deciding how a Drive file
/// is saved. Google-native files (Docs/Sheets/Slides/Drawings) cannot be downloaded as-is — they
/// must be <b>exported</b>; everything else is downloaded raw. We export each native file in BOTH an
/// editable Office format and PDF.
/// </summary>
public static class GoogleExportFormats
{
    public const string NativePrefix = "application/vnd.google-apps.";
    public const string FolderMimeType = "application/vnd.google-apps.folder";

    // Office Open XML + PDF + PNG export MIME types.
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    private const string Pdf = "application/pdf";
    private const string Png = "image/png";

    private static readonly Dictionary<string, ExportTarget[]> NativeExports =
        new(StringComparer.Ordinal)
        {
            ["application/vnd.google-apps.document"] =
                new[] { new ExportTarget(".docx", Docx), new ExportTarget(".pdf", Pdf) },
            ["application/vnd.google-apps.spreadsheet"] =
                new[] { new ExportTarget(".xlsx", Xlsx), new ExportTarget(".pdf", Pdf) },
            ["application/vnd.google-apps.presentation"] =
                new[] { new ExportTarget(".pptx", Pptx), new ExportTarget(".pdf", Pdf) },
            ["application/vnd.google-apps.drawing"] =
                new[] { new ExportTarget(".png", Png), new ExportTarget(".pdf", Pdf) },
        };

    /// <summary>True for any Google-native type (Docs, Sheets, Slides, folders, forms, …).</summary>
    public static bool IsGoogleNative(string? mimeType)
        => mimeType is not null && mimeType.StartsWith(NativePrefix, StringComparison.Ordinal);

    /// <summary>True for a Drive folder (used to build the tree, never downloaded as a file).</summary>
    public static bool IsFolder(string? mimeType)
        => string.Equals(mimeType, FolderMimeType, StringComparison.Ordinal);

    /// <summary>
    /// The export targets for a Google-native file. Empty for non-exportable native types (e.g. Forms,
    /// Sites, Apps Script, shortcuts) — the caller logs those as skipped.
    /// </summary>
    public static IReadOnlyList<ExportTarget> ExportTargetsFor(string mimeType)
        => NativeExports.TryGetValue(mimeType, out var targets) ? targets : Array.Empty<ExportTarget>();
}
