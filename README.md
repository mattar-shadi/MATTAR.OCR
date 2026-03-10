# MATTAR.OCR

![.NET](https://img.shields.io/badge/.NET-7.0-512BD4?logo=dotnet) ![NuGet](https://img.shields.io/nuget/v/MATTAR.OCR?logo=nuget) ![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)

**MATTAR.OCR** is a C# .NET 7 library that extracts text from PDF documents and images using the Tesseract OCR engine. It solves the common need to programmatically read and digitise scanned PDFs or raster images by providing a clean, interface-driven API that integrates easily into any .NET application.

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
- **Cross-architecture support**: Bundled Tesseract native libraries for both `x86` and `x64` environments.
- **Cross-platform**: Works on Windows, Linux, and macOS (x64/arm64) with no manual DLL setup required.
- **Dependency-injection friendly**: All services are backed by interfaces (`IPdfToTextService`, `IPdfToImageService`, `IOCRPath`) and accept constructor injection.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# 11 / .NET 7.0 |
| OCR engine | [Tesseract 5.2.0](https://www.nuget.org/packages/Tesseract/) |
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
| **.NET 7.0 SDK** | [Download](https://dotnet.microsoft.com/download/dotnet/7.0) |
| **Tesseract language data** | A `tessdata/` directory containing the desired language data files (e.g. `fra.traineddata`) must exist under your application's root path. Download language packs from the [tessdata repository](https://github.com/tesseract-ocr/tessdata). |

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
└── tessdata/
    └── fra.traineddata  # Tesseract language data (French by default)

<temp path>/           # Writable directory for intermediate PNG files
```

> **Note:** The OCR language is currently hardcoded to French (`"fra"`). To use a different language, change the language code in `PdfToTextService.cs` and provide the corresponding `.traineddata` file in `tessdata/`.

### Environment variables

| Variable | Where used | Purpose |
|---|---|---|
| `NUGET_API_KEY` | GitHub Actions secret | Authenticates NuGet package publishing |

---

## Project Structure

```
MATTAR.OCR/
├── src/
│   ├── MATTAR.OCR.csproj          # Library project file (.NET 7.0)
│   ├── PdfToImageService.cs       # Converts PDF pages to PNG images (PDFtoImage/PDFium)
│   ├── PdfToTextService.cs        # Converts PDF to text via image pipeline (Tesseract)
│   └── Interfaces/
│       ├── IOCRPath.cs            # Path abstraction (root + temp paths)
│       ├── IPdfToImageService.cs  # PDF-to-image contract
│       └── IPdfToTextService.cs   # PDF-to-text contract
│
├── tests/
│   └── MATTAR.OCR.Tests/
│       ├── MATTAR.OCR.Tests.csproj
│       ├── SmpleTests.cs          # NUnit test fixtures
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
