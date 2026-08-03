using NAPS2.Ocr;
using Xunit;

namespace NAPS2.Lib.Tests.Ocr;

public class ContentPlaceholdersTests
{
    [Fact]
    public void SubstitutesDollarParenTokens()
    {
        var fields = new List<ZonalOcrField>
        {
            new("VENDOR", "AcmeCorp"),
            new("INVOICE_NUM", "4562")
        };
        var result = ContentPlaceholders.SubstituteFieldTokens(
            @"C:\Scans\$(VENDOR)_Invoice-$(INVOICE_NUM).pdf", fields);
        Assert.Equal(@"C:\Scans\AcmeCorp_Invoice-4562.pdf", result);
    }

    [Fact]
    public void SubstitutesLegacyBraceTokens()
    {
        var fields = new List<ZonalOcrField> { new("Vendor", "Acme") };
        var result = ContentPlaceholders.SubstituteFieldTokens("{Vendor}.pdf", fields);
        Assert.Equal("Acme.pdf", result);
    }

    [Fact]
    public void TokenMatchingIsCaseInsensitive()
    {
        var fields = new List<ZonalOcrField> { new("vendor", "Acme") };
        var result = ContentPlaceholders.SubstituteFieldTokens("$(VENDOR).pdf", fields);
        Assert.Equal("Acme.pdf", result);
    }

    [Fact]
    public void SanitizesIllegalCharacters()
    {
        var fields = new List<ZonalOcrField> { new("F", "a/b\\c:d*e?f\"g<h>i|j") };
        var result = ContentPlaceholders.SubstituteFieldTokens("$(F).pdf", fields);
        Assert.Equal("a_b_c_d_e_f_g_h_i_j.pdf", result);
    }

    [Fact]
    public void EmptyValueUsesFallback()
    {
        var fields = new List<ZonalOcrField> { new("F", "   ") };
        var result = ContentPlaceholders.SubstituteFieldTokens("$(F).pdf", fields);
        Assert.Equal("Unknown.pdf", result);
    }

    [Fact]
    public void LongValueIsCapped()
    {
        var fields = new List<ZonalOcrField> { new("F", new string('x', 200)) };
        var result = ContentPlaceholders.SubstituteFieldTokens("$(F).pdf", fields);
        Assert.Equal(60 + ".pdf".Length, result.Length);
    }

    [Fact]
    public void ValueWithPlaceholderSyntaxCannotBeExpandedAgain()
    {
        var fields = new List<ZonalOcrField> { new("F", "$100 $(n)") };
        var result = ContentPlaceholders.SubstituteFieldTokens("$(F).pdf", fields);
        Assert.Equal("_100 _(n).pdf", result);
    }

    [Fact]
    public void FallbacksReplaceUnresolvedTokens()
    {
        var result = ContentPlaceholders.SubstituteFallbacks(
            "$(DOC_SENDER)_$(DOC_REF).pdf", ContentPlaceholders.GenericTokenNames);
        Assert.Equal("Unknown_Unknown.pdf", result);
    }

    [Fact]
    public void FallbacksLeaveDatePlaceholdersAlone()
    {
        var result = ContentPlaceholders.SubstituteFallbacks(
            "$(YYYY)-$(MM)-$(DD)_$(DOC_TYPE).pdf", ContentPlaceholders.GenericTokenNames);
        Assert.Equal("$(YYYY)-$(MM)-$(DD)_Unknown.pdf", result);
    }

    [Fact]
    public void ReservedZoneNameDoesNotOverrideBuiltInPlaceholder()
    {
        var fields = new List<ZonalOcrField> { new("YYYY", "not-a-year"), new("n", "not-a-number") };
        var result = ContentPlaceholders.SubstituteFieldTokens("$(YYYY)_$(n).pdf", fields);
        Assert.Equal("$(YYYY)_$(n).pdf", result);
    }

    [Fact]
    public void ContainsAnyTokenDetectsBothSyntaxes()
    {
        Assert.True(ContentPlaceholders.ContainsAnyToken("$(DOC_DATE).pdf",
            ContentPlaceholders.GenericTokenNames));
        Assert.True(ContentPlaceholders.ContainsAnyToken("{doc_date}.pdf",
            ContentPlaceholders.GenericTokenNames));
        Assert.False(ContentPlaceholders.ContainsAnyToken("$(YYYY).pdf",
            ContentPlaceholders.GenericTokenNames));
    }
}
