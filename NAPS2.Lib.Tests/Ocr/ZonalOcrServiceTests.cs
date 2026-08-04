using NAPS2.Ocr;
using NAPS2.Sdk.Tests;
using NSubstitute;
using System.Threading;
using System.Collections.Immutable;
using Xunit;

namespace NAPS2.Lib.Tests.Ocr;

public class ZonalOcrServiceTests : ContextualTests
{
    [Fact]
    public async Task BarcodeZoneExtractsValueWithoutOcrEngine()
    {
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "ProductCode",
                    Left = 0,
                    Top = 0,
                    Width = 1,
                    Height = 1,
                    ExtractionMode = OcrZoneExtractionMode.Barcode,
                    BarcodeFormat = OcrZoneBarcodeFormat.UpcA
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("725272730706", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task BarcodeZoneUsesAnyFormatByDefault()
    {
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "ProductCode",
                    Width = 1,
                    Height = 1,
                    ExtractionMode = OcrZoneExtractionMode.Barcode
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("725272730706", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task BarcodeZoneReturnsEmptyWhenNoBarcodeIsFound()
    {
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.dog));
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "ProductCode",
                    Width = 1,
                    Height = 1,
                    ExtractionMode = OcrZoneExtractionMode.Barcode
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task ExistingZoneDefaultsToTextAndUsesOcrEngine()
    {
        var config = Naps2Config.Stub();
        SetUpFakeOcr(ifNoMatch: "printed value", delay: 0);
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.dog));
        var template = new OcrZoneTemplate
        {
            Name = "Text",
            Zones =
            [
                new OcrZone
                {
                    Name = "PrintedValue",
                    Width = 1,
                    Height = 1
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("printed value", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task MalformedZoneWithZeroDimensionsProducesEmptyField()
    {
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "ProductCode",
                    Left = 0,
                    Top = 0,
                    Width = 0,
                    Height = 0,
                    ExtractionMode = OcrZoneExtractionMode.Barcode
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task MalformedZoneWithNegativeDimensionsProducesEmptyField()
    {
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "ProductCode",
                    Left = 0,
                    Top = 0,
                    Width = -0.5,
                    Height = -0.5,
                    ExtractionMode = OcrZoneExtractionMode.Barcode
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task MalformedZoneWithOutOfRangeOriginProducesEmptyField()
    {
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        // Left > 1 means the zone starts past the right edge of the image; nothing to decode.
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "ProductCode",
                    Left = 1.5,
                    Top = 1.5,
                    Width = 0.5,
                    Height = 0.5,
                    ExtractionMode = OcrZoneExtractionMode.Barcode
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task PartiallyOverlappingZoneClipsToImageEdgeAndDecodes()
    {
        // Zone extends past the right/bottom edge. The visible portion should still be decoded.
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        // Width = 2.0 → clamped to 1.0, then further clipped to (1.0 - Left) of the image.
        // The barcode is centred in this image so a left-anchored over-wide zone still covers it.
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "ProductCode",
                    Left = 0,
                    Top = 0,
                    Width = 2.0,
                    Height = 2.0,
                    ExtractionMode = OcrZoneExtractionMode.Barcode,
                    BarcodeFormat = OcrZoneBarcodeFormat.UpcA
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("725272730706", Assert.Single(result.Fields).Value);
    }

    [Fact]
    public async Task MultiZoneTemplateWithOneMalformedZoneStillDecodesValidZone()
    {
        // The first zone is malformed (zero size); the second is a valid barcode zone.
        // Both fields must be present: first is empty, second carries the decoded value.
        var config = Naps2Config.Stub();
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        var template = new OcrZoneTemplate
        {
            Name = "Barcode",
            Zones =
            [
                new OcrZone
                {
                    Name = "BadZone",
                    Left = 0,
                    Top = 0,
                    Width = 0,
                    Height = 0,
                    ExtractionMode = OcrZoneExtractionMode.Barcode
                },
                new OcrZone
                {
                    Name = "ProductCode",
                    Left = 0,
                    Top = 0,
                    Width = 1,
                    Height = 1,
                    ExtractionMode = OcrZoneExtractionMode.Barcode,
                    BarcodeFormat = OcrZoneBarcodeFormat.UpcA
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Fields.Count);
        Assert.Equal("", result.Fields[0].Value);
        Assert.Equal("725272730706", result.Fields[1].Value);
    }

    [Fact]
    public async Task FailingZoneSetsExtractionErrorAndContinuesToNextZone()
    {
        // The OCR engine throws for the first (text) zone. The pipeline must:
        //   (a) set ExtractionError on that field instead of silently leaving it blank, and
        //   (b) still decode the second (barcode) zone successfully.
        var config = Naps2Config.Stub();
        var ocrMock = Substitute.For<IOcrEngine>();
        ocrMock.ProcessImage(ScanningContext, Arg.Any<string>(), Arg.Any<OcrParams>(), Arg.Any<CancellationToken>())
            .Returns<Task<OcrResult?>>(_ => throw new InvalidOperationException("simulated engine crash"));
        ScanningContext.OcrEngine = ocrMock;

        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));
        using var image = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.image_upc_barcode));
        var template = new OcrZoneTemplate
        {
            Name = "Mixed",
            Zones =
            [
                new OcrZone
                {
                    Name = "TextZone",
                    Left = 0,
                    Top = 0,
                    Width = 1,
                    Height = 1,
                    ExtractionMode = OcrZoneExtractionMode.Text
                },
                new OcrZone
                {
                    Name = "BarcodeZone",
                    Left = 0,
                    Top = 0,
                    Width = 1,
                    Height = 1,
                    ExtractionMode = OcrZoneExtractionMode.Barcode,
                    BarcodeFormat = OcrZoneBarcodeFormat.UpcA
                }
            ]
        };

        var result = await service.ExtractFields(image, template, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Fields.Count);

        var textField = result.Fields[0];
        Assert.Equal("TextZone", textField.Name);
        Assert.Equal("", textField.Value);
        Assert.NotNull(textField.ExtractionError);
        Assert.Contains("simulated engine crash", textField.ExtractionError);

        var barcodeField = result.Fields[1];
        Assert.Equal("BarcodeZone", barcodeField.Name);
        Assert.Equal("725272730706", barcodeField.Value);
        Assert.Null(barcodeField.ExtractionError);
    }

    [Fact]
    public void ActiveTemplateLookupIgnoresNameCasing()
    {
        var config = Naps2Config.Stub();
        config.User.Set(c => c.OcrZoneTemplates,
            ImmutableList.Create(new OcrZoneTemplate { Name = "Invoices" }));
        config.User.Set(c => c.ActiveOcrZoneTemplateName, "invoices");
        var service = new ZonalOcrService(ScanningContext, config, new ZonalOcrResultsStore(),
            new LlmFieldNormalizer(config));

        var activeTemplate = service.GetActiveTemplate();

        Assert.NotNull(activeTemplate);
        Assert.Equal("Invoices", activeTemplate.Name);
    }
}