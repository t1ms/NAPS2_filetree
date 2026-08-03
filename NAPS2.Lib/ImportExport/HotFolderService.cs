using System.Collections.Concurrent;
using System.Threading;
using NAPS2.Config;
using NAPS2.Ocr;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace NAPS2.ImportExport;

public interface IHotFolderService
{
    void Start();
    void Stop();
    bool IsActive { get; }
    int ProcessedCount { get; }
    int FailedCount { get; }
    string StatusText { get; }
    event EventHandler? StatusChanged;
}

/// <summary>
/// Watches a folder while NAPS2 is running and processes new PDF/image files through the same
/// importer and auto-save path used by scans. Files are only accepted after their size and write
/// time have stopped changing, so copier/network writes are not imported prematurely.
/// </summary>
public class HotFolderService : IHotFolderService, IDisposable
{
    private const int StabilityCheckDelayMs = 1500;
    private const int RequiredStableChecks = 2;

    private readonly Naps2Config _config;
    private readonly IProfileManager _profileManager;
    private readonly FileImporter _fileImporter;
    private readonly AutoSaver _autoSaver;
    private readonly ImageContext _imageContext;
    private readonly ConcurrentQueue<string> _pendingPaths = new();
    private readonly ConcurrentDictionary<string, byte> _queuedPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly object _sync = new();

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cancellation;
    private Task? _processorTask;
    private string? _watchFolder;
    private string? _destinationFolder;

    public HotFolderService(Naps2Config config, IProfileManager profileManager, ScanningContext scanningContext,
        AutoSaver autoSaver, ImageContext imageContext)
    {
        _config = config;
        _profileManager = profileManager;
        _fileImporter = new FileImporter(scanningContext);
        _autoSaver = autoSaver;
        _imageContext = imageContext;
    }

    public bool IsActive { get; private set; }
    public int ProcessedCount { get; private set; }
    public int FailedCount { get; private set; }
    public string StatusText { get; private set; } = "Hot folder is off";

    public event EventHandler? StatusChanged;

    public virtual void Start()
    {
        lock (_sync)
        {
            StopInternal();
            ProcessedCount = 0;
            FailedCount = 0;
            if (!_config.Get(c => c.EnableHotFolder))
            {
                UpdateStatus("Hot folder is off");
                return;
            }
            var folder = _config.Get(c => c.HotFolderPath);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                UpdateStatus("Hot folder is unavailable");
                return;
            }
            _watchFolder = Path.GetFullPath(folder);
            var destination = _config.Get(c => c.HotFolderDestinationPath);
            if (string.IsNullOrWhiteSpace(destination) || IsPathInsideOrEqual(destination, _watchFolder))
            {
                _watchFolder = null;
                UpdateStatus("Hot folder destination must be outside the watched folder");
                return;
            }
            _destinationFolder = Path.GetFullPath(destination);
            _cancellation = new CancellationTokenSource();
            _watcher = new FileSystemWatcher(_watchFolder)
            {
                IncludeSubdirectories = false,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Created += WatcherOnFileAppeared;
            _watcher.Changed += WatcherOnFileAppeared;
            _watcher.Renamed += WatcherOnRenamed;
            _processorTask = Task.Run(() => ProcessQueue(_cancellation.Token));
            IsActive = true;
            UpdateStatus("Hot folder active (0 processed, 0 failed)");

            // Process files already present when NAPS2 starts, which is useful after a restart.
            foreach (var path in Directory.EnumerateFiles(_watchFolder))
            {
                EnqueueIfSupported(path);
            }
        }
    }

    public virtual void Stop()
    {
        lock (_sync)
        {
            StopInternal();
            UpdateStatus("Hot folder is off");
        }
    }

    private void StopInternal()
    {
        IsActive = false;
        _watcher?.Dispose();
        _watcher = null;
        _cancellation?.Cancel();
        // Let a current file reach its lifecycle outcome (processed/failed plus activity log)
        // before forgetting the folder paths. This is bounded so application close is never stuck.
        try
        {
            _processorTask?.Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception)
        {
            // The service logs individual failures; shutdown should not prevent app close.
        }
        _cancellation?.Dispose();
        _cancellation = null;
        _processorTask = null;
        _watchFolder = null;
        _destinationFolder = null;
        while (_pendingPaths.TryDequeue(out _))
        {
        }
        _queuedPaths.Clear();
    }

    private void WatcherOnFileAppeared(object sender, FileSystemEventArgs e) => EnqueueIfSupported(e.FullPath);

    private void WatcherOnRenamed(object sender, RenamedEventArgs e) => EnqueueIfSupported(e.FullPath);

    private void EnqueueIfSupported(string path)
    {
        if (_watchFolder == null || IsLifecycleFolder(path) || IsUnderDestination(path) || !IsSupportedPath(path))
        {
            return;
        }
        // FileSystemWatcher can report Created and several Changed events. Only one worker item is
        // allowed per path while it is awaiting stability/processing.
        if (_queuedPaths.TryAdd(path, 0))
        {
            _pendingPaths.Enqueue(path);
            _queueSignal.Release();
        }
    }

