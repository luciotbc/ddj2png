using System.Drawing;

namespace DdjToPng.Core.Interfaces;

public interface IDdsDecoderService
{
    Bitmap Decode(byte[] ddsBytes);
    int GetWidth(byte[] ddsBytes);
    int GetHeight(byte[] ddsBytes);
}
