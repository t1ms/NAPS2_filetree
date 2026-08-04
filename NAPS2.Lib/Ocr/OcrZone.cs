namespace NAPS2.Ocr;

public enum OcrZoneExtractionMode
{
    Text,
    Barcode
}

public enum OcrZoneBarcodeFormat
{
    Any,
    Code128,
    Code39,
    Ean13,
    Ean8,
    UpcA,
    UpcE,
    QrCode,
    DataMatrix,
    Pdf417
}

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

    /// <summary>
    /// Controls whether this zone is read as printed text or as a barcode. This defaults to text
    /// so templates saved before barcode zones were added continue to work unchanged.
    /// </summary>
    public OcrZoneExtractionMode ExtractionMode { get; set; } = OcrZoneExtractionMode.Text;

    /// <summary>
    /// The barcode format to use when this zone extracts a barcode. Any allows all supported
    /// formats to be tried.
    /// </summary>
    public OcrZoneBarcodeFormat BarcodeFormat { get; set; } = OcrZoneBarcodeFormat.Any;
}
