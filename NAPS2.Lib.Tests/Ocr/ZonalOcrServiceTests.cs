using NAPS2.Ocr;
using NAPS2.Sdk.Tests;
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