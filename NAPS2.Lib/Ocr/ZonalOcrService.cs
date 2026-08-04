using System.Threading;
using NAPS2.Scan;
using ZXing;
using ZXing.Common;

namespace NAPS2.Ocr;

/// <summary>
/// Runs zonal OCR: crops each named zone from a page and runs the configured OCR engine
/// on just that region, collecting text per field.
/// </summary>
public class ZonalOcrService
{
    private readonly ScanningContext _scanningContext;
    private readonly Naps2Config _config;
    private readonly ZonalOcrResultsStore _store;
    private readonly LlmFieldNormalizer _llmNormalizer;

    // Bound the number of concurrent page extractions so batch scans don't spawn
    // pages x zones parallel Tesseract processes
    private readonly SemaphoreSlim _extractionSemaphore = new(2, 2);

    public ZonalOcrService(ScanningContext scanningContext, Naps2Config config, ZonalOcrResultsStore store,
        LlmFieldNormalizer llmNormalizer)
    {
        _scanningContext = scanningContext;
        _config = config;
        _store = store;
        _llmNormalizer = llmNormalizer;
    }

    public OcrZoneTemplate? GetActiveTemplate()
    {
        var name = _config.Get(c => c.ActiveOcrZoneTemplateName);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        return _config.Get(c => c.OcrZoneTemplates)
            .FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the stored (or in-progress) result for the image, or extracts fields with the active
    /// template if no result exists yet. Returns null if there is no active template.
    /// </summary>
    public async Task<ZonalOcrResult?> GetOrExtract(ProcessedImage image)
    {
        var existing = await _store.WaitForResult(image);
        if (existing != null)
        {
            return existing;
        }
        var template = GetActiveTemplate();
        if (template == null)
        {
            return null;
        }
        var result = await ExtractFields(image, template, CancellationToken.None);
        if (result != null)
        {
            _store.AddResult(image, result);
        }
        return result;
    }

    public async Task<ZonalOcrResult?> ExtractFields(ProcessedImage image, OcrZoneTemplate template,
        CancellationToken cancelToken)
    {
        var engine = _scanningContext.OcrEngine;
        if (template.Zones.Any(z => z.ExtractionMode == OcrZoneExtractionMode.Text) && engine == null)
        {
            Log.Error("Zonal OCR: no OCR engine is configured.");
            return null;
        }
        if (template.Zones.Count == 0)
        {
            return null;
        }
        var ocrParams = engine == null ? OcrParams.Empty : GetOcrParams();
        await _extractionSemaphore.WaitAsync(cancelToken);
        try
        {
            return await ExtractFieldsInternal(image, template, engine, ocrParams, cancelToken);
        }
        finally
        {
            _extractionSemaphore.Release();
        }
    }

    private async Task<ZonalOcrResult?> ExtractFieldsInternal(ProcessedImage image, OcrZoneTemplate template,
        IOcrEngine? engine, OcrParams ocrParams, CancellationToken cancelToken)
    {
        using var rendered = image.Render();
        int w = rendered.Width;
        int h = rendered.Height;
        var fields = new List<ZonalOcrField>();
        foreach (var zone in template.Zones)
        {
            cancelToken.ThrowIfCancellationRequested();
            string value = "";
            string? extractionError = null;
            int zx = (int) Math.Round(zone.Left.Clamp(0, 1) * w);
            int zy = (int) Math.Round(zone.Top.Clamp(0, 1) * h);
            int zw = (int) Math.Round(zone.Width.Clamp(0, 1) * w);
            int zh = (int) Math.Round(zone.Height.Clamp(0, 1) * h);
            zw = Math.Min(zw, w - zx);
            zh = Math.Min(zh, h - zy);
            if (zw < 4 || zh < 4)
            {
                Log.Debug(
                    $"Zonal OCR: zone \"{zone.Name}\" skipped — effective size {zw}x{zh} px is too small " +
                    $"(raw coords: left={zone.Left:F3} top={zone.Top:F3} w={zone.Width:F3} h={zone.Height:F3}).");
            }
            else
            {
                string tempPath = Path.Combine(_scanningContext.TempFolderPath,
                    Path.GetRandomFileName() + ".png");
                try
                {
                    using (var zoneImage = rendered.Clone().PerformTransform(
                               new CropTransform(zx, w - zx - zw, zy, h - zy - zh, w, h)))
                    {
                        zoneImage.Save(tempPath, ImageFileFormat.Png);
                    }
                    if (zone.ExtractionMode == OcrZoneExtractionMode.Barcode)
                    {
                        using var barcodeImage = _scanningContext.ImageContext.Load(tempPath);
                        var barcode = BarcodeDetector.Detect(barcodeImage, new BarcodeDetectionOptions
                        {
                            DetectBarcodes = true,
                            ZXingOptions = new DecodingOptions
                            {
                                TryHarder = true,
                                PossibleFormats = GetBarcodeFormats(zone.BarcodeFormat)
                            }
                        });
                        value = barcode.DetectedText ?? "";
                    }
                    else
                    {
                        var ocrResult = await engine!.ProcessImage(
                            _scanningContext, tempPath, ocrParams, cancelToken);
                        if (ocrResult != null)
                        {
                            value = string.Join(" ", ocrResult.Lines.Select(l => l.Text.Trim())).Trim();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.ErrorException($"Zonal OCR failed for zone \"{zone.Name}\"", ex);
                    extractionError = ex.Message;
                }
                finally
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception)
                    {
                        // Ignore temp file cleanup errors
                    }
                }
            }
            fields.Add(new ZonalOcrField(zone.Name, value) { ExtractionError = extractionError });
        }
        var notice = await MaybeNormalizeWithLlm(template, fields, cancelToken);
        return new ZonalOcrResult
        {
            PageNumber = _store.NextPageNumber(),
            TemplateName = template.Name,
            Fields = fields,
            Notice = notice
        };
    }

    /// <summary>
    /// If LLM cleanup is enabled, normalizes each field value in place. Returns a user-visible
    /// notice if cleanup was enabled but unavailable (raw OCR text is kept in that case - LLM
    /// issues never block the scan pipeline).
    /// </summary>
    private async Task<string?> MaybeNormalizeWithLlm(OcrZoneTemplate template, List<ZonalOcrField> fields,
        CancellationToken cancelToken)
    {
        if (!_llmNormalizer.IsEnabled)
        {
            return null;
        }
        for (int i = 0; i < fields.Count && i < template.Zones.Count; i++)
        {
            var field = fields[i];
            if (string.IsNullOrWhiteSpace(field.Value) ||
                template.Zones[i].ExtractionMode == OcrZoneExtractionMode.Barcode)
            {
                continue;
            }
            var cleaned = await _llmNormalizer.NormalizeAsync(
                field.Name, template.Zones[i].LlmPrompt, field.Value, cancelToken);
            if (cleaned == null)
            {
                // Model unavailable or failed; keep raw values and stop trying for this page
                return _llmNormalizer.UnavailableReason ?? "AI cleanup unavailable; showing raw OCR text.";
            }
            fields[i] = field with { Value = cleaned };
        }
        return null;
    }

    private static IList<BarcodeFormat>? GetBarcodeFormats(OcrZoneBarcodeFormat format)
    {
        return format switch
        {
            OcrZoneBarcodeFormat.Any => null,
            OcrZoneBarcodeFormat.Code128 => [BarcodeFormat.CODE_128],
            OcrZoneBarcodeFormat.Code39 => [BarcodeFormat.CODE_39],
            OcrZoneBarcodeFormat.Ean13 => [BarcodeFormat.EAN_13],
            OcrZoneBarcodeFormat.Ean8 => [BarcodeFormat.EAN_8],
            OcrZoneBarcodeFormat.UpcA => [BarcodeFormat.UPC_A],
            OcrZoneBarcodeFormat.UpcE => [BarcodeFormat.UPC_E],
            OcrZoneBarcodeFormat.QrCode => [BarcodeFormat.QR_CODE],
            OcrZoneBarcodeFormat.DataMatrix => [BarcodeFormat.DATA_MATRIX],
            OcrZoneBarcodeFormat.Pdf417 => [BarcodeFormat.PDF_417],
            _ => null
        };
    }

    private OcrParams GetOcrParams()
    {
        var ocrParams = _config.DefaultOcrParams();
        if (!string.IsNullOrEmpty(ocrParams.LanguageCode))
        {
            return ocrParams;
        }
        // OCR for searchable PDFs may be disabled; zonal OCR still needs a language
        var lang = _config.Get(c => c.OcrLanguageCode);
        if (string.IsNullOrEmpty(lang))
        {
            lang = "eng";
        }
        var timeout = _config.Get(c => c.OcrTimeoutInSeconds);
        return new OcrParams(lang, OcrMode.Fast, timeout > 0 ? timeout : 120);
    }
}
