using NAPS2.ImportExport;
using Xunit;

namespace NAPS2.Lib.Tests.ImportExport;

public class HotFolderServiceTests
{
    [Theory]
    [InlineData("document.pdf")]
    [InlineData("document.PDF")]
    [InlineData("scan.jpg")]
    [InlineData("scan.JPEG")]
    [InlineData("scan.png")]
    [InlineData("scan.tiff")]
    public void AcceptsSupportedDocumentFiles(string path)
    {
        Assert.True(HotFolderService.IsSupportedPath(path));
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    [InlineData("document.docx")]
    [InlineData("")]
    public void IgnoresUnsupportedFiles(string path)
    {
        Assert.False(HotFolderService.IsSupportedPath(path));
    }

    [Theory]
    [InlineData(@"C:\inbox\out", @"C:\inbox")]
    [InlineData(@"C:\inbox", @"C:\inbox")]
    [InlineData(@"C:\inbox\processed\scan.pdf", @"C:\inbox")]
    public void DetectsDestinationInsideWatchFolder(string destination, string watchFolder)
    {
        Assert.True(HotFolderService.IsPathInsideOrEqual(destination, watchFolder));
    }

    [Theory]
    [InlineData(@"C:\output", @"C:\inbox")]
    [InlineData(@"C:\inbox-archive", @"C:\inbox")]
    public void AllowsDestinationOutsideWatchFolder(string destination, string watchFolder)
    {
        Assert.False(HotFolderService.IsPathInsideOrEqual(destination, watchFolder));
    }

    [Fact]
    public void NormalizesFileUriToExistingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "naps2-hot-folder-test");
        Directory.CreateDirectory(path);
        try
        {
            var uri = new Uri(path).AbsoluteUri;
            Assert.True(HotFolderService.TryGetExistingDirectory(uri, out var normalized));
            Assert.Equal(Path.GetFullPath(path), normalized);
        }
        finally
        {
            Directory.Delete(path);
        }
    }

    [Fact]
    public void RejectsMissingConfiguredDirectory()
    {
        Assert.False(HotFolderService.TryGetExistingDirectory(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), out _));
    }
}
