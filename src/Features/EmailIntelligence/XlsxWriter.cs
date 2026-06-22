using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Writes a multi-sheet <c>.xlsx</c> workbook with <b>no third-party dependency</b> — a minimal OOXML
/// (SpreadsheetML) package assembled with <see cref="ZipArchive"/>. Numeric-looking cells are written
/// as numbers (so Excel can sort/sum them); everything else is an inline string. Fully offline.
/// </summary>
public sealed class XlsxWriter
{
    public sealed record Sheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

    private static readonly Regex NumberRe = new(@"^-?\d+(\.\d+)?$", RegexOptions.Compiled);
    private static readonly Regex InvalidXmlRe = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", RegexOptions.Compiled);

    public void Write(string path, IReadOnlyList<Sheet> sheets)
    {
        if (sheets.Count == 0)
            sheets = new[] { new Sheet("Sheet1", Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>()) };

        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        WriteEntry(zip, "[Content_Types].xml", ContentTypes(sheets.Count));
        WriteEntry(zip, "_rels/.rels", RootRels());
        WriteEntry(zip, "xl/workbook.xml", Workbook(sheets));
        WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(sheets.Count));
        for (var i = 0; i < sheets.Count; i++)
            WriteEntry(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(sheets[i]));
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ContentTypes(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        for (var i = 0; i < sheetCount; i++)
            sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        sb.Append("</Types>");
        return sb.ToString();
    }

    private static string RootRels() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static string Workbook(IReadOnlyList<Sheet> sheets)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
        sb.Append("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
        for (var i = 0; i < sheets.Count; i++)
            sb.Append($"<sheet name=\"{Escape(SafeSheetName(sheets[i].Name))}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private static string WorkbookRels(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var i = 0; i < sheetCount; i++)
            sb.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string SheetXml(Sheet sheet)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var rowNum = 1;
        if (sheet.Headers.Count > 0)
            AppendRow(sb, rowNum++, sheet.Headers);
        foreach (var row in sheet.Rows)
            AppendRow(sb, rowNum++, row);
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowNum, IReadOnlyList<string> cells)
    {
        sb.Append($"<row r=\"{rowNum}\">");
        for (var c = 0; c < cells.Count; c++)
        {
            var reference = ColumnName(c) + rowNum;
            var value = cells[c] ?? "";
            if (NumberRe.IsMatch(value))
                sb.Append($"<c r=\"{reference}\"><v>{value}</v></c>");
            else
                sb.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(value)}</t></is></c>");
        }
        sb.Append("</row>");
    }

    /// <summary>0-based column index to an Excel column name (0 -> A, 26 -> AA).</summary>
    public static string ColumnName(int index)
    {
        var sb = new StringBuilder();
        var n = index + 1;
        while (n > 0)
        {
            n--;
            sb.Insert(0, (char)('A' + n % 26));
            n /= 26;
        }
        return sb.ToString();
    }

    private static string SafeSheetName(string name)
    {
        // Excel sheet names: max 31 chars, none of : \ / ? * [ ]
        var clean = Regex.Replace(name, @"[:\\/?*\[\]]", " ").Trim();
        if (clean.Length == 0)
            clean = "Sheet";
        return clean.Length > 31 ? clean[..31] : clean;
    }

    private static string Escape(string s)
    {
        s = InvalidXmlRe.Replace(s, "");
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
