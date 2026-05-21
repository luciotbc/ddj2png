namespace DdjToPng.Core.Models;

public sealed record ConversionResult(
    string SourcePath,
    string? OutputPath,
    bool Success,
    string? ErrorMessage = null
);
