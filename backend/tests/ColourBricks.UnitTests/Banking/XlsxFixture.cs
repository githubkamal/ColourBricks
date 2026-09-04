using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace ColourBricks.UnitTests.Banking;

/// <summary>
/// Builds a minimal but valid single-sheet .xlsx in memory so the parser tests do
/// not need a checked-in binary. Every cell is written as an inline string; the
/// parser's date/amount handling turns the text back into typed values.
/// </summary>
internal static class XlsxFixture
{
    public static MemoryStream Build(IEnumerable<string[]> rows)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);

            Write(zip, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            Write(zip, "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);

            Write(zip, "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            var sheet = new StringBuilder();
            sheet.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sheet.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
            int rowNum = 1;
            foreach (string[] cells in rows)
            {
                sheet.Append($"<row r=\"{rowNum}\">");
                for (int c = 0; c < cells.Length; c++)
                {
                    string reference = $"{ColumnName(c)}{rowNum}";
                    sheet.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(cells[c])}</t></is></c>");
                }

                sheet.Append("</row>");
                rowNum++;
            }

            sheet.Append("</sheetData></worksheet>");
            Write(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }

        ms.Position = 0;
        return ms;
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    private static string Escape(string s) => new XmlDocument().CreateTextNode(s).OuterXml;

    private static string ColumnName(int zeroBased)
    {
        string name = "";
        int n = zeroBased;
        do
        {
            name = (char)('A' + (n % 26)) + name;
            n = (n / 26) - 1;
        }
        while (n >= 0);

        return name;
    }
}
