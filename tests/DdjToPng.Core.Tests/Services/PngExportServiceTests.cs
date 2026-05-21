using System.Drawing;
using System.Drawing.Imaging;
using DdjToPng.Core.Services;

namespace DdjToPng.Core.Tests.Services;

public sealed class PngExportServiceTests : IDisposable
{
    private readonly PngExportService _sut = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public PngExportServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Export_Should_Create_Png_File_At_Output_Path()
    {
        using var bitmap = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        var outputPath = Path.Combine(_tempDir, "output.png");

        _sut.Export(bitmap, outputPath);

        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public void Export_Should_Return_The_Output_Path()
    {
        using var bitmap = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        var outputPath = Path.Combine(_tempDir, "output.png");

        var result = _sut.Export(bitmap, outputPath);

        result.Should().Be(outputPath);
    }

    [Fact]
    public void Export_Should_Produce_Valid_Png_File()
    {
        using var bitmap = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        var outputPath = Path.Combine(_tempDir, "output.png");

        _sut.Export(bitmap, outputPath);

        var bytes = File.ReadAllBytes(outputPath);
        // PNG magic: 89 50 4E 47 0D 0A 1A 0A
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50); // 'P'
        bytes[2].Should().Be(0x4E); // 'N'
        bytes[3].Should().Be(0x47); // 'G'
    }

    [Fact]
    public void Export_Should_Throw_ArgumentNullException_When_Bitmap_Is_Null()
    {
        var act = () => _sut.Export(null!, Path.Combine(_tempDir, "out.png"));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Export_Should_Create_Output_Directory_If_Missing()
    {
        using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        var nestedDir = Path.Combine(_tempDir, "nested", "output");
        var outputPath = Path.Combine(nestedDir, "icon.png");

        _sut.Export(bitmap, outputPath);

        File.Exists(outputPath).Should().BeTrue();
    }
}
