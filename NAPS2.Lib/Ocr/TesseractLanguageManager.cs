using NAPS2.Dependencies;

namespace NAPS2.Ocr;

public class TesseractLanguageManager
{
    private static readonly List<DownloadMirror> Mirrors =
    [
        new(@"https://github.com/cyanfish/naps2-components/releases/download/tesseract-4.0.0b4/{0}"),
        new(@"https://sourceforge.net/projects/naps2/files/components/tesseract-4.0.0b4/{0}/download")
    ];

    private readonly TesseractLanguageData _languageData = TesseractLanguageData.Latest;

    public TesseractLanguageManager(string basePath)
    {
        TessdataBasePath = GetTessdataBasePath(basePath);
        LanguageComponents = _languageData.Data.Select(x =>
            new MultiFileExternalComponent($"ocr-{x.Code}", TessdataBasePath,
                new[] { $"best/{x.Code}.traineddata", $"fast/{x.Code}.traineddata" },
                new DownloadInfo(x.Filename, Mirrors, x.Size, x.Sha256, DownloadFormat.Zip)));
        OsdComponent = new MultiFileExternalComponent("ocr-osd", TessdataBasePath,
            new[] { "best/osd.traineddata", "fast/osd.traineddata" },
            new DownloadInfo("osd.traineddata.zip", Mirrors, 8.22,
                "e37afe697de9de3ae40285773dc3f5e7983a2e263026871ecc6d072678e8b36a", DownloadFormat.Zip));
    }

    private string GetTessdataBasePath(string basePath)
    {
        var newBasePath = Path.Combine(basePath, "tesseract4");
        var legacyBasePath = Path.Combine(basePath, "tesseract-4.0.0b4");
        if (Directory.Exists(newBasePath))
        {
            return newBasePath;
        }
        if (Directory.Exists(legacyBasePath))
        {
            return legacyBasePath;
        }
        return newBasePath;
    }

    public string TessdataBasePath { get; }

    public virtual IEnumerable<Language> InstalledLanguages =>
        LanguageComponents.Where(x => x.IsInstalled).Select(x => _languageData.LanguageMap[x.Id]);

    public virtual IEnumerable<Language> NotInstalledLanguages =>
        LanguageComponents.Where(x => !x.IsInstalled).Select(x => _languageData.LanguageMap[x.Id]);

    public Language GetLanguage(string code) => _languageData.LanguageMap["ocr-" + code];

    public IEnumerable<IExternalComponent> LanguageComponents { get; }

    /// <summary>
    /// The osd.traineddata component used for orientation detection (auto-rotate).
    /// </summary>
    public IExternalComponent OsdComponent { get; }

    public virtual bool IsOsdInstalled => OsdComponent.IsInstalled;
}