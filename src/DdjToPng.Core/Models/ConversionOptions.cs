namespace DdjToPng.Core.Models;

public sealed record ConversionOptions(
    string OutputDirectory,
    bool OverwriteExisting = false,
    int MaxDegreeOfParallelism = 4
);
