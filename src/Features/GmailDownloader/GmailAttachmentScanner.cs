using MimeKit;

namespace DigitalSecretary.Features.GmailDownloader;

/// <summary>
/// Decides which MIME parts of a message are saved as separate files and how they are named.
/// Separated from the IMAP/IO code so the "save ALL attachments (incl. inline)" rule is testable.
/// </summary>
public static class GmailAttachmentScanner
{
    /// <summary>
    /// Every part worth saving as its own file: standard attachments, inline files such as
    /// embedded images, and attached messages — i.e. everything except the readable body.
    /// </summary>
    public static IEnumerable<MimeEntity> GetAttachmentEntities(MimeMessage message)
    {
        foreach (var part in message.BodyParts)
        {
            if (part is MessagePart)
                yield return part;
            else if (part is MimePart mimePart && IsSaveableAttachment(mimePart))
                yield return part;
        }
    }

    public static bool IsSaveableAttachment(MimePart part)
    {
        if (part.IsAttachment)
            return true;
        if (!string.IsNullOrEmpty(part.FileName))
            return true;

        // Keep the readable text/HTML body out; treat anything else (e.g. an inline image
        // referenced only by Content-Id) as an attachment so nothing is missed.
        return !part.ContentType.IsMimeType("text", "plain")
            && !part.ContentType.IsMimeType("text", "html");
    }

    public static string GetAttachmentName(MimeEntity entity)
    {
        if (entity is MessagePart)
            return "attached-message.eml";

        if (entity is MimePart part)
        {
            if (!string.IsNullOrWhiteSpace(part.FileName))
                return part.FileName;

            var ext = MimeTypes.TryGetExtension(part.ContentType.MimeType, out var e) ? e : ".dat";
            return "inline" + ext;
        }

        return "attachment";
    }
}
