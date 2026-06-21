namespace DigitalSecretary.Features.ClipboardHistory;

/// <summary>Pure formatting of a clipboard entry into a single-line list preview (testable).</summary>
public static class ClipboardPreview
{
    public const int MaxLength = 90;

    /// <summary>Collapses newlines, trims, and truncates long text with an ellipsis.</summary>
    public static string Format(string text)
    {
        var firstLine = text.ReplaceLineEndings(" ").Trim();
        return firstLine.Length > MaxLength ? firstLine[..MaxLength] + "…" : firstLine;
    }
}
