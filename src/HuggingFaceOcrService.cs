using System.Diagnostics;
using MATTAR.OCR.Interfaces;

namespace MATTAR.OCR
{
    /// <summary>
    /// Converts a PDF to plain text using a Hugging Face open-source OCR model (TrOCR by
    /// default).  Each PDF page is first rasterised to a PNG by <see cref="IPdfToImageService"/>;
    /// then the Python helper script <c>huggingface_ocr.py</c> is invoked as a subprocess to
    /// perform the actual recognition.
    /// </summary>
    /// <remarks>
    /// <para><b>Prerequisites</b></para>
    /// <list type="bullet">
    ///   <item>Python 3.8 or later must be available on the system PATH (or via
    ///     <paramref name="pythonExecutable"/>).</item>
    ///   <item>The Python packages listed in <c>requirements.txt</c> must be installed
    ///     (<c>pip install -r requirements.txt</c>).</item>
    ///   <item>The file <c>huggingface_ocr.py</c> must be present either in the directory
    ///     returned by <see cref="IOCRPath.GetRootPath"/> or next to the executing assembly.
    ///     The model weights are downloaded and cached automatically by the Hugging Face
    ///     <c>transformers</c> library on first use.</item>
    /// </list>
    /// </remarks>
    public class HuggingFaceOcrService : IPdfToTextService
    {
        private readonly IOCRPath _path;
        private readonly IPdfToImageService _pdfToImage;
        private readonly string _pythonExecutable;
        private readonly string _modelId;

        /// <summary>
        /// Initialises a new instance of <see cref="HuggingFaceOcrService"/>.
        /// </summary>
        /// <param name="path">Path provider used to locate temp files and the Python script.</param>
        /// <param name="pdfToImage">Service that converts PDF pages to PNG images.</param>
        /// <param name="pythonExecutable">
        ///   Path or name of the Python interpreter to use (default: <c>python</c>).
        ///   On some systems this may need to be <c>python3</c>.
        /// </param>
        /// <param name="modelId">
        ///   Hugging Face model repository ID (default: <c>microsoft/trocr-base-printed</c>).
        ///   Override to use a different TrOCR variant, e.g. <c>microsoft/trocr-large-printed</c>.
        /// </param>
        public HuggingFaceOcrService(
            IOCRPath path,
            IPdfToImageService pdfToImage,
            string pythonExecutable = "python",
            string modelId = "microsoft/trocr-base-printed")
        {
            _path = path;
            _pdfToImage = pdfToImage;
            _pythonExecutable = pythonExecutable;
            _modelId = modelId;
        }

        /// <inheritdoc />
        public string Convert(string fileName)
        {
            string pdfPath = Path.Combine(_path.GetTempPath(), fileName);
            var imagePaths = _pdfToImage.ConvertToImages(pdfPath);

            if (imagePaths.Count == 0)
                return string.Empty;

            string scriptPath = FindScript();
            return RunPythonOcr(scriptPath, imagePaths);
        }

        // ------------------------------------------------------------------ helpers

        private string FindScript()
        {
            // 1. Root path supplied by the IOCRPath implementation
            string rootScript = Path.Combine(_path.GetRootPath(), "huggingface_ocr.py");
            if (File.Exists(rootScript))
                return rootScript;

            // 2. Directory next to the executing assembly
            string assemblyDir =
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? string.Empty;

            string assemblyScript = Path.Combine(assemblyDir, "huggingface_ocr.py");
            if (File.Exists(assemblyScript))
                return assemblyScript;

            throw new FileNotFoundException(
                "huggingface_ocr.py not found. " +
                "Place it in the root path returned by IOCRPath.GetRootPath() " +
                "or next to the executing assembly.",
                "huggingface_ocr.py");
        }

        private string RunPythonOcr(string scriptPath, IEnumerable<string> imagePaths)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Pass the script, model ID, and all image paths as arguments so the model
            // is loaded only once per Convert() call regardless of page count.
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add(_modelId);
            foreach (var p in imagePaths)
                psi.ArgumentList.Add(p);

            using var process =
                Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start the Python OCR process.");

            // Read stdout and stderr concurrently with Task.Run to avoid deadlocks
            // that can occur when output buffers fill up before WaitForExit is called.
            var outputTask = Task.Run(() => process.StandardOutput.ReadToEnd());
            var errorTask  = Task.Run(() => process.StandardError.ReadToEnd());

            process.WaitForExit();

            string output = outputTask.GetAwaiter().GetResult();
            string error  = errorTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"huggingface_ocr.py exited with code {process.ExitCode}: {error}");

            return output;
        }
    }
}
