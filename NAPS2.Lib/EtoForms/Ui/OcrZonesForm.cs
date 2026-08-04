using System.Collections.Immutable;
using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Dialog for defining named zonal OCR field zones on a sample page. The user draws rectangles
/// on the page preview and names them (e.g. "Invoice Number", "Date", "Total"). Zones are saved
/// as a named template in config.
/// </summary>
public class OcrZonesForm : ImageFormBase
{
    private const int MIN_ZONE_SIZE = 10;
    private const string NEW_TEMPLATE_ITEM = "(new template)";

    private readonly DropDown _templateDropDown = new();
    private readonly TextBox _templateName = new();
    private readonly ListBox _zoneList = new() { Height = 120 };
    private readonly TextBox _zoneName = new();
    private readonly Button _deleteZone;
    private readonly DropDown _zoneExtractionMode = new();
    private readonly DropDown _barcodeFormat = new();
    private readonly Label _validationMessage = new() { Text = "", Visible = false };
    private readonly CheckBox _useForScanning = new()
        { Text = "Use this template for scans (extract fields from each scanned page)", Checked = true };
    private readonly TextBox _zonePrompt = new();
    private readonly CheckBox _llmEnabled = new()
        { Text = "Clean up field values with a local AI model (CPU-only, .gguf)" };
    private readonly Label _llmModelLabel = new() { Text = "" };
    private readonly Button _llmModelPicker;

    private readonly List<EditableZone> _zones = new();
    private int _selectedZoneIndex = -1;
    private bool _syncing;
    private string? _editingTemplateName;

    // Drag state (in overlay coordinates)
    private bool _dragging;
    private PointF _dragOrigin;
    private PointF _dragCurrent;

    private IMemoryImage? _workingImage;

    public OcrZonesForm(Naps2Config config, UiImageList imageList, ThumbnailController thumbnailController)
        : base(config, imageList, thumbnailController)
    {
        Title = "OCR Field Zones";
        IconName = "text_small";

        _deleteZone = C.Button("Delete Zone", DeleteSelectedZone);
        _llmModelPicker = C.Button("Choose Model...", PickLlmModel);
        _llmEnabled.Checked = config.Get(c => c.EnableLlmFieldCleanup);
        UpdateLlmModelLabel();

        Overlay.MouseDown += Overlay_MouseDown;
        Overlay.MouseMove += Overlay_MouseMove;
        Overlay.MouseUp += Overlay_MouseUp;

        _templateDropDown.SelectedIndexChanged += TemplateDropDown_SelectedIndexChanged;
        _zoneList.SelectedIndexChanged += ZoneList_SelectedIndexChanged;
        _zoneName.TextChanged += ZoneName_TextChanged;
        _zonePrompt.TextChanged += ZonePrompt_TextChanged;
        _zoneExtractionMode.SelectedIndexChanged += ZoneExtractionMode_SelectedIndexChanged;
        _barcodeFormat.SelectedIndexChanged += BarcodeFormat_SelectedIndexChanged;
    }

    private record EditableZone(string Name, RectangleF Rect, string? LlmPrompt = null,
        OcrZoneExtractionMode ExtractionMode = OcrZoneExtractionMode.Text,
        OcrZoneBarcodeFormat BarcodeFormat = OcrZoneBarcodeFormat.Any)
    {
        // Rect coordinates are fractions of the image size (0-1)
    }

