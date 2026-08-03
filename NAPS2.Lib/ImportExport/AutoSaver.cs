using NAPS2.EtoForms;
using NAPS2.EtoForms.Notifications;
using NAPS2.ImportExport.Images;
using NAPS2.Ocr;
using NAPS2.Pdf;
using NAPS2.Scan;
using NAPS2.Search;
using System.Threading;

namespace NAPS2.ImportExport;

public class AutoSaver
{
    private readonly ErrorOutput _errorOutput;
    private readonly DialogHelper _dialogHelper;
    private readonly OperationProgress _operationProgress;
    private readonly ISaveNotify _notify;
    private readonly PdfExporter _pdfExporter;
    private readonly IOverwritePrompt _overwritePrompt;
    private readonly Naps2Config _config;
    private readonly ImageContext _imageContext;
    private readonly UiImageList _imageList;
    private readonly ZonalOcrService _zonalOcrService;
    private readonly SearchIndexService _searchIndexService;
    private readonly LlmDocumentExtractor _llmDocumentExtractor;

    public AutoSaver(ErrorOutput errorOutput, DialogHelper dialogHelper,
        OperationProgress operationProgress, ISaveNotify notify, PdfExporter pdfExporter,
        IOverwritePrompt overwritePrompt, Naps2Config config, ImageContext imageContext, UiImageList imageList,
        ZonalOcrService zonalOcrService, SearchIndexService searchIndexService,
        LlmDocumentExtractor llmDocumentExtractor)
    {
        _searchIndexService = searchIndexService;
        _llmDocumentExtractor = llmDocumentExtractor;
        _errorOutput = errorOutput;
        _dialogHelper = dialogHelper;
        _operationProgress = operationProgress;
        _notify = notify;
        _pdfExporter = pdfExporter;
        _overwritePrompt = overwritePrompt;
        _config = config;
        _imageContext = imageContext;
        _imageList = imageList;
        _zonalOcrService = zonalOcrService;
    }

    public IAsyncEnumerable<ProcessedImage> Save(AutoSaveSettings settings, IAsyncEnumerable<ProcessedImage> images)
    {
        return AsyncProducers.RunProducer<ProcessedImage>(async produceImage =>
        {
            var imageList = new List<ProcessedImage>();
            try
            {
                await foreach (var img in images)
                {
                    imageList.Add(img);
                    if (!settings.ClearImagesAfterSaving)
                    {
                        produceImage(img.Clone());
                    }
                }
            }
            finally
            {
                if (!await InternalSave(settings, imageList) && settings.ClearImagesAfterSaving)
                {
                    // Fallback in case auto save failed; pipe all the images back at once
                    foreach (var img in imageList)
                    {
                        produceImage(img);
                    }
                }
                else
                {
                    foreach (var img in imageList)
                    {
                        img.Dispose();
                    }
                }
            }
        });
    }

    public async Task<bool> SaveForHotFolder(AutoSaveSettings settings, List<ProcessedImage> images)
    {
        // Hot-folder documents never belong to the desktop image list. In particular, do not
        // advance its saved-state token: that would incorrectly make an unrelated open document
        // look saved and suppress the user's unsaved-changes prompt.
        return await InternalSave(settings, images, updateUiSavedState: false);
    }

    internal static bool ShouldUpdateUiSavedStateForHotFolder() => false;

    private async Task<bool> InternalSave(AutoSaveSettings settings, List<ProcessedImage> images,
        bool updateUiSavedState = true)
    {
        try
        {
            bool ok = true;
            var placeholders = Placeholders.All.WithDate(DateTime.Now);
            int i = 0;
            string? firstFileSaved = null;
            var scans = SaveSeparatorHelper.SeparateScans(new[] { images }, settings.Separator).ToList();
            foreach (var imagesToSave in scans)
            {
                (bool success, string? filePath) =
                    await SaveOneFile(settings, placeholders, i++, imagesToSave, scans.Count == 1);
                if (success)
                {
                    // Normally we're supposed to take the CurrentState before the save operation starts, but that
                    // doesn't really work here since populating the UiImageList happens asynchronously so the images
                    // we're saving might not be present yet. In practice waiting until after saving will ensure the
                    // list is populated so that this logic works correctly.
                    if (updateUiSavedState)
                    {
                        _imageList.MarkSaved(_imageList.CurrentState, imagesToSave);
                    }
                    firstFileSaved ??= filePath;
                }
                else
                {
                    ok = false;
                }
            }
            // TODO: Shouldn't this give duplicate notifications?
            if (scans.Count > 1 && ok)
            {
                // Can't just do images.Count because that includes patch codes
                int imageCount = scans.SelectMany(x => x).Count();
                _notify.ImagesSaved(imageCount, firstFileSaved!);
            }
            return ok;
        }
        catch (Exception ex)
        {
            Log.ErrorException(MiscResources.AutoSaveError, ex);
            _errorOutput.DisplayError(MiscResources.AutoSaveError, ex);
            return false;
        }
    }

