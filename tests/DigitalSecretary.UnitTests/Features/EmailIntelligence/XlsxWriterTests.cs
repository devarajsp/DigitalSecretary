using System.IO.Compression;
using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class XlsxWriterTests
{
    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    public void ColumnName_maps_index_to_excel_column(int index, string expected)
        => XlsxWriter.ColumnName(index).Should().Be(expected);

    [Fact]
    public void Writes_a_valid_multi_sheet_workbook()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "wb.xlsx");
        var sheets = new[]
        {
            new XlsxWriter.Sheet("Contacts", new[] { "Name", "Total" },
                new[] { (IReadOnlyList<string>)new[] { "Alice & co", "3" } }),
            new XlsxWriter.Sheet("Life Data", new[] { "Category" }, Array.Empty<IReadOnlyList<string>>()),
        };

        new XlsxWriter().Write(path, sheets);

        using var zip = ZipFile.OpenRead(path);
        zip.GetEntry("[Content_Types].xml").Should().NotBeNull();
        zip.GetEntry("xl/workbook.xml").Should().NotBeNull();
        zip.GetEntry("xl/worksheets/sheet1.xml").Should().NotBeNull();
        zip.GetEntry("xl/worksheets/sheet2.xml").Should().NotBeNull();

        var sheet1 = Read(zip, "xl/worksheets/sheet1.xml");
        sheet1.Should().Contain("Alice &amp; co");   // text escaped
        sheet1.Should().Contain("<v>3</v>");          // numeric cell, not inline string
    }

    [Fact]
    public void Sanitizes_invalid_sheet_names()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "wb.xlsx");

        new XlsxWriter().Write(path, new[]
        {
            new XlsxWriter.Sheet("A/B:C*", Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>()),
        });

        using var zip = ZipFile.OpenRead(path);
        var workbook = Read(zip, "xl/workbook.xml");
        workbook.Should().Contain("A B C");
        workbook.Should().NotContain("A/B:C*");
    }

    [Fact]
    public void WorkbookExporter_writes_three_sheets_from_a_report()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "EmailIntelligence.xlsx");
        var report = new IntelligenceReport { OwnerAddress = "me@example.com" };
        var person = new Person { Id = "a@example.com", DisplayName = "Alice", TotalMessages = 2 };
        person.Addresses.Add("a@example.com");
        report.People.Add(person);
        report.LifeData.Add(new LifeDataItem("Purchase", default, "shop@example.com", "Order", 10m, "USD", ""));
        report.Documents.Add(new DocumentItem("a.pdf", "PDF", 100, "h", 1, "a@example.com"));

        new WorkbookExporter().Write(report, path);

        using var zip = ZipFile.OpenRead(path);
        zip.GetEntry("xl/worksheets/sheet3.xml").Should().NotBeNull();
        Read(zip, "xl/worksheets/sheet1.xml").Should().Contain("Alice");
    }

    private static string Read(ZipArchive zip, string entry)
    {
        using var reader = new StreamReader(zip.GetEntry(entry)!.Open());
        return reader.ReadToEnd();
    }
}
