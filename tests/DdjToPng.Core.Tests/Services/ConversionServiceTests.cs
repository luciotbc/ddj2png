using System.Drawing;
using System.Drawing.Imaging;
using DdjToPng.Core.Interfaces;
using DdjToPng.Core.Models;
using DdjToPng.Core.Services;

namespace DdjToPng.Core.Tests.Services;

public sealed class ConversionServiceTests : IDisposable
{
    private readonly Mock<IDdjReaderService> _reader = new();
    private readonly Mock<IDdsDecoderService> _decoder = new();
    private readonly Mock<IPngExportService> _exporter = new();
    private readonly ConversionService _sut;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ConversionServiceTests()
    {
        _sut = new ConversionService(_reader.Object, _decoder.Object, _exporter.Object);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task ConvertAsync_Should_Return_Success_Result_For_Each_File()
    {
        var files = new[] { "a.ddj", "b.ddj" };
        var options = new ConversionOptions(_tempDir);
        SetupSuccessfulPipeline();

        var results = await _sut.ConvertAsync(files, options);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task ConvertAsync_Should_Return_Failure_Result_When_Reader_Throws()
    {
        var files = new[] { "bad.ddj" };
        var options = new ConversionOptions(_tempDir);
        _reader.Setup(r => r.ReadDdsBytes(It.IsAny<string>()))
               .Throws(new FileNotFoundException("not found"));

        var results = await _sut.ConvertAsync(files, options);

        results.Should().ContainSingle(r => !r.Success);
        results[0].ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ConvertAsync_Should_Report_Progress()
    {
        var files = new[] { "a.ddj", "b.ddj", "c.ddj" };
        var options = new ConversionOptions(_tempDir);
        SetupSuccessfulPipeline();
        var reported = new List<(int completed, int total)>();

        await _sut.ConvertAsync(files, options,
            progress: new Progress<(int, int)>(p => reported.Add(p)));

        // allow async progress callbacks to flush
        await Task.Delay(50);
        reported.Should().NotBeEmpty();
        reported.Should().AllSatisfy(p => p.total.Should().Be(3));
    }

    [Fact]
    public async Task ConvertAsync_Should_Honour_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        var files = Enumerable.Range(0, 20).Select(i => $"file{i}.ddj").ToArray();
        var options = new ConversionOptions(_tempDir, MaxDegreeOfParallelism: 1);
        SetupSuccessfulPipeline(delayMs: 20);

        cts.CancelAfter(30);
        var act = async () => await _sut.ConvertAsync(files, options, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConvertAsync_Should_Return_Empty_List_For_Empty_Input()
    {
        var options = new ConversionOptions(_tempDir);
        var results = await _sut.ConvertAsync([], options);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ConvertAsync_Should_Preserve_Relative_Path_Under_OutputDirectory()
    {
        var inputDir  = Path.Combine(_tempDir, "input");
        var outputDir = Path.Combine(_tempDir, "output");
        var inputFile = Path.Combine(inputDir, "Data", "icon", "skills", "ice.ddj");
        var options   = new ConversionOptions(outputDir, InputDirectory: inputDir);
        SetupSuccessfulPipeline();

        string? capturedOutputPath = null;
        _exporter.Setup(e => e.Export(It.IsAny<Bitmap>(), It.IsAny<string>()))
                 .Returns<Bitmap, string>((_, p) => { capturedOutputPath = p; return p; });

        await _sut.ConvertAsync([inputFile], options);

        var expectedPath = Path.Combine(outputDir, "Data", "icon", "skills", "ice.png");
        capturedOutputPath.Should().Be(expectedPath);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private void SetupSuccessfulPipeline(int delayMs = 0)
    {
        var fakeDds = new byte[128];
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);

        _reader.Setup(r => r.ReadDdsBytes(It.IsAny<string>()))
               .Returns(() =>
               {
                   if (delayMs > 0) Thread.Sleep(delayMs);
                   return fakeDds;
               });
        _decoder.Setup(d => d.Decode(It.IsAny<byte[]>())).Returns(bmp);
        _exporter.Setup(e => e.Export(It.IsAny<Bitmap>(), It.IsAny<string>()))
                 .Returns<Bitmap, string>((_, p) => p);
    }
}
