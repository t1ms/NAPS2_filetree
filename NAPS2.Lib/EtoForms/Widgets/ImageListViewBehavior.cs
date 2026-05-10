using Eto.Drawing;
using Eto.Forms;
using Google.Protobuf;
using NAPS2.ImportExport.Images;

namespace NAPS2.EtoForms.Widgets;

public class ImageListViewBehavior : ListViewBehavior<UiImage>
{
    private readonly UiThumbnailProvider _thumbnailProvider;
    private readonly Naps2Config _config;
    private readonly ImageTransfer _imageTransfer = new();

    public ImageListViewBehavior(UiThumbnailProvider thumbnailProvider,
        ColorScheme colorScheme, Naps2Config config) : base(colorScheme)
    {
        _thumbnailProvider = thumbnailProvider;
        _config = config;
        MultiSelect = true;
        ShowLabels = false;
        ScrollOnDrag = true;
        UseHandCursor = true;
    }

    public override bool ShowPageNumbers => _config.Get(c => c.ShowPageNumbers);

    public override Image GetImage(IListView<UiImage> listView, UiImage item)
    {
        using var thumbnail = _thumbnailProvider.GetThumbnail(item, listView.ImageSize.Width);
        var etoImage = thumbnail.ToEtoImage();

        if (item.DocumentGroupId != Guid.Empty)
        {
            var bmp = new Bitmap(etoImage.Size, PixelFormat.Format32bppRgba);
            using (var g = new Graphics(bmp))
            {
                g.DrawImage(etoImage, 0, 0);
                var color = GetColorForGroup(item.DocumentGroupId);
                g.DrawRectangle(new Pen(color, 8), new RectangleF(0, 0, etoImage.Width - 1, etoImage.Height - 1));
            }
            etoImage.Dispose();
            return bmp;
        }

        return etoImage;
    }

    private Color GetColorForGroup(Guid groupId)
    {
        var hash = groupId.GetHashCode();
        var r = (byte)(hash & 0xFF);
        var g = (byte)((hash >> 8) & 0xFF);
        var b = (byte)((hash >> 16) & 0xFF);
        if (r < 100 && g < 100 && b < 100)
        {
            r += 100; g += 100; b += 100;
        }
        return Color.FromArgb(r, g, b);
    }

    public override bool AllowDragDrop => true;

    public override bool AllowFileDrop => true;

    public override string CustomDragDataType => _imageTransfer.TypeName;

    public override byte[] SerializeCustomDragData(UiImage[] items)
    {
        using var processedImages = items.Select(x => x.GetClonedImage()).ToDisposableList();
        return _imageTransfer.ToBinaryData(processedImages);
    }

    public override DragEffects GetCustomDragEffect(byte[] data)
    {
        var dataObj = _imageTransfer.FromBinaryData(data);
        return dataObj.ProcessId == Process.GetCurrentProcess().Id
            ? DragEffects.Move
            : DragEffects.Copy;
    }

    public override byte[] MergeCustomDragData(byte[][] dataItems)
    {
        // TODO: Move to ImageTransfer?
        var mergedObj = new ImageTransferData();
        foreach (var data in dataItems)
        {
            var dataObj = _imageTransfer.FromBinaryData(data);
            if (mergedObj.ProcessId != 0 && mergedObj.ProcessId != dataObj.ProcessId)
            {
                throw new ArgumentException("Inconsistent process IDs in drag data");
            }
            mergedObj.ProcessId = dataObj.ProcessId;
            mergedObj.SerializedImages.AddRange(dataObj.SerializedImages);
        }
        return mergedObj.ToByteArray();
    }
}