    private bool IsLifecycleFolder(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        return parent != null && (string.Equals(Path.GetFileName(parent), "processed", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(Path.GetFileName(parent), "failed", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsUnderDestination(string path) =>
        _destinationFolder != null && IsPathInsideOrEqual(path, _destinationFolder);

    internal static bool IsPathInsideOrEqual(string path, string possibleParent)
    {
        string fullPath = NormalizePathForComparison(path);
        string fullParent = NormalizePathForComparison(possibleParent);
        return fullPath.Equals(fullParent, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullParent + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathForComparison(string path)
    {
        // Normalize separators before resolving the path so the check has the same containment
        // semantics for Windows-configured paths in cross-platform builds/tests.
        path = path.Replace('\\', '/');
        return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
    }

    internal static bool IsSupportedPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
               ImageContext.GetFileFormatFromExtension(path) != ImageFileFormat.Unknown;
    }

    private async Task ProcessQueue(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _queueSignal.WaitAsync(cancellationToken);
                if (!_pendingPaths.TryDequeue(out var path))
                {
                    continue;
                }
                try
                {
                    await ProcessOne(path, cancellationToken);
                }
                finally
                {
                    _queuedPaths.TryRemove(path, out _);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.ErrorException("Hot folder processing queue failed", ex);
            }
        }
    }

    private async Task ProcessOne(string sourcePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath) || !await WaitForStableFile(sourcePath, cancellationToken))
        {
            return;
        }
        try
        {
            var profile = ResolveProfile();
            if (profile?.AutoSaveSettings == null)
            {
                throw new InvalidOperationException(
                    "The selected hot-folder profile must have Auto Save configured.");
            }
            var destination = _config.Get(c => c.HotFolderDestinationPath);
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new InvalidOperationException("No hot-folder destination was configured.");
            }
            Directory.CreateDirectory(destination);
            var saveSettings = profile.AutoSaveSettings with
            {
                FilePath = CombineDestination(destination, profile.AutoSaveSettings.FilePath),
                PromptForFilePath = false,
                ClearImagesAfterSaving = true
            };
            var images = new List<ProcessedImage>();
            try
            {
                await foreach (var image in _fileImporter.Import(sourcePath).WithCancellation(cancellationToken))
                {
                    images.Add(image);
                }
                bool saved = await _autoSaver.SaveForHotFolder(saveSettings, images);
                if (!saved)
                {
                    throw new IOException("The document could not be saved.");
                }
            }
            finally
            {
                foreach (var image in images)
                {
                    image.Dispose();
                }
            }
            MoveToLifecycleFolder(sourcePath, "processed");
            ProcessedCount++;
            UpdateStatus($"Hot folder active ({ProcessedCount} processed, {FailedCount} failed)");
            WriteLog($"PROCESSED\t{sourcePath}");
        }
        catch (Exception ex)
        {
            Log.ErrorException($"Hot folder failed to process \"{sourcePath}\"", ex);
            try
            {
                if (File.Exists(sourcePath))
                {
                    MoveToLifecycleFolder(sourcePath, "failed");
                }
            }
            catch (Exception moveEx)
            {
                Log.ErrorException("Hot folder could not move failed source file", moveEx);
            }
            FailedCount++;
            UpdateStatus($"Hot folder active ({ProcessedCount} processed, {FailedCount} failed)");
            WriteLog($"FAILED\t{sourcePath}\t{ex.Message}");
        }
    }

    private ScanProfile? ResolveProfile()
    {
        string? configuredName = _config.Get(c => c.HotFolderProfileName);
        return _profileManager.Profiles.FirstOrDefault(p =>
                   string.Equals(p.DisplayName, configuredName, StringComparison.OrdinalIgnoreCase)) ??
               _profileManager.DefaultProfile;
    }

    private static string CombineDestination(string destination, string pattern)
    {
        // If the auto-save profile uses an absolute path, retain only the filename pattern.
        string name = Path.GetFileName(pattern);
        return Path.Combine(destination, string.IsNullOrWhiteSpace(name) ? "$(YYYY)-$(MM)-$(DD)_$(n).pdf" : name);
    }

    private async Task<bool> WaitForStableFile(string path, CancellationToken cancellationToken)
    {
        long previousLength = -1;
        DateTime previousWrite = DateTime.MinValue;
        int stableChecks = 0;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    return false;
                }
                // Exclusive read verifies the producer has released its write handle.
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                }
                if (info.Length == previousLength && info.LastWriteTimeUtc == previousWrite)
                {
                    stableChecks++;
                    if (stableChecks >= RequiredStableChecks)
                    {
                        return true;
                    }
                }
                else
                {
                    stableChecks = 0;
                    previousLength = info.Length;
                    previousWrite = info.LastWriteTimeUtc;
                }
            }
            catch (IOException)
            {
                stableChecks = 0;
            }
            catch (UnauthorizedAccessException)
            {
                stableChecks = 0;
            }
            await Task.Delay(StabilityCheckDelayMs, cancellationToken);
        }
        throw new IOException("The source file did not become stable in time.");
    }

    private void MoveToLifecycleFolder(string sourcePath, string folderName)
    {
        if (_watchFolder == null)
        {
            return;
        }
        string lifecycleFolder = Path.Combine(_watchFolder, folderName);
        Directory.CreateDirectory(lifecycleFolder);
        string destination = Path.Combine(lifecycleFolder, Path.GetFileName(sourcePath));
        if (File.Exists(destination))
        {
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            string ext = Path.GetExtension(sourcePath);
            destination = Path.Combine(lifecycleFolder, $"{name}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}");
        }
        File.Move(sourcePath, destination);
    }

    private void WriteLog(string line)
    {
        if (_watchFolder == null)
        {
            return;
        }
        try
        {
            File.AppendAllText(Path.Combine(_watchFolder, "hot-folder.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{line}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Log.ErrorException("Hot folder could not write activity log", ex);
        }
    }

    private void UpdateStatus(string text)
    {
        StatusText = text;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
        _queueSignal.Dispose();
    }
}