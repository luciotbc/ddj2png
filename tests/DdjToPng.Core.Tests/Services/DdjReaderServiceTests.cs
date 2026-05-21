using DdjToPng.Core.Services;
using DdjToPng.Core.Tests.Helpers;

namespace DdjToPng.Core.Tests.Services;

public sealed class DdjReaderServiceTests
{
    private readonly DdjReaderService _sut = new();
    private const int DdjHeaderSize = 20;

    [Fact]
    public void StripDdjHeader_Should_Remove_First_20_Bytes()
    {
        var dds = DdsTestDataBuilder.BuildDxt1();
        var ddj = BuildFakeDdj(dds);

        var result = _sut.StripDdjHeader(ddj);

        result.Should().BeEquivalentTo(dds);
    }

    [Fact]
    public void StripDdjHeader_Should_Return_Exactly_Length_Minus_20()
    {
        var dds = DdsTestDataBuilder.BuildDxt1();
        var ddj = BuildFakeDdj(dds);

        var result = _sut.StripDdjHeader(ddj);

        result.Length.Should().Be(ddj.Length - DdjHeaderSize);
    }

    [Fact]
    public void StripDdjHeader_Should_Throw_ArgumentNullException_When_Input_Is_Null()
    {
        var act = () => _sut.StripDdjHeader(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void StripDdjHeader_Should_Throw_ArgumentException_When_Input_Too_Short()
    {
        var act = () => _sut.StripDdjHeader(new byte[5]);
        act.Should().Throw<ArgumentException>().WithMessage("*20*");
    }

    [Fact]
    public void ReadDdsBytes_Should_Throw_FileNotFoundException_When_File_Missing()
    {
        var act = () => _sut.ReadDdsBytes("nonexistent_file.ddj");
        act.Should().Throw<FileNotFoundException>();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static byte[] BuildFakeDdj(byte[] ddsPayload)
    {
        var ddj = new byte[DdjHeaderSize + ddsPayload.Length];
        // fill header with arbitrary non-zero bytes
        for (var i = 0; i < DdjHeaderSize; i++)
            ddj[i] = (byte)(i + 1);
        Buffer.BlockCopy(ddsPayload, 0, ddj, DdjHeaderSize, ddsPayload.Length);
        return ddj;
    }
}
