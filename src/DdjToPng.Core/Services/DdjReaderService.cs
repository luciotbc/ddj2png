using DdjToPng.Core.Interfaces;

namespace DdjToPng.Core.Services;

public sealed class DdjReaderService : IDdjReaderService
{
    private const int DdjHeaderSize = 20;

    public byte[] ReadDdsBytes(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("DDJ file not found.", filePath);

        var raw = File.ReadAllBytes(filePath);
        return StripDdjHeader(raw);
    }

    public byte[] StripDdjHeader(byte[] ddjBytes)
    {
        ArgumentNullException.ThrowIfNull(ddjBytes);
        if (ddjBytes.Length < DdjHeaderSize)
            throw new ArgumentException($"Input must be at least {DdjHeaderSize} bytes to contain a valid DDJ header.", nameof(ddjBytes));

        var dds = new byte[ddjBytes.Length - DdjHeaderSize];
        Buffer.BlockCopy(ddjBytes, DdjHeaderSize, dds, 0, dds.Length);
        return dds;
    }
}
