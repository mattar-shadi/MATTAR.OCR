namespace MATTAR.OCR.Tests
{
    [TestFixture]
    public class SmpleTests
    {
        IPdfToImageService _pdfToImageService = default!;
        IPdfToTextService _pdfToTextService = default!;
        TestOCRPath _rootPath = default!;

        [SetUp]
        public void Setup()
        {
            _rootPath = new TestOCRPath();
            _pdfToImageService = new PdfToImageService(_rootPath);
            _pdfToTextService = new PdfToTextService(_rootPath, _pdfToImageService);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up generated images from the temp directory after each test
            var tempPath = _rootPath.GetTempPath();
            foreach (var file in Directory.GetFiles(tempPath, "*.png"))
                File.Delete(file);
            foreach (var file in Directory.GetFiles(tempPath, "*.pdf"))
                File.Delete(file);
        }

        [Test]
        public void SimpleTest()
        {
            Assert.That(_pdfToImageService, Is.Not.Null);
            Assert.That(_pdfToTextService, Is.Not.Null);
        }

        [Test]
        public void ConvertToImages_WithValidPdf_ReturnsImagePaths()
        {
            // Arrange: write a minimal one-page PDF to the temp directory
            string tempPath = _rootPath.GetTempPath();
            string pdfPath = Path.Combine(tempPath, "test_input.pdf");
            File.WriteAllBytes(pdfPath, CreateMinimalOnePagePdf());

            // Act
            var imagePaths = _pdfToImageService.ConvertToImages(pdfPath);

            // Assert: at least one image was produced and the file exists on disk
            Assert.That(imagePaths, Is.Not.Empty);
            Assert.That(imagePaths.Count, Is.EqualTo(1));
            Assert.That(File.Exists(imagePaths[0]), Is.True);
        }

        /// <summary>
        /// Returns a minimal valid single-page PDF as a byte array.
        /// The offsets are pre-calculated for this exact content.
        /// </summary>
        private static byte[] CreateMinimalOnePagePdf()
        {
            const string pdf =
                "%PDF-1.4\n" +
                "1 0 obj\n<</Type /Catalog /Pages 2 0 R>>\nendobj\n" +
                "2 0 obj\n<</Type /Pages /Kids [3 0 R] /Count 1>>\nendobj\n" +
                "3 0 obj\n<</Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]>>\nendobj\n" +
                "xref\n0 4\n" +
                "0000000000 65535 f \n" +
                "0000000009 00000 n \n" +
                "0000000058 00000 n \n" +
                "0000000115 00000 n \n" +
                "trailer\n<</Size 4 /Root 1 0 R>>\n" +
                "startxref\n190\n%%EOF\n";
            return System.Text.Encoding.ASCII.GetBytes(pdf);
        }
    }
}