    private async Task<(bool, string?)> SaveOneFile(AutoSaveSettings settings, Placeholders placeholders, int i,
        List<ProcessedImage> images, bool doNotify)
    {
        if (images.Count == 0)
        {
            return (true, null);
        }
        // Collect zonal OCR field results (waiting for any in-progress extractions, or extracting
        // now if an active zone template is configured)
        var zonalResults = new List<ZonalOcrResult>();
        foreach (var image in images)
        {
            try
            {
                var result = await _zonalOcrService.GetOrExtract(image);
                if (result != null)
                {
                    zonalResults.Add(result);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error running zonal OCR during auto save", ex);
            }
        }
        string filePathPattern = settings.FilePath;
        if (zonalResults.Count > 0)
        {
            // Support $(FieldName) / {FieldName} placeholders in the auto-save file name pattern
            filePathPattern = ContentPlaceholders.SubstituteFieldTokens(filePathPattern, zonalResults[0].Fields);
        }
        // If the pattern uses generic document tokens (DOC_DATE etc.) and the local LLM is set up,
        // fill them from a whole-page extraction of the first page
        if (ContentPlaceholders.ContainsAnyToken(filePathPattern, ContentPlaceholders.GenericTokenNames) &&
            _llmDocumentExtractor.IsAvailable)
        {
            try
            {
                var genericFields =
                    await _llmDocumentExtractor.ExtractGenericFields(images[0], CancellationToken.None);
                if (genericFields != null)
                {
                    filePathPattern = ContentPlaceholders.SubstituteFieldTokens(filePathPattern, genericFields);
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error running LLM document extraction during auto save", ex);
            }
        }
        // Any content tokens still unresolved (extraction unavailable/failed, or field not in this
        // page's results) fall back to a safe default so they never leak into file names
        var knownTokenNames = ContentPlaceholders.GenericTokenNames
            .Concat(zonalResults.SelectMany(r => r.Fields.Select(f => f.Name)))
            .Concat(_zonalOcrService.GetActiveTemplate()?.Zones.Select(z => z.Name) ??
                    Enumerable.Empty<string>())
            .Where(name => !ContentPlaceholders.ReservedTokenNames.Contains(name));
        filePathPattern = ContentPlaceholders.SubstituteFallbacks(filePathPattern, knownTokenNames);
        string subPath = placeholders.Substitute(filePathPattern, true, i);
        subPath = EnsureNonEmptyFileName(subPath);
        if (settings.PromptForFilePath)
        {
            string? newPath = null!;
            if (Invoker.Current.InvokeGet(() => _dialogHelper.PromptToSavePdfOrImage(subPath, out newPath)))
            {
                subPath = placeholders.Substitute(newPath!, true, i);
                subPath = EnsureNonEmptyFileName(subPath);
            }
            else
            {
                return (false, null);
            }
        }
        // TODO: This placeholder handling is complex and wrong in some cases (e.g. FilePerScan with ext = "jpg")
        // TODO: Maybe have initial placeholders that replace date, then rely on the ops to increment the file num
        var extension = Path.GetExtension(subPath);
        if (extension != null && extension.Equals(".pdf", StringComparison.InvariantCultureIgnoreCase))
        {
            if (File.Exists(subPath))
            {
                subPath = placeholders.Substitute(subPath, true, 0, 1);
            }
            var op = new SavePdfOperation(_pdfExporter, _overwritePrompt, searchIndexService: _searchIndexService);
            if (op.Start(subPath, placeholders, images, _config.Get(c => c.PdfSettings), _config.DefaultOcrParams()))
            {
                _operationProgress.ShowProgress(op);
            }
            bool success = await op.Success;
            if (success)
            {
                AppendZonalOcrCsv(subPath, zonalResults);
            }
            if (success && doNotify)
            {
                _notify.PdfSaved(subPath);
            }
            return (success, subPath);
        }
        else
        {
            // A content-derived name may have been capped after placeholder expansion. Re-run
            // collision numbering for image saves so truncation cannot turn two documents into
            // the same filename.
            if (File.Exists(subPath))
            {
                subPath = AddNumericSuffix(subPath);
            }
            var op = new SaveImagesOperation(_overwritePrompt, _imageContext);
            if (op.Start(subPath, placeholders, images, _config.Get(c => c.ImageSettings)))
            {
                _operationProgress.ShowProgress(op);
            }
            bool success = await op.Success;
            if (success)
            {
                AppendZonalOcrCsv(subPath, zonalResults);
            }
            if (success && doNotify && op.FirstFileSaved != null)
            {
                _notify.ImagesSaved(images.Count, op.FirstFileSaved);
            }
            return (success, subPath);
        }
    }

    private static string EnsureNonEmptyFileName(string path)
    {
        try
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(name.Trim('_', '-', ' ', '.')))
            {
                const int maxNameLength = 180;
                if (name.Length <= maxNameLength)
                {
                    return path;
                }
                string cappedDirectory = Path.GetDirectoryName(path) ?? "";
                string cappedExtension = Path.GetExtension(path);
                return Path.Combine(cappedDirectory, name.Substring(0, maxNameLength).TrimEnd() + cappedExtension);
            }
            string dir = Path.GetDirectoryName(path) ?? "";
            string ext = Path.GetExtension(path);
            return Path.Combine(dir, "Document" + ext);
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static string AddNumericSuffix(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        string baseName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        int suffix = 2;
        string candidate;
        do
        {
            string suffixText = "_" + suffix++;
            const int maxBaseLength = 180;
            string trimmedBase = baseName.Length + suffixText.Length > maxBaseLength
                ? baseName.Substring(0, maxBaseLength - suffixText.Length).TrimEnd()
                : baseName;
            candidate = Path.Combine(directory, trimmedBase + suffixText + extension);
        } while (File.Exists(candidate));
        return candidate;
    }

    private void AppendZonalOcrCsv(string savedFilePath, List<ZonalOcrResult> zonalResults)
    {
        if (zonalResults.Count == 0)
        {
            return;
        }
        try
        {
            string csvPath = Path.ChangeExtension(savedFilePath, ".csv");
            ZonalOcrCsv.AppendRows(csvPath, zonalResults, savedFilePath);
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error writing zonal OCR CSV log", ex);
        }
    }
}