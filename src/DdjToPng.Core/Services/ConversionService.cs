using System.Drawing;
using DdjToPng.Core.Interfaces;
using DdjToPng.Core.Models;

namespace DdjToPng.Core.Services;

public sealed class ConversionService : IConversionService
{
    private readonly IDdjReaderService _reader;
    private readonly IDdsDecoderService _decoder;
    private readonly IPngExportService _exporter;

    public ConversionService(IDdjReaderService reader, IDdsDecoderService decoder, IPngExportService exporter)
    {
        _reader   = reader;
        _decoder  = decoder;
        _exporter = exporter;
    }

    public async Task<IReadOnlyList<ConversionResult>> ConvertAsync(
        IReadOnlyList<string> inputFiles,
        ConversionOptions options,
        IProgress<(int completed, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (inputFiles.Count == 0)
            return [];

        var total     = inputFiles.Count;
        var completed = 0;
        var results   = new ConversionResult[total];

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
            CancellationToken      = cancellationToken,
        };

        var reportEvery = Math.Max(1, total / 200);   // at most ~200 UI updates

        await Parallel.ForEachAsync(
            inputFiles.Select((path, idx) => (path, idx)),
            parallelOptions,
            async (item, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                results[item.idx] = await ConvertSingleAsync(item.path, options, ct);
                var done = Interlocked.Increment(ref completed);
                if (done % reportEvery == 0 || done == total)
                    progress?.Report((done, total));
            });

        return results;
    }

    private Task<ConversionResult> ConvertSingleAsync(
        string inputPath, ConversionOptions options, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ddsBytes   = _reader.ReadDdsBytes(inputPath);
                ct.ThrowIfCancellationRequested();

                using var bitmap = _decoder.Decode(ddsBytes);
                ct.ThrowIfCancellationRequested();

                var fileName   = Path.GetFileNameWithoutExtension(inputPath) + ".png";
                var outputPath = Path.Combine(options.OutputDirectory, fileName);
                var saved      = _exporter.Export(bitmap, outputPath);

                return new ConversionResult(inputPath, saved, Success: true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ConversionResult(inputPath, OutputPath: null, Success: false, ErrorMessage: ex.Message);
            }
        }, ct);
    }
}
