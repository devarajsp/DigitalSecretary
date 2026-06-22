using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using DriveData = Google.Apis.Drive.v3.Data;

namespace DigitalSecretary.Features.GoogleDriveDownloader;

/// <summary>
/// Downloads a local COPY of a user's Google Drive over the Drive API (read-only scope), mirroring
/// the Drive folder tree on disk. Regular files are downloaded as-is; Google-native files
/// (Docs/Sheets/Slides/Drawings) are <b>exported</b> to both an Office format and PDF. Authentication
/// is the OAuth 2.0 installed-app flow: the user authorises once in a browser and the token is cached.
/// </summary>
public sealed class GoogleDriveDownloader
{
    private const string ApplicationName = "DigitalSecretary";

    public async Task RunAsync(
        string credentialsPath,
        string tokenDir,
        string rootDir,
        IProgress<int> percent,
        IProgress<string> log,
        CancellationToken ct)
    {
        Directory.CreateDirectory(rootDir);

        log.Report("Signing in to Google (a browser window may open the first time)…");
        using var service = await CreateServiceAsync(credentialsPath, tokenDir, ct).ConfigureAwait(false);

        log.Report("Listing your Drive files…");
        var all = await ListAllAsync(service, log, ct).ConfigureAwait(false);

        var folders = all
            .Where(f => GoogleExportFormats.IsFolder(f.MimeType))
            .GroupBy(f => f.Id, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new DriveFolderNode(g.Key, g.First().Name ?? g.Key, g.First().Parents?.FirstOrDefault()),
                StringComparer.Ordinal);

        var files = all.Where(f => !GoogleExportFormats.IsFolder(f.MimeType)).ToList();
        log.Report($"Found {files.Count} file(s) in {folders.Count} folder(s).");

        long processed = 0;
        int savedFiles = 0, exportedFiles = 0, skipped = 0, errors = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var segments = DriveFileNaming.ResolveFolderPath(folders, file.Parents?.FirstOrDefault());
            var localDir = segments.Aggregate(rootDir, Path.Combine);
            var baseName = DriveFileNaming.Sanitize(file.Name ?? file.Id);

            try
            {
                Directory.CreateDirectory(localDir);

                if (GoogleExportFormats.IsGoogleNative(file.MimeType))
                {
                    var targets = GoogleExportFormats.ExportTargetsFor(file.MimeType);
                    if (targets.Count == 0)
                    {
                        skipped++;
                        log.Report($"– Skipped '{file.Name}' ({file.MimeType}) — not exportable.");
                    }
                    else
                    {
                        foreach (var target in targets)
                        {
                            ct.ThrowIfCancellationRequested();
                            var path = DriveFileNaming.GetUniquePath(localDir, baseName + target.Extension);
                            await ExportAsync(service, file.Id, target.MimeType, path, ct).ConfigureAwait(false);
                        }
                        exportedFiles++;
                    }
                }
                else
                {
                    var path = DriveFileNaming.GetUniquePath(localDir, baseName);
                    await DownloadAsync(service, file.Id, path, ct).ConfigureAwait(false);
                    savedFiles++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                log.Report($"! Error with '{file.Name}': {ex.Message}");
            }
            finally
            {
                processed++;
                if (files.Count > 0)
                    percent.Report((int)(processed * 100 / files.Count));
            }
        }

        percent.Report(100);
        log.Report("──────────────────────────────");
        log.Report($"Done. Downloaded {savedFiles} file(s) and exported {exportedFiles} Google document(s) " +
                   $"(as Office + PDF). Skipped {skipped} non-exportable item(s).");
        log.Report($"Location: {rootDir}");
        if (errors > 0)
            log.Report($"Completed with {errors} error(s) — see the messages marked '!' above.");
    }

    private static async Task<DriveService> CreateServiceAsync(string credentialsPath, string tokenDir, CancellationToken ct)
    {
        if (!File.Exists(credentialsPath))
            throw new InvalidOperationException(
                "OAuth client credentials file not found. In Google Cloud Console create an OAuth " +
                "client ID of type \"Desktop app\", download its JSON, and select it here.");

        GoogleClientSecrets secrets;
        using (var stream = File.OpenRead(credentialsPath))
            secrets = await GoogleClientSecrets.FromStreamAsync(stream, ct).ConfigureAwait(false);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets.Secrets,
            new[] { DriveService.Scope.DriveReadonly },
            "user",
            ct,
            new FileDataStore(tokenDir, fullPath: true)).ConfigureAwait(false);

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });
    }

    private static async Task<List<DriveData.File>> ListAllAsync(DriveService service, IProgress<string> log, CancellationToken ct)
    {
        var all = new List<DriveData.File>();
        string? pageToken = null;
        do
        {
            ct.ThrowIfCancellationRequested();
            var request = service.Files.List();
            request.Q = "trashed = false";
            request.Spaces = "drive";
            request.Fields = "nextPageToken, files(id, name, mimeType, parents)";
            request.PageSize = 1000;
            request.PageToken = pageToken;

            var result = await request.ExecuteAsync(ct).ConfigureAwait(false);
            if (result.Files is { Count: > 0 })
                all.AddRange(result.Files);
            pageToken = result.NextPageToken;
            if (pageToken is not null)
                log.Report($"Listing… {all.Count} item(s) so far.");
        }
        while (!string.IsNullOrEmpty(pageToken));

        return all;
    }

    private static async Task ExportAsync(DriveService service, string fileId, string exportMimeType, string path, CancellationToken ct)
    {
        var request = service.Files.Export(fileId, exportMimeType);
        await SaveDownloadAsync(request.DownloadAsync, path, ct).ConfigureAwait(false);
    }

    private static async Task DownloadAsync(DriveService service, string fileId, string path, CancellationToken ct)
    {
        var request = service.Files.Get(fileId);
        await SaveDownloadAsync(request.DownloadAsync, path, ct).ConfigureAwait(false);
    }

    private static async Task SaveDownloadAsync(
        Func<Stream, CancellationToken, Task<IDownloadProgress>> download, string path, CancellationToken ct)
    {
        IDownloadProgress progress;
        using (var fs = File.Create(path))
            progress = await download(fs, ct).ConfigureAwait(false);

        if (progress.Status != DownloadStatus.Completed)
        {
            TryDelete(path);
            throw progress.Exception ?? new InvalidOperationException("Download did not complete.");
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup of a partial file */ }
    }
}
