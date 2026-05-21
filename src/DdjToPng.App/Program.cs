using DdjToPng.App.Views;
using DdjToPng.Core.Services;
using DdjToPng.Core.ViewModels;

namespace DdjToPng.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var scanner    = new FileScannerService();
        var reader     = new DdjReaderService();
        var decoder    = new DdsDecoderService();
        var exporter   = new PngExportService();
        var converter  = new ConversionService(reader, decoder, exporter);
        var viewModel  = new MainViewModel(scanner, converter);

        Application.Run(new MainForm(viewModel));
    }
}
