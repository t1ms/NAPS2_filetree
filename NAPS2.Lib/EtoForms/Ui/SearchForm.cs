using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.Search;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Full-text search dialog: type a query to instantly find any indexed scanned document by its
/// content, with matched-text snippets. Also allows indexing an existing folder of PDFs.
/// </summary>
public class SearchForm : EtoDialogBase
{
    private readonly SearchIndexService _searchIndexService;
    private readonly TextBox _queryBox = new();
    private readonly GridView _grid = new();
    private readonly Label _status = new() { Text = "" };
    private List<SearchResult> _results = [];
    private readonly UITimer _debounceTimer = new() { Interval = 0.25 };
    private int _searchSequence;

    public SearchForm(Naps2Config config, SearchIndexService searchIndexService)
        : base(config)
    {
        _searchIndexService = searchIndexService;
        Title = "Search Scanned Documents";
        IconName = "zoom_small";

        _grid.Columns.Add(new GridColumn
        {
            HeaderText = "File",
            DataCell = new TextBoxCell { Binding = Binding.Property<SearchResult, string>(x => Path.GetFileName(x.Path)) },
            Width = 180
        });
        _grid.Columns.Add(new GridColumn
        {
            HeaderText = "Page",
            DataCell = new TextBoxCell
            {
                Binding = Binding.Property<SearchResult, string>(x => x.Page.ToString())
            },
            Width = 50
        });
        _grid.Columns.Add(new GridColumn
        {
            HeaderText = "Matched Text",
            DataCell = new TextBoxCell { Binding = Binding.Property<SearchResult, string>(x => x.Snippet) },
            Width = 280
        });
        _grid.Columns.Add(new GridColumn
        {
            HeaderText = "Folder",
            DataCell = new TextBoxCell
            {
                Binding = Binding.Property<SearchResult, string>(x => Path.GetDirectoryName(x.Path) ?? "")
            },
            Width = 220
        });
        _grid.CellDoubleClick += (_, _) => OpenSelectedFile();
        _queryBox.KeyDown += (_, e) =>
        {
            if (e.Key == Keys.Enter)
            {
                e.Handled = true;
                RunSearch();
            }
        };
        _debounceTimer.Elapsed += (_, _) =>
        {
            _debounceTimer.Stop();
            RunSearch();
        };
        _queryBox.TextChanged += (_, _) =>
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        };
    }

    protected override void BuildLayout()
    {
        FormStateController.DefaultExtraLayoutSize = new Size(350, 250);

        LayoutController.Content = L.Column(
            L.Row(
                _queryBox.Scale(),
                C.Button("Search", RunSearch)
            ),
            _grid.Scale(),
            _status,
            L.Row(
                C.Button("Open File", OpenSelectedFile),
                C.Button("Open Folder", OpenSelectedFolder),
                C.Filler(),
                C.Button("Index Folder...", IndexFolder),
                C.Button("Close", () => Close())
            )
        );
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateStatus();
        _queryBox.Focus();
    }

    private void UpdateStatus()
    {
        try
        {
            _status.Text = $"{_searchIndexService.Index.DocumentCount} document(s) in the search index";
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error reading search index", ex);
            _status.Text = "Search index unavailable";
        }
    }

    private void RunSearch()
    {
        var query = _queryBox.Text;
        var sequence = ++_searchSequence;
        if (string.IsNullOrWhiteSpace(query))
        {
            _results = [];
            _grid.DataStore = _results;
            UpdateStatus();
            return;
        }
        // Run the query off the UI thread so the dialog stays responsive even if the index is busy
        Task.Run(() =>
        {
            try
            {
                var results = _searchIndexService.Search(query);
                Invoker.Current.InvokeDispatch(() =>
                {
                    if (sequence != _searchSequence) return; // A newer search superseded this one
                    _results = results;
                    _grid.DataStore = _results;
                    _status.Text = _results.Count == 0
                        ? "No matches"
                        : $"{_results.Count} matching page(s)";
                });
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error searching document index", ex);
                Invoker.Current.InvokeDispatch(() =>
                {
                    if (sequence != _searchSequence) return;
                    _status.Text = "Search error - see log for details";
                });
            }
        });
    }

    private SearchResult? SelectedResult => _grid.SelectedItem as SearchResult ?? _results.FirstOrDefault();

    private void OpenSelectedFile()
    {
        var result = SelectedResult;
        if (result == null) return;
        if (!File.Exists(result.Path))
        {
            _status.Text = "File no longer exists; it will be removed from the index on the next search.";
            return;
        }
        try
        {
            ProcessHelper.OpenFile(result.Path);
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error opening file", ex);
        }
    }

    private void OpenSelectedFolder()
    {
        var result = SelectedResult;
        if (result == null) return;
        var folder = Path.GetDirectoryName(result.Path);
        if (folder == null || !Directory.Exists(folder)) return;
        try
        {
            ProcessHelper.OpenFolder(folder);
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error opening folder", ex);
        }
    }

    private void IndexFolder()
    {
        var dialog = new SelectFolderDialog { Title = "Select a folder of PDFs to index" };
        if (dialog.ShowDialog(this) != DialogResult.Ok) return;
        var folder = dialog.Directory;
        _status.Text = "Indexing...";
        Task.Run(() =>
        {
            try
            {
                var (indexed, skipped) = _searchIndexService.IndexFolder(folder,
                    (done, total) => Invoker.Current.InvokeDispatch(() =>
                        _status.Text = $"Indexing... {done}/{total}"));
                Invoker.Current.InvokeDispatch(() =>
                {
                    _status.Text = $"Indexed {indexed} document(s), skipped {skipped} with no text";
                    RunSearch();
                });
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error indexing folder", ex);
                Invoker.Current.InvokeDispatch(() => _status.Text = "Error indexing folder - see log for details");
            }
        });
    }
}
