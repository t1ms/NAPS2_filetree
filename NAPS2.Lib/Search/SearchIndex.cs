using Microsoft.Data.Sqlite;

namespace NAPS2.Search;

/// <summary>
/// SQLite FTS5-backed full-text index of scanned document text, keyed by file path and page number.
/// </summary>
public class SearchIndex : IDisposable
{
    private readonly string _dbPath;
    private readonly object _lock = new();
    private SqliteConnection? _connection;

    public SearchIndex(string dbPath)
    {
        _dbPath = dbPath;
    }

    private SqliteConnection Connection
    {
        get
        {
            if (_connection == null)
            {
                var connection = new SqliteConnection($"Data Source={_dbPath}");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    """
                    CREATE VIRTUAL TABLE IF NOT EXISTS pages USING fts5(path UNINDEXED, page UNINDEXED, text);
                    CREATE TABLE IF NOT EXISTS documents (path TEXT PRIMARY KEY, indexed_at TEXT NOT NULL);
                    """;
                cmd.ExecuteNonQuery();
                _connection = connection;
            }
            return _connection;
        }
    }

    /// <summary>
    /// Adds or replaces the indexed text for a document. Pages with no text are skipped.
    /// </summary>
    public void IndexDocument(string path, IEnumerable<string> pageTexts)
    {
        lock (_lock)
        {
            using var transaction = Connection.BeginTransaction();
            DeleteDocumentInternal(path, transaction);
            int page = 0;
            int indexedPages = 0;
            foreach (var text in pageTexts)
            {
                page++;
                if (string.IsNullOrWhiteSpace(text)) continue;
                using var insert = Connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO pages (path, page, text) VALUES ($path, $page, $text)";
                insert.Parameters.AddWithValue("$path", path);
                insert.Parameters.AddWithValue("$page", page);
                insert.Parameters.AddWithValue("$text", text);
                insert.ExecuteNonQuery();
                indexedPages++;
            }
            if (indexedPages > 0)
            {
                using var upsert = Connection.CreateCommand();
                upsert.Transaction = transaction;
                upsert.CommandText =
                    "INSERT INTO documents (path, indexed_at) VALUES ($path, $now) " +
                    "ON CONFLICT(path) DO UPDATE SET indexed_at = $now";
                upsert.Parameters.AddWithValue("$path", path);
                upsert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
                upsert.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public void DeleteDocument(string path)
    {
        lock (_lock)
        {
            using var transaction = Connection.BeginTransaction();
            DeleteDocumentInternal(path, transaction);
            transaction.Commit();
        }
    }

    private void DeleteDocumentInternal(string path, SqliteTransaction transaction)
    {
        using var deletePages = Connection.CreateCommand();
        deletePages.Transaction = transaction;
        deletePages.CommandText = "DELETE FROM pages WHERE path = $path";
        deletePages.Parameters.AddWithValue("$path", path);
        deletePages.ExecuteNonQuery();
        using var deleteDoc = Connection.CreateCommand();
        deleteDoc.Transaction = transaction;
        deleteDoc.CommandText = "DELETE FROM documents WHERE path = $path";
        deleteDoc.Parameters.AddWithValue("$path", path);
        deleteDoc.ExecuteNonQuery();
    }

    /// <summary>
    /// Searches the index, returning ranked results with matched-text snippets.
    /// </summary>
    public List<SearchResult> Search(string query, int limit = 100)
    {
        var ftsQuery = BuildFtsQuery(query);
        if (ftsQuery == null) return [];
        lock (_lock)
        {
            using var cmd = Connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT path, page, snippet(pages, 2, '[', ']', '…', 14)
                FROM pages WHERE pages MATCH $query ORDER BY rank LIMIT $limit
                """;
            cmd.Parameters.AddWithValue("$query", ftsQuery);
            cmd.Parameters.AddWithValue("$limit", limit);
            var results = new List<SearchResult>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new SearchResult(reader.GetString(0), reader.GetInt32(1), reader.GetString(2)));
            }
            return results;
        }
    }

    /// <summary>
    /// Turns free-form user input into a safe FTS5 query: each whitespace-separated token becomes a
    /// quoted prefix term, combined with implicit AND. Returns null if there are no usable tokens.
    /// </summary>
    private static string? BuildFtsQuery(string query)
    {
        var tokens = query.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Replace("\"", ""))
            .Where(t => t.Length > 0)
            .Select(t => $"\"{t}\"*")
            .ToList();
        return tokens.Count == 0 ? null : string.Join(" ", tokens);
    }

    public List<string> GetAllDocumentPaths()
    {
        lock (_lock)
        {
            using var cmd = Connection.CreateCommand();
            cmd.CommandText = "SELECT path FROM documents";
            var paths = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                paths.Add(reader.GetString(0));
            }
            return paths;
        }
    }

    public int DocumentCount
    {
        get
        {
            lock (_lock)
            {
                using var cmd = Connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM documents";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}

public record SearchResult(string Path, int Page, string Snippet);
