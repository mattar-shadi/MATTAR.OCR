using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;
using MATTAR.OCR.Interfaces;
using System.Drawing;
using System.Drawing.Imaging;
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
            string pathFile = Path.Combine(pdfFilePath);
            string outImageName = Path.GetFileNameWithoutExtension(pathFile);

            GhostscriptVersionInfo gvi = GetGhostscriptVersionInfo();
            using (var rasterizer = new GhostscriptRasterizer())
            {
                rasterizer.Open(pathFile, gvi, true);
                for (int i = 1; i <= rasterizer.PageCount; i++)
                {
                    Image image = rasterizer.GetPage(300, i);
                    string pagePath = Path.Combine($"{outImageName}_page{i}.png");
                    image.Save(pagePath, ImageFormat.Png);
                    result.Add(pagePath);
                }
            }

            return result;
        }

        private GhostscriptVersionInfo GetGhostscriptVersionInfo()
        {
            string contentDllPath = Path.Combine(_path.GetRootPath(), "DLLs");
            GhostscriptVersionInfo gvi;
            if (IntPtr.Size== 4 && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                gvi = new GhostscriptVersionInfo(Path.Combine(contentDllPath, "gsdll32.dll"));
            }
            else if (IntPtr.Size== 8 && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                gvi = new GhostscriptVersionInfo(Path.Combine(contentDllPath, "gsdll64.dll"));
            }
            else if (IntPtr.Size== 4 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                gvi = new GhostscriptVersionInfo(Path.Combine(contentDllPath, "gsdll32.dll"));
            }
            else if (IntPtr.Size== 8 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                gvi = new GhostscriptVersionInfo(Path.Combine(contentDllPath, "gsdll64.dll"));
            }
            else
            {
                throw new NotImplementedException("Os Not know.");
            }

            return gvi;
        }

        public string ConvertToImage(string InputPDFFile)
        {
            string pathFile = Path.Combine(_path.GetRootPath(), InputPDFFile);
            string outImageName = Path.GetFileNameWithoutExtension(pathFile);
            string imgPath = Path.Combine(_path.GetRootPath(), $"{outImageName}.png");

            GhostscriptVersionInfo gvi = GetGhostscriptVersionInfo();
            using (var rasterizer = new GhostscriptRasterizer())
            {
                rasterizer.Open(pathFile, gvi, true);

                var dev = new GhostscriptPngDevice(GhostscriptPngDeviceType.Png256);
                dev.GraphicsAlphaBits = GhostscriptImageDeviceAlphaBits.V_4;
                dev.TextAlphaBits = GhostscriptImageDeviceAlphaBits.V_4;
                dev.ResolutionXY = new GhostscriptImageDeviceResolution(290, 290);
                dev.InputFiles.Add(pathFile);
                dev.Pdf.FirstPage = 1;
                dev.Pdf.LastPage = rasterizer.PageCount;
                dev.CustomSwitches.Add("-dDOINTERPOLATE");
                dev.OutputPath = imgPath;
                dev.Process();
            }

            return imgPath;
        }
    }
}