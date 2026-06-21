using System.Text;
using DigitalSecretary.Features.EmailDownloader;
using FluentAssertions;
using MimeKit;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class AttachmentScannerTests
{
    // Built explicitly (not via BodyBuilder) so the inline image is deterministically present.
    private static MimeMessage BuildMessage()
    {
        var multipart = new Multipart("mixed")
        {
            new TextPart("plain") { Text = "hello body" }
        };

        // A normal file attachment.
        multipart.Add(new MimePart("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 fake"))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            FileName = "report.pdf"
        });

        // An inline image referenced only by Content-Id (no filename) — the kind the old code missed.
        multipart.Add(new MimePart("image", "png")
        {
            Content = new MimeContent(new MemoryStream(new byte[] { 1, 2, 3 })),
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
            ContentId = "logo"
        });

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "to@example.com"));
        message.Subject = "test";
        message.Body = multipart;
        return message;
    }

    [Fact]
    public void Captures_both_the_file_attachment_and_the_inline_image()
    {
        var message = BuildMessage();

        var saved = AttachmentScanner.GetAttachmentEntities(message).ToList();

        // Two attachment-like parts; the text body is NOT among them.
        saved.Should().HaveCount(2);
        saved.OfType<MimePart>()
            .Any(p => p.ContentType.IsMimeType("text", "plain") && string.IsNullOrEmpty(p.FileName))
            .Should().BeFalse("the readable text body must not be saved as an attachment");
    }

    [Fact]
    public void Named_attachment_keeps_its_filename()
    {
        var message = BuildMessage();
        var pdf = AttachmentScanner.GetAttachmentEntities(message)
            .OfType<MimePart>()
            .First(p => p.ContentType.IsMimeType("application", "pdf"));

        AttachmentScanner.GetAttachmentName(pdf).Should().Be("report.pdf");
    }

    [Fact]
    public void Inline_part_without_filename_gets_synthesised_name()
    {
        var message = BuildMessage();
        var image = AttachmentScanner.GetAttachmentEntities(message)
            .OfType<MimePart>()
            .First(p => p.ContentType.IsMimeType("image", "png"));

        AttachmentScanner.GetAttachmentName(image).Should().Be("inline.png");
    }

    [Fact]
    public void Plain_text_body_is_not_a_saveable_attachment()
    {
        var part = new TextPart("plain") { Text = "body" };

        AttachmentScanner.IsSaveableAttachment(part).Should().BeFalse();
    }
}
