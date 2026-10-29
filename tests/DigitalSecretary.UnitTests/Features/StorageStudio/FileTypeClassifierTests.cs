using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="FileTypeClassifier"/> - the extension-to-category map behind every colour.</summary>
public sealed class FileTypeClassifierTests
{
    [Theory]
    [InlineData("holiday.MP4", FileCategory.Video)]
    [InlineData("IMG_0001.heic", FileCategory.Picture)]
    [InlineData("song.flac", FileCategory.Audio)]
    [InlineData("tax.pdf", FileCategory.Document)]
    [InlineData("backup.7z", FileCategory.Archive)]
    [InlineData("setup.msi", FileCategory.Installer)]
    [InlineData("Program.cs", FileCategory.Code)]
    [InlineData("app.ts", FileCategory.Code)]
    [InlineData("README", FileCategory.Other)]
    [InlineData("weird.xyz123", FileCategory.Other)]
    [InlineData(@"C:\Users\You\Videos\a.mkv", FileCategory.Video)]
    public void Classifies_by_extension_ignoring_case(string name, FileCategory expected)
        => FileTypeClassifier.Classify(name).Should().Be(expected);

    [Fact]
    public void Every_category_has_a_distinct_label()
    {
        var labels = Enum.GetValues<FileCategory>().Select(FileTypeClassifier.Label).ToList();
        labels.Should().OnlyHaveUniqueItems();
        labels.Should().NotContain(string.Empty);
        FileTypeClassifier.CategoryCount.Should().Be(Enum.GetValues<FileCategory>().Length);
    }
}
