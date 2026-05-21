namespace DdjToPng.Core.Models;

public sealed record ConversionOptions(
    string OutputDirectory,
    string InputDirectory = "",
    bool OverwriteExisting = false,
    int MaxDegreeOfParallelism = 4
);
