using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DdjToPng.Core.Interfaces;

namespace DdjToPng.Core.Services;

public sealed class DdsDecoderService : IDdsDecoderService
{
    private const int DdsHeaderSize = 128;

    private const int Dxt1 = 0x44585431;
    private const int Dxt2 = 0x44585432;
    private const int Dxt3 = 0x44585433;
    private const int Dxt4 = 0x44585434;
    private const int Dxt5 = 0x44585435;

    private const int A1R5G5B5 = 0x10002;
    private const int A4R4G4B4 = 0x30002;
    private const int A8B8G8R8 = 0x10004;
    private const int A8R8G8B8 = 0x30004;
    private const int R5G6B5   = 0x50002;
    private const int R8G8B8   = 0x10003;
    private const int X1R5G5B5 = 0x20002;
    private const int X4R4G4B4 = 0x40002;
    private const int X8B8G8R8 = 0x20004;
    private const int X8R8G8B8 = 0x40004;

    private static readonly int[] Bit5 = BuildBitTable(5);
    private static readonly int[] Bit6 = BuildBitTable(6);

    private static readonly int[] A1R5G5B5Masks = { 0x7C00, 0x03E0, 0x001F, 0x8000 };
    private static readonly int[] A4R4G4B4Masks = { 0x0F00, 0x00F0, 0x000F, 0xF000 };
    private static readonly int[] A8B8G8R8Masks = { 0x000000FF, 0x0000FF00, 0x00FF0000, unchecked((int)0xFF000000) };
    private static readonly int[] A8R8G8B8Masks = { 0x00FF0000, 0x0000FF00, 0x000000FF, unchecked((int)0xFF000000) };
    private static readonly int[] R5G6B5Masks   = { 0xF800, 0x07E0, 0x001F, 0x0000 };
    private static readonly int[] R8G8B8Masks   = { 0xFF0000, 0x00FF00, 0x0000FF, 0x000000 };
    private static readonly int[] X1R5G5B5Masks = { 0x7C00, 0x03E0, 0x001F, 0x0000 };
    private static readonly int[] X4R4G4B4Masks = { 0x0F00, 0x00F0, 0x000F, 0x0000 };
    private static readonly int[] X8B8G8R8Masks = { 0x000000FF, 0x0000FF00, 0x00FF0000, 0x00000000 };
    private static readonly int[] X8R8G8B8Masks = { 0x00FF0000, 0x0000FF00, 0x000000FF, 0x00000000 };

    // ARGB layout: A=24, R=16, G=8, B=0
    private static readonly (int A, int R, int G, int B) ArgbLayout = (24, 16, 8, 0);

    public int GetWidth(byte[] ddsBytes)
    {
        ValidateBuffer(ddsBytes);
        return ReadInt32(ddsBytes, 16);
    }

    public int GetHeight(byte[] ddsBytes)
    {
        ValidateBuffer(ddsBytes);
        return ReadInt32(ddsBytes, 12);
    }

