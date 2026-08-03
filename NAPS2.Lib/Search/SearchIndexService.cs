using System.Threading;
using NAPS2.Pdf;

namespace NAPS2.Search;

/// <summary>
/// High-level service for indexing saved/exported documents into the local full-text search index
/// and querying it. The index is a single SQLite file in the app data folder; everything is local.
/// </summary>
public class SearchIndexService
{
    private readonly Lazy<SearchIndex> _index;

    public SearchIndexService() : this(Path.Combine(Paths.AppData, "search-index.db"))
    {
    }

    public SearchIndexService(string dbPath)
    {
        _index = new Lazy<SearchIndex>(() => new SearchIndex(dbPath));
    }

    public SearchIndex Index => _index.Value;

    /// <summary>
    /// Indexes a saved PDF by extracting its embedded text (e.g. from OCR). Returns true if any text was indexed.
    /// </summary>
    public bool TryIndexPdf(string path)
    {
        try
        {
            var pageTexts = new PdfiumPdfReader().ReadTextByPage(path).ToList();
            if (!pageTexts.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                return false;
            }
            Index.IndexDocument(Path.GetFullPath(path), pageTexts);
            return true;
        }
        catch (Exception ex)
        {
            Log.ErrorException($"Error indexing document for search: {path}", ex);
            return false;
        }
    }

    /// <summary>
    /// Indexes a PDF in the background after a save, so saving isn't slowed down.
    /// </summary>
    public void IndexPdfInBackground(string path)
    {
        Task.Run(() => TryIndexPdf(path));
    }

    /// <summary>
    /// Indexes all PDFs in a folder (recursively). Returns (indexed, skipped) counts.
    /// </summary>
    public (int indexed, int skipped) IndexFolder(string folderPath, Action<int, int>? progress = null,
        CancellationToken cancelToken = default)
    {
        var files = Directory.EnumerateFiles(folderPath, "*.pdf", SearchOption.AllDirectories).ToList();
        int indexed = 0, skipped = 0, done = 0;
        foreach (var file in files)
        {
            if (cancelToken.IsCancellationRequested) break;
            if (TryIndexPdf(file))
            {
                indexed++;
            }
            else
            {
                skipped++;
            }
            done++;
            progress?.Invoke(done, files.Count);
        }
        return (indexed, skipped);
    }

    /// <summary>
    /// Searches the index, pruning results whose files no longer exist on disk.
    /// </summary>
    public List<SearchResult> Search(string query, int limit = 100)
    {
        var results = Index.Search(query, limit);
        var missing = results.Select(r => r.Path).Distinct().Where(p => !File.Exists(p)).ToHashSet();
        foreach (var path in missing)
        {
            try
            {
                Index.DeleteDocument(path);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error pruning missing document from search index", ex);
            }
        }
        return results.Where(r => !missing.Contains(r.Path)).ToList();
    }

    /// <summary>
    /// Removes index entries for files that no longer exist. Returns the number pruned.
    /// </summary>
    public int PruneMissingFiles()
    {
        int pruned = 0;
        foreach (var path in Index.GetAllDocumentPaths())
        {
            if (!File.Exists(path))
            {
                Index.DeleteDocument(path);
                pruned++;
            }
        }
        return pruned;
    }
}
