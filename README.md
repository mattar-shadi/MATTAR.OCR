# MATTAR.OCR

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet) ![NuGet](https://img.shields.io/nuget/v/MATTAR.OCR?logo=nuget) ![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

**MATTAR.OCR** is a C# .NET 10 library that extracts text from PDF documents and images using either the Tesseract OCR engine or an open-source Hugging Face model (TrOCR). It solves the common need to programmatically read and digitise scanned PDFs or raster images by providing a clean, interface-driven API that integrates easily into any .NET application.

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Project Structure](#project-structure)
- [Development](#development)
- [Contributing](#contributing)
- [License](#license)

---

## Features

- **PDF → Text**: Convert a multi-page PDF document directly to a plain-text string.
- **PDF → Images**: Rasterise each page of a PDF to a high-resolution PNG file (300 DPI).
- **Single-page PDF → Image**: Convert a single PDF to a PNG using PDFium's rendering pipeline.
- **Dual OCR engines**: Choose between [Tesseract 5](https://github.com/tesseract-ocr/tesseract) (default, no extra setup) and a Hugging Face open-source model ([TrOCR](https://huggingface.co/onnx-community/trocr-base-stage1-ONNX)) running via [ONNX Runtime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) — **no Python required at runtime**.
- **Engine selection at runtime**: Use `OcrEngine` enum and `PdfToTextServiceFactory` to switch engines without code changes.
- **Automatic model download**: `TrOcrModelDownloader` downloads the pre-exported ONNX files directly from [Hugging Face Hub](https://huggingface.co/onnx-community/trocr-base-stage1-ONNX) with progress reporting, skip-if-exists, and cancellation support — no Python, no manual export step.
- **Cross-architecture support**: Bundled Tesseract native libraries for both `x86` and `x64` environments.
- **Cross-platform**: Works on Windows, Linux, and macOS (x64/arm64) with no manual DLL setup required.
- **Dependency-injection friendly**: All services are backed by interfaces (`IPdfToTextService`, `IPdfToImageService`, `IOCRPath`) and accept constructor injection.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# 14 / .NET 10.0 |
| OCR engine (default) | [Tesseract 5.2.0](https://www.nuget.org/packages/Tesseract/) |
| OCR engine (optional) | [onnx-community/trocr-base-stage1-ONNX](https://huggingface.co/onnx-community/trocr-base-stage1-ONNX) via [ONNX Runtime 1.24.4](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) (pure C#, no Python at runtime) |
| PDF rasterisation | [PDFtoImage 5.2.0](https://www.nuget.org/packages/PDFtoImage/) (MIT — powered by PDFium) |
| PDF utilities | [PdfSharpCore 1.3.67](https://www.nuget.org/packages/PdfSharpCore/), [PdfPig 0.1.14](https://www.nuget.org/packages/PdfPig/) |
| Image encoding | [SkiaSharp 3.119.2](https://www.nuget.org/packages/SkiaSharp/) (MIT) |
| Testing | [NUnit 4.5.1](https://www.nuget.org/packages/NUnit/) |
| CI / CD | GitHub Actions → NuGet publish |

---

## Installation

### Via NuGet (recommended)

```bash
dotnet add package MATTAR.OCR
```

Or with the Package Manager Console:

```powershell
Install-Package MATTAR.OCR
```

### Prerequisites

| Requirement | Notes |
|---|---|
| **.NET 10.0 SDK** | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Tesseract language data** | A `tessdata/` directory containing the desired language data files (e.g. `fra.traineddata`) must exist under your application's root path. Download language packs from the [tessdata repository](https://github.com/tesseract-ocr/tessdata). Required only for the Tesseract engine. |
| **ONNX model files** *(optional)* | Required only for `OcrEngine.HuggingFace`. Use `TrOcrModelDownloader.EnsureModelAsync()` to download them automatically — no Python required. See [Hugging Face Engine Setup](#hugging-face-engine-setup). |

> **No Ghostscript required.** PDF rasterisation is handled by [PDFtoImage](https://www.nuget.org/packages/PDFtoImage/) (backed by PDFium), whose native assets are automatically included via NuGet for Windows, Linux, and macOS. There is no `DLLs/` directory to manage.

---

## Usage

### 1. Implement `IOCRPath`

The library requires you to provide path information by implementing the `IOCRPath` interface:

```csharp
using MATTAR.OCR.Interfaces;

public class MyOCRPath : IOCRPath
{
    // Root path – must contain the tessdata/ subdirectory
    public string GetRootPath() => AppContext.BaseDirectory;

    // Temporary path – used to store intermediate image files
    public string GetTempPath() => Path.Combine(AppContext.BaseDirectory, "tmp");
}
```

### 2. Convert a PDF to text

```csharp
using MATTAR.OCR;
using MATTAR.OCR.Interfaces;

IOCRPath ocrPath = new MyOCRPath();
IPdfToImageService pdfToImage = new PdfToImageService(ocrPath);
IPdfToTextService pdfToText  = new PdfToTextService(ocrPath, pdfToImage);

// fileName is resolved relative to the temp path returned by IOCRPath.GetTempPath()
string extractedText = pdfToText.Convert("scanned-document.pdf");
Console.WriteLine(extractedText);
```

### 2b. Convert using the Hugging Face OCR engine

Use `PdfToTextServiceFactory` to select the engine at runtime:

```csharp
using MATTAR.OCR;
using MATTAR.OCR.Interfaces;

IOCRPath ocrPath = new MyOCRPath();
IPdfToImageService pdfToImage = new PdfToImageService(ocrPath);

// Tesseract (default – no extra files required)
IPdfToTextService tesseract = PdfToTextServiceFactory.Create(ocrPath, pdfToImage);

// Hugging Face TrOCR via ONNX Runtime (no Python required at runtime)
IPdfToTextService hf = PdfToTextServiceFactory.Create(
    ocrPath, pdfToImage, OcrEngine.HuggingFace);

// Auto – uses HuggingFace when ONNX model files are present, otherwise Tesseract
IPdfToTextService auto = PdfToTextServiceFactory.Create(
    ocrPath, pdfToImage, OcrEngine.Auto);

string text = hf.Convert("scanned-document.pdf");
Console.WriteLine(text);
```

You can also instantiate `HuggingFaceOcrService` directly and specify a custom model
directory (e.g. for a large-print or handwritten model export):

```csharp
IPdfToTextService hf = new HuggingFaceOcrService(
    ocrPath, pdfToImage,
    modelDirectory: "/models/trocr-large-printed");
```

### 2c. Download the ONNX model automatically

Call `TrOcrModelDownloader.EnsureModelAsync` once at application startup (or on first use).
Files that already exist on disk are silently skipped.

```csharp
using MATTAR.OCR;

// Minimal – no progress output
await TrOcrModelDownloader.EnsureModelAsync("./trocr-onnx");
```

With a progress callback:

```csharp
await TrOcrModelDownloader.EnsureModelAsync(
    modelDirectory: "./trocr-onnx",
    progress: new Progress<TrOcrDownloadProgress>(p =>
    {
        if (p.Status == TrOcrDownloadStatus.Skipped)
            Console.WriteLine($"[{p.FileIndex}/{p.TotalFiles}] {p.FileName} — already present");
        else
            Console.Write($"\r[{p.FileIndex}/{p.TotalFiles}] {p.FileName}  {p.Percent:F0} %   ");

        if (p.Status == TrOcrDownloadStatus.Completed)
            Console.WriteLine();
    }));
```

With DI (`IHttpClientFactory`) and cancellation:

```csharp
await TrOcrModelDownloader.EnsureModelAsync(
    modelDirectory: "./trocr-onnx",
    httpClient: httpClientFactory.CreateClient(),
    cancellationToken: stoppingToken);
```

`TrOcrDownloadProgress` properties:

| Property | Type | Description |
|---|---|---|
| `FileName` | `string` | Name of the file being downloaded |
| `FileIndex` | `int` | 1-based index of the current file |
| `TotalFiles` | `int` | Total number of files to download (3) |
| `BytesReceived` | `long` | Bytes received so far |
| `TotalBytes` | `long` | Total file size, or −1 if unknown |
| `Percent` | `double` | 0–100, or −1 if total size is unknown |
| `Status` | `TrOcrDownloadStatus` | `Downloading` / `Completed` / `Skipped` |

### 3. Convert a PDF to a list of page images

```csharp
var pdfToImage = new PdfToImageService(new MyOCRPath());

// Returns a list of absolute paths to the generated PNG files
List<string> imagePaths = pdfToImage.ConvertToImages("/absolute/path/to/document.pdf");
foreach (var imgPath in imagePaths)
    Console.WriteLine(imgPath);
```

### 4. Convert a single PDF to one PNG

```csharp
var pdfToImage = new PdfToImageService(new MyOCRPath());

// The PDF is resolved relative to the root path returned by IOCRPath.GetRootPath()
string imagePath = pdfToImage.ConvertToImage("document.pdf");
Console.WriteLine($"Image saved to: {imagePath}");
```

---

## Configuration

### Directory layout expected at runtime

```
<root path>/
├── tessdata/
│   └── fra.traineddata  # Tesseract language data (French by default)
└── trocr-onnx/          # Required only for the Hugging Face engine
    ├── encoder_model.onnx
    ├── decoder_model.onnx
    └── vocab.json

<temp path>/             # Writable directory for intermediate PNG files
```

> **Note:** The Tesseract OCR language is currently hardcoded to French (`"fra"`). To use a different language, change the language code in `PdfToTextService.cs` and provide the corresponding `.traineddata` file in `tessdata/`.

### Hugging Face Engine Setup

The Hugging Face engine runs entirely in-process using **ONNX Runtime** — no Python is
needed, not even for the initial model download.

Call `TrOcrModelDownloader.EnsureModelAsync` once before using the engine:

```csharp
// Downloads encoder_model.onnx, decoder_model.onnx and vocab.json
// from https://huggingface.co/onnx-community/trocr-base-stage1-ONNX
await TrOcrModelDownloader.EnsureModelAsync(
    modelDirectory: Path.Combine(ocrPath.GetRootPath(), "trocr-onnx"));

IPdfToTextService hf = PdfToTextServiceFactory.Create(
    ocrPath, pdfToImage, OcrEngine.HuggingFace);
```

The three files are saved under `modelDirectory` and reused on subsequent runs (already-
present files are never re-downloaded). See [§ 2c](#2c-download-the-onnx-model-automatically)
for the full progress-reporting and DI examples.

#### GPU acceleration (optional)

Replace the CPU NuGet package with the GPU variant:

```bash
dotnet remove package Microsoft.ML.OnnxRuntime
dotnet add package Microsoft.ML.OnnxRuntime.Gpu
```

No code changes are required; ONNX Runtime selects a CUDA execution provider automatically
when a compatible GPU is present.

#### Choosing a different model

Export a different TrOCR variant and point `modelDirectory` at the new folder:

| Model | Best for | ONNX export size |
|---|---|---|
| `microsoft/trocr-base-printed` | Printed text – balanced speed/accuracy (default) | ~400 MB |
| `microsoft/trocr-large-printed` | Printed text – higher accuracy, slower | ~1.3 GB |
| `microsoft/trocr-base-handwritten` | Handwritten text | ~400 MB |
| `microsoft/trocr-large-handwritten` | Handwritten text – higher accuracy | ~1.3 GB |

All listed models are released under the **MIT licence**.

### Environment variables

| Variable | Where used | Purpose |
|---|---|---|
| `NUGET_API_KEY` | GitHub Actions secret | Authenticates NuGet package publishing |

---

## Project Structure

```
MATTAR.OCR/
├── src/
│   ├── MATTAR.OCR.csproj          # Library project file (.NET 10.0)
│   ├── PdfToImageService.cs       # Converts PDF pages to PNG images (PDFtoImage/PDFium)
│   ├── PdfToTextService.cs        # Converts PDF to text via image pipeline (Tesseract)
│   ├── HuggingFaceOcrService.cs   # Converts PDF to text via TrOCR ONNX Runtime (native C#, no Python)
│   ├── TrOcrModelDownloader.cs    # Downloads TrOCR ONNX files from Hugging Face Hub with progress reporting
│   ├── PdfToTextServiceFactory.cs # Factory: creates the right IPdfToTextService for OcrEngine
│   └── Interfaces/
│       ├── IOCRPath.cs            # Path abstraction (root + temp paths)
│       ├── IPdfToImageService.cs  # PDF-to-image contract
│       ├── IPdfToTextService.cs   # PDF-to-text contract
│       └── OcrEngine.cs           # Enum: Tesseract | HuggingFace | Auto
│
├── tests/
│   └── MATTAR.OCR.Tests/
│       ├── MATTAR.OCR.Tests.csproj
│       ├── SmpleTests.cs          # NUnit test fixtures (Tesseract + engine-selection tests)
│       ├── Usings.cs              # Global using declarations
│       └── Implementation/
│           └── TestOCRPath.cs     # IOCRPath implementation for tests
│
├── .github/
│   └── workflows/
│       └── dotnet.yml             # CI: build, test, and publish to NuGet
│
├── MATTAR.OCR.sln
├── LICENSE
└── README.md
```

---

## Development

### Build from source

```bash
# Clone the repository
git clone https://github.com/mattar-shadi/MATTAR.OCR.git
cd MATTAR.OCR

# Restore NuGet packages (.NET 10 SDK required)
dotnet restore src/

# Build (Debug)
dotnet build src/

# Build (Release)
dotnet build src/ --configuration Release
```

### Run tests

```bash
dotnet test tests/MATTAR.OCR.Tests/
```

### CI pipeline

The GitHub Actions workflow (`.github/workflows/dotnet.yml`) runs automatically on every push and pull-request targeting `main`. It:

1. Restores dependencies.
2. Builds the project.
3. Runs the NUnit test suite.
4. Publishes the NuGet package (on push to `main`, requires `NUGET_API_KEY` secret).

---

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork** the repository and create your branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. **Write tests** for any new functionality using NUnit.
3. **Ensure the build passes** locally before opening a PR:
   ```bash
   dotnet build src/ && dotnet test tests/MATTAR.OCR.Tests/
   ```
4. **Open a Pull Request** against `main` with a clear title and description of your changes.
5. Keep code style consistent with the existing codebase (C# conventions, nullable reference types enabled).

---

## License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

Copyright © 2026 MATTAR S.A.S.
