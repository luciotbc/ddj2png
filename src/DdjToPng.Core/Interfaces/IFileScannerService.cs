namespace DdjToPng.Core.Interfaces;

public interface IFileScannerService
{
    IReadOnlyList<string> FindDdjFiles(string directory, bool recursive = false);
}
