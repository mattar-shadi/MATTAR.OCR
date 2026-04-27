namespace MATTAR.OCR.Interfaces;

/// <summary>
/// Selects which OCR back-end is used by <see cref="PdfToTextServiceFactory"/>.
/// </summary>
public enum OcrEngine
{
    /// <summary>Uses the Tesseract 5 engine (default). No additional setup required.</summary>
    Tesseract,

    /// <summary>
    /// Uses an open-source Hugging Face TrOCR model running entirely in-process via
    /// <b>ONNX Runtime</b> — no Python or external process is required.
    /// Pre-exported ONNX model files must exist in the configured model directory
    /// (see <c>HuggingFaceOcrService</c> for export instructions).
    /// </summary>
    HuggingFace,

    /// <summary>
    /// Automatically selects the best available engine.
    /// Uses the Hugging Face engine when the ONNX model files are present in the
    /// configured model directory; otherwise falls back to Tesseract.
    /// </summary>
    Auto
}
