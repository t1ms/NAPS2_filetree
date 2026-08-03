using System.Threading;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Desktop;

public class DesktopImagesController
{
    private readonly UiImageList _imageList;
    private readonly ZonalOcrService _zonalOcrService;
    private readonly ZonalOcrResultsStore _zonalOcrResultsStore;

    public DesktopImagesController(UiImageList imageList, ZonalOcrService zonalOcrService,
        ZonalOcrResultsStore zonalOcrResultsStore)
    {
        _imageList = imageList;
        _zonalOcrService = zonalOcrService;
        _zonalOcrResultsStore = zonalOcrResultsStore;
    }

    /// <summary>
    /// Constructs a receiver for scanned images.
    /// This keeps images from the same source together, even if multiple sources are providing images at the same time.
    /// </summary>
    /// <returns></returns>
    public Action<ProcessedImage> ReceiveScannedImage()
    {
        var lockObj = new object();
        UiImage? last = null;
        return scannedImage =>
        {
            lock (lockObj)
            {
                MaybeRunZonalOcr(scannedImage);
                var uiImage = new UiImage(scannedImage);
                _imageList.Mutate(new ImageListMutation.InsertAfter(uiImage, last), isPassiveInteraction: true);
                last = uiImage;
            }
        };
    }

    public void AppendImageBatch(IEnumerable<ProcessedImage> images)
    {
        var imageList = images.ToList();
        foreach (var image in imageList)
        {
            MaybeRunZonalOcr(image);
        }
        _imageList.Mutate(
            new ImageListMutation.Append(imageList.Select(image => new UiImage(image))),
            isPassiveInteraction: true);
    }

    private void MaybeRunZonalOcr(ProcessedImage image)
    {
        var template = _zonalOcrService.GetActiveTemplate();
        if (template == null || _zonalOcrResultsStore.HasResultOrPending(image))
        {
            return;
        }
        var clone = image.Clone();
        var task = Task.Run(async () =>
        {
            try
            {
                var result = await _zonalOcrService.ExtractFields(clone, template, CancellationToken.None);
                if (result != null)
                {
                    _zonalOcrResultsStore.AddResult(clone, result);
                }
                return result;
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error running zonal OCR after scanning", ex);
                return null;
            }
            finally
            {
                clone.Dispose();
            }
        });
        _zonalOcrResultsStore.RegisterPending(clone, task);
    }
}
