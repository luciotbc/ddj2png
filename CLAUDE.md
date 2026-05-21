# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Build
dotnet build src/DdjToPng.App/DdjToPng.App.csproj

# Run
dotnet run --project src/DdjToPng.App

# Test (all)
dotnet test

# Test (single project)
dotnet test tests/DdjToPng.Core.Tests/DdjToPng.Core.Tests.csproj

# Publish (self-contained single-file exe)
dotnet publish src/DdjToPng.App/DdjToPng.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```

## Architecture

Three projects:

- **DdjToPng.Core** — pure business logic, no UI dependencies. Contains interfaces, immutable record models, services, and `MainViewModel`.
- **DdjToPng.App** — WinForms UI shell (`net10.0-windows`). Manually wires DI in `Program.cs` and binds controls to `MainViewModel`.
- **DdjToPng.Core.Tests** — xunit tests with FluentAssertions and Moq. All DDS decoder tests use in-memory fake buffers (no disk I/O).

### Conversion pipeline

```
DDJ file → DdjReaderService (strip 20-byte header)
         → DdsDecoderService (DXT1/3/5 or RGB → Bitmap)
         → PngExportService (save PNG)
```

`ConversionService` orchestrates the pipeline using `Parallel.ForEachAsync` (default max 4 threads) with cancellation and throttled progress reporting (~200 UI updates max).

### Key patterns

- **MVVM-lite**: `MainViewModel` implements `INotifyPropertyChanged` with a `SetField<T>` helper. No external MVVM framework.
- **Command-like methods**: `ScanFiles()`, `ConvertAsync()`, `Cancel()` on the ViewModel bound directly to UI button handlers.
- **DDS decoder**: Manual `Marshal.Copy` + unsafe bit-shifting with precomputed `Bit5`/`Bit6` lookup tables for block decompression. Format detected via magic constants (e.g., `0x44445320` DDS magic, `0x44585431` DXT1). No external image libraries.
- **Immutable models**: `ConversionOptions` and `ConversionResult` are records.
- **Sealed services**: All service implementations are sealed.

## Development approach

This project was built test-first (TDD red-green-refactor). When adding new features, write the Core test first, implement in Core, then wire to the UI.
