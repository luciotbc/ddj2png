using System.ComponentModel;
using System.Runtime.CompilerServices;
using DdjToPng.Core.Interfaces;
using DdjToPng.Core.Models;

namespace DdjToPng.Core.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IFileScannerService _scanner;
    private readonly IConversionService  _converter;
    private CancellationTokenSource?     _cts;

    private string _sourceDirectory = string.Empty;
    private string _outputDirectory = string.Empty;
    private string _statusMessage   = "Ready";
    private int    _progress;
    private bool   _isBusy;
    private bool   _recursive;

    public MainViewModel(IFileScannerService scanner, IConversionService converter)
    {
        _scanner   = scanner;
        _converter = converter;
    }

    // ── observable properties ─────────────────────────────────────────────────

    public string SourceDirectory
    {
        get => _sourceDirectory;
        set => SetField(ref _sourceDirectory, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetField(ref _outputDirectory, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public int Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public bool Recursive
    {
        get => _recursive;
        set => SetField(ref _recursive, value);
    }

    public List<string> InputFiles { get; } = [];

    // ── commands ──────────────────────────────────────────────────────────────

    public void ScanFiles()
    {
        if (string.IsNullOrWhiteSpace(SourceDirectory)) return;

        try
        {
            var found = _scanner.FindDdjFiles(SourceDirectory, Recursive);
            InputFiles.Clear();
            InputFiles.AddRange(found);
            StatusMessage = $"Found {InputFiles.Count} file(s). Select an output directory and click Convert.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
    }

    public async Task ConvertAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(OutputDirectory)) return;

        IsBusy   = true;
        Progress = 0;

        _cts = new CancellationTokenSource();

        try
        {
            var options = new ConversionOptions(OutputDirectory, InputDirectory: SourceDirectory);
            var progress = new Progress<(int completed, int total)>(p =>
            {
                Progress      = p.total > 0 ? (int)(100.0 * p.completed / p.total) : 0;
                StatusMessage = $"Converting… {p.completed}/{p.total}";
            });

            var results = await _converter.ConvertAsync(InputFiles, options, progress, _cts.Token);

            var succeeded = results.Count(r => r.Success);
            var failed    = results.Count(r => !r.Success);
            StatusMessage = failed == 0
                ? $"Done — {succeeded} file(s) converted successfully."
                : $"Done — {succeeded} succeeded, {failed} failed.";

            Progress = 100;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Conversion cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Cancel() => _cts?.Cancel();

    // ── test helper (internal visibility controlled via InternalsVisibleTo) ──

    internal void SetBusyForTest(bool value) => IsBusy = value;

    // ── INotifyPropertyChanged ─────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
