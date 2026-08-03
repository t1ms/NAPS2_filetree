using System.Text;
using System.Text.RegularExpressions;

namespace NAPS2.Ocr;

/// <summary>
/// Helpers for exporting zonal OCR results to CSV and for substituting {FieldName}
/// placeholders in file name patterns.
/// </summary>
public static class ZonalOcrCsv
{
    /// <summary>
    /// Appends one row per result to a CSV log file, writing a header row if the file is new.
    /// </summary>
    public static void AppendRows(string csvPath, IEnumerable<ZonalOcrResult> results, string savedFilePath = "")
    {
        var resultList = results.ToList();
        if (resultList.Count == 0)
        {
            return;
        }
        var fieldNames = resultList
            .SelectMany(r => r.Fields.Select(f => f.Name))
            .Distinct()
            .ToList();
        bool writeHeader = !File.Exists(csvPath) || new FileInfo(csvPath).Length == 0;
        using var writer = new StreamWriter(csvPath, append: true, Encoding.UTF8);
        if (writeHeader)
        {
            writer.WriteLine(ToCsvLine(new[] { "Timestamp", "File", "Page", "Template" }.Concat(fieldNames)));
        }
        foreach (var result in resultList)
        {
            var cells = new List<string>
            {
                result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                savedFilePath,
                result.PageNumber.ToString(),
                result.TemplateName
            };
            cells.AddRange(fieldNames.Select(name =>
                result.Fields.FirstOrDefault(f => f.Name == name)?.Value ?? ""));
            writer.WriteLine(ToCsvLine(cells));
        }
    }

    public static string ToCsvLine(IEnumerable<string> cells)
    {
        return string.Join(",", cells.Select(EscapeCsvCell));
    }

    private static string EscapeCsvCell(string cell)
    {
        if (cell.Contains('"') || cell.Contains(',') || cell.Contains('\n') || cell.Contains('\r'))
        {
            return "\"" + cell.Replace("\"", "\"\"") + "\"";
        }
        return cell;
    }

    /// <summary>
    /// Replaces {FieldName} placeholders in a file path pattern with sanitized extracted values.
    /// </summary>
    public static string SubstituteFields(string path, IEnumerable<ZonalOcrField> fields)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                continue;
            }
            path = Regex.Replace(path, Regex.Escape("{" + field.Name + "}"),
                SanitizeForFileName(field.Value).Replace("$", "$$"), RegexOptions.IgnoreCase);
        }
        return path;
    }

    // Fixed (Windows) set so behavior is consistent across platforms
    private static readonly char[] InvalidFileNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    private static string SanitizeForFileName(string value)
    {
        var sb = new StringBuilder();
        foreach (char c in value)
        {
            sb.Append(InvalidFileNameChars.Contains(c) || char.IsControl(c) ? '_' : c);
        }
        var result = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        return result.Length == 0 ? "blank" : result;
    }
}
