using System.Threading;
using NAPS2.EtoForms.Ui;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Desktop;

public class DesktopSubFormController : IDesktopSubFormController
{
    private readonly IFormFactory _formFactory;
    private readonly UiImageList _imageList;
    private readonly DesktopImagesController _desktopImagesController;
    private readonly TesseractLanguageManager _tesseractLanguageManager;
    private readonly ZonalOcrService _zonalOcrService;
    private readonly ZonalOcrResultsStore _zonalOcrResultsStore;
    private readonly DocumentManager _documentManager;

    public DesktopSubFormController(IFormFactory formFactory, UiImageList imageList,
        DesktopImagesController desktopImagesController, TesseractLanguageManager tesseractLanguageManager,
        ZonalOcrService zonalOcrService, ZonalOcrResultsStore zonalOcrResultsStore, DocumentManager documentManager)
    {
        _formFactory = formFactory;
        _imageList = imageList;
        _desktopImagesController = desktopImagesController;
        _tesseractLanguageManager = tesseractLanguageManager;
        _zonalOcrService = zonalOcrService;
        _zonalOcrResultsStore = zonalOcrResultsStore;
        _documentManager = documentManager;
    }

    private Func<ListSelection<UiImage>>? SelectionFunc { get; init; }

    private ListSelection<UiImage> Selection => SelectionFunc?.Invoke() ?? _imageList.Selection;

    public IDesktopSubFormController WithSelection(Func<ListSelection<UiImage>> selectionFunc)
    {
        return new DesktopSubFormController(_formFactory, _imageList, _desktopImagesController,
            _tesseractLanguageManager, _zonalOcrService, _zonalOcrResultsStore, _documentManager)
        {
            SelectionFunc = selectionFunc
        };
    }

    public void ShowCropForm() => ShowImageForm<CropForm>();
    public void ShowBrightnessContrastForm() => ShowImageForm<BrightContForm>();
    public void ShowHueSaturationForm() => ShowImageForm<HueSatForm>();
    public void ShowBlackWhiteForm() => ShowImageForm<BlackWhiteForm>();
    public void ShowSharpenForm() => ShowImageForm<SharpenForm>();
    public void ShowSplitForm() => ShowImageForm<SplitForm>();
    public void ShowRotateForm() => ShowImageForm<RotateForm>();

    public void ShowCombineForm()
    {
        if (_imageList.Images.Count < 2) return;
        ShowImageForm<CombineForm>();
    }

    private void ShowImageForm<T>() where T : ImageFormBase
    {
        var selection = Selection;
        if (selection.Any())
        {
            var form = _formFactory.Create<T>();
            form.Image = selection.First();
            form.SelectedImages = selection.ToList();
            form.ShowModal();
        }
    }

    public void ShowProfilesForm()
    {
        var form = _formFactory.Create<ProfilesForm>();
        form.ImageCallback = _desktopImagesController.ReceiveScannedImage();
        form.ShowModal();
    }

    public void ShowOcrForm()
    {
        if (_tesseractLanguageManager.InstalledLanguages.Any())
        {
            _formFactory.Create<OcrSetupForm>().ShowModal();
        }
        else
        {
            _formFactory.Create<OcrDownloadForm>().ShowModal();
            if (_tesseractLanguageManager.InstalledLanguages.Any())
            {
                _formFactory.Create<OcrSetupForm>().ShowModal();
            }
        }
    }

    public void ShowOcrZonesForm() => ShowImageForm<OcrZonesForm>();

    public void ShowZonalOcrResultsForm()
    {
        _formFactory.Create<ZonalOcrResultsForm>().ShowModal();
    }

    public void ShowSearchForm()
    {
        _formFactory.Create<SearchForm>().ShowModal();
    }

    public void ShowDocumentIndexForm()
    {
        _formFactory.Create<DocumentIndexForm>().ShowModal();
    }

    public async void ExtractZonalFields()
    {
        var template = _zonalOcrService.GetActiveTemplate();
        if (template == null)
        {
            // No active template defined yet; let the user define one first
            ShowOcrZonesForm();
            return;
        }
        var selection = Selection;
        var images = selection.Any() ? selection.ToList() : _imageList.Images.ToList();
        if (images.Count == 0) return;
        try
        {
            foreach (var uiImage in images)
            {
                using var processedImage = uiImage.GetClonedImage();
                // Waits for any in-progress extraction (e.g. auto-extract after scanning) instead of
                // starting a duplicate one
                var existing = await _zonalOcrResultsStore.WaitForResult(processedImage);
                if (existing == null)
                {
                    var result = await Task.Run(() =>
                        _zonalOcrService.ExtractFields(processedImage, template, CancellationToken.None));
                    if (result != null)
                    {
                        _zonalOcrResultsStore.AddResult(processedImage, result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error extracting zonal OCR fields", ex);
        }
        ShowZonalOcrResultsForm();
    }

    public void ShowBatchScanForm()
    {
        var form = _formFactory.Create<BatchScanForm>();
        form.ImageCallback = _desktopImagesController.ReceiveScannedImage();
        form.ShowModal();
    }

    public void ShowScannerSharingForm()
    {
        var form = _formFactory.Create<ScannerSharingForm>();
        form.ShowModal();
    }

    public void ShowViewerForm()
    {
        var selected = Selection.FirstOrDefault();
        if (selected != null)
        {
            using var viewer = _formFactory.Create<PreviewForm>();
            viewer.CurrentImage = selected;
            viewer.ShowModal();
        }
    }

    public void ShowPdfSettingsForm()
    {
        _formFactory.Create<PdfSettingsForm>().ShowModal();
    }

    public void ShowImageSettingsForm()
    {
        _formFactory.Create<ImageSettingsForm>().ShowModal();
    }

    public void ShowEmailSettingsForm()
    {
        _formFactory.Create<EmailSettingsForm>().ShowModal();
    }

    public void ShowSettingsForm()
    {
        _formFactory.Create<SettingsForm>().ShowModal();
    }

    public void ShowHotFolderSettingsForm()
    {
        _formFactory.Create<HotFolderSettingsForm>().ShowModal();
    }

    public void ShowAboutForm()
    {
        _formFactory.Create<AboutForm>().ShowModal();
    }
}