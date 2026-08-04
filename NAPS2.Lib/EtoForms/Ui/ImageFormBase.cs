using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

public abstract class ImageFormBase : EtoDialogBase
{
    private readonly ImageView _imageView = new();

    private readonly RefreshThrottle _renderThrottle;

    // Image bounds in the coordinate space of the overlay control
    protected float _overlayT, _overlayL, _overlayR, _overlayB, _overlayW, _overlayH;

    protected ImageFormBase(Naps2Config config, UiImageList imageList, ThumbnailController thumbnailController) :
        base(config)
    {
        ImageList = imageList;
        ThumbnailController = thumbnailController;
        _renderThrottle = new RefreshThrottle(RenderImage);
        Overlay.Paint += PaintOverlay;
        Overlay.SizeChanged += (_, _) => UpdateImageCoords();
        FormStateController.DefaultExtraLayoutSize = new Size(400, 400);
    }

    public UiImage Image { get; set; } = null!;
    public List<UiImage> SelectedImages { get; set; } = null!;

    protected UiImageList ImageList { get; }
    protected ThumbnailController ThumbnailController { get; }

    protected int DisplayImageHeight { get; set; }
    protected int DisplayImageWidth { get; set; }

    protected IMemoryImage? DisplayImage { get; set; }
    protected Drawable Overlay { get; } = new();
    protected int OverlayBorderSize { get; set; }

    protected override void BuildLayout()
    {
        LayoutController.Content = L.Column(
            Overlay.Scale(),
            CreateControls(),
            L.Row(
                CreateExtraButtons(),
                C.Filler(),
                L.OkCancel(
                    C.OkButton(this, beforeClose: Apply),
                    C.CancelButton(this))
            )
        );
    }

    protected override void OnPreLoad(EventArgs e)
    {
        base.OnPreLoad(e);
        InitDisplayImage();
        DisplayImageWidth = DisplayImage!.Width;
        DisplayImageHeight = DisplayImage.Height;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdatePreviewBox();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        DisplayImage?.Dispose();
        _imageView.Image?.Dispose();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateImageCoords();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateImageCoords();
    }

    private void UpdateImageCoords()
    {
        if (!Overlay.Loaded) return;
        var availableWidth = Overlay.Width - OverlayBorderSize * 2;
        var availableHeight = Overlay.Height - OverlayBorderSize * 2;
        if (availableWidth <= 0 || availableHeight <= 0 || DisplayImageWidth <= 0 || DisplayImageHeight <= 0)
        {
            _overlayL = _overlayT = _overlayR = _overlayB = _overlayW = _overlayH = 0;
            return;
        }

        // Fit the image into the available area while keeping the same inset on all
        // sides. The old calculation mixed border widths and produced an asymmetric
        // right/bottom edge, which was especially visible in the split editor.
        var scale = Math.Min(
            availableWidth / (float) DisplayImageWidth,
            availableHeight / (float) DisplayImageHeight);
        var imageWidth = DisplayImageWidth * scale;
        var imageHeight = DisplayImageHeight * scale;
        _overlayL = OverlayBorderSize + (availableWidth - imageWidth) / 2;
        _overlayT = OverlayBorderSize + (availableHeight - imageHeight) / 2;
        _overlayR = _overlayL + imageWidth;
        _overlayB = _overlayT + imageHeight;
        _overlayW = _overlayR - _overlayL;
        _overlayH = _overlayB - _overlayT;
        Overlay.Invalidate();
    }

    protected virtual void PaintOverlay(object? sender, PaintEventArgs e)
    {
        if (DisplayImage == null || _overlayW <= 0 || _overlayH <= 0)
        {
            return;
        }
        using var etoImage = DisplayImage!.ToEtoImage();
        e.Graphics.DrawImage(etoImage, _overlayL, _overlayT, _overlayW, _overlayH);
    }

    private void RenderImage()
    {
        var bitmap = RenderPreview();
        Invoker.Current.Invoke(() =>
        {
            DisplayImage?.Dispose();
            DisplayImage = bitmap;
            if (DisplayImage.Width != DisplayImageWidth || DisplayImage.Height != DisplayImageHeight)
            {
                DisplayImageWidth = DisplayImage.Width;
                DisplayImageHeight = DisplayImage.Height;
                UpdateImageCoords();
            }
            Overlay.Invalidate();
        });
    }

    protected abstract LayoutElement CreateControls();

    protected virtual LayoutElement CreateExtraButtons() => C.None();

    protected abstract IMemoryImage RenderPreview();

    protected abstract void InitDisplayImage();

    protected abstract void Apply();

    protected void UpdatePreviewBox()
    {
        Overlay.Invalidate();
        _renderThrottle.RunAction();
    }

    protected RectangleF GetScreenWorkingArea()
    {
        try
        {
            var screen = Screen ?? Screen.PrimaryScreen;
            if (screen != null)
            {
                return screen.WorkingArea;
            }
        }
        catch (Exception)
        {
            // On Linux sometimes we can't get the working area
        }

        // Assume 1080p screen by default
        return new RectangleF(0, 0, 1920, 1080);
    }
}