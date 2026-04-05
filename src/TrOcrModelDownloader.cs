using System.Net.Http.Headers;

namespace MATTAR.OCR
{
    /// <summary>
    /// Downloads the pre-exported TrOCR ONNX model files from Hugging Face Hub
    /// (<c>onnx-community/trocr-base-stage1-ONNX</c>) into a local directory so that
    /// <see cref="HuggingFaceOcrService"/> can load them without any Python installation.
    /// </summary>
    /// <example>
    /// <code>
    /// await TrOcrModelDownloader.EnsureModelAsync(
    ///     modelDirectory: "./trocr-onnx",
    ///     progress: new Progress&lt;TrOcrDownloadProgress&gt;(p =>
    ///         Console.WriteLine($"[{p.FileIndex}/{p.TotalFiles}] {p.FileName} {p.Percent:F0}%")));
    /// </code>
    /// </example>
    public static class TrOcrModelDownloader
    {
        private const string BaseUrl =
            "https://huggingface.co/onnx-community/trocr-base-stage1-ONNX/resolve/main/";

        /// <summary>
        /// Remote path (relative to <see cref="BaseUrl"/>) → local file name saved in
        /// <c>modelDirectory</c>.
        /// </summary>
        private static readonly (string RemotePath, string LocalFile)[] ModelFiles =
        [
            ("onnx/encoder_model.onnx", "encoder_model.onnx"),
            ("onnx/decoder_model.onnx", "decoder_model.onnx"),
            ("vocab.json",              "vocab.json"),
        ];

        /// <summary>
        /// Ensures all required model files are present in <paramref name="modelDirectory"/>.
        /// Files that already exist on disk are skipped.
        /// </summary>
        /// <param name="modelDirectory">Target directory (created automatically if needed).</param>
        /// <param name="progress">Optional progress callback, called for every chunk received.</param>
        /// <param name="httpClient">
        ///   Optional <see cref="HttpClient"/> instance (e.g. from DI). When <c>null</c> a
        ///   temporary client is created internally.
        /// </param>
        /// <param name="cancellationToken">Propagates cancellation to every download.</param>
        public static async Task EnsureModelAsync(
            string modelDirectory,
            IProgress<TrOcrDownloadProgress>? progress = null,
            HttpClient? httpClient = null,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(modelDirectory);

            bool ownsClient = httpClient is null;
            httpClient ??= new HttpClient();

            try
            {
                for (int i = 0; i < ModelFiles.Length; i++)
                {
                    var (remotePath, localFile) = ModelFiles[i];
                    string destPath = Path.Combine(modelDirectory, localFile);
                    int fileNumber  = i + 1;

                    if (File.Exists(destPath))
                    {
                        progress?.Report(new TrOcrDownloadProgress(
                            localFile, fileNumber, ModelFiles.Length,
                            BytesReceived: 0, TotalBytes: 0,
                            TrOcrDownloadStatus.Skipped));
                        continue;
                    }

                    await DownloadFileAsync(
                        httpClient,
                        url:         BaseUrl + remotePath,
                        destination: destPath,
                        fileName:    localFile,
                        fileIndex:   fileNumber,
                        totalFiles:  ModelFiles.Length,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (ownsClient)
                    httpClient.Dispose();
            }
        }

        // ------------------------------------------------------------------ internals

        private static async Task DownloadFileAsync(
            HttpClient http,
            string url,
            string destination,
            string fileName,
            int fileIndex,
            int totalFiles,
            IProgress<TrOcrDownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            string tempPath = destination + ".tmp";

            using var request  = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;

            await using var networkStream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81_920, useAsync: true))
            {
                var buffer        = new byte[81_920];
                long bytesReceived = 0L;
                int  bytesRead;

                while ((bytesRead = await networkStream
                           .ReadAsync(buffer, cancellationToken)
                           .ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                        .ConfigureAwait(false);

                    bytesReceived += bytesRead;
                    progress?.Report(new TrOcrDownloadProgress(
                        fileName, fileIndex, totalFiles,
                        bytesReceived, totalBytes,
                        Status: TrOcrDownloadStatus.Downloading));
                }
            }

            File.Move(tempPath, destination, overwrite: true);

            progress?.Report(new TrOcrDownloadProgress(
                fileName, fileIndex, totalFiles,
                totalBytes > 0 ? totalBytes : 0, totalBytes,
                Status: TrOcrDownloadStatus.Completed));
        }
    }

    // ---------------------------------------------------------------------- progress types

    /// <summary>Status of an individual file download.</summary>
    public enum TrOcrDownloadStatus
    {
        /// <summary>Bytes are currently being received.</summary>
        Downloading,

        /// <summary>The file was downloaded and saved successfully.</summary>
        Completed,

        /// <summary>The file already existed on disk and was not re-downloaded.</summary>
        Skipped,
    }

    /// <summary>Snapshot of download progress for a single model file.</summary>
    /// <param name="FileName">Name of the file being downloaded.</param>
    /// <param name="FileIndex">1-based index of the current file.</param>
    /// <param name="TotalFiles">Total number of files to download.</param>
    /// <param name="BytesReceived">Bytes received so far (0 when skipped).</param>
    /// <param name="TotalBytes">Total file size in bytes, or −1 if unknown.</param>
    /// <param name="Status">Current status of this file.</param>
    public sealed record TrOcrDownloadProgress(
        string               FileName,
        int                  FileIndex,
        int                  TotalFiles,
        long                 BytesReceived,
        long                 TotalBytes,
        TrOcrDownloadStatus  Status)
    {
        /// <summary>
        /// Download completion percentage (0–100), or −1 when the total size is unknown.
        /// Always 100 for <see cref="TrOcrDownloadStatus.Skipped"/> and
        /// <see cref="TrOcrDownloadStatus.Completed"/> files.
        /// </summary>
        public double Percent =>
            Status is TrOcrDownloadStatus.Skipped or TrOcrDownloadStatus.Completed ? 100 :
            TotalBytes > 0 ? BytesReceived * 100.0 / TotalBytes : -1;
    }
}
