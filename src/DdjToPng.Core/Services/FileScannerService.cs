using DdjToPng.Core.Interfaces;

namespace DdjToPng.Core.Services;

public sealed class FileScannerService : IFileScannerService
{
    public IReadOnlyList<string> FindDdjFiles(string directory, bool recursive = false)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Directory not found: {directory}");

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(directory, "*.ddj", option);
    }
}
