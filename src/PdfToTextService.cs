using MATTAR.OCR.Interfaces;
using Tesseract;

namespace MATTAR.Logistics.Server.Services
{
    public class PdfToTextService : IPdfToTextService
    {
        private readonly IOCRPath _path;
        private readonly IPdfToImageService _pdfToImage;

        public PdfToTextService(IOCRPath path, IPdfToImageService pdfToImage)
        {
            _path = path;
            _pdfToImage = pdfToImage;
        }

        public string Convert(string fileName)
        {
            string text = string.Empty;
            string tessdataPath = Path.Combine(_path.GetRootPath(), "tessdata");
            string path = Path.Combine(_path.GetTempPath(), fileName);
            var imagePaths = _pdfToImage.ConvertToImages(path);
            foreach(var imagePath in imagePaths)
            {
                using (var engine = new TesseractEngine(tessdataPath, "fra", EngineMode.Default))
                using (var image = Pix.LoadFromFile(imagePath))
                using (var page = engine.Process(image))
                    text += page.GetText();
            }

            return text;
        }
    }
}