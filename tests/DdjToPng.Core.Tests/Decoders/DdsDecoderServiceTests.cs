using DdjToPng.Core.Services;
using DdjToPng.Core.Tests.Helpers;

namespace DdjToPng.Core.Tests.Decoders;

public sealed class DdsDecoderServiceTests
{
    private readonly DdsDecoderService _sut = new();

    // ── GetWidth / GetHeight ─────────────────────────────────────────────────

    [Fact]
    public void GetWidth_Should_Return_Correct_Value_From_DDS_Header()
    {
        var dds = DdsTestDataBuilder.BuildDxt1(width: 64, height: 32);
        _sut.GetWidth(dds).Should().Be(64);
    }

    [Fact]
    public void GetHeight_Should_Return_Correct_Value_From_DDS_Header()
    {
        var dds = DdsTestDataBuilder.BuildDxt1(width: 64, height: 32);
        _sut.GetHeight(dds).Should().Be(32);
    }

    // ── DXT1 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Decode_Dxt1_Texture_To_Bitmap_With_Correct_Dimensions()
    {
        var dds = DdsTestDataBuilder.BuildDxt1(width: 4, height: 4);

        var bitmap = _sut.Decode(dds);

        bitmap.Width.Should().Be(4);
        bitmap.Height.Should().Be(4);
    }

    [Fact]
    public void Should_Decode_Dxt1_Texture_And_Produce_Non_Null_Bitmap()
    {
        var dds = DdsTestDataBuilder.BuildDxt1(width: 4, height: 4);
        _sut.Decode(dds).Should().NotBeNull();
    }

    // ── DXT3 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Decode_Dxt3_Texture_To_Rgba_Bitmap()
    {
        var dds = DdsTestDataBuilder.BuildDxt3(width: 4, height: 4);

        var bitmap = _sut.Decode(dds);

        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(4);
        bitmap.Height.Should().Be(4);
    }

    // ── DXT5 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Decode_Dxt5_Texture_To_Rgba_Bitmap()
    {
        var dds = DdsTestDataBuilder.BuildDxt5(width: 4, height: 4);

        var bitmap = _sut.Decode(dds);

        bitmap.Should().NotBeNull();
        bitmap.Width.Should().Be(4);
        bitmap.Height.Should().Be(4);
    }

    // ── Uncompressed RGB ─────────────────────────────────────────────────────

    [Fact]
    public void Should_Decode_Argb8888_Texture_With_Correct_Dimensions()
    {
        var dds = DdsTestDataBuilder.BuildArgb8888(width: 2, height: 2);

        var bitmap = _sut.Decode(dds);

        bitmap.Width.Should().Be(2);
        bitmap.Height.Should().Be(2);
    }

    [Fact]
    public void Should_Decode_R8G8B8_Texture_With_Correct_Dimensions()
    {
        var dds = DdsTestDataBuilder.BuildR8G8B8(width: 2, height: 2);

        var bitmap = _sut.Decode(dds);

        bitmap.Width.Should().Be(2);
        bitmap.Height.Should().Be(2);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public void Decode_Should_Throw_ArgumentNullException_When_Buffer_Is_Null()
    {
        var act = () => _sut.Decode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decode_Should_Throw_ArgumentException_When_Buffer_Too_Short()
    {
        var act = () => _sut.Decode(new byte[10]);
        act.Should().Throw<ArgumentException>();
    }
}
