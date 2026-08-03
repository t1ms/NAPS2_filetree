using NAPS2.Ocr;
using Xunit;

namespace NAPS2.Lib.Tests.Ocr;

public class ZonalOcrCsvTests
{
    [Fact]
    public void SubstituteFields_ReplacesPlaceholders()
    {
        var fields = new List<ZonalOcrField>
        {
            new("Invoice Number", "INV-1234"),
            new("Total", "$56.78")
        };
        var result = ZonalOcrCsv.SubstituteFields(@"C:\Scans\{Invoice Number}_{Total}.pdf", fields);
        Assert.Equal(@"C:\Scans\INV-1234_$56.78.pdf", result);
    }

    [Fact]
    public void SubstituteFields_IsCaseInsensitive()
    {
        var fields = new List<ZonalOcrField> { new("Date", "2026-08-03") };
        Assert.Equal("scan_2026-08-03.pdf", ZonalOcrCsv.SubstituteFields("scan_{date}.pdf", fields));
    }

    [Fact]
    public void SubstituteFields_SanitizesInvalidChars()
    {
        var fields = new List<ZonalOcrField> { new("Total", "12/34: 56") };
        var result = ZonalOcrCsv.SubstituteFields("{Total}.pdf", fields);
        Assert.DoesNotContain('/', result.Replace(".pdf", ""));
        Assert.DoesNotContain(':', result);
    }

    [Fact]
    public void SubstituteFields_EmptyValueBecomesBlank()
    {
        var fields = new List<ZonalOcrField> { new("Total", "  ") };
        Assert.Equal("blank.pdf", ZonalOcrCsv.SubstituteFields("{Total}.pdf", fields));
    }

    [Fact]
    public void SubstituteFields_LeavesUnknownPlaceholders()
    {
        var fields = new List<ZonalOcrField> { new("Total", "5") };
        Assert.Equal("{Other}_5.pdf", ZonalOcrCsv.SubstituteFields("{Other}_{Total}.pdf", fields));
    }

    [Fact]
    public void AppendRows_WritesHeaderAndRows()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".csv");
        try
        {
            var results = new List<ZonalOcrResult>
            {
                new()
                {
                    PageNumber = 1,
                    TemplateName = "Invoices",
                    Fields = new List<ZonalOcrField>
                    {
                        new("Invoice Number", "INV-1"),
                        new("Total", "1,000.00")
                    }
                }
            };
            ZonalOcrCsv.AppendRows(path, results, "/tmp/scan1.pdf");
            ZonalOcrCsv.AppendRows(path, results, "/tmp/scan2.pdf");

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("Timestamp,File,Page,Template,Invoice Number,Total", lines[0]);
            Assert.Contains("/tmp/scan1.pdf", lines[1]);
            Assert.Contains("\"1,000.00\"", lines[1]);
            Assert.Contains("/tmp/scan2.pdf", lines[2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ToCsvLine_EscapesQuotes()
    {
        Assert.Equal("a,\"b\"\"c\"\"\",\"d,e\"", ZonalOcrCsv.ToCsvLine(new[] { "a", "b\"c\"", "d,e" }));
    }
}
