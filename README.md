# ddj2png

A Windows desktop utility that converts **DDJ** image files (used by Silkroad Online) to standard **PNG** files.

---

## Introduction

### What is the DDJ format?

DDJ is a proprietary image container used by the Silkroad Online game client. It is a DDS (DirectDraw Surface) file with a **20-byte custom header** prepended to it. To decode a DDJ file:

1. Strip the first 20 bytes (the DDJ header).
2. Decode the remaining bytes as a standard DDS file.

### How does the conversion work?

```
DDJ file on disk
     │
     ▼
DdjReaderService   ← strips 20-byte DDJ header
     │
     ▼
DdsDecoderService  ← decodes DDS (DXT1 / DXT3 / DXT5 / RGB variants) → Bitmap
     │
     ▼
PngExportService   ← saves Bitmap as PNG
     │
     ▼
PNG file on disk
```

The `ConversionService` orchestrates the pipeline in parallel, with progress reporting and cancellation support.

---

## Technical details

### Architecture

The project follows **Clean Architecture** with a strict separation between core logic and UI:

```
/src
  /DdjToPng.Core      ← pure business logic (no WinForms dependency)
    /Interfaces       ← service contracts
    /Models           ← immutable records (ConversionResult, ConversionOptions)
    /Services         ← implementations
    /ViewModels       ← presentation logic (INotifyPropertyChanged)
  /DdjToPng.App       ← WinForms shell, wires up services
    /Views            ← MainForm
/tests
  /DdjToPng.Core.Tests
    /Decoders         ← DDS decoder tests
    /Helpers          ← DdsTestDataBuilder (fake DDS bytes, no disk I/O)
    /Services         ← service unit tests
    /ViewModels       ← ViewModel tests
```

### DDS decoder

`DdsDecoderService` supports:

| Format     | Description                          |
|------------|--------------------------------------|
| DXT1       | BC1 — opaque or 1-bit alpha          |
| DXT2/DXT3  | BC2 — explicit 4-bit alpha           |
| DXT4/DXT5  | BC3 — interpolated alpha             |
| R8G8B8     | 24-bit uncompressed RGB              |
| A8R8G8B8   | 32-bit ARGB                          |
| X8R8G8B8   | 32-bit RGB (alpha ignored)           |
| A8B8G8R8   | 32-bit ABGR                          |
| X8B8G8R8   | 32-bit BGR (alpha ignored)           |
| A1R5G5B5   | 16-bit — 1-bit alpha                 |
| X1R5G5B5   | 16-bit — no alpha                    |
| A4R4G4B4   | 16-bit — 4-bit alpha                 |
| X4R4G4B4   | 16-bit — no alpha                    |
| R5G6B5     | 16-bit — no alpha                    |

### Conversion pipeline

`ConversionService.ConvertAsync` uses `Parallel.ForEachAsync` with a configurable `MaxDegreeOfParallelism` (default: 4). Each file is processed in a dedicated `Task.Run` call to keep I/O off the UI thread.

### Cancellation

Pass a `CancellationToken` to `ConvertAsync`. Each file task checks the token before and after reading. On cancellation, `OperationCanceledException` propagates out of the method. The `MainViewModel.Cancel()` method triggers the `CancellationTokenSource` held during an active conversion.

### TDD approach

Every service and ViewModel was built following strict TDD:

1. Write a failing test.
2. Run — confirm it fails (red).
3. Write the minimal implementation to make it pass.
4. Run — confirm it passes (green).
5. Refactor if needed; re-run tests.

All decoder tests use **in-memory fake DDS buffers** (`DdsTestDataBuilder`) — no files are written to disk. Moq is used to mock service boundaries in `ConversionService` and `MainViewModel` tests.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Windows (WinForms target: `net10.0-windows`)

### NuGet dependencies (Core.Tests)

| Package              | Version | Purpose                |
|----------------------|---------|------------------------|
| xunit                | 2.9.x   | Test framework         |
| FluentAssertions     | 6.x     | Readable assertions    |
| Moq                  | 4.x     | Mock dependencies      |

---

## Running the tests

```bash
dotnet test tests/DdjToPng.Core.Tests/DdjToPng.Core.Tests.csproj
```

Or run all projects in the solution:

```bash
dotnet test
```

---

## Building

### Debug build

```bash
dotnet build src/DdjToPng.App/DdjToPng.App.csproj
```

### Release build

```bash
dotnet build src/DdjToPng.App/DdjToPng.App.csproj -c Release
```

### Single-file publish (self-contained)

```bash
dotnet publish src/DdjToPng.App/DdjToPng.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```

The executable will be at `publish/ddj2png.exe`.

---

## Running the application

1. Open `ddj2png.exe` (or run `dotnet run --project src/DdjToPng.App`).
2. Click **Browse…** next to *Source directory* and select the folder containing your `.ddj` files.
3. Click **Browse…** next to *Output directory* and select where PNGs should be saved.
4. Optionally check **Search subdirectories recursively**.
5. Click **Scan for DDJ Files** — the file list will populate.
6. Click **Convert** — a progress bar tracks completion.
7. Click **Cancel** at any time to abort the conversion.

Log messages (status, errors) are shown in the status bar at the bottom of the window.

---

## Project structure

```
ddj2png/
├── src/
│   ├── DdjToPng.Core/
│   │   ├── Interfaces/
│   │   │   ├── IConversionService.cs
│   │   │   ├── IDdjReaderService.cs
│   │   │   ├── IDdsDecoderService.cs
│   │   │   ├── IFileScannerService.cs
│   │   │   └── IPngExportService.cs
│   │   ├── Models/
│   │   │   ├── ConversionOptions.cs
│   │   │   └── ConversionResult.cs
│   │   ├── Services/
│   │   │   ├── ConversionService.cs
│   │   │   ├── DdjReaderService.cs
│   │   │   ├── DdsDecoderService.cs
│   │   │   ├── FileScannerService.cs
│   │   │   └── PngExportService.cs
│   │   └── ViewModels/
│   │       └── MainViewModel.cs
│   └── DdjToPng.App/
│       ├── Views/
│       │   └── MainForm.cs
│       └── Program.cs
└── tests/
    └── DdjToPng.Core.Tests/
        ├── Decoders/
        │   └── DdsDecoderServiceTests.cs
        ├── Helpers/
        │   └── DdsTestDataBuilder.cs
        ├── Services/
        │   ├── ConversionServiceTests.cs
        │   ├── DdjReaderServiceTests.cs
        │   ├── FileScannerServiceTests.cs
        │   └── PngExportServiceTests.cs
        └── ViewModels/
            └── MainViewModelTests.cs
```