    protected override void BuildLayout()
    {
        _zoneExtractionMode.Items.Add(new ListItem { Text = "Printed text", Key = "text" });
        _zoneExtractionMode.Items.Add(new ListItem { Text = "Barcode", Key = "barcode" });
        _barcodeFormat.Items.AddRange(Enum.GetValues<OcrZoneBarcodeFormat>()
            .Select(x => new ListItem { Text = x switch
            {
                OcrZoneBarcodeFormat.Any => "Any format",
                OcrZoneBarcodeFormat.Code128 => "Code 128",
                OcrZoneBarcodeFormat.Code39 => "Code 39",
                OcrZoneBarcodeFormat.Ean13 => "EAN-13",
                OcrZoneBarcodeFormat.Ean8 => "EAN-8",
                OcrZoneBarcodeFormat.UpcA => "UPC-A",
                OcrZoneBarcodeFormat.UpcE => "UPC-E",
                OcrZoneBarcodeFormat.QrCode => "QR Code",
                OcrZoneBarcodeFormat.DataMatrix => "Data Matrix",
                OcrZoneBarcodeFormat.Pdf417 => "PDF417",
                _ => x.ToString()
            }}));

        LayoutController.Content = L.Column(
            Overlay.Scale(),
            C.Label("Drag on the page to draw a field zone. Click a zone to select it, then rename or delete it."),
            L.Row(
                L.Column(
                    C.Label("Template name"),
                    L.Row(_templateDropDown.NaturalWidth(150), _templateName.NaturalWidth(150)),
                    _useForScanning
                ),
                L.Column(
                    C.Label("Zones"),
                    _zoneList.NaturalWidth(200),
                    L.Row(_zoneName.NaturalWidth(150), _deleteZone),
                    C.Label("Extract as"),
                    _zoneExtractionMode.NaturalWidth(180),
                    C.Label("Barcode format"),
                    _barcodeFormat.NaturalWidth(180),
                    C.Label("AI prompt for this zone (optional, {FieldType} = zone name)"),
                    _zonePrompt.NaturalWidth(360)
                ).Scale()
            ),
            C.Label("Renaming a field is saved together with the template."),
            _validationMessage,
            C.Label("Draw at least one usable zone, give every field a unique non-empty name, then click Save."),
            _llmEnabled,
            L.Row(_llmModelPicker, _llmModelLabel.Scale()),
            L.Row(
                C.Filler(),
                L.OkCancel(
                    C.OkButton(this, SaveTemplate, "Save"),
                    C.CancelButton(this))
            )
        );
    }

    protected override void OnPreLoad(EventArgs e)
    {
        base.OnPreLoad(e);
        LoadTemplateList();
    }

    private void LoadTemplateList()
    {
        _templateDropDown.Items.Clear();
        _templateDropDown.Items.Add(NEW_TEMPLATE_ITEM);
        var templates = Config.Get(c => c.OcrZoneTemplates);
        foreach (var template in templates)
        {
            _templateDropDown.Items.Add(template.Name);
        }
        var activeName = Config.Get(c => c.ActiveOcrZoneTemplateName);
        var activeIndex = templates.FindIndex(t => t.Name == activeName);
        _templateDropDown.SelectedIndex = activeIndex == -1 ? 0 : activeIndex + 1;
    }

