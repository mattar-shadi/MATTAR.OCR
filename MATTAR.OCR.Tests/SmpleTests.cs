namespace MATTAR.OCR.Tests
{
    [TestFixture]
    public class SmpleTests
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
}