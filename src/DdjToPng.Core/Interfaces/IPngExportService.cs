using System.Drawing;

namespace DdjToPng.Core.Interfaces;

public interface IPngExportService
{
    string Export(Bitmap bitmap, string outputPath);
}
