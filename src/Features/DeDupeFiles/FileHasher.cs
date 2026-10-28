using System.Buffers;
using System.Security.Cryptography;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>Computes a content fingerprint for a file. Abstracted so the engines can be unit-tested.</summary>
public interface IFileHasher
{
    /// <summary>Returns the file's checksum, or <c>null</c> if the work was cancelled part-way.</summary>
    string? Compute(string path, CancellationToken cancel);
}

/// <summary>
/// Streaming MD5 hasher. MD5 is used here purely as a fast <b>content fingerprint</b> for finding
/// identical files - never for security - and every deletion is additionally verified byte-for-byte
/// against the copy being kept (see <see cref="FileComparer"/>), so a hash collision cannot cause
/// data loss. Files are read in 1 MB blocks, so file size is not a memory concern and a long hash
/// can be cancelled between blocks.
/// </summary>
public sealed class Md5FileHasher : IFileHasher
{
    private const int BufferSize = 1024 * 1024;

    public string? Compute(string path, CancellationToken cancel)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            BufferSize, FileOptions.SequentialScan);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, BufferSize)) > 0)
            {
                if (cancel.IsCancellationRequested)
                {
                    return null;
                }
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return HexText.FromBytes(hash.GetHashAndReset());
    }
}
