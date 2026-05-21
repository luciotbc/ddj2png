using DdjToPng.Core.Interfaces;
using DdjToPng.Core.Models;
using DdjToPng.Core.ViewModels;

namespace DdjToPng.Core.Tests.ViewModels;

public sealed class MainViewModelTests
{
    private readonly Mock<IFileScannerService> _scanner    = new();
    private readonly Mock<IConversionService>  _converter  = new();
    private readonly MainViewModel             _sut;

    public MainViewModelTests()
    {
        _sut = new MainViewModel(_scanner.Object, _converter.Object);
    }

    // ── initial state ────────────────────────────────────────────────────────

    [Fact]
    public void Initial_State_Should_Not_Be_Busy()
    {
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void Initial_StatusMessage_Should_Be_Ready()
    {
        _sut.StatusMessage.Should().Be("Ready");
    }

    [Fact]
    public void Initial_Progress_Should_Be_Zero()
    {
        _sut.Progress.Should().Be(0);
    }

    [Fact]
    public void Initial_SourceDirectory_Should_Be_Empty()
    {
        _sut.SourceDirectory.Should().BeNullOrEmpty();
    }

    // ── SourceDirectory ──────────────────────────────────────────────────────

    [Fact]
    public void Setting_SourceDirectory_Should_Raise_PropertyChanged()
    {
        var raised = false;
        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_sut.SourceDirectory)) raised = true;
        };

        _sut.SourceDirectory = @"C:\icons";

        raised.Should().BeTrue();
    }

    // ── ScanCommand ──────────────────────────────────────────────────────────

    [Fact]
    public void ScanFiles_Should_Populate_InputFiles_From_Scanner()
    {
        _sut.SourceDirectory = @"C:\icons";
        _scanner.Setup(s => s.FindDdjFiles(@"C:\icons", false))
                .Returns(["a.ddj", "b.ddj"]);

        _sut.ScanFiles();

        _sut.InputFiles.Should().BeEquivalentTo(["a.ddj", "b.ddj"]);
    }

    [Fact]
    public void ScanFiles_Should_Update_StatusMessage()
    {
        _sut.SourceDirectory = @"C:\icons";
        _scanner.Setup(s => s.FindDdjFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(["x.ddj"]);

        _sut.ScanFiles();

        _sut.StatusMessage.Should().Contain("1");
    }

    [Fact]
    public void ScanFiles_Should_Not_Throw_When_SourceDirectory_Is_Empty()
    {
        _sut.SourceDirectory = string.Empty;
        var act = _sut.ScanFiles;
        act.Should().NotThrow();
    }

    // ── ConvertAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertAsync_Should_Set_IsBusy_Then_Clear_It()
    {
        _sut.SourceDirectory = @"C:\icons";
        _sut.OutputDirectory = @"C:\out";
        var busySnapshots = new List<bool>();

        _sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_sut.IsBusy))
                busySnapshots.Add(_sut.IsBusy);
        };

        _converter.Setup(c => c.ConvertAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<(int, int)>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.ConvertAsync();

        busySnapshots.Should().Contain(true);
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertAsync_Should_Update_StatusMessage_With_Result_Count()
    {
        _sut.OutputDirectory = @"C:\out";
        _sut.InputFiles.AddRange(["a.ddj", "b.ddj"]);

        _converter.Setup(c => c.ConvertAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<(int, int)>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ConversionResult("a.ddj", "a.png", true),
                new ConversionResult("b.ddj", "b.png", true),
            ]);

        await _sut.ConvertAsync();

        _sut.StatusMessage.Should().Contain("2");
    }

    [Fact]
    public async Task ConvertAsync_Should_Not_Run_When_Already_Busy()
    {
        _sut.OutputDirectory = @"C:\out";

        // simulate already busy
        _sut.SetBusyForTest(true);

        await _sut.ConvertAsync();

        _converter.Verify(c => c.ConvertAsync(
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<ConversionOptions>(),
            It.IsAny<IProgress<(int, int)>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_Should_Not_Throw_When_Not_Running()
    {
        var act = _sut.Cancel;
        act.Should().NotThrow();
    }
}
