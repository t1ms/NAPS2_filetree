using NAPS2.EtoForms;
using NAPS2.EtoForms.Notifications;
using NAPS2.EtoForms.Ui;
using NAPS2.ImportExport.Email;
using NAPS2.ImportExport.Images;
using NAPS2.Pdf;

namespace NAPS2.ImportExport;

public class ExportController : IExportController
{
    private readonly DialogHelper _dialogHelper;
    private readonly IOperationFactory _operationFactory;
    private readonly IFormFactory _formFactory;
    private readonly OperationProgress _operationProgress;
    private readonly Naps2Config _config;
    private readonly UiImageList _imageList;
    private readonly DocumentManager _documentManager;

    public ExportController(DialogHelper dialogHelper, IOperationFactory operationFactory, IFormFactory formFactory,
        OperationProgress operationProgress, Naps2Config config, UiImageList imageList, DocumentManager documentManager)
    {
        _dialogHelper = dialogHelper;
        _operationFactory = operationFactory;
        _formFactory = formFactory;
        _operationProgress = operationProgress;
        _config = config;
        _imageList = imageList;
        _documentManager = documentManager;
    }

    public async Task<bool> SavePdf(ICollection<UiImage> uiImages, ISaveNotify notify)
    {
        var groups = uiImages.Select(x => x.DocumentGroupId).Distinct().ToList();
        if (groups.Count > 1)
        {
            var wizard = _formFactory.Create<NAPS2.EtoForms.Ui.ExportWizardForm>();
            wizard.DocumentCount = groups.Count;
            wizard.ShowModal();
            if (wizard.Result)
            {
                if (wizard.IsSeparate)
                {
                    return await SavePdfByGroup(notify, uiImages);
                }
                // Else continue to single file logic
            }
            else
            {
                return false; // User cancelled
            }
        }

        using var images = GetSnapshots(uiImages);
        if (!images.Any())
        {
            return false;
        }

        string savePath;
        var defaultFileName = _config.Get(c => c.PdfSettings.DefaultFileName);
        if (_config.Get(c => c.PdfSettings.SkipSavePrompt) && Path.IsPathRooted(defaultFileName))
        {
            savePath = defaultFileName!;
        }
        else
        {
            if (!_dialogHelper.PromptToSavePdf(GetDefaultPath(defaultFileName, uiImages, true), out savePath!))
            {
                return false;
            }
        }

        if (await DoSavePdf(images, notify, savePath))
        {
            MaybeDeleteAfterSaving(uiImages);
            return true;
        }
        return false;
    }

    public async Task<bool> SavePdfByGroup(ISaveNotify notify, ICollection<UiImage>? sourceImages = null)
    {
        var source = sourceImages ?? _imageList.Images;
        if (!source.Any()) return false;

        string folderPath = "";
        if (!_dialogHelper.PromptToSelectFolder(null, out folderPath!))
        {
            return false;
        }

        var dir = folderPath;
        var groupsToSave = _documentManager.Groups
            .Select(group => new
            {
                Group = group,
                Images = source.Where(image => image.DocumentGroupId == group.Id).ToList(),
                FileName = (string.IsNullOrWhiteSpace(group.IndexField) ? "Document" : group.IndexField) + ".pdf"
            })
            .Where(item => item.Images.Any())
            .ToList();
        var duplicateOutputName = groupsToSave
            .GroupBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOutputName != null)
        {
            return false;
        }

        bool anySaved = false;

