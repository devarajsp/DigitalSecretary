namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// One line of the checksum list: a file's MD5 fingerprint plus the size and modified stamp that let
/// a later run decide whether the stored checksum is still valid.
/// </summary>
/// <param name="Md5">Lower-case 32-character MD5 of the file's contents.</param>
/// <param name="Length">Size in bytes, or <c>-1</c> when the list did not record one.</param>
/// <param name="ModifiedUtc">Last-write time in UTC, or <c>default</c> when not recorded.</param>
/// <param name="FullPath">Absolute path of the file.</param>
public sealed record ChecksumEntry(string Md5, long Length, DateTime ModifiedUtc, string FullPath)
{
    /// <summary>True when this entry carries the size and stamp an incremental rescan needs.</summary>
    public bool HasFileStamp => Length >= 0 && ModifiedUtc != default;

    /// <summary>Size for arithmetic: an unrecorded size counts as zero rather than skewing totals.</summary>
    public long BillableLength => Length > 0 ? Length : 0;

    /// <summary>
    /// True when the file on disk still has exactly the size and modified time this entry recorded,
    /// which is what makes re-hashing it unnecessary.
    /// </summary>
    public bool Matches(long length, DateTime modifiedUtc)
        => HasFileStamp && Length == length && ModifiedUtc == modifiedUtc;

    /// <summary>Builds an entry for a file that has just been hashed.</summary>
    public static ChecksumEntry FromFile(FileInfo file, string md5)
        => new(md5, file.Length, file.LastWriteTimeUtc, file.FullName);
}
