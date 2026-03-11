using PDFtoImage;
using MATTAR.OCR.Interfaces;
using SkiaSharp;

namespace MATTAR.OCR
{
    public class PdfToImageService : IPdfToImageService
    {
        private readonly IOCRPath _path;

        public PdfToImageService(IOCRPath rootPath)
        {
            _path = rootPath;
        }

        public List<string> ConvertToImages(string pdfFilePath)
        {
            var result = new List<string>();
            string outImageName = Path.GetFileNameWithoutExtension(pdfFilePath);

            using var stream = File.OpenRead(pdfFilePath);
            int pageCount = Conversion.GetPageCount(stream);

            for (int i = 0; i < pageCount; i++)
            {
                stream.Position = 0;
                using SKBitmap bitmap = Conversion.ToImage(stream, page: i, options: new RenderOptions(Dpi: 300));
                string pagePath = $"{outImageName}_page{i + 1}.png";
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(pagePath, data.ToArray());
                result.Add(pagePath);
            }

            return result;
        }

        public string ConvertToImage(string InputPDFFile)
        {
            string pathFile = Path.Combine(_path.GetRootPath(), InputPDFFile);
            string outImageName = Path.GetFileNameWithoutExtension(pathFile);
            string imgPath = Path.Combine(_path.GetRootPath(), $"{outImageName}.png");

            using var stream = File.OpenRead(pathFile);
            using SKBitmap bitmap = Conversion.ToImage(stream, page: 0, options: new RenderOptions(Dpi: 290));
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(imgPath, data.ToArray());

            return imgPath;
        }
    }
}
