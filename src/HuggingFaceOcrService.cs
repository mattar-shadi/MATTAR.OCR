using System.Text;
using System.Text.Json;
using MATTAR.OCR.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace MATTAR.OCR;

/// <summary>
/// Converts a PDF to plain text using a Hugging Face TrOCR model running entirely in-process
/// via <b>ONNX Runtime</b> — no Python or external process is required at runtime.
/// </summary>
/// <remarks>
/// <para><b>Model files</b></para>
/// <para>
/// The service loads two pre-exported ONNX files from <paramref name="modelDirectory"/>
/// (default: <c>&lt;rootPath&gt;/trocr-onnx/</c>):
/// </para>
/// <list type="bullet">
///   <item><c>encoder_model.onnx</c> — ViT image encoder</item>
///   <item><c>decoder_model.onnx</c> — autoregressive text decoder</item>
///   <item><c>vocab.json</c> — token vocabulary (for decoding output IDs to text)</item>
/// </list>
/// <para>
/// Export the models once using <a href="https://huggingface.co/docs/optimum">Hugging Face Optimum</a>:
/// </para>
/// <code>
/// pip install optimum[exporters]
/// optimum-cli export onnx --model microsoft/trocr-base-printed ./trocr-onnx/
/// </code>
/// <para>
/// GPU acceleration is handled automatically by ONNX Runtime when a CUDA execution provider
/// is available; install <c>Microsoft.ML.OnnxRuntime.Gpu</c> instead of the CPU package and
/// no code changes are needed.
/// </para>
/// </remarks>
public class HuggingFaceOcrService : IPdfToTextService
{
    private readonly IOCRPath _path;
    private readonly IPdfToImageService _pdfToImage;
    private readonly string _modelDirectory;

    // TrOCR ViT input dimensions
    private const int TrocrInputHeight = 384;
    private const int TrocrInputWidth  = 384;

    // Normalisation: pixel → (pixel / 255 − 0.5) / 0.5 → range [−1, 1]
    private const float NormMean = 0.5f;
    private const float NormStd  = 0.5f;

    // TrOCR uses a RoBERTa tokeniser where decoder_start_token_id == eos_token_id == 2
    private const int DecoderStartTokenId = 2;
    private const int EosTokenId          = 2;
    private const int MaxNewTokens        = 128;

    /// <summary>
    /// Initialises a new instance of <see cref="HuggingFaceOcrService"/>.
    /// </summary>
    /// <param name="path">Path provider (root + temp directories).</param>
    /// <param name="pdfToImage">PDF-to-image conversion service.</param>
    /// <param name="modelDirectory">
    ///   Directory containing the ONNX model files.
    ///   Defaults to <c>&lt;rootPath&gt;/trocr-onnx/</c>.
    /// </param>
    public HuggingFaceOcrService(
        IOCRPath path,
        IPdfToImageService pdfToImage,
        string? modelDirectory = null)
    {
        _path = path;
        _pdfToImage = pdfToImage;
        _modelDirectory = modelDirectory
            ?? Path.Combine(path.GetRootPath(), "trocr-onnx");
    }

    /// <inheritdoc />
    public string Convert(string fileName)
    {
        string pdfPath = Path.Combine(_path.GetTempPath(), fileName);
        List<string> imagePaths = _pdfToImage.ConvertToImages(pdfPath);
        if (imagePaths.Count == 0)
            return string.Empty;

        string encoderPath = Path.Combine(_modelDirectory, "encoder_model.onnx");
        string decoderPath = Path.Combine(_modelDirectory, "decoder_model.onnx");
        string vocabPath   = Path.Combine(_modelDirectory, "vocab.json");

        Dictionary<int, string> vocabDecoder = LoadVocabDecoder(vocabPath);
        Dictionary<char, byte> bytesDecoder = BuildBytesDecoder();

        using InferenceSession encoderSession = new(encoderPath);
        using InferenceSession decoderSession = new(decoderPath);

        StringBuilder sb = new();
        foreach (string imagePath in imagePaths)
        {
            var (hiddenState, hiddenShape) = RunEncoder(encoderSession, PreprocessImage(imagePath));
            var tokenIds = RunGreedyDecoder(decoderSession, hiddenState, hiddenShape);
            sb.AppendLine(DecodeTokens(tokenIds, vocabDecoder, bytesDecoder));
        }

        return sb.ToString();
    }

    #region Image preprocessing

    private static float[] PreprocessImage(string imagePath)
    {
        ArgumentNullException.ThrowIfNull(imagePath);

        SKImageInfo imageInfo = new(TrocrInputWidth, TrocrInputHeight);
        SKSamplingOptions samplingOption = new(SKFilterMode.Linear, SKMipmapMode.Linear);
        using SKBitmap? bitmap  = SKBitmap.Decode(imagePath);
        using SKBitmap resized = bitmap?.Resize(imageInfo, samplingOption)
            ?? throw new InvalidOperationException($"Failed to resize image '{imagePath}' to {TrocrInputWidth}×{TrocrInputHeight}.");

        // Channel-first layout: [C, H, W] = [3, 384, 384]
        var result  = new float[3 * TrocrInputHeight * TrocrInputWidth];
        int rOffset = 0;
        int gOffset = TrocrInputHeight * TrocrInputWidth;
        int bOffset = 2 * TrocrInputHeight * TrocrInputWidth;

        for (int y = 0; y < TrocrInputHeight; y++)
        {
            for (int x = 0; x < TrocrInputWidth; x++)
            {
                var pixel = resized.GetPixel(x, y);
                int idx   = y * TrocrInputWidth + x;
                result[rOffset + idx] = (pixel.Red   / 255f - NormMean) / NormStd;
                result[gOffset + idx] = (pixel.Green / 255f - NormMean) / NormStd;
                result[bOffset + idx] = (pixel.Blue  / 255f - NormMean) / NormStd;
            }
        }

        return result;
    }

