using DdjToPng.Core.Models;

namespace DdjToPng.Core.Interfaces;

public interface IConversionService
{
    Task<IReadOnlyList<ConversionResult>> ConvertAsync(
        IReadOnlyList<string> inputFiles,
        ConversionOptions options,
        IProgress<(int completed, int total)>? progress = null,
        CancellationToken cancellationToken = default
    );
}
