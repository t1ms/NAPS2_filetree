namespace NAPS2.Ocr;

/// <summary>
/// A named rectangular zone on a page template, used for zonal OCR field extraction.
/// Coordinates are stored as fractions of the page size (0.0 - 1.0) so they are
/// independent of scan resolution.
/// </summary>
public class OcrZone
{
    public string Name { get; set; } = "";

    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    /// <summary>
    /// Optional per-zone prompt template for LLM field cleanup. "{FieldType}" is replaced with
    /// the zone name. If null/empty, the default prompt template is used.
    /// </summary>
    public string? LlmPrompt { get; set; }
}
