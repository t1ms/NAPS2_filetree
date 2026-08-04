using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Panel listing the field values extracted with zonal OCR for each scanned page,
/// with an option to export everything to CSV.
/// </summary>
public class ZonalOcrResultsForm : EtoDialogBase
{
    private readonly ZonalOcrResultsStore _store;
    private readonly GridView _grid = new();
    private readonly Label _notice = new() { Text = "", Visible = false };

    public ZonalOcrResultsForm(Naps2Config config, ZonalOcrResultsStore store)
        : base(config)
    {
        _store = store;
        Title = "Extracted Fields";
        IconName = "text_small";

        _grid.Columns.Add(new GridColumn
        {
            HeaderText = "Page",
            DataCell = new TextBoxCell { Binding = Binding.Property<Entry, string>(x => x.Page) },
            Width = 60
        });
        _grid.Columns.Add(new GridColumn
        {
            HeaderText = "Field",
            DataCell = new TextBoxCell { Binding = Binding.Property<Entry, string>(x => x.Field) },
            Width = 150
        });
        _grid.Columns.Add(new GridColumn
        {
            HeaderText = "Value",
            DataCell = new TextBoxCell { Binding = Binding.Property<Entry, string>(x => x.DisplayValue) },
            Width = 300
        });

        _store.ResultsUpdated += Store_ResultsUpdated;
    }

    private record Entry(string Page, string Field, string Value, string? ExtractionError)
    {
        /// <summary>
        /// Value shown in the grid cell. When extraction failed the cell shows a
        /// "⚠ Extraction error" prefix so users can distinguish a blank from a crash.
        /// </summary>
        public string DisplayValue => ExtractionError != null
            ? $"⚠ Extraction error: {ExtractionError}"
            : Value;
    }

    protected override void BuildLayout()
    {
        FormStateController.DefaultExtraLayoutSize = new Size(200, 200);

        LayoutController.Content = L.Column(
            _grid.Scale(),
            _notice,
            L.Row(
                C.Button("Export CSV...", ExportCsv),
                C.Button("Clear", () =>
                {
                    _store.Clear();
                    RefreshGrid();
                }),
                C.Filler(),
                C.Button("Close", () => Close())
            )
        );
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RefreshGrid();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _store.ResultsUpdated -= Store_ResultsUpdated;
    }

    private void Store_ResultsUpdated(object? sender, EventArgs e)
    {
        Invoker.Current.InvokeDispatch(RefreshGrid);
    }

    private void RefreshGrid()
    {
        var entries = new List<Entry>();
        string? notice = null;
        foreach (var result in _store.GetAll())
        {
            foreach (var field in result.Fields)
            {
                entries.Add(new Entry(result.PageNumber.ToString(), field.Name, field.Value, field.ExtractionError));
            }
            if (!string.IsNullOrEmpty(result.Notice))
            {
                notice = result.Notice;
            }
        }
        _grid.DataStore = entries;
        _notice.Text = notice ?? "";
        _notice.Visible = notice != null;
    }

    private void ExportCsv()
    {
        var results = _store.GetAll();
        if (results.Count == 0)
        {
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filters = { new FileFilter("CSV files", ".csv") },
            FileName = "extracted-fields.csv"
        };
        if (dialog.ShowDialog(this) == DialogResult.Ok)
        {
            try
            {
                var path = dialog.FileName;
                if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".csv";
                }
                ZonalOcrCsv.AppendRows(path, results);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error exporting zonal OCR CSV", ex);
            }
        }
    }
}
