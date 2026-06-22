using System.Text;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;

namespace DigitalSecretary.Features.GmailDownloader;

/// <summary>
/// Downloads a read-only COPY of every message (and its attachments) from all Gmail IMAP
/// folders/labels, mirroring the remote structure on disk. Messages are never deleted, moved,
/// or marked read on the server (folders are opened with EXAMINE / read-only access).
/// Each email is saved twice — as readable .txt and as raw .eml — plus all attachments.
/// </summary>
public sealed class GmailDownloader
{
    private const string Host = "imap.gmail.com";
    private const int Port = 993;

    public async Task RunAsync(
        string email,
        string appPassword,
        string rootDir,
        IProgress<int> percent,
        IProgress<string> log,
        CancellationToken ct)
    {
        Directory.CreateDirectory(rootDir);

        using var client = new ImapClient();

        log.Report($"Connecting to {Host}:{Port} over SSL…");
        await client.ConnectAsync(Host, Port, SecureSocketOptions.SslOnConnect, ct).ConfigureAwait(false);

        log.Report("Authenticating…");
        try
        {
            await client.AuthenticateAsync(email, appPassword, ct).ConfigureAwait(false);
        }
        catch (AuthenticationException)
        {
            throw new InvalidOperationException(
                "Login failed. Gmail requires an APP PASSWORD for IMAP access — your normal " +
                "account password will not work. Turn on 2-Step Verification, then create one at: " +
                "Google Account → Security → \"App passwords\", and paste it here. Also make sure " +
                "IMAP is enabled in Gmail Settings → Forwarding and POP/IMAP.");
        }

        log.Report("Connected. Discovering folders/labels…");
        var folders = await CollectSelectableFoldersAsync(client, ct).ConfigureAwait(false);
        log.Report($"Found {folders.Count} folder(s).");
        log.Report("Note: Gmail labels can place one message under several folders (e.g. Inbox and " +
                   "All Mail), so some messages may be saved more than once.");

        long total = 0;
        foreach (var folder in folders)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await folder.StatusAsync(StatusItems.Count, ct).ConfigureAwait(false);
                total += folder.Count;
            }
            catch (Exception ex)
            {
                log.Report($"! Could not read count for '{folder.FullName}': {ex.Message}");
            }
        }
        log.Report($"Total messages to download: {total}");

        long processed = 0;
        int savedEmails = 0, savedAttachments = 0, errors = 0;

        foreach (var folder in folders)
        {
            ct.ThrowIfCancellationRequested();

            var localFolder = BuildLocalFolderPath(rootDir, folder);

            int count;
            try
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, ct).ConfigureAwait(false);
                count = folder.Count;
            }
            catch (Exception ex)
            {
                errors++;
                log.Report($"! Skipping folder '{folder.FullName}': {ex.Message}");
                continue;
            }

            Directory.CreateDirectory(localFolder);
            log.Report($"Folder '{folder.FullName}' — {count} message(s)");

            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var message = await folder.GetMessageAsync(i, ct).ConfigureAwait(false);
                    SaveMessage(message, localFolder, out int attachmentCount);
                    savedEmails++;
                    savedAttachments += attachmentCount;
                }
                catch (Exception ex)
                {
                    errors++;
                    log.Report($"! Error in '{folder.FullName}' message #{i + 1}: {ex.Message}");
                }
                finally
                {
                    processed++;
                    if (total > 0)
                        percent.Report((int)(processed * 100 / total));
                }
            }
        }

        await client.DisconnectAsync(true, ct).ConfigureAwait(false);

        percent.Report(100);
        log.Report("──────────────────────────────");
        log.Report($"Done. Saved {savedEmails} email(s) as .txt + .eml and {savedAttachments} attachment(s).");
        log.Report($"Location: {rootDir}");
        if (errors > 0)
            log.Report($"Completed with {errors} error(s) — see the messages marked '!' above.");
    }

    private static async Task<List<IMailFolder>> CollectSelectableFoldersAsync(ImapClient client, CancellationToken ct)
    {
        var all = new List<IMailFolder>();

        foreach (var ns in client.PersonalNamespaces)
        {
            var root = client.GetFolder(ns);
            await CollectRecursiveAsync(root, all, ct).ConfigureAwait(false);
        }

        if (!all.Any(f => f.FullName.Equals(client.Inbox.FullName, StringComparison.OrdinalIgnoreCase)))
            all.Insert(0, client.Inbox);

        return all
            .Where(f => !f.Attributes.HasFlag(FolderAttributes.NoSelect))
            .GroupBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static async Task CollectRecursiveAsync(IMailFolder folder, List<IMailFolder> acc, CancellationToken ct)
    {
        foreach (var sub in await folder.GetSubfoldersAsync(false, ct).ConfigureAwait(false))
        {
            acc.Add(sub);
            if (sub.Attributes.HasFlag(FolderAttributes.HasChildren))
                await CollectRecursiveAsync(sub, acc, ct).ConfigureAwait(false);
        }
    }

    private static string BuildLocalFolderPath(string root, IMailFolder folder)
    {
        if (string.IsNullOrEmpty(folder.FullName))
            return root;

        var delimiter = folder.DirectorySeparator == '\0' ? '/' : folder.DirectorySeparator;
        var path = root;
        foreach (var segment in folder.FullName.Split(delimiter, StringSplitOptions.RemoveEmptyEntries))
            path = Path.Combine(path, GmailFileNaming.Sanitize(segment));
        return path;
    }

    private static void SaveMessage(MimeMessage message, string dir, out int attachmentCount)
    {
        // Collect ALL attachment-like parts: real attachments, inline files (e.g. embedded
        // images), and attached messages — not just those flagged Content-Disposition: attachment.
        var attachments = GmailAttachmentScanner.GetAttachmentEntities(message).ToList();
        attachmentCount = 0;

        var baseName = GmailFileNaming.BaseNameFor(message.Date, message.Subject);

        // Save the email in BOTH formats: a readable .txt and the full-fidelity raw .eml.
        var txtPath = GmailFileNaming.GetUniquePath(dir, baseName + ".txt");
        var actualBase = Path.GetFileNameWithoutExtension(txtPath);
        File.WriteAllText(txtPath, BuildText(message, attachments), Encoding.UTF8);

        var emlPath = GmailFileNaming.GetUniquePath(dir, actualBase + ".eml");
        using (var emlStream = File.Create(emlPath))
            message.WriteTo(emlStream);

        foreach (var attachment in attachments)
        {
            var attachmentName = GmailFileNaming.Sanitize(GmailAttachmentScanner.GetAttachmentName(attachment));
            var attachmentPath = GmailFileNaming.GetUniquePath(dir, $"{actualBase} - {attachmentName}");
            SaveAttachment(attachment, attachmentPath);
            attachmentCount++;
        }
    }

    private static string BuildText(MimeMessage m, List<MimeEntity> attachments)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"From:    {m.From}");
        sb.AppendLine($"To:      {m.To}");
        if (m.Cc.Count > 0)
            sb.AppendLine($"Cc:      {m.Cc}");
        sb.AppendLine($"Date:    {m.Date}");
        sb.AppendLine($"Subject: {m.Subject}");

        if (attachments.Count > 0)
            sb.AppendLine($"Attachments: {string.Join(", ", attachments.Select(GmailAttachmentScanner.GetAttachmentName))}");

        sb.AppendLine(new string('=', 70));
        sb.AppendLine();

        var body = m.TextBody;
        if (string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(m.HtmlBody))
            body = StripHtml(m.HtmlBody);

        sb.AppendLine(string.IsNullOrEmpty(body) ? "(no text content)" : body);
        return sb.ToString();
    }

    private static void SaveAttachment(MimeEntity entity, string path)
    {
        using var stream = File.Create(path);
        if (entity is MessagePart { Message: { } innerMessage })
            innerMessage.WriteTo(stream);
        else if (entity is MimePart { Content: { } content })
            content.DecodeTo(stream);
    }

    private static string StripHtml(string html)
    {
        var noTags = Regex.Replace(html, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(noTags).Trim();
    }
}
