using System.Collections.Immutable;

namespace NAPS2.Ocr;

/// <summary>
/// A named template of OCR zones (e.g. "Invoices" with zones "Invoice Number", "Date", "Total").
/// </summary>
public class OcrZoneTemplate
{
    public string Name { get; set; } = "";

    public ImmutableList<OcrZone> Zones { get; set; } = ImmutableList<OcrZone>.Empty;
}
