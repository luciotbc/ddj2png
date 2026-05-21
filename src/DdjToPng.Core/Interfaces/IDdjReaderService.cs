namespace DdjToPng.Core.Interfaces;

public interface IDdjReaderService
{
    /// <summary>Reads a DDJ file and returns the embedded DDS bytes (header stripped).</summary>
    byte[] ReadDdsBytes(string filePath);

    /// <summary>Strips the 20-byte DDJ header from raw DDJ bytes and returns DDS bytes.</summary>
    byte[] StripDdjHeader(byte[] ddjBytes);
}