    public Bitmap Decode(byte[] ddsBytes)
    {
        ValidateBuffer(ddsBytes);

        var width  = GetWidth(ddsBytes);
        var height = GetHeight(ddsBytes);
        var pixels = ReadPixels(ddsBytes, width, height);

        if (pixels is null)
            return new Bitmap(width > 0 ? width : 1, height > 0 ? height : 1);

        var bitmap    = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height),
                                          ImageLockMode.WriteOnly,
                                          bitmap.PixelFormat);
        Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
        bitmap.UnlockBits(bitmapData);
        return bitmap;
    }

    // ── private pipeline ─────────────────────────────────────────────────────

    private static int[]? ReadPixels(byte[] buf, int width, int height)
    {
        var type = DetectType(buf);
        if (type == 0) return null;

        return type switch
        {
            Dxt1     => DecodeDxt1(width, height, DdsHeaderSize, buf),
            Dxt2     => DecodeDxt3(width, height, DdsHeaderSize, buf),
            Dxt3     => DecodeDxt3(width, height, DdsHeaderSize, buf),
            Dxt4     => DecodeDxt5(width, height, DdsHeaderSize, buf),
            Dxt5     => DecodeDxt5(width, height, DdsHeaderSize, buf),
            A1R5G5B5 => ReadPackedRgb(width, height, DdsHeaderSize, buf, A1R5G5B5Masks, bitsPerPixel: 2, ReadA1R5G5B5Pixel),
            X1R5G5B5 => ReadPackedRgb(width, height, DdsHeaderSize, buf, X1R5G5B5Masks, bitsPerPixel: 2, ReadX1R5G5B5Pixel),
            A4R4G4B4 => ReadPackedRgb(width, height, DdsHeaderSize, buf, A4R4G4B4Masks, bitsPerPixel: 2, ReadA4R4G4B4Pixel),
            X4R4G4B4 => ReadPackedRgb(width, height, DdsHeaderSize, buf, X4R4G4B4Masks, bitsPerPixel: 2, ReadX4R4G4B4Pixel),
            R5G6B5   => ReadPackedRgb(width, height, DdsHeaderSize, buf, R5G6B5Masks,   bitsPerPixel: 2, ReadR5G6B5Pixel),
            R8G8B8   => ReadR8G8B8(width, height, DdsHeaderSize, buf),
            A8B8G8R8 => ReadA8B8G8R8(width, height, DdsHeaderSize, buf),
            X8B8G8R8 => ReadX8B8G8R8(width, height, DdsHeaderSize, buf),
            A8R8G8B8 => ReadA8R8G8B8(width, height, DdsHeaderSize, buf),
            X8R8G8B8 => ReadX8R8G8B8(width, height, DdsHeaderSize, buf),
            _        => null,
        };
    }

    // ── DXT1 ─────────────────────────────────────────────────────────────────

    private static int[] DecodeDxt1(int width, int height, int offset, byte[] buf)
    {
        var pixels = new int[width * height];
        var idx    = offset;
        var w = (width + 3) / 4;
        var h = (height + 3) / 4;

        for (var i = 0; i < h; i++)
        for (var j = 0; j < w; j++)
        {
            var c0 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
            var c1 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;

            for (var k = 0; k < 4; k++)
            {
                if (4 * i + k >= height) break;
                var t0 =  buf[idx] & 0x03;
                var t1 = (buf[idx] & 0x0C) >> 2;
                var t2 = (buf[idx] & 0x30) >> 4;
                var t3 = (buf[idx++] & 0xC0) >> 6;

                var baseIdx = 4 * width * i + 4 * j + width * k;
                pixels[baseIdx] = GetDxtColor(c0, c1, 0xFF, t0);
                if (4 * j + 1 < width) pixels[baseIdx + 1] = GetDxtColor(c0, c1, 0xFF, t1);
                if (4 * j + 2 < width) pixels[baseIdx + 2] = GetDxtColor(c0, c1, 0xFF, t2);
                if (4 * j + 3 < width) pixels[baseIdx + 3] = GetDxtColor(c0, c1, 0xFF, t3);
            }
        }
        return pixels;
    }

    // ── DXT3 ─────────────────────────────────────────────────────────────────

    private static int[] DecodeDxt3(int width, int height, int offset, byte[] buf)
    {
        var pixels     = new int[width * height];
        var alphaTable = new int[16];
        var idx        = offset;
        var w = (width + 3) / 4;
        var h = (height + 3) / 4;

        for (var i = 0; i < h; i++)
        for (var j = 0; j < w; j++)
        {
            for (var k = 0; k < 4; k++)
            {
                var a0 = buf[idx++] & 0xFF;
                var a1 = buf[idx++] & 0xFF;
                alphaTable[4 * k]     = 17 * ((a0 & 0xF0) >> 4);
                alphaTable[4 * k + 1] = 17 *  (a0 & 0x0F);
                alphaTable[4 * k + 2] = 17 * ((a1 & 0xF0) >> 4);
                alphaTable[4 * k + 3] = 17 *  (a1 & 0x0F);
            }

            var c0 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
            var c1 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;

            for (var k = 0; k < 4; k++)
            {
                if (4 * i + k >= height) break;
                var t0 =  buf[idx] & 0x03;
                var t1 = (buf[idx] & 0x0C) >> 2;
                var t2 = (buf[idx] & 0x30) >> 4;
                var t3 = (buf[idx++] & 0xC0) >> 6;

                var baseIdx = 4 * width * i + 4 * j + width * k;
                pixels[baseIdx] = GetDxtColor(c0, c1, alphaTable[4 * k], t0);
                if (4 * j + 1 < width) pixels[baseIdx + 1] = GetDxtColor(c0, c1, alphaTable[4 * k + 1], t1);
                if (4 * j + 2 < width) pixels[baseIdx + 2] = GetDxtColor(c0, c1, alphaTable[4 * k + 2], t2);
                if (4 * j + 3 < width) pixels[baseIdx + 3] = GetDxtColor(c0, c1, alphaTable[4 * k + 3], t3);
            }
        }
        return pixels;
    }

    // ── DXT5 ─────────────────────────────────────────────────────────────────

    private static int[] DecodeDxt5(int width, int height, int offset, byte[] buf)
    {
        var pixels     = new int[width * height];
        var alphaTable = new int[16];
        var idx        = offset;
        var w = (width + 3) / 4;
        var h = (height + 3) / 4;

        for (var i = 0; i < h; i++)
        for (var j = 0; j < w; j++)
        {
            var a0 = buf[idx++] & 0xFF;
            var a1 = buf[idx++] & 0xFF;
            var b0 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8) | ((buf[idx + 2] & 0xFF) << 16); idx += 3;
            var b1 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8) | ((buf[idx + 2] & 0xFF) << 16); idx += 3;

            for (var t = 0; t < 8; t++)  alphaTable[t]     = (b0 >> (t * 3)) & 0x07;
            for (var t = 0; t < 8; t++)  alphaTable[t + 8] = (b1 >> (t * 3)) & 0x07;

            var c0 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
            var c1 = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;

            for (var k = 0; k < 4; k++)
            {
                if (4 * i + k >= height) break;
                var t0 =  buf[idx] & 0x03;
                var t1 = (buf[idx] & 0x0C) >> 2;
                var t2 = (buf[idx] & 0x30) >> 4;
                var t3 = (buf[idx++] & 0xC0) >> 6;

                var baseIdx = 4 * width * i + 4 * j + width * k;
                pixels[baseIdx] = GetDxtColor(c0, c1, GetDxt5Alpha(a0, a1, alphaTable[4 * k]), t0);
                if (4 * j + 1 < width) pixels[baseIdx + 1] = GetDxtColor(c0, c1, GetDxt5Alpha(a0, a1, alphaTable[4 * k + 1]), t1);
                if (4 * j + 2 < width) pixels[baseIdx + 2] = GetDxtColor(c0, c1, GetDxt5Alpha(a0, a1, alphaTable[4 * k + 2]), t2);
                if (4 * j + 3 < width) pixels[baseIdx + 3] = GetDxtColor(c0, c1, GetDxt5Alpha(a0, a1, alphaTable[4 * k + 3]), t3);
            }
        }
        return pixels;
    }

    // ── Uncompressed RGB readers ──────────────────────────────────────────────

    private static int[] ReadR8G8B8(int width, int height, int offset, byte[] buf)
    {
        var pixels = new int[width * height];
        var idx    = offset;
        var (a, r, g, b) = ArgbLayout;
        for (var i = 0; i < width * height; i++)
        {
            var bv = buf[idx++] & 0xFF;
            var gv = buf[idx++] & 0xFF;
            var rv = buf[idx++] & 0xFF;
            pixels[i] = (255 << a) | (rv << r) | (gv << g) | (bv << b);
        }
        return pixels;
    }

    private static int[] ReadA8R8G8B8(int width, int height, int offset, byte[] buf)
    {
        var pixels = new int[width * height];
        var idx    = offset;
        var (a, r, g, b) = ArgbLayout;
        for (var i = 0; i < width * height; i++)
        {
            var bv = buf[idx++] & 0xFF;
            var gv = buf[idx++] & 0xFF;
            var rv = buf[idx++] & 0xFF;
            var av = buf[idx++] & 0xFF;
            pixels[i] = (av << a) | (rv << r) | (gv << g) | (bv << b);
        }
        return pixels;
    }

    private static int[] ReadX8R8G8B8(int width, int height, int offset, byte[] buf)
    {
        var pixels = new int[width * height];
        var idx    = offset;
        var (a, r, g, b) = ArgbLayout;
        for (var i = 0; i < width * height; i++)
        {
            var bv = buf[idx++] & 0xFF;
            var gv = buf[idx++] & 0xFF;
            var rv = buf[idx++] & 0xFF;
            idx++;
            pixels[i] = (255 << a) | (rv << r) | (gv << g) | (bv << b);
        }
        return pixels;
    }

    private static int[] ReadA8B8G8R8(int width, int height, int offset, byte[] buf)
    {
        var pixels = new int[width * height];
        var idx    = offset;
        var (a, r, g, b) = ArgbLayout;
        for (var i = 0; i < width * height; i++)
        {
            var rv = buf[idx++] & 0xFF;
            var gv = buf[idx++] & 0xFF;
            var bv = buf[idx++] & 0xFF;
            var av = buf[idx++] & 0xFF;
            pixels[i] = (av << a) | (rv << r) | (gv << g) | (bv << b);
        }
        return pixels;
    }

    private static int[] ReadX8B8G8R8(int width, int height, int offset, byte[] buf)
    {
        var pixels = new int[width * height];
        var idx    = offset;
        var (a, r, g, b) = ArgbLayout;
        for (var i = 0; i < width * height; i++)
        {
            var rv = buf[idx++] & 0xFF;
            var gv = buf[idx++] & 0xFF;
            var bv = buf[idx++] & 0xFF;
            idx++;
            pixels[i] = (255 << a) | (rv << r) | (gv << g) | (bv << b);
        }
        return pixels;
    }

    private delegate int PixelReader(byte[] buf, ref int idx);

    private static int[] ReadPackedRgb(int width, int height, int offset, byte[] buf,
                                        int[] masks, int bitsPerPixel, PixelReader reader)
    {
        _ = masks;
        var pixels = new int[width * height];
        var idx    = offset;
        for (var i = 0; i < width * height; i++)
            pixels[i] = reader(buf, ref idx);
        return pixels;
    }

    private static int ReadA1R5G5B5Pixel(byte[] buf, ref int idx)
    {
        var rgba = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
        var (a, r, g, b) = ArgbLayout;
        var rv = Bit5[(rgba & A1R5G5B5Masks[0]) >> 10];
        var gv = Bit5[(rgba & A1R5G5B5Masks[1]) >> 5];
        var bv = Bit5[rgba & A1R5G5B5Masks[2]];
        var av = 255 * ((rgba & A1R5G5B5Masks[3]) >> 15);
        return (av << a) | (rv << r) | (gv << g) | (bv << b);
    }

    private static int ReadX1R5G5B5Pixel(byte[] buf, ref int idx)
    {
        var rgba = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
        var (a, r, g, b) = ArgbLayout;
        var rv = Bit5[(rgba & X1R5G5B5Masks[0]) >> 10];
        var gv = Bit5[(rgba & X1R5G5B5Masks[1]) >> 5];
        var bv = Bit5[rgba & X1R5G5B5Masks[2]];
        return (255 << a) | (rv << r) | (gv << g) | (bv << b);
    }

    private static int ReadA4R4G4B4Pixel(byte[] buf, ref int idx)
    {
        var rgba = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
        var (a, r, g, b) = ArgbLayout;
        var rv = 17 * ((rgba & A4R4G4B4Masks[0]) >> 8);
        var gv = 17 * ((rgba & A4R4G4B4Masks[1]) >> 4);
        var bv = 17 *  (rgba & A4R4G4B4Masks[2]);
        var av = 17 * ((rgba & A4R4G4B4Masks[3]) >> 12);
        return (av << a) | (rv << r) | (gv << g) | (bv << b);
    }

    private static int ReadX4R4G4B4Pixel(byte[] buf, ref int idx)
    {
        var rgba = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
        var (a, r, g, b) = ArgbLayout;
        var rv = 17 * ((rgba & X4R4G4B4Masks[0]) >> 8);
        var gv = 17 * ((rgba & X4R4G4B4Masks[1]) >> 4);
        var bv = 17 *  (rgba & X4R4G4B4Masks[2]);
        return (255 << a) | (rv << r) | (gv << g) | (bv << b);
    }

    private static int ReadR5G6B5Pixel(byte[] buf, ref int idx)
    {
        var rgba = (buf[idx] & 0xFF) | ((buf[idx + 1] & 0xFF) << 8); idx += 2;
        var (a, r, g, b) = ArgbLayout;
        var rv = Bit5[(rgba & R5G6B5Masks[0]) >> 11];
        var gv = Bit6[(rgba & R5G6B5Masks[1]) >> 5];
        var bv = Bit5[rgba & R5G6B5Masks[2]];
        return (255 << a) | (rv << r) | (gv << g) | (bv << b);
    }

    // ── DXT color helpers ─────────────────────────────────────────────────────

    private static int GetDxtColor(int c0, int c1, int alpha, int t) => t switch
    {
        0 => MakeDxtColor1(c0, alpha),
        1 => MakeDxtColor1(c1, alpha),
        2 => c0 > c1 ? MakeDxtColor2_1(c0, c1, alpha) : MakeDxtColor1_1(c0, c1, alpha),
        3 => c0 > c1 ? MakeDxtColor2_1(c1, c0, alpha) : 0,
        _ => 0,
    };

    private static int MakeDxtColor1(int c, int alpha)
    {
        var (a, r, g, b) = ArgbLayout;
        return (alpha << a) | (Bit5[(c & 0xFC00) >> 11] << r) | (Bit6[(c & 0x07E0) >> 5] << g) | (Bit5[c & 0x001F] << b);
    }

    private static int MakeDxtColor1_1(int c0, int c1, int alpha)
    {
        var (a, r, g, b) = ArgbLayout;
        var rv = (Bit5[(c0 & 0xFC00) >> 11] + Bit5[(c1 & 0xFC00) >> 11]) / 2;
        var gv = (Bit6[(c0 & 0x07E0) >> 5]  + Bit6[(c1 & 0x07E0) >> 5])  / 2;
        var bv = (Bit5[c0 & 0x001F]          + Bit5[c1 & 0x001F])          / 2;
        return (alpha << a) | (rv << r) | (gv << g) | (bv << b);
    }

    private static int MakeDxtColor2_1(int c0, int c1, int alpha)
    {
        var (a, r, g, b) = ArgbLayout;
        var rv = (2 * Bit5[(c0 & 0xFC00) >> 11] + Bit5[(c1 & 0xFC00) >> 11]) / 3;
        var gv = (2 * Bit6[(c0 & 0x07E0) >> 5]  + Bit6[(c1 & 0x07E0) >> 5])  / 3;
        var bv = (2 * Bit5[c0 & 0x001F]          + Bit5[c1 & 0x001F])          / 3;
        return (alpha << a) | (rv << r) | (gv << g) | (bv << b);
    }

    private static int GetDxt5Alpha(int a0, int a1, int t)
    {
        if (a0 > a1)
            return t switch
            {
                0 => a0,
                1 => a1,
                2 => (6 * a0 + a1) / 7,
                3 => (5 * a0 + 2 * a1) / 7,
                4 => (4 * a0 + 3 * a1) / 7,
                5 => (3 * a0 + 4 * a1) / 7,
                6 => (2 * a0 + 5 * a1) / 7,
                7 => (a0 + 6 * a1) / 7,
                _ => 0,
            };
        return t switch
        {
            0 => a0,
            1 => a1,
            2 => (4 * a0 + a1) / 5,
            3 => (3 * a0 + 2 * a1) / 5,
            4 => (2 * a0 + 3 * a1) / 5,
            5 => (a0 + 4 * a1) / 5,
            6 => 0,
            7 => 255,
            _ => 0,
        };
    }

    // ── format detection ──────────────────────────────────────────────────────

    private static int DetectType(byte[] buf)
    {
        var flags = ReadInt32(buf, 80);

        if ((flags & 0x04) != 0)
        {
            return ((buf[84] & 0xFF) << 24)
                 | ((buf[85] & 0xFF) << 16)
                 | ((buf[86] & 0xFF) << 8)
                 |  (buf[87] & 0xFF);
        }

        if ((flags & 0x40) != 0)
        {
            var bitCount  = ReadInt32(buf, 88);
            var rMask     = ReadInt32(buf, 92);
            var gMask     = ReadInt32(buf, 96);
            var bMask     = ReadInt32(buf, 100);
            var hasAlpha  = (flags & 0x01) != 0;
            var aMask     = hasAlpha ? ReadInt32(buf, 104) : 0;

            return bitCount switch
            {
                16 when Matches(rMask, gMask, bMask, aMask, A1R5G5B5Masks) => A1R5G5B5,
                16 when Matches(rMask, gMask, bMask, aMask, X1R5G5B5Masks) => X1R5G5B5,
                16 when Matches(rMask, gMask, bMask, aMask, A4R4G4B4Masks) => A4R4G4B4,
                16 when Matches(rMask, gMask, bMask, aMask, X4R4G4B4Masks) => X4R4G4B4,
                16 when Matches(rMask, gMask, bMask, aMask, R5G6B5Masks)   => R5G6B5,
                24 when Matches(rMask, gMask, bMask, aMask, R8G8B8Masks)   => R8G8B8,
                32 when Matches(rMask, gMask, bMask, aMask, A8B8G8R8Masks) => A8B8G8R8,
                32 when Matches(rMask, gMask, bMask, aMask, X8B8G8R8Masks) => X8B8G8R8,
                32 when Matches(rMask, gMask, bMask, aMask, A8R8G8B8Masks) => A8R8G8B8,
                32 when Matches(rMask, gMask, bMask, aMask, X8R8G8B8Masks) => X8R8G8B8,
                _ => 0,
            };
        }

        return 0;
    }

    private static bool Matches(int r, int g, int b, int a, int[] masks) =>
        r == masks[0] && g == masks[1] && b == masks[2] && a == masks[3];

    // ── utilities ─────────────────────────────────────────────────────────────

    private static int ReadInt32(byte[] buf, int offset) =>
        (buf[offset] & 0xFF)
        | ((buf[offset + 1] & 0xFF) << 8)
        | ((buf[offset + 2] & 0xFF) << 16)
        | ((buf[offset + 3] & 0xFF) << 24);

    private static void ValidateBuffer(byte[] ddsBytes)
    {
        ArgumentNullException.ThrowIfNull(ddsBytes);
        if (ddsBytes.Length < DdsHeaderSize)
            throw new ArgumentException($"Buffer must be at least {DdsHeaderSize} bytes (DDS header size).", nameof(ddsBytes));
    }

    private static int[] BuildBitTable(int bits)
    {
        var count = 1 << bits;
        var table = new int[count];
        for (var i = 0; i < count; i++)
            table[i] = (int)Math.Round(i * 255.0 / (count - 1));
        return table;
    }
}
