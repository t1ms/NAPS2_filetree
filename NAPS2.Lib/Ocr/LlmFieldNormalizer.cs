using System.Threading;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace NAPS2.Ocr;

/// <summary>
/// Cleans up noisy zonal OCR field values using a small local LLM (via llama.cpp / LLamaSharp,
/// CPU-only). The user drops a .gguf model file into the models folder (or picks a file
/// explicitly), and the model is lazy-loaded on first use. If no model is available or loading
/// fails, callers fall back to the raw OCR text - normalization never blocks the scan pipeline.
/// </summary>
public class LlmFieldNormalizer : IDisposable
{
    public const string DefaultPromptTemplate = "Extract the {FieldType} from this OCR text. Return only the value.";

    private const int MaxOutputTokens = 48;
    private const int InferenceTimeoutSeconds = 60;

    private readonly Naps2Config _config;
    private readonly object _loadLock = new();
    // llama.cpp inference is CPU-heavy; run one completion at a time
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);

    private LLamaWeights? _model;
    private ModelParams? _modelParams;
    private string? _loadedPath;
    private string? _failedPath;
    private bool _disposed;

    public LlmFieldNormalizer(Naps2Config config)
    {
        _config = config;
    }

    public bool IsEnabled => _config.Get(c => c.EnableLlmFieldCleanup);

    /// <summary>
    /// If normalization is unavailable (no model file, load failure), explains why.
    /// </summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>
    /// The folder scanned for .gguf model files when no explicit model path is configured.
    /// </summary>
    public static string DefaultModelsFolder => Path.Combine(Paths.AppData, "models");

    /// <summary>
    /// Resolves the model file to use: the explicitly configured path if it exists, otherwise
    /// the first .gguf file found in the default models folder.
    /// </summary>
    public string? ResolveModelPath()
    {
        var configured = _config.Get(c => c.LlmModelPath);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? configured : null;
        }
        try
        {
            if (Directory.Exists(DefaultModelsFolder))
            {
                return Directory.EnumerateFiles(DefaultModelsFolder, "*.gguf")
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error scanning LLM models folder", ex);
        }
        return null;
    }

    /// <summary>
    /// Normalizes a raw OCR field value with the local LLM. Returns the cleaned value, or null
    /// if normalization is unavailable or failed (callers should fall back to the raw value and
    /// surface UnavailableReason as a notice).
    /// </summary>
    public async Task<string?> NormalizeAsync(string fieldName, string? promptOverride, string rawValue,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return rawValue;
        }
        var executor = GetExecutor();
        if (executor == null)
        {
            return null;
        }
        var template = string.IsNullOrWhiteSpace(promptOverride) ? DefaultPromptTemplate : promptOverride!;
        var instruction = template.Replace("{FieldType}", fieldName);
        var prompt = $"{instruction}\n\nOCR text:\n{rawValue}\n\nValue:";
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(InferenceTimeoutSeconds));
            await _inferenceLock.WaitAsync(timeoutCts.Token);
            try
            {
                var inferenceParams = new InferenceParams
                {
                    MaxTokens = MaxOutputTokens,
                    AntiPrompts = new List<string> { "\n" },
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = 0.1f
                    }
                };
                var sb = new System.Text.StringBuilder();
                await foreach (var token in executor.InferAsync(prompt, inferenceParams, timeoutCts.Token))
                {
                    sb.Append(token);
                }
                var cleaned = CleanOutput(sb.ToString());
                // An empty completion isn't useful; keep the raw OCR text in that case
                return string.IsNullOrWhiteSpace(cleaned) ? rawValue : cleaned;
            }
            finally
            {
                _inferenceLock.Release();
            }
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.ErrorException($"LLM cleanup failed for field \"{fieldName}\"", ex);
            UnavailableReason = "AI cleanup failed; showing raw OCR text.";
            return null;
        }
    }

    private static string CleanOutput(string output)
    {
        var value = output.Trim();
        // Take just the first line in case the model rambles
        var newline = value.IndexOfAny(new[] { '\r', '\n' });
        if (newline != -1)
        {
            value = value.Substring(0, newline).Trim();
        }
        return value.Trim('"', '\'', '`', ' ');
    }

    private StatelessExecutor? GetExecutor()
    {
        lock (_loadLock)
        {
            if (_disposed)
            {
                return null;
            }
            var path = ResolveModelPath();
            if (path == null)
            {
                UnavailableReason =
                    $"No AI model found. Drop a .gguf model file into \"{DefaultModelsFolder}\" or pick one in the OCR Field Zones dialog.";
                return null;
            }
            if (_model != null && _loadedPath == path)
            {
                return new StatelessExecutor(_model, _modelParams!);
            }
            if (_failedPath == path)
            {
                // Don't retry a failing load for every field/page
                return null;
            }
            try
            {
                _model?.Dispose();
                _model = null;
                _loadedPath = null;
                var modelParams = new ModelParams(path)
                {
                    ContextSize = 2048,
                    GpuLayerCount = 0
                };
                Log.Info($"Loading local LLM for OCR cleanup: {path}");
                _model = LLamaWeights.LoadFromFile(modelParams);
                _modelParams = modelParams;
                _loadedPath = path;
                _failedPath = null;
                UnavailableReason = null;
                return new StatelessExecutor(_model, _modelParams);
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Failed to load LLM model \"{path}\"", ex);
                _failedPath = path;
                UnavailableReason = $"Couldn't load AI model \"{Path.GetFileName(path)}\"; showing raw OCR text.";
                return null;
            }
        }
    }

    public void Dispose()
    {
        lock (_loadLock)
        {
            _disposed = true;
            _model?.Dispose();
            _model = null;
        }
    }
}
