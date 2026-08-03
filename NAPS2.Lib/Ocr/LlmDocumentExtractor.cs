using System.Collections.Concurrent;
using System.Threading;
using NAPS2.Scan;

namespace NAPS2.Ocr;

/// <summary>
/// Fills the generic document tokens (DOC_DATE, DOC_SENDER, DOC_TYPE, DOC_REF) by running whole-page
/// OCR and asking the local LLM to extract each value. Results are cached per page so repeated saves
/// don't redo the (expensive) extraction. Failures degrade to null; they never block saving.
/// </summary>
public class LlmDocumentExtractor
{
    // Cap the OCR text passed to the small local model
    private const int MaxOcrTextLength = 4000;

    private readonly ScanningContext _scanningContext;
    private readonly Naps2Config _config;
    private readonly LlmFieldNormalizer _llm;

    // Keyed by the underlying image storage, which is shared across ProcessedImage clones
    private readonly ConcurrentDictionary<IImageStorage, Task<List<ZonalOcrField>?>> _cache = new();
    private const int MaxCachedPages = 200;

    public LlmDocumentExtractor(ScanningContext scanningContext, Naps2Config config, LlmFieldNormalizer llm)
    {
        _scanningContext = scanningContext;
        _config = config;
        _llm = llm;
    }

    public bool IsAvailable => _llm.IsEnabled && _scanningContext.OcrEngine != null;

    /// <summary>
    /// Extracts the generic document fields from the page. Returns null if extraction is
    /// unavailable or failed; individual fields that couldn't be determined have empty values.
    /// </summary>
    public Task<List<ZonalOcrField>?> ExtractGenericFields(ProcessedImage image, CancellationToken cancelToken)
    {
        if (!IsAvailable)
        {
            return Task.FromResult<List<ZonalOcrField>?>(null);
        }
        // The image storage is released by the rest of the scanning pipeline, but dictionary keys
        // would retain it. Keep this cache bounded: it is only a save-time optimization.
        if (_cache.Count >= MaxCachedPages)
        {
            _cache.Clear();
        }
        return _cache.GetOrAdd(image.Storage, _ =>
        {
            // Clone so the cached task isn't tied to the caller's image lifetime
            var clone = image.Clone();
            return Task.Run(async () =>
            {
                try
                {
                    return await ExtractInternal(clone, cancelToken);
                }
                catch (Exception ex)
                {
                    Log.ErrorException("LLM document extraction failed", ex);
                    return null;
                }
                finally
                {
                    clone.Dispose();
                }
            });
        });
    }

    private async Task<List<ZonalOcrField>?> ExtractInternal(ProcessedImage image, CancellationToken cancelToken)
    {
        string? text = await GetPageText(image, cancelToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (text!.Length > MaxOcrTextLength)
        {
            text = text.Substring(0, MaxOcrTextLength);
        }
        var fields = new List<ZonalOcrField>();
        foreach (var (name, description) in ContentPlaceholders.GenericTokens)
        {
            cancelToken.ThrowIfCancellationRequested();
            string value = "";
            try
            {
                var extracted = await _llm.NormalizeAsync(description, null, text, cancelToken);
                // NormalizeAsync returns the raw input on an empty completion; that means
                // "couldn't extract" here, not a usable value
                if (extracted != null && extracted != text)
                {
                    value = extracted.Trim();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.ErrorException($"LLM extraction failed for {name}", ex);
            }
            fields.Add(new ZonalOcrField(name, value));
        }
        return fields;
    }

    private async Task<string?> GetPageText(ProcessedImage image, CancellationToken cancelToken)
    {
        var engine = _scanningContext.OcrEngine;
        if (engine == null)
        {
            return null;
        }
        string tempPath = Path.Combine(_scanningContext.TempFolderPath, Path.GetRandomFileName() + ".png");
        try
        {
            using (var rendered = image.Render())
            {
                rendered.Save(tempPath, ImageFileFormat.Png);
            }
            var ocrResult = await engine.ProcessImage(_scanningContext, tempPath, GetOcrParams(), cancelToken);
            if (ocrResult == null)
            {
                return null;
            }
            return string.Join("\n", ocrResult.Lines.Select(l => l.Text.Trim())).Trim();
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception)
            {
                // Ignore temp file cleanup errors
            }
        }
    }

    private OcrParams GetOcrParams()
    {
        var ocrParams = _config.DefaultOcrParams();
        if (!string.IsNullOrEmpty(ocrParams.LanguageCode))
        {
            return ocrParams;
        }
        // OCR for searchable PDFs may be disabled; extraction still needs a language
        var lang = _config.Get(c => c.OcrLanguageCode);
        if (string.IsNullOrEmpty(lang))
        {
            lang = "eng";
        }
        var timeout = _config.Get(c => c.OcrTimeoutInSeconds);
        return new OcrParams(lang, OcrMode.Fast, timeout > 0 ? timeout : 120);
    }
}
