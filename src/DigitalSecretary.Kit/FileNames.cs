using System.Text.RegularExpressions;

namespace DigitalSecretary.Kit;

/// <summary>Rules about file names shared by the look-alike finders.</summary>
public static partial class FileNames
{
    /// <summary>
    /// True when a file name looks like a duplicate made by copying: "IMG_1 (2).jpg", "x - Copy.mp4",
    /// "clip_copy.mov", "Copy of trip.mp4". Used to prefer the original as a group's keeper.
    /// </summary>
    public static bool LooksLikeCopy(string path) => CopyName().IsMatch(Path.GetFileNameWithoutExtension(path));

    [GeneratedRegex(@"(\(\d+\)$)|([ _-]+copy( \(\d+\))?$)|(^copy of )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CopyName();
}
