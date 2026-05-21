# ddj2png

> Batch-convert **DDJ** image files from Silkroad Online into standard **PNG** files — fast, parallel, and folder-structure-aware. Think of it as a WinRAR-style extractor, but for `.ddj` textures.

![ddj2png main screen](ddj2png_printscreen.png)

---

## What is it?

Silkroad Online stores its UI and skill textures as `.ddj` files — a proprietary container that is just a standard DDS (DirectDraw Surface) file with a 20-byte custom header prepended. **ddj2png** strips that header and decodes the DDS payload into a regular PNG, for every file in a folder tree, in one click.

It is designed for bulk work: point it at your unpacked PK2 folder (thousands of files), pick an output folder, and let it rip — all files are processed in parallel and the original subfolder structure is preserved under the output root.

---

## Quick start

1. **Download** the latest `ddj2png.exe` from [Releases](../../releases) (single-file, no install needed).
2. Run it — no .NET runtime required in the self-contained build.
3. Follow the steps below.

---

## How to use

### Step-by-step

| # | What to do | Notes |
|---|-----------|-------|
| 1 | Click **Browse…** next to **Source directory** | Select the root folder that contains your `.ddj` files (e.g. `C:\sro\pk2\Data`) |
| 2 | Check **Search subdirectories recursively** | Tick this to scan all nested folders — recommended for full PK2 trees |
| 3 | Click **Browse…** next to **Output directory** | Select (or create) the destination folder (e.g. `C:\sro\pk2_png`) |
| 4 | Click **Scan for DDJ Files** | The app lists every `.ddj` it found and shows the count |
| 5 | Click **Convert** | A progress bar tracks completion; status bar shows `x/total` in real time |
| 6 | *(optional)* Click **Cancel** | Stops in-flight conversions gracefully — already-saved files are kept |

### Folder structure is preserved

Output files mirror the input tree, so you never lose track of where a texture came from:

```
Input:   C:\sro\pk2\Data\icon\skills\ice.ddj
Output:  C:\sro\pk2_png\Data\icon\skills\ice.png
```

This makes it safe to diff two conversion runs, or to drop the output next to the original tree.

---

## What it converts

Supports every DDS pixel format used by the Silkroad client:

| Format | Description |
|--------|-------------|
| DXT1 | BC1 — opaque or 1-bit alpha |
| DXT2 / DXT3 | BC2 — explicit 4-bit alpha |
| DXT4 / DXT5 | BC3 — interpolated alpha |
| R8G8B8 | 24-bit uncompressed RGB |
| A8R8G8B8 | 32-bit ARGB |
| X8R8G8B8 | 32-bit RGB (alpha ignored) |
| A8B8G8R8 | 32-bit ABGR |
| A1R5G5B5 / X1R5G5B5 | 16-bit, 1-bit alpha |
| A4R4G4B4 / X4R4G4B4 | 16-bit, 4-bit alpha |
| R5G6B5 | 16-bit, no alpha |

---

## How the conversion works

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
PngExportService   ← saves Bitmap as PNG (creates subdirectories as needed)
     │
     ▼
PNG file on disk
```

`ConversionService` orchestrates the pipeline with `Parallel.ForEachAsync` (default: 4 threads), keeping I/O off the UI thread and capping progress updates at ~200 per run to stay responsive on large batches.

---

## Building from source

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), Windows.

```bash
# run
dotnet run --project src/DdjToPng.App

# debug build
dotnet build src/DdjToPng.App/DdjToPng.App.csproj

# single-file self-contained exe
dotnet publish src/DdjToPng.App/DdjToPng.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
# → publish/ddj2png.exe

# tests
dotnet test
```

---

## Architecture

Clean Architecture with a strict Core / UI split:

```
/src
  /DdjToPng.Core      ← pure business logic (no WinForms dependency)
    /Interfaces       ← service contracts
    /Models           ← immutable records (ConversionResult, ConversionOptions)
    /Services         ← implementations
    /ViewModels       ← presentation logic (INotifyPropertyChanged, no MVVM framework)
  /DdjToPng.App       ← WinForms shell, manual DI wiring in Program.cs
    /Views            ← MainForm
/tests
  /DdjToPng.Core.Tests
    /Decoders         ← DDS decoder tests (in-memory fake buffers, no disk I/O)
    /Services         ← service unit tests
    /ViewModels       ← ViewModel tests
```

Every service and ViewModel was built test-first (red → green → refactor). All 43 tests run without touching the filesystem except for `PngExportService` integration tests.
