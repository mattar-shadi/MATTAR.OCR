# MATTAR.OCR

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet) ![NuGet](https://img.shields.io/nuget/v/MATTAR.OCR?logo=nuget) ![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

**MATTAR.OCR** is a C# .NET 8 library that extracts text from PDF documents and images using either the Tesseract OCR engine or an open-source Hugging Face model (TrOCR). It solves the common need to programmatically read and digitise scanned PDFs or raster images by providing a clean, interface-driven API that integrates easily into any .NET application.

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
- **Dual OCR engines**: Choose between [Tesseract 5](https://github.com/tesseract-ocr/tesseract) (default, no Python required) and a Hugging Face open-source model ([TrOCR](https://huggingface.co/microsoft/trocr-base-printed), requires Python + `transformers`).
- **Engine selection at runtime**: Use `OcrEngine` enum and `PdfToTextServiceFactory` to switch engines without code changes.
- **Automatic model caching**: Hugging Face model weights are downloaded and cached transparently by the `transformers` library on first use.
- **Cross-architecture support**: Bundled Tesseract native libraries for both `x86` and `x64` environments.
- **Cross-platform**: Works on Windows, Linux, and macOS (x64/arm64) with no manual DLL setup required.
- **Dependency-injection friendly**: All services are backed by interfaces (`IPdfToTextService`, `IPdfToImageService`, `IOCRPath`) and accept constructor injection.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# 11 / .NET 8.0 |
| OCR engine (default) | [Tesseract 5.2.0](https://www.nuget.org/packages/Tesseract/) |
| OCR engine (optional) | [Hugging Face TrOCR](https://huggingface.co/microsoft/trocr-base-printed) via Python `transformers` |
| PDF rasterisation | [PDFtoImage 4.0.0](https://www.nuget.org/packages/PDFtoImage/) (MIT — powered by PDFium) |
| PDF utilities | [PdfSharpCore 1.3.57](https://www.nuget.org/packages/PdfSharpCore/), [PdfPig 0.1.8](https://www.nuget.org/packages/PdfPig/) |
| Image encoding | [SkiaSharp 2.88.x](https://www.nuget.org/packages/SkiaSharp/) (MIT) |
| Testing | [NUnit 3.13.3](https://www.nuget.org/packages/NUnit/) |
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
| **.NET 8.0 SDK** | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **Tesseract language data** | A `tessdata/` directory containing the desired language data files (e.g. `fra.traineddata`) must exist under your application's root path. Download language packs from the [tessdata repository](https://github.com/tesseract-ocr/tessdata). Required only for the Tesseract engine. |
| **Python 3.8+** *(optional)* | Required only for `OcrEngine.HuggingFace`. See [Hugging Face Engine Setup](#hugging-face-engine-setup) below. |

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

// Tesseract (default – no Python required)
IPdfToTextService tesseract = PdfToTextServiceFactory.Create(ocrPath, pdfToImage);

// Hugging Face TrOCR (requires Python + transformers)
IPdfToTextService hf = PdfToTextServiceFactory.Create(
    ocrPath, pdfToImage, OcrEngine.HuggingFace);

// Auto – uses HuggingFace if Python is found on PATH, otherwise falls back to Tesseract
IPdfToTextService auto = PdfToTextServiceFactory.Create(
    ocrPath, pdfToImage, OcrEngine.Auto);

string text = hf.Convert("scanned-document.pdf");
Console.WriteLine(text);
```

You can also instantiate `HuggingFaceOcrService` directly and pass a custom Python
executable path or a different TrOCR model variant:

```csharp
IPdfToTextService hf = new HuggingFaceOcrService(
    ocrPath, pdfToImage,
    pythonExecutable: "python3",
    modelId: "microsoft/trocr-large-printed");
```

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
└── huggingface_ocr.py   # Required for the Hugging Face engine

<temp path>/           # Writable directory for intermediate PNG files
```

> **Note:** The Tesseract OCR language is currently hardcoded to French (`"fra"`). To use a different language, change the language code in `PdfToTextService.cs` and provide the corresponding `.traineddata` file in `tessdata/`.

### Hugging Face Engine Setup

1. **Install Python dependencies** (only needed for `OcrEngine.HuggingFace`):

   ```bash
   pip install -r requirements.txt
   ```

   Or install manually:

   ```bash
   pip install transformers torch torchvision Pillow
   ```

2. **Place `huggingface_ocr.py`** in the directory returned by `IOCRPath.GetRootPath()` (or next to your assembly output). The file ships at the root of this repository.

3. **First run**: model weights (`microsoft/trocr-base-printed`, ~400 MB) are downloaded automatically by the `transformers` library and cached in `~/.cache/huggingface/hub/` (controlled by the `HF_HOME` environment variable).

#### GPU acceleration (optional)

Install a CUDA-enabled version of PyTorch to speed up inference:

```bash
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
```

No code changes are required; `transformers` detects a GPU automatically.

#### Choosing a different model

Pass the Hugging Face model ID to `PdfToTextServiceFactory.Create` or `HuggingFaceOcrService`:

| Model | Best for | Size |
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
| `HF_HOME` | Python / `transformers` | Cache directory for downloaded model weights |
| `HF_MODEL_ID` | `huggingface_ocr.py` fallback | Overrides the default model ID when not passed as a CLI arg |

---

## Project Structure

```
MATTAR.OCR/
├── src/
│   ├── MATTAR.OCR.csproj          # Library project file (.NET 8.0)
│   ├── PdfToImageService.cs       # Converts PDF pages to PNG images (PDFtoImage/PDFium)
│   ├── PdfToTextService.cs        # Converts PDF to text via image pipeline (Tesseract)
│   ├── HuggingFaceOcrService.cs   # Converts PDF to text via Hugging Face TrOCR (Python subprocess)
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
├── huggingface_ocr.py             # Python TrOCR helper (called by HuggingFaceOcrService)
├── requirements.txt               # Python dependencies for the Hugging Face engine
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

# Restore NuGet packages
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
