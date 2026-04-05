using MATTAR.Logistics.Server.Services;
using MATTAR.OCR.Interfaces;

namespace MATTAR.OCR
{
    /// <summary>
    /// Factory that creates the appropriate <see cref="IPdfToTextService"/> implementation based
    /// on the requested <see cref="OcrEngine"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// // Tesseract (default)
    /// var svc = PdfToTextServiceFactory.Create(ocrPath, pdfToImage);
    ///
    /// // Hugging Face TrOCR
    /// var svc = PdfToTextServiceFactory.Create(ocrPath, pdfToImage, OcrEngine.HuggingFace);
    ///
    /// // Hugging Face with a custom model and non-default Python interpreter
    /// var svc = PdfToTextServiceFactory.Create(
    ///     ocrPath, pdfToImage,
    ///     engine: OcrEngine.HuggingFace,
    ///     pythonExecutable: "python3",
    ///     huggingFaceModelId: "microsoft/trocr-large-printed");
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
        ///   When <see cref="OcrEngine.Auto"/> is specified, Tesseract is used unless a Python
        ///   interpreter can be located on the system PATH, in which case the Hugging Face engine
        ///   is preferred.
        /// </param>
        /// <param name="pythonExecutable">
        ///   Python interpreter name or full path (default: <c>python</c>).
        ///   Only used when <paramref name="engine"/> is <see cref="OcrEngine.HuggingFace"/> or
        ///   <see cref="OcrEngine.Auto"/>.
        /// </param>
        /// <param name="huggingFaceModelId">
        ///   Hugging Face model repository ID used by the Hugging Face engine
        ///   (default: <c>microsoft/trocr-base-printed</c>).
        /// </param>
        public static IPdfToTextService Create(
            IOCRPath path,
            IPdfToImageService pdfToImage,
            OcrEngine engine = OcrEngine.Tesseract,
            string pythonExecutable = "python",
            string huggingFaceModelId = "microsoft/trocr-base-printed")
        {
            return engine switch
            {
                OcrEngine.Tesseract => new PdfToTextService(path, pdfToImage),
                OcrEngine.HuggingFace => new HuggingFaceOcrService(
                    path, pdfToImage, pythonExecutable, huggingFaceModelId),
                OcrEngine.Auto => IsPythonAvailable(pythonExecutable)
                    ? new HuggingFaceOcrService(path, pdfToImage, pythonExecutable, huggingFaceModelId)
                    : new PdfToTextService(path, pdfToImage),
                _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, null)
            };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Returns <c>true</c> when the given Python executable can be found on the system PATH.
        /// </summary>
        private static bool IsPythonAvailable(string pythonExecutable)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonExecutable,
                    ArgumentList = { "--version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc is null)
                    return false;

                bool exited = proc.WaitForExit(3000);
                if (!exited)
                {
                    try { proc.Kill(); } catch { /* best-effort */ }
                    return false;
                }

                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
