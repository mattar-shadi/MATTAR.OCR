namespace MATTAR.OCR.Interfaces;

public interface IPdfToImageService
{
    List<string> ConvertToImages(string pdfFilePath);
}
