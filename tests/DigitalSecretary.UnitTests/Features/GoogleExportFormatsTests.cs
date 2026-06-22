using DigitalSecretary.Features.GoogleDriveDownloader;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class GoogleExportFormatsTests
{
    [Theory]
    [InlineData("application/vnd.google-apps.document", true)]
    [InlineData("application/vnd.google-apps.folder", true)]
    [InlineData("application/pdf", false)]
    [InlineData("image/png", false)]
    [InlineData(null, false)]
    public void IsGoogleNative_detects_google_apps_types(string? mime, bool expected)
        => GoogleExportFormats.IsGoogleNative(mime).Should().Be(expected);

    [Theory]
    [InlineData("application/vnd.google-apps.folder", true)]
    [InlineData("application/vnd.google-apps.document", false)]
    [InlineData("application/pdf", false)]
    public void IsFolder_only_true_for_folder_type(string mime, bool expected)
        => GoogleExportFormats.IsFolder(mime).Should().Be(expected);

    [Theory]
    [InlineData("application/vnd.google-apps.document", ".docx", ".pdf")]
    [InlineData("application/vnd.google-apps.spreadsheet", ".xlsx", ".pdf")]
    [InlineData("application/vnd.google-apps.presentation", ".pptx", ".pdf")]
    [InlineData("application/vnd.google-apps.drawing", ".png", ".pdf")]
    public void Native_docs_export_to_office_and_pdf(string mime, string office, string pdf)
    {
        var targets = GoogleExportFormats.ExportTargetsFor(mime);

        targets.Should().HaveCount(2);
        targets.Select(t => t.Extension).Should().ContainInOrder(office, pdf);
        targets.Should().Contain(t => t.MimeType == "application/pdf");
    }

    [Fact]
    public void Office_export_uses_open_xml_mime_types()
    {
        var docx = GoogleExportFormats.ExportTargetsFor("application/vnd.google-apps.document")[0];

        docx.Extension.Should().Be(".docx");
        docx.MimeType.Should().Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    [Theory]
    [InlineData("application/vnd.google-apps.form")]
    [InlineData("application/vnd.google-apps.shortcut")]
    [InlineData("application/pdf")]
    public void Non_exportable_or_regular_types_have_no_export_targets(string mime)
        => GoogleExportFormats.ExportTargetsFor(mime).Should().BeEmpty();
}
