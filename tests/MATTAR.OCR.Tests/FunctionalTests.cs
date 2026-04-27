namespace MATTAR.OCR.Tests;

[TestFixture]
[Ignore("Functional test - requires actual OCR processing and file I/O. Run manually when needed.")]
public class FunctionalTests
{
    IPdfToTextService _pdfToTextService;

    [SetUp]
    public void Setup()
    {
        var rootPath = new TestOCRPath();
        var pdfToImageService = new PdfToImageService(rootPath);
        _pdfToTextService = new PdfToTextService(rootPath, pdfToImageService);
    }

    [Test]
    public void SimpleTest()
    {
        var path = _pdfToTextService.Convert("");
        Assert.Pass();
    }
}