    #endregion

    #region Encoder

    private static (float[] hiddenState, int[] shape) RunEncoder(
        InferenceSession session, float[] pixelValues)
    {
        DenseTensor<float> inputTensor = new(pixelValues, [1, 3, TrocrInputHeight, TrocrInputWidth]);

        using var results = session.Run(
            [NamedOnnxValue.CreateFromTensor("pixel_values", inputTensor)]);

        var outputValue = results.First(r => r.Name == "last_hidden_state");
        var tensor      = outputValue.AsTensor<float>();
        var shape       = tensor.Dimensions.ToArray();

        return (tensor.ToArray(), shape);
    }

    #endregion

    #region Greedy decoder

    private static List<int> RunGreedyDecoder(
        InferenceSession session, float[] encoderHiddenState, int[] encoderShape)
    {
        List<int> generatedTokens = [];
        var inputIds        = new List<int> { DecoderStartTokenId };

        // Encoder tensors are constant across all decoding steps
        DenseTensor<float> encoderTensor = new(encoderHiddenState, encoderShape);

        int encoderSeqLen = encoderShape[1];
        long[]? maskData = [.. Enumerable.Repeat(1L, encoderSeqLen)];
        DenseTensor<long> encoderMask = new(maskData, [1, encoderSeqLen]);

        // Determine once whether the decoder expects encoder_attention_mask
        bool hasEncoderMask = session.InputMetadata.ContainsKey("encoder_attention_mask");

        for (int step = 0; step < MaxNewTokens; step++)
        {
            var idData = inputIds.Select(x => (long)x).ToArray();
            var idTensor = new DenseTensor<long>(idData, [1, inputIds.Count]);

            List<NamedOnnxValue> inputs = [
                NamedOnnxValue.CreateFromTensor("input_ids", idTensor),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderTensor)
            ];

            if (hasEncoderMask)
                inputs.Add(NamedOnnxValue.CreateFromTensor("encoder_attention_mask", encoderMask));

            using var outputs = session.Run(inputs);

            var logitsTensor = outputs.First(o => o.Name == "logits").AsTensor<float>();
            int vocabSize    = logitsTensor.Dimensions[2];
            int lastSeqPos   = inputIds.Count - 1;

            int nextToken = ArgMax(logitsTensor, batchIdx: 0, seqIdx: lastSeqPos, vocabSize);
            if (nextToken == EosTokenId)
                break;

            generatedTokens.Add(nextToken);
            inputIds.Add(nextToken);
        }

        return generatedTokens;
    }

    private static int ArgMax(Tensor<float> logits, int batchIdx, int seqIdx, int vocabSize)
    {
        int   bestToken = 0;
        float bestScore = float.MinValue;

        for (int v = 0; v < vocabSize; v++)
        {
            float score = logits[batchIdx, seqIdx, v];
            if (score > bestScore)
            {
                bestScore = score;
                bestToken = v;
            }
        }

        return bestToken;
    }

    #endregion

    #region Tokenizer / decoder

    private static Dictionary<int, string> LoadVocabDecoder(string vocabPath)
    {
        using var stream = File.OpenRead(vocabPath);
        var vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(stream)
            ?? throw new InvalidOperationException(
                $"Failed to deserialise vocab.json at '{vocabPath}'.");

        return vocab.ToDictionary(kv => kv.Value, kv => kv.Key);
    }

    private static string DecodeTokens(
        IEnumerable<int> tokenIds,
        IDictionary<int, string> vocabDecoder,
        IDictionary<char, byte> bytesDecoder)
    {
        // Concatenate token strings (GPT-2 unicode representation)
        string raw = string.Concat(
            tokenIds.Select(id =>
                vocabDecoder.TryGetValue(id, out var s) ? s : "\uFFFD"));

        // Map each character back to the original byte value
        byte[] bytes = raw
            .Select(c => bytesDecoder.TryGetValue(c, out var b) ? b : (byte)'?')
            .ToArray();

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Builds the reverse of GPT-2's <c>bytes_to_unicode()</c> mapping:
    /// Unicode surrogate character → original byte value.
    /// </summary>
    private static Dictionary<char, byte> BuildBytesDecoder()
    {
        // Printable ASCII + Latin-1 supplement ranges that map to themselves
        var bs = Enumerable.Range('!', '~' - '!' + 1)
            .Concat(Enumerable.Range(0x00A1, 0x00AC - 0x00A1 + 1))
            .Concat(Enumerable.Range(0x00AE, 0x00FF - 0x00AE + 1))
            .Select(b => (byte)b)
            .ToList();

        var cs = bs.Select(b => (char)b).ToList();

        int n = 0;
        for (int b = 0; b < 256; b++)
        {
            if (!bs.Contains((byte)b))
            {
                bs.Add((byte)b);
                cs.Add((char)(256 + n++));
            }
        }

        return cs.Zip(bs, (c, b) => (c, b)).ToDictionary(t => t.c, t => t.b);
    }
    #endregion
}

