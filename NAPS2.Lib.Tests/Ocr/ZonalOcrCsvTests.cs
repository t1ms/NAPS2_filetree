using NAPS2.Ocr;
using Xunit;

namespace NAPS2.Lib.Tests.Ocr;

public class ZonalOcrCsvTests
{
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
