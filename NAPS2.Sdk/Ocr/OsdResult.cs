namespace NAPS2.Ocr;

/// <summary>
/// The result of Tesseract orientation & script detection (OSD).
/// </summary>
/// <param name="RotateDegrees">The clockwise rotation (0/90/180/270) needed to make the page text upright.</param>
/// <param name="OrientationConfidence">Tesseract's confidence in the detected orientation (higher is better).</param>
public record OsdResult(int RotateDegrees, double OrientationConfidence)
{
    /// <summary>
    /// The default minimum orientation confidence to act on a detected rotation. Below this, pages are left as-is
    /// to avoid incorrectly rotating sparse or ambiguous pages.
    /// </summary>
    public const double DefaultConfidenceThreshold = 2.0;
}