    private void TemplateDropDown_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var templates = Config.Get(c => c.OcrZoneTemplates);
        int index = _templateDropDown.SelectedIndex - 1;
        _zones.Clear();
        _selectedZoneIndex = -1;
        if (index >= 0 && index < templates.Count)
        {
            var template = templates[index];
            _editingTemplateName = template.Name;
            _templateName.Text = template.Name;
            foreach (var zone in template.Zones)
            {
                _zones.Add(new EditableZone(zone.Name,
                    new RectangleF((float) zone.Left, (float) zone.Top, (float) zone.Width, (float) zone.Height),
                    zone.LlmPrompt, zone.ExtractionMode, zone.BarcodeFormat));
            }
            _useForScanning.Checked = Config.Get(c => c.ActiveOcrZoneTemplateName) == template.Name;
        }
        else
        {
            _editingTemplateName = null;
            _templateName.Text = "";
            _useForScanning.Checked = true;
        }
        UpdateZoneList();
        Overlay.Invalidate();
    }

    protected override LayoutElement CreateControls() => C.None();

    protected override IMemoryImage RenderPreview() => _workingImage!.Clone();

    protected override void InitDisplayImage()
    {
        using var imageToRender = Image.GetClonedImage();
        _workingImage = imageToRender.Render();

        // Scale down the image to the screen size for better efficiency without losing much fidelity
        var workingArea = GetScreenWorkingArea();
        var widthRatio = _workingImage.Width / workingArea.Width;
        var heightRatio = _workingImage.Height / workingArea.Height;
        if (widthRatio > 1 || heightRatio > 1)
        {
            _workingImage = _workingImage.PerformTransform(new ScaleTransform(1 / Math.Max(widthRatio, heightRatio)));
        }

        DisplayImage = _workingImage.Clone();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _workingImage?.Dispose();
    }

    protected override void Apply()
    {
    }

    private bool SaveTemplate()
    {
        _validationMessage.Text = "";
        _validationMessage.Visible = false;
        var name = _templateName.Text.Trim();
        if (name.Length == 0)
        {
            ShowValidation("Enter a template name before saving.", _templateName);
            return false;
        }
        var validation = ValidateZones();
        if (validation != null)
        {
            ShowValidation(validation.Value.Message, validation.Value.Control);
            return false;
        }
        var template = new OcrZoneTemplate
        {
            Name = name,
            Zones = _zones.Select(z => new OcrZone
            {
                Name = z.Name.Trim(),
                Left = z.Rect.X,
                Top = z.Rect.Y,
                Width = z.Rect.Width,
                Height = z.Rect.Height,
                LlmPrompt = string.IsNullOrWhiteSpace(z.LlmPrompt) ? null : z.LlmPrompt.Trim(),
                ExtractionMode = z.ExtractionMode,
                BarcodeFormat = z.BarcodeFormat
            }).ToImmutableList()
        };
        var templates = Config.Get(c => c.OcrZoneTemplates);
        var existingIndex = _editingTemplateName == null
            ? -1
            : templates.FindIndex(t => t.Name.Equals(_editingTemplateName, StringComparison.OrdinalIgnoreCase));
        var conflictingIndex = templates.FindIndex(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            !t.Name.Equals(_editingTemplateName, StringComparison.OrdinalIgnoreCase));
        if (conflictingIndex != -1)
        {
            ShowValidation($"A template named \"{name}\" already exists. Choose a different name.", _templateName);
            return false;
        }
        templates = existingIndex == -1
            ? templates.Add(template)
            : templates.SetItem(existingIndex, template);
        Config.User.Set(c => c.OcrZoneTemplates, templates);
        var activeName = Config.Get(c => c.ActiveOcrZoneTemplateName);
        if (_useForScanning.IsChecked())
        {
            Config.User.Set(c => c.ActiveOcrZoneTemplateName, name);
        }
        else if (activeName?.Equals(_editingTemplateName, StringComparison.OrdinalIgnoreCase) == true ||
                 activeName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
        {
            Config.User.Set(c => c.ActiveOcrZoneTemplateName, "");
        }
        Config.User.Set(c => c.EnableLlmFieldCleanup, _llmEnabled.IsChecked());
        _editingTemplateName = name;
        return true;
    }

    private (string Message, Control Control)? ValidateZones()
    {
        if (_zones.Count == 0)
        {
            return ("Draw at least one usable zone before saving.", _zoneList);
        }
        var namedZones = _zones.Select((zone, index) => (zone, index, name: zone.Name.Trim())).ToList();
        var unnamed = namedZones.FirstOrDefault(x => x.name.Length == 0);
        if (unnamed.zone != null)
        {
            _selectedZoneIndex = unnamed.index;
            UpdateZoneList();
            return ("Every zone needs a non-empty field name.", _zoneName);
        }
        var duplicate = namedZones
            .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            var duplicateZone = duplicate.First();
            _selectedZoneIndex = duplicateZone.index;
            UpdateZoneList();
            return ($"Field name \"{duplicate.Key}\" is used more than once. Give each field a unique name.",
                _zoneName);
        }
        var unusable = namedZones.FirstOrDefault(x => !IsUsableZone(x.zone.Rect));
        if (unusable.zone != null)
        {
            _selectedZoneIndex = unusable.index;
            UpdateZoneList();
            return ($"The zone \"{unusable.name}\" is not usable. Draw a positive-size rectangle inside the page.",
                _zoneList);
        }
        return null;
    }

    private static bool IsUsableZone(RectangleF rect)
    {
        return float.IsFinite(rect.X) && float.IsFinite(rect.Y) &&
            float.IsFinite(rect.Width) && float.IsFinite(rect.Height) &&
            rect.X >= 0 && rect.Y >= 0 && rect.Width > 0 && rect.Height > 0 &&
            rect.X + rect.Width <= 1 && rect.Y + rect.Height <= 1;
    }

    private void ShowValidation(string message, Control focus)
    {
        _validationMessage.Text = message;
        _validationMessage.Visible = true;
        focus.Focus();
    }

    private void PickLlmModel()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose AI Model",
            Filters = { new FileFilter("GGUF model files", ".gguf"), new FileFilter("All files", ".*") }
        };
        if (dialog.ShowDialog(this) == DialogResult.Ok)
        {
            Config.User.Set(c => c.LlmModelPath, dialog.FileName);
            UpdateLlmModelLabel();
        }
    }

    private void UpdateLlmModelLabel()
    {
        var configured = Config.Get(c => c.LlmModelPath);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _llmModelLabel.Text = File.Exists(configured)
                ? $"Model: {Path.GetFileName(configured)}"
                : $"Model not found: {configured}";
        }
        else
        {
            _llmModelLabel.Text =
                $"No model selected. You can also drop a .gguf file into \"{LlmFieldNormalizer.DefaultModelsFolder}\".";
        }
    }

    private void UpdateZoneList()
    {
        _syncing = true;
        try
        {
            _zoneList.Items.Clear();
            foreach (var zone in _zones)
            {
                _zoneList.Items.Add(new ListItem { Text = zone.Name });
            }
            _zoneList.SelectedIndex = _selectedZoneIndex;
            _zoneName.Text = _selectedZoneIndex != -1 ? _zones[_selectedZoneIndex].Name : "";
            _zonePrompt.Text = _selectedZoneIndex != -1 ? _zones[_selectedZoneIndex].LlmPrompt ?? "" : "";
            _zoneExtractionMode.SelectedIndex = _selectedZoneIndex != -1
                ? (int) _zones[_selectedZoneIndex].ExtractionMode : 0;
            _barcodeFormat.SelectedIndex = _selectedZoneIndex != -1
                ? (int) _zones[_selectedZoneIndex].BarcodeFormat : 0;
            _barcodeFormat.Enabled = _selectedZoneIndex != -1 &&
                _zones[_selectedZoneIndex].ExtractionMode == OcrZoneExtractionMode.Barcode;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ZoneList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncing) return;
        _selectedZoneIndex = _zoneList.SelectedIndex;
        _syncing = true;
        _zoneName.Text = _selectedZoneIndex != -1 ? _zones[_selectedZoneIndex].Name : "";
        _zonePrompt.Text = _selectedZoneIndex != -1 ? _zones[_selectedZoneIndex].LlmPrompt ?? "" : "";
        _zoneExtractionMode.SelectedIndex = _selectedZoneIndex != -1
            ? (int) _zones[_selectedZoneIndex].ExtractionMode : 0;
        _barcodeFormat.SelectedIndex = _selectedZoneIndex != -1
            ? (int) _zones[_selectedZoneIndex].BarcodeFormat : 0;
        _barcodeFormat.Enabled = _selectedZoneIndex != -1 &&
            _zones[_selectedZoneIndex].ExtractionMode == OcrZoneExtractionMode.Barcode;
        _syncing = false;
        Overlay.Invalidate();
    }

    private void ZoneName_TextChanged(object? sender, EventArgs e)
    {
        if (_syncing || _selectedZoneIndex == -1) return;
        _zones[_selectedZoneIndex] = _zones[_selectedZoneIndex] with { Name = _zoneName.Text };
        _syncing = true;
        ((ListItem) _zoneList.Items[_selectedZoneIndex]).Text = _zoneName.Text;
        // Some platforms need a rebuild to visually refresh the item text
        var selected = _selectedZoneIndex;
        _zoneList.Items.Clear();
        foreach (var zone in _zones)
        {
            _zoneList.Items.Add(new ListItem { Text = zone.Name });
        }
        _zoneList.SelectedIndex = selected;
        _syncing = false;
        Overlay.Invalidate();
    }

    private void ZonePrompt_TextChanged(object? sender, EventArgs e)
    {
        if (_syncing || _selectedZoneIndex == -1) return;
        _zones[_selectedZoneIndex] = _zones[_selectedZoneIndex] with { LlmPrompt = _zonePrompt.Text };
    }

    private void ZoneExtractionMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncing || _selectedZoneIndex == -1 || _zoneExtractionMode.SelectedIndex < 0) return;
        var mode = (OcrZoneExtractionMode) _zoneExtractionMode.SelectedIndex;
        _zones[_selectedZoneIndex] = _zones[_selectedZoneIndex] with { ExtractionMode = mode };
        _barcodeFormat.Enabled = mode == OcrZoneExtractionMode.Barcode;
    }

    private void BarcodeFormat_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncing || _selectedZoneIndex == -1 || _barcodeFormat.SelectedIndex < 0) return;
        _zones[_selectedZoneIndex] = _zones[_selectedZoneIndex] with
        {
            BarcodeFormat = (OcrZoneBarcodeFormat) _barcodeFormat.SelectedIndex
        };
    }

    private void DeleteSelectedZone()
    {
        if (_selectedZoneIndex == -1) return;
        _zones.RemoveAt(_selectedZoneIndex);
        _selectedZoneIndex = -1;
        UpdateZoneList();
        Overlay.Invalidate();
    }

    private PointF ToFraction(PointF overlayPoint)
    {
        if (_overlayW <= 0 || _overlayH <= 0) return PointF.Empty;
        return new PointF(
            ((overlayPoint.X - _overlayL) / _overlayW).Clamp(0, 1),
            ((overlayPoint.Y - _overlayT) / _overlayH).Clamp(0, 1));
    }

    private RectangleF ToOverlayRect(RectangleF fractionRect)
    {
        return new RectangleF(
            _overlayL + fractionRect.X * _overlayW,
            _overlayT + fractionRect.Y * _overlayH,
            fractionRect.Width * _overlayW,
            fractionRect.Height * _overlayH);
    }

    private void Overlay_MouseDown(object? sender, MouseEventArgs e)
    {
        // Clicking inside an existing zone selects it; otherwise start drawing a new zone
        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            if (ToOverlayRect(_zones[i].Rect).Contains(e.Location))
            {
                _selectedZoneIndex = i;
                UpdateZoneList();
                Overlay.Invalidate();
                return;
            }
        }
        _dragging = true;
        _dragOrigin = e.Location;
        _dragCurrent = e.Location;
    }

    private void Overlay_MouseMove(object? sender, MouseEventArgs e)
    {
        Overlay.Cursor = Cursors.Crosshair;
        if (_dragging)
        {
            _dragCurrent = e.Location;
            Overlay.Invalidate();
        }
    }

    private void Overlay_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _dragCurrent = e.Location;
        if (Math.Abs(_dragCurrent.X - _dragOrigin.X) >= MIN_ZONE_SIZE &&
            Math.Abs(_dragCurrent.Y - _dragOrigin.Y) >= MIN_ZONE_SIZE)
        {
            var p1 = ToFraction(_dragOrigin);
            var p2 = ToFraction(_dragCurrent);
            var rect = new RectangleF(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p2.X - p1.X),
                Math.Abs(p2.Y - p1.Y));
            _zones.Add(new EditableZone($"Field{_zones.Count + 1}", rect));
            _selectedZoneIndex = _zones.Count - 1;
            UpdateZoneList();
            _zoneName.Focus();
            _zoneName.SelectAll();
        }
        Overlay.Invalidate();
    }

    protected override void PaintOverlay(object? sender, PaintEventArgs e)
    {
        base.PaintOverlay(sender, e);
        if (_overlayW <= 0 || _overlayH <= 0) return;

        var zoneColor = new Color(0.1f, 0.4f, 0.9f);
        var selectedColor = new Color(0.9f, 0.2f, 0.1f);
        for (int i = 0; i < _zones.Count; i++)
        {
            var rect = ToOverlayRect(_zones[i].Rect);
            bool selected = i == _selectedZoneIndex;
            var color = selected ? selectedColor : zoneColor;
            e.Graphics.FillRectangle(new Color(color, 0.15f), rect);
            e.Graphics.DrawRectangle(new Pen(color, selected ? 3 : 2), rect);
            e.Graphics.DrawText(SystemFonts.Default(), color,
                new PointF(rect.X + 3, rect.Y + 3), _zones[i].Name);
        }
        if (_dragging)
        {
            var rect = RectangleF.FromSides(
                Math.Min(_dragOrigin.X, _dragCurrent.X),
                Math.Min(_dragOrigin.Y, _dragCurrent.Y),
                Math.Max(_dragOrigin.X, _dragCurrent.X),
                Math.Max(_dragOrigin.Y, _dragCurrent.Y));
            e.Graphics.DrawRectangle(new Pen(zoneColor, 2), rect);
        }
    }
}
