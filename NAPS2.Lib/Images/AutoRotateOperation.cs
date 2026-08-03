using NAPS2.Ocr;
using NAPS2.Scan;

namespace NAPS2.Images;

/// <summary>
/// Batch operation that detects text orientation on the selected pages (via Tesseract OSD) and rotates them
/// upright. Pages where detection fails or confidence is low are left unchanged.
/// </summary>
public class AutoRotateOperation : OperationBase
{
    private readonly ScanningContext _scanningContext;

    public AutoRotateOperation(ScanningContext scanningContext)
    {
        _scanningContext = scanningContext;
        AllowCancel = true;
        AllowBackground = true;
    }

    public bool Start(UiImageList imageList, List<UiImage> images, AutoRotateParams autoRotateParams)
    {
        ProgressTitle = "Auto Rotate";
        Status = new OperationStatus
        {
            StatusText = "Detecting page orientation...",
            MaxProgress = images.Count
        };

        if (_scanningContext.OcrEngine is not TesseractOcrEngine engine)
        {
            return false;
        }

        RunAsync(async () =>
        {
            var changedImages = new List<UiImage>();
            var beforeTransforms = new List<TransformState>();
            var afterTransforms = new List<TransformState>();
            var result = await Pipeline.For(images, CancelToken).StepParallel(img =>
            {
                using var processedImage = img.GetClonedImage();
                var image = processedImage.Render();
                string? tempPath = null;
                try
                {
                    CancelToken.ThrowIfCancellationRequested();
                    tempPath = Path.Combine(_scanningContext.TempFolderPath, Path.GetRandomFileName() + ".jpg");
                    image.Save(tempPath);
                    var osdResult = engine.DetectOrientation(_scanningContext, tempPath, CancelToken)
                        .GetAwaiter().GetResult();
                    CancelToken.ThrowIfCancellationRequested();
                    (UiImage, TransformState, TransformState)? transformResult = null;
                    if (osdResult != null && osdResult.RotateDegrees != 0 &&
                        osdResult.OrientationConfidence >= OsdResult.DefaultConfidenceThreshold)
                    {
                        var transform = new RotationTransform(osdResult.RotateDegrees);
                        var rotated = image.PerformTransform(transform);
                        try
                        {
                            var thumbnail = autoRotateParams.ThumbnailSize.HasValue
                                ? rotated.PerformTransform(new ThumbnailTransform(autoRotateParams.ThumbnailSize.Value))
                                : null;
                            var before = img.TransformState;
                            img.AddTransform(transform, thumbnail);
                            var after = img.TransformState;
                            transformResult = (img, before, after);
                        }
                        finally
                        {
                            rotated.Dispose();
                        }
                    }
                    lock (this)
                    {
                        Status.CurrentProgress += 1;
                    }
                    InvokeStatusChanged();
                    return transformResult;
                }
                finally
                {
                    image.Dispose();
                    if (tempPath != null)
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
            }).Run(transformState =>
            {
                if (transformState != null)
                {
                    changedImages.Add(transformState.Value.Item1);
                    beforeTransforms.Add(transformState.Value.Item2);
                    afterTransforms.Add(transformState.Value.Item3);
                }
            });
            if (changedImages.Count > 0)
            {
                imageList.PushUndoElement(
                    new TransformImagesUndoElement(changedImages, beforeTransforms, afterTransforms));
            }
            return result;
        });

        return true;
    }
}
