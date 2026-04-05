namespace MATTAR.OCR.Interfaces
{
    /// <summary>
    /// Selects which OCR back-end is used by <see cref="PdfToTextServiceFactory"/>.
    /// </summary>
    public enum OcrEngine
    {
        /// <summary>Uses the Tesseract 5 engine (default).</summary>
        Tesseract,

        /// <summary>
        /// Uses an open-source Hugging Face model (TrOCR by default).
        /// Requires Python 3.8+ and the packages listed in <c>requirements.txt</c>.
        /// </summary>
        HuggingFace,

        /// <summary>
        /// Automatically selects the best available engine.
        /// Falls back to Tesseract when the Hugging Face prerequisites are not detected.
        /// </summary>
        Auto
    }
}
