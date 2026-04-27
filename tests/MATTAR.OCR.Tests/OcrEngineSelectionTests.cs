namespace MATTAR.OCR.Tests;

[TestFixture]
public class OcrEngineSelectionTests
{
    [Test]
    public void Factory_DefaultEngine_ReturnsTesseractService()
    {
        var rootPath = new TestOCRPath();
        var pdfToImageService = new PdfToImageService(rootPath);

        var service = PdfToTextServiceFactory.Create(rootPath, pdfToImageService);

        Assert.That(service, Is.InstanceOf<PdfToTextService>());
    }

    [Test]
    public void Factory_TesseractEngine_ReturnsTesseractService()
    {
        var rootPath = new TestOCRPath();
        var pdfToImageService = new PdfToImageService(rootPath);

        var service = PdfToTextServiceFactory.Create(
            rootPath, pdfToImageService, OcrEngine.Tesseract);

        Assert.That(service, Is.InstanceOf<PdfToTextService>());
    }

    [Test]
    public void Factory_HuggingFaceEngine_ReturnsHuggingFaceService()
    {
        var rootPath = new TestOCRPath();
        var pdfToImageService = new PdfToImageService(rootPath);

        var service = PdfToTextServiceFactory.Create(
            rootPath, pdfToImageService, OcrEngine.HuggingFace);

        Assert.That(service, Is.InstanceOf<HuggingFaceOcrService>());
    }

    [Test]
    public void Factory_AutoEngine_ReturnsIPdfToTextService()
    {
        var rootPath = new TestOCRPath();
        var pdfToImageService = new PdfToImageService(rootPath);

        var service = PdfToTextServiceFactory.Create(
            rootPath, pdfToImageService, OcrEngine.Auto);

        // Auto resolves to either engine – just assert the contract is fulfilled.
        Assert.That(service, Is.InstanceOf<IPdfToTextService>());
    }

    [Test]
    public void HuggingFaceOcrService_CanBeInstantiated()
    {
        var rootPath = new TestOCRPath();
        var pdfToImageService = new PdfToImageService(rootPath);

        Assert.DoesNotThrow(() =>
        {
            _ = new HuggingFaceOcrService(rootPath, pdfToImageService);
        });
    }
}