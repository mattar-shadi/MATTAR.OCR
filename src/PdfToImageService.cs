using Docnet.Core;
using Docnet.Core.Models;
using MATTAR.OCR.Interfaces;
using SkiaSharp;
using System.Runtime.InteropServices;

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
            double scale = 300.0 / 72.0;

            using var library = DocLib.Instance;
            using var docReader = library.GetDocReader(pdfFilePath, new PageDimensions(scale));
            int pageCount = docReader.GetPageCount();

            for (int i = 0; i < pageCount; i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                string pagePath = $"{outImageName}_page{i + 1}.png";
                SavePage(pageReader, pagePath);
                result.Add(pagePath);
            }

            return result;
        }

        public string ConvertToImage(string InputPDFFile)
        {
            string pathFile = Path.Combine(_path.GetRootPath(), InputPDFFile);
            string outImageName = Path.GetFileNameWithoutExtension(pathFile);
            string imgPath = Path.Combine(_path.GetRootPath(), $"{outImageName}.png");
            double scale = 290.0 / 72.0;

            using var library = DocLib.Instance;
            using var docReader = library.GetDocReader(pathFile, new PageDimensions(scale));
            using var pageReader = docReader.GetPageReader(0);
            SavePage(pageReader, imgPath);

            return imgPath;
        }

        private static void SavePage(Docnet.Core.Readers.IPageReader pageReader, string filePath)
        {
            byte[] rawBytes = pageReader.GetImage();
            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();

            using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            Marshal.Copy(rawBytes, 0, bitmap.GetPixels(), rawBytes.Length);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(filePath, data.ToArray());
        }
    }
}
