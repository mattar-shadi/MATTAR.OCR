using MATTAR.OCR.Interfaces;

namespace MATTAR.OCR;

/// <summary>
/// Factory that creates the appropriate <see cref="IPdfToTextService"/> implementation based
/// on the requested <see cref="OcrEngine"/>.
/// </summary>
/// <example>
/// <code>
/// // Tesseract (default)
/// var svc = PdfToTextServiceFactory.Create(ocrPath, pdfToImage);
///
/// // Hugging Face TrOCR (ONNX — no Python required)
/// var svc = PdfToTextServiceFactory.Create(ocrPath, pdfToImage, OcrEngine.HuggingFace);
///
/// // Hugging Face with a custom model directory
/// var svc = PdfToTextServiceFactory.Create(
///     ocrPath, pdfToImage,
///     engine: OcrEngine.HuggingFace,
///     huggingFaceModelDirectory: "/models/trocr-large");
/// </code>
/// </example>
public static class PdfToTextServiceFactory
{
    /// <summary>
    /// Creates an <see cref="IPdfToTextService"/> for the specified OCR engine.
    /// </summary>
    /// <param name="path">Path provider (root and temp directories).</param>
    /// <param name="pdfToImage">PDF-to-image conversion service.</param>
    /// <param name="engine">
    ///   Which OCR engine to use.  Defaults to <see cref="OcrEngine.Tesseract"/>.
    ///   When <see cref="OcrEngine.Auto"/> is specified, the Hugging Face engine is used
    ///   when the ONNX model files are found in <paramref name="huggingFaceModelDirectory"/>;
    ///   otherwise Tesseract is used.
    /// </param>
    /// <param name="huggingFaceModelDirectory">
    ///   Directory that contains the exported ONNX model files
    ///   (<c>encoder_model.onnx</c>, <c>decoder_model.onnx</c>, <c>vocab.json</c>).
    ///   Defaults to <c>&lt;rootPath&gt;/trocr-onnx/</c>.
    ///   Only used when <paramref name="engine"/> is <see cref="OcrEngine.HuggingFace"/> or
    ///   <see cref="OcrEngine.Auto"/>.
    /// </param>
    public static IPdfToTextService Create(
        IOCRPath path,
        IPdfToImageService pdfToImage,
        OcrEngine engine = OcrEngine.Tesseract,
        string? huggingFaceModelDirectory = null)
    {
        return engine switch
        {
            OcrEngine.Tesseract    => new PdfToTextService(path, pdfToImage),
            OcrEngine.HuggingFace  => new HuggingFaceOcrService(path, pdfToImage, huggingFaceModelDirectory),
            OcrEngine.Auto         => IsOnnxModelAvailable(path, huggingFaceModelDirectory)
                ? new HuggingFaceOcrService(path, pdfToImage, huggingFaceModelDirectory)
                : new PdfToTextService(path, pdfToImage),
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, null)
        };
    }

    #region Helpers

    /// <summary>
    /// Returns <c>true</c> when the required ONNX model files exist in the model directory.
    /// </summary>
    private static bool IsOnnxModelAvailable(IOCRPath path, string? modelDirectory)
    {
        string dir = modelDirectory ?? Path.Combine(path.GetRootPath(), "trocr-onnx");
        return File.Exists(Path.Combine(dir, "encoder_model.onnx"))
            && File.Exists(Path.Combine(dir, "decoder_model.onnx"))
            && File.Exists(Path.Combine(dir, "vocab.json"));
    }

    #endregion
}