        foreach (var item in groupsToSave)
        {
            using var images = GetSnapshots(item.Images);
            string savePath = Path.Combine(dir, item.FileName);

            if (await DoSavePdf(images, notify, savePath))
            {
                MaybeDeleteAfterSaving(item.Images);
                anySaved = true;
            }
        }
        return anySaved;
    }

    public async Task<bool> SaveImages(ICollection<UiImage> uiImages, ISaveNotify notify)
    {
        using var images = GetSnapshots(uiImages);
        if (!images.Any())
        {
            return false;
        }

        string savePath;
        var defaultFileName = _config.Get(c => c.ImageSettings.DefaultFileName);
        if (_config.Get(c => c.ImageSettings.SkipSavePrompt) &&
            Path.IsPathRooted(defaultFileName))
        {
            savePath = defaultFileName!;
        }
        else
        {
            if (!_dialogHelper.PromptToSaveImage(GetDefaultPath(defaultFileName, uiImages, false), out savePath!))
            {
                return false;
            }
        }

        if (await DoSaveImages(images, notify, savePath))
        {
            MaybeDeleteAfterSaving(uiImages);
            return true;
        }
        return false;
    }

    public async Task<bool> SavePdfOrImages(ICollection<UiImage> uiImages, ISaveNotify notify)
    {
        // Check for multiple document groups and show the export wizard
        var groups = uiImages.Select(x => x.DocumentGroupId).Distinct().ToList();
        if (groups.Count > 1)
        {
            var wizard = _formFactory.Create<NAPS2.EtoForms.Ui.ExportWizardForm>();
            wizard.DocumentCount = groups.Count;
            wizard.ShowModal();
            if (wizard.Result)
            {
                if (wizard.IsSeparate)
                {
                    return await SavePdfByGroup(notify, uiImages);
                }
                // Else continue to single file logic below
            }
            else
            {
                return false; // User cancelled
            }
        }

        // Note this path bypasses some of the pdf/image save options (e.g. default file name)
        using var images = GetSnapshots(uiImages);

        string savePath;
        var pdfDefaultFileName = _config.Get(c => c.PdfSettings.DefaultFileName);
        var imageDefaultFileName = _config.Get(c => c.ImageSettings.DefaultFileName);
        if (_config.Get(c => c.PdfSettings.SkipSavePrompt) && Path.IsPathRooted(pdfDefaultFileName))
        {
            savePath = pdfDefaultFileName!;
        }
        else if (_config.Get(c => c.ImageSettings.SkipSavePrompt) && Path.IsPathRooted(imageDefaultFileName))
        {
            savePath = imageDefaultFileName!;
        }
        else
        {
            var defaultFileName = string.IsNullOrWhiteSpace(pdfDefaultFileName)
                ? imageDefaultFileName
                : pdfDefaultFileName;
            if (!_dialogHelper.PromptToSavePdfOrImage(GetDefaultPath(defaultFileName, uiImages, null), out savePath!))
            {
                return false;
            }
        }

        if (Path.GetExtension(savePath).ToLowerInvariant() == ".pdf"
                ? await DoSavePdf(images, notify, savePath)
                : await DoSaveImages(images, notify, savePath))
        {
            MaybeDeleteAfterSaving(uiImages);
            return true;
        }
        return false;
    }

    public async Task<bool> EmailPdf(ICollection<UiImage> uiImages)
    {
        using var images = GetSnapshots(uiImages);
        if (!images.Any())
        {
            return false;
        }

        if (!_config.User.Has(c => c.EmailSetup.ProviderType))
        {
            // First email attempt; prompt for a provider
            var form = _formFactory.Create<EmailProviderForm>();
            Invoker.Current.Invoke(() => form.ShowModal());
            if (!form.Result)
            {
                return false;
            }
        }

        var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        var attachmentName = new string(_config.Get(c => c.EmailSettings.AttachmentName)
            .Where(x => !invalidChars.Contains(x)).ToArray());
        if (string.IsNullOrEmpty(attachmentName))
        {
            attachmentName = "Scan.pdf";
        }
        if (!attachmentName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
        {
            attachmentName += ".pdf";
        }
        attachmentName = Placeholders.All.Substitute(attachmentName, false);

        if (await DoEmailPdf(images, attachmentName))
        {
            MaybeDeleteAfterSaving(uiImages);
            return true;
        }
        return false;
    }

    private async Task<bool> DoSavePdf(IList<ProcessedImage> images, ISaveNotify notify, string savePath)
    {
        var subSavePath = Placeholders.All.Substitute(savePath);
        var state = _imageList.CurrentState;
        if (await RunSavePdfOperation(subSavePath, images, originalFilename: savePath))
        {
            _imageList.MarkSaved(state, images);
            notify.PdfSaved(subSavePath);
            return true;
        }
        return false;
    }

    private async Task<bool> DoSaveImages(IList<ProcessedImage> images, ISaveNotify notify, string savePath)
    {
        var op = _operationFactory.Create<SaveImagesOperation>();
        var state = _imageList.CurrentState;
        if (op.Start(savePath, Placeholders.All.WithDate(DateTime.Now), images, _config.Get(c => c.ImageSettings),
                savePath))
        {
            _operationProgress.ShowProgress(op);
        }
        if (await op.Success)
        {
            _imageList.MarkSaved(state, images);
            notify.ImagesSaved(images.Count, op.FirstFileSaved!);
            return true;
        }
        return false;
    }

    private async Task<bool> DoEmailPdf(IList<ProcessedImage> images, string attachmentName)
    {
        var tempFolder = new DirectoryInfo(Path.Combine(Paths.Temp, Path.GetRandomFileName()));
        tempFolder.Create();
        try
        {
            string targetPath = Path.Combine(tempFolder.FullName, attachmentName);
            var state = _imageList.CurrentState;

            if (await RunSavePdfOperation(targetPath, images, new EmailMessage()))
            {
                _imageList.MarkSaved(state, images);
                return true;
            }
        }
        finally
        {
            tempFolder.Delete(true);
        }
        return false;
    }

    private async Task<bool> RunSavePdfOperation(string filename, IList<ProcessedImage> images,
        EmailMessage? emailMessage = null, string? originalFilename = null)
    {
        var op = _operationFactory.Create<SavePdfOperation>();

        if (op.Start(filename, Placeholders.All.WithDate(DateTime.Now), images, _config.Get(c => c.PdfSettings),
                _config.DefaultOcrParams(), emailMessage, originalFilename ?? filename))
        {
            _operationProgress.ShowProgress(op);
        }
        return await op.Success;
    }

    private DisposableList<ProcessedImage> GetSnapshots(IEnumerable<UiImage> uiImages)
    {
        return uiImages.Select(x => x.GetClonedImage()).ToDisposableList();
    }

    private void MaybeDeleteAfterSaving(ICollection<UiImage> uiImages)
    {
        if (_config.Get(c => c.DeleteAfterSaving))
        {
            _imageList.Mutate(new ImageListMutation.DeleteSelected(), ListSelection.From(uiImages));
        }
    }

    private string? GetDefaultPath(string? defaultFileName, ICollection<UiImage> uiImages, bool? pdf)
    {
        if (!string.IsNullOrEmpty(defaultFileName))
        {
            return defaultFileName;
        }
        var originalFilePaths = uiImages
            .Select(x => x.GetImageWeakReference().ProcessedImage.PostProcessingData.OriginalFilePath)
            .WhereNotNull()
            .Where(x => pdf == null || pdf == (Path.GetExtension(x).ToLowerInvariant() == ".pdf"))
            .Distinct()
            .ToList();
        if (originalFilePaths.Count == 1)
        {
            return originalFilePaths[0];
        }
        return null;
    }
}