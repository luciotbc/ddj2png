namespace DdjToPng.Core.Tests.Helpers;

/// <summary>Builds minimal valid DDS byte buffers for unit testing without touching the disk.</summary>
public static class DdsTestDataBuilder
{
    private const int HeaderSize = 128;

    public static byte[] BuildDxt1(int width = 4, int height = 4)
    {
        var buffer = new byte[HeaderSize + 8]; // one 4x4 block = 8 bytes for DXT1
        WriteHeader(buffer, width, height, isDxt: true, fourCC: "DXT1");
        WriteDxt1Block(buffer, HeaderSize, 0xF800, 0x001F); // red / blue
        return buffer;
    }

    public static byte[] BuildDxt3(int width = 4, int height = 4)
    {
        var buffer = new byte[HeaderSize + 16]; // one 4x4 block = 16 bytes for DXT3
        WriteHeader(buffer, width, height, isDxt: true, fourCC: "DXT3");
        // alpha table: all opaque (0xFF per nibble → 0xFF bytes)
        for (var i = HeaderSize; i < HeaderSize + 8; i++)
            buffer[i] = 0xFF;
        WriteDxt1Block(buffer, HeaderSize + 8, 0xF800, 0x001F);
        return buffer;
    }

    public static byte[] BuildDxt5(int width = 4, int height = 4)
    {
        var buffer = new byte[HeaderSize + 16];
        WriteHeader(buffer, width, height, isDxt: true, fourCC: "DXT5");
        buffer[HeaderSize] = 0xFF; // a0 = 255
        buffer[HeaderSize + 1] = 0x00; // a1 = 0
        // alpha indices: all 0 → all use a0 = 255
        WriteDxt1Block(buffer, HeaderSize + 8, 0xF800, 0x001F);
        return buffer;
    }

    public static byte[] BuildArgb8888(int width = 2, int height = 2, byte r = 255, byte g = 0, byte b = 0, byte a = 255)
    {
        var buffer = new byte[HeaderSize + width * height * 4];
        WriteHeader(buffer, width, height, isDxt: false, bitCount: 32,
            rMask: 0x00FF0000, gMask: 0x0000FF00, bMask: 0x000000FF, aMask: unchecked((int)0xFF000000), hasAlpha: true);
        var idx = HeaderSize;
        for (var i = 0; i < width * height; i++)
        {
            // stored as BGRA in memory for A8R8G8B8
            buffer[idx++] = b;
            buffer[idx++] = g;
            buffer[idx++] = r;
            buffer[idx++] = a;
        }
        return buffer;
    }

    public static byte[] BuildR8G8B8(int width = 2, int height = 2, byte r = 0, byte g = 255, byte b = 0)
    {
        var buffer = new byte[HeaderSize + width * height * 3];
        WriteHeader(buffer, width, height, isDxt: false, bitCount: 24,
            rMask: 0xFF0000, gMask: 0x00FF00, bMask: 0x0000FF, aMask: 0, hasAlpha: false);
        var idx = HeaderSize;
        for (var i = 0; i < width * height; i++)
        {
            buffer[idx++] = b;
            buffer[idx++] = g;
            buffer[idx++] = r;
        }
        return buffer;
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static void WriteHeader(byte[] buf, int width, int height,
        bool isDxt, string? fourCC = null, int bitCount = 0,
        int rMask = 0, int gMask = 0, int bMask = 0, int aMask = 0, bool hasAlpha = false)
    {
        // DDS magic
        buf[0] = (byte)'D'; buf[1] = (byte)'D'; buf[2] = (byte)'S'; buf[3] = (byte)' ';
        // dwSize = 124
        Write32(buf, 4, 124);
        // dwFlags: DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT
        Write32(buf, 8, 0x1007);
        // height, width (little-endian, offsets 12 and 16)
        Write32(buf, 12, height);
        Write32(buf, 16, width);
        // mipmap count = 1 at offset 28
        Write32(buf, 28, 1);
        // ddspf starts at offset 76, dwSize=32
        Write32(buf, 76, 32);
        if (isDxt)
        {
            // dwFlags DDPF_FOURCC = 0x04
            Write32(buf, 80, 0x04);
            // fourCC
            buf[84] = (byte)fourCC![0];
            buf[85] = (byte)fourCC[1];
            buf[86] = (byte)fourCC[2];
            buf[87] = (byte)fourCC[3];
        }
        else
        {
            // dwFlags DDPF_RGB = 0x40, with optional alpha
            Write32(buf, 80, hasAlpha ? 0x41 : 0x40);
            Write32(buf, 88, bitCount);
            Write32(buf, 92, rMask);
            Write32(buf, 96, gMask);
            Write32(buf, 100, bMask);
            Write32(buf, 104, aMask);
        }
    }

    private static void WriteDxt1Block(byte[] buf, int offset, int c0, int c1)
    {
        buf[offset] = (byte)(c0 & 0xFF);
        buf[offset + 1] = (byte)((c0 >> 8) & 0xFF);
        buf[offset + 2] = (byte)(c1 & 0xFF);
        buf[offset + 3] = (byte)((c1 >> 8) & 0xFF);
        // indices: all 0 → all pixels use color0
        buf[offset + 4] = 0x00;
        buf[offset + 5] = 0x00;
        buf[offset + 6] = 0x00;
        buf[offset + 7] = 0x00;
    }

    private static void Write32(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
