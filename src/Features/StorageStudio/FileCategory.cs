namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// The kinds of file Storage Studio groups space by. The numeric values index the per-folder
/// size array in <see cref="SpaceNode"/>, so <see cref="Other"/> must stay last.
/// </summary>
public enum FileCategory
{
    Video,
    Picture,
    Audio,
    Document,
    Archive,
    Installer,
    Code,
    Other
}

/// <summary>
/// Maps a file name to a <see cref="FileCategory"/> by its extension. Pure, case-insensitive and
/// culture-free; unknown or missing extensions are <see cref="FileCategory.Other"/>.
/// </summary>
public static class FileTypeClassifier
{
    /// <summary>Number of categories - the length of every per-category array.</summary>
    public const int CategoryCount = (int)FileCategory.Other + 1;

    private static readonly Dictionary<string, FileCategory> ByExtension = Build();

    /// <summary>Human label for a category, as shown in charts and reports.</summary>
    public static string Label(FileCategory category) => category switch
    {
        FileCategory.Video => "Videos",
        FileCategory.Picture => "Pictures",
        FileCategory.Audio => "Music & audio",
        FileCategory.Document => "Documents",
        FileCategory.Archive => "Archives",
        FileCategory.Installer => "Installers & disk images",
        FileCategory.Code => "Code & build files",
        _ => "Other"
    };

    /// <summary>Classifies <paramref name="fileName"/> (a name or a full path) by its extension.</summary>
    public static FileCategory Classify(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
        {
            return FileCategory.Other;
        }
        return ByExtension.TryGetValue(ext, out var category) ? category : FileCategory.Other;
    }

    private static Dictionary<string, FileCategory> Build()
    {
        var map = new Dictionary<string, FileCategory>(StringComparer.OrdinalIgnoreCase);
        Add(map, FileCategory.Video, ".mp4 .mkv .mov .avi .wmv .m4v .webm .flv .mpg .mpeg .3gp .mts .m2ts .vob");
        Add(map, FileCategory.Picture, ".jpg .jpeg .png .gif .bmp .tif .tiff .heic .heif .webp .raw .cr2 .cr3 .nef .arw .dng .orf .rw2 .psd .svg .ico");
        Add(map, FileCategory.Audio, ".mp3 .wav .flac .aac .m4a .ogg .wma .opus .aiff .mid");
        Add(map, FileCategory.Document, ".pdf .doc .docx .xls .xlsx .ppt .pptx .txt .rtf .odt .ods .odp .csv .md .epub .mobi .pst .ost .eml .msg .one");
        Add(map, FileCategory.Archive, ".zip .7z .rar .tar .gz .tgz .bz2 .xz .cab .zst");
        Add(map, FileCategory.Installer, ".exe .msi .msix .msixbundle .appx .iso .img .dmg .vhd .vhdx .vmdk .apk");
        Add(map, FileCategory.Code, ".cs .js .ts .tsx .jsx .py .java .c .cpp .h .hpp .go .rs .json .xml .yml .yaml .html .css .dll .pdb .obj .o .lib .so .class .jar .pyc .nupkg .map .lock .sln .csproj");
        return map;
    }

    private static void Add(Dictionary<string, FileCategory> map, FileCategory category, string extensions)
    {
        foreach (var ext in extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            map[ext] = category;
        }
    }
}
