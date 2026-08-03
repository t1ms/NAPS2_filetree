namespace NAPS2.Ocr;

public record ZonalOcrField(string Name, string Value);

public class ZonalOcrResult
{
    public int PageNumber { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string TemplateName { get; init; } = "";
    public List<ZonalOcrField> Fields { get; init; } = new();

    /// <summary>
    /// A user-visible notice about the extraction (e.g. AI cleanup was enabled but unavailable,
    /// so raw OCR text is shown).
    /// </summary>
    public string? Notice { get; init; }
}

/// <summary>
/// In-memory store of zonal OCR extraction results, keyed by the underlying image storage
/// (which is shared between clones of the same ProcessedImage).
/// </summary>
public class ZonalOcrResultsStore
{
    private readonly object _lock = new();
    private readonly Dictionary<object, ZonalOcrResult> _byStorage = new();
    private readonly Dictionary<object, Task<ZonalOcrResult?>> _pending = new();
    private readonly List<ZonalOcrResult> _ordered = new();
    private int _nextPageNumber = 1;

    public event EventHandler? ResultsUpdated;

    public int NextPageNumber()
    {
        lock (_lock)
        {
            return _nextPageNumber++;
        }
    }

    public void AddResult(ProcessedImage image, ZonalOcrResult result)
    {
        lock (_lock)
        {
            if (_byStorage.TryGetValue(image.Storage, out var existing))
            {
                var index = _ordered.IndexOf(existing);
                if (index != -1)
                {
                    _ordered[index] = result;
                }
                else
                {
                    _ordered.Add(result);
                }
            }
            else
            {
                _ordered.Add(result);
            }
            _byStorage[image.Storage] = result;
            _pending.Remove(image.Storage);
        }
        ResultsUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterPending(ProcessedImage image, Task<ZonalOcrResult?> task)
    {
        var storage = image.Storage;
        lock (_lock)
        {
            _pending[storage] = task;
        }
        // Ensure the pending entry is always cleaned up, even if the task fails or produces no result
        task.ContinueWith(t =>
        {
            lock (_lock)
            {
                if (_pending.TryGetValue(storage, out var current) && current == t)
                {
                    _pending.Remove(storage);
                }
            }
        }, TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// Returns true if a result already exists or an extraction is currently in progress for the image.
    /// </summary>
    public bool HasResultOrPending(ProcessedImage image)
    {
        lock (_lock)
        {
            return _byStorage.ContainsKey(image.Storage) || _pending.ContainsKey(image.Storage);
        }
    }

    public ZonalOcrResult? GetResult(ProcessedImage image)
    {
        lock (_lock)
        {
            return _byStorage.GetValueOrDefault(image.Storage);
        }
    }

    /// <summary>
    /// Gets the result for the given image, waiting for a pending extraction if one is in progress.
    /// </summary>
    public async Task<ZonalOcrResult?> WaitForResult(ProcessedImage image)
    {
        Task<ZonalOcrResult?>? pending;
        lock (_lock)
        {
            if (_byStorage.TryGetValue(image.Storage, out var result))
            {
                return result;
            }
            pending = _pending.GetValueOrDefault(image.Storage);
        }
        if (pending != null)
        {
            try
            {
                return await pending;
            }
            catch (Exception)
            {
                return null;
            }
        }
        return null;
    }

    public List<ZonalOcrResult> GetAll()
    {
        lock (_lock)
        {
            return _ordered.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _byStorage.Clear();
            _pending.Clear();
            _ordered.Clear();
            _nextPageNumber = 1;
        }
        ResultsUpdated?.Invoke(this, EventArgs.Empty);
    }
}
