using System.Drawing;
using System.Drawing.Imaging;
using DdjToPng.Core.Interfaces;

namespace DdjToPng.Core.Services;

public sealed class PngExportService : IPngExportService
{
    public string Export(Bitmap bitmap, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        bitmap.Save(outputPath, ImageFormat.Png);
        return outputPath;
    }
}
