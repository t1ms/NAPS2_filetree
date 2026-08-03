using NAPS2.Search;
using NAPS2.Sdk.Tests;
using Xunit;

namespace NAPS2.Lib.Tests.Search;

public class SearchIndexTests : ContextualTests
{
    private string NewDbPath() => Path.Combine(FolderPath, Path.GetRandomFileName() + ".db");

    [Fact]
    public void IndexAndSearch()
    {
        using var index = new SearchIndex(NewDbPath());
        index.IndexDocument("/docs/invoice.pdf", ["Invoice number 4562 from Acme Corp", "Payment terms net 30"]);
        index.IndexDocument("/docs/letter.pdf", ["Dear Sir, regarding your account"]);

        var results = index.Search("invoice 4562");
        Assert.Single(results);
        Assert.Equal("/docs/invoice.pdf", results[0].Path);
        Assert.Equal(1, results[0].Page);
        Assert.Contains("4562", results[0].Snippet);

        // Prefix matching
        Assert.Single(index.Search("acm"));
        // Second page
        var page2 = index.Search("payment terms");
        Assert.Single(page2);
        Assert.Equal(2, page2[0].Page);
        // No match
        Assert.Empty(index.Search("nonexistentterm"));
    }

    [Fact]
    public void ReindexReplacesOldText()
    {
        using var index = new SearchIndex(NewDbPath());
        index.IndexDocument("/docs/a.pdf", ["old contents here"]);
        index.IndexDocument("/docs/a.pdf", ["new contents here"]);

        Assert.Empty(index.Search("old"));
        Assert.Single(index.Search("new"));
        Assert.Equal(1, index.DocumentCount);
    }

    [Fact]
    public void DeleteDocument()
    {
        using var index = new SearchIndex(NewDbPath());
        index.IndexDocument("/docs/a.pdf", ["some text"]);
        index.DeleteDocument("/docs/a.pdf");

        Assert.Empty(index.Search("some"));
        Assert.Equal(0, index.DocumentCount);
        Assert.Empty(index.GetAllDocumentPaths());
    }

    [Fact]
    public void MalformedQueryIsSafe()
    {
        using var index = new SearchIndex(NewDbPath());
        index.IndexDocument("/docs/a.pdf", ["some text"]);

        // FTS5 syntax characters shouldn't cause errors
        Assert.Empty(index.Search("\"unbalanced AND (OR NOT"));
        Assert.Empty(index.Search("   "));
        Assert.Single(index.Search("\"some\" text*"));
    }

    [Fact]
    public void BlankPagesAreSkipped()
    {
        using var index = new SearchIndex(NewDbPath());
        index.IndexDocument("/docs/a.pdf", ["", "text on page two", "   "]);

        var results = index.Search("text");
        Assert.Single(results);
        Assert.Equal(2, results[0].Page);
    }
}
