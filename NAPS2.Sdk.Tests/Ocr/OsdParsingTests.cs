using NAPS2.Ocr;
using Xunit;

namespace NAPS2.Sdk.Tests.Ocr;

public class OsdParsingTests
{
    [Fact]
    public void ParseTypicalOsdOutput()
    {
        var output = """
            Page number: 0
            Orientation in degrees: 180
            Rotate: 180
            Orientation confidence: 9.51
            Script: Latin
            Script confidence: 4.06
            """;
        var result = TesseractOcrEngine.ParseOsdOutput(output);
        Assert.NotNull(result);
        Assert.Equal(180, result!.RotateDegrees);
        Assert.Equal(9.51, result.OrientationConfidence, 2);
    }

    [Fact]
    public void ParseUprightPage()
    {
        var output = "Rotate: 0\nOrientation confidence: 25.3\n";
        var result = TesseractOcrEngine.ParseOsdOutput(output);
        Assert.NotNull(result);
        Assert.Equal(0, result!.RotateDegrees);
    }

    [Fact]
    public void ParseFailureOutput()
    {
        // e.g. "Too few characters. Skipping this page" or missing osd.traineddata errors
        var output = "Error opening data file ./osd.traineddata\nToo few characters. Skipping this page\n";
        Assert.Null(TesseractOcrEngine.ParseOsdOutput(output));
    }

    [Fact]
    public void ParseRejectsNonRightAngleRotation()
    {
        var output = "Rotate: 45\nOrientation confidence: 12.0\n";
        Assert.Null(TesseractOcrEngine.ParseOsdOutput(output));
    }

    [Fact]
    public void ParseEmptyOutput()
    {
        Assert.Null(TesseractOcrEngine.ParseOsdOutput(""));
    }
}
