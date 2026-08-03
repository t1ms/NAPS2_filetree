using Microsoft.Extensions.Logging;
using NAPS2.Ocr;

namespace NAPS2.Scan.Internal;

internal class LocalPostProcessor : ILocalPostProcessor
{
    private readonly ScanningContext _scanningContext;
    private readonly OcrController _ocrController;

    public LocalPostProcessor(ScanningContext scanningContext, OcrController ocrController)
    {
        _ocrController = ocrController;
        _scanningContext = scanningContext;
    }

    public ProcessedImage PostProcess(ProcessedImage image, ScanOptions options, PostProcessingContext postProcessingContext)
    {
        if (options.AutoRotateOrientation)
        {
            MaybeAutoRotate(ref image, postProcessingContext);
        }
        if (!string.IsNullOrEmpty(options.OcrParams.LanguageCode))
        {
            RunBackgroundOcr(ref image, options, postProcessingContext.TempPath);
        }
        return image;
    }

    private void MaybeAutoRotate(ref ProcessedImage image, PostProcessingContext postProcessingContext)
    {
        try
        {
            if (_scanningContext.OcrEngine is not TesseractOcrEngine engine)
            {
                return;
            }
            string tempPath = _scanningContext.SaveToTempFile(image);
            try
            {
                var result = engine.DetectOrientation(_scanningContext, tempPath).GetAwaiter().GetResult();
                if (result != null && result.RotateDegrees != 0 &&
                    result.OrientationConfidence >= OsdResult.DefaultConfidenceThreshold)
                {
                    image = image.WithTransform(new RotationTransform(result.RotateDegrees), true);
                    if (postProcessingContext.TempPath != null)
                    {
                        // The OCR temp file was saved before rotation; regenerate it so background OCR
                        // runs against the corrected page orientation
                        string oldOcrTempPath = postProcessingContext.TempPath;
                        postProcessingContext.TempPath = _scanningContext.SaveToTempFile(image);
                        try
                        {
                            File.Delete(oldOcrTempPath);
                        }
                        catch (Exception)
                        {
                            // Ignore temp file cleanup errors
                        }
                    }
                }
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
        catch (Exception ex)
        {
            // Orientation detection failures should never block the scan
            _scanningContext.Logger.LogError(ex, "Error running automatic orientation detection");
        }
    }

    private void RunBackgroundOcr(ref ProcessedImage image, ScanOptions options, string? tempPath)
    {
        if (tempPath == null)
        {
            throw new InvalidOperationException("Expected OCR tempPath to be set");
            // TODO: If we ever support a network scan bridge again, we'll want to set this here in that case
            // tempPath = _scanningContext.SaveToTempFile(image, options.BitDepth);
        }
        _ocrController.Start(ref image, tempPath, options.OcrParams, options.OcrPriority).AssertNoAwait();
    }
}