using System.ComponentModel;
using DdjToPng.Core.ViewModels;

namespace DdjToPng.App.Views;

public sealed class MainForm : Form
{
    private readonly MainViewModel _vm;

    private readonly TextBox   _txtSource;
    private readonly TextBox   _txtOutput;
    private readonly CheckBox  _chkRecursive;
    private readonly Button    _btnBrowseSource;
    private readonly Button    _btnBrowseOutput;
    private readonly Button    _btnScan;
    private readonly Button    _btnConvert;
    private readonly Button    _btnCancel;
    private readonly ListBox   _lstFiles;
    private readonly ProgressBar _progressBar;
    private readonly Label     _lblStatus;

    public MainForm(MainViewModel viewModel)
    {
        _vm = viewModel;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        Text            = "DDJ to PNG Converter";
        Size            = new Size(700, 560);
        MinimumSize     = new Size(600, 500);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition   = FormStartPosition.CenterScreen;

        // ── layout ───────────────────────────────────────────────────────────

        var mainPanel = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 7,
            Padding     = new Padding(10),
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // source row
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // output row
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // options row
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // scan button
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // file list
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // progress
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // status + action buttons

        Controls.Add(mainPanel);

        // Source directory row
        var sourceRow = BuildBrowseRow("Source directory:", out _txtSource, out _btnBrowseSource);
        _btnBrowseSource.Click += (_, _) => BrowseDirectory(_txtSource, isSource: true);
        mainPanel.Controls.Add(sourceRow);

        // Output directory row
        var outputRow = BuildBrowseRow("Output directory:", out _txtOutput, out _btnBrowseOutput);
        _btnBrowseOutput.Click += (_, _) => BrowseDirectory(_txtOutput, isSource: false);
        mainPanel.Controls.Add(outputRow);

        // Options row
        _chkRecursive = new CheckBox { Text = "Search subdirectories recursively", AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        _chkRecursive.CheckedChanged += (_, _) => _vm.Recursive = _chkRecursive.Checked;
        mainPanel.Controls.Add(_chkRecursive);

        // Scan button
        _btnScan = new Button { Text = "Scan for DDJ Files", AutoSize = true, Margin = new Padding(0, 2, 0, 6) };
        _btnScan.Click += OnScanClicked;
        mainPanel.Controls.Add(_btnScan);

        // File list
        _lstFiles = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.None };
        mainPanel.Controls.Add(_lstFiles);

        // Progress bar
        _progressBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Height = 22, Margin = new Padding(0, 6, 0, 4) };
        mainPanel.Controls.Add(_progressBar);

        // Bottom row: status + buttons
        var bottomRow = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = 1,
            AutoSize    = true,
        };
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _lblStatus = new Label { Text = "Ready", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        bottomRow.Controls.Add(_lblStatus);

        _btnCancel = new Button { Text = "Cancel", Width = 80, Enabled = false, Margin = new Padding(4, 0, 0, 0) };
        _btnCancel.Click += (_, _) => _vm.Cancel();
        bottomRow.Controls.Add(_btnCancel);

        _btnConvert = new Button { Text = "Convert", Width = 90, Margin = new Padding(4, 0, 0, 0) };
        _btnConvert.Click += OnConvertClicked;
        bottomRow.Controls.Add(_btnConvert);

        mainPanel.Controls.Add(bottomRow);
    }

    // ── event handlers ────────────────────────────────────────────────────────

    private void OnScanClicked(object? sender, EventArgs e)
    {
        _vm.ScanFiles();
        RefreshFileList();
    }

    private async void OnConvertClicked(object? sender, EventArgs e)
    {
        await _vm.ConvertAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (InvokeRequired) { Invoke(() => OnViewModelPropertyChanged(sender, e)); return; }

        switch (e.PropertyName)
        {
            case nameof(_vm.IsBusy):
                _btnConvert.Enabled      = !_vm.IsBusy;
                _btnScan.Enabled         = !_vm.IsBusy;
                _btnBrowseSource.Enabled = !_vm.IsBusy;
                _btnBrowseOutput.Enabled = !_vm.IsBusy;
                _btnCancel.Enabled       = _vm.IsBusy;
                break;
            case nameof(_vm.StatusMessage):
                _lblStatus.Text = _vm.StatusMessage;
                break;
            case nameof(_vm.Progress):
                _progressBar.Value = Math.Clamp(_vm.Progress, 0, 100);
                break;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void BrowseDirectory(TextBox target, bool isSource)
    {
        var title   = isSource ? "Select source directory" : "Select output directory";
        var initial = string.IsNullOrWhiteSpace(target.Text) ? null : target.Text;

        // Use the modern Windows Explorer folder picker (IFileOpenDialog via COM).
        // Falls back to FolderBrowserDialog if the COM object is unavailable.
        string? selected;
        try
        {
            selected = WindowsFolderPicker.Show(Handle, title, initial);
        }
        catch (InvalidOperationException)
        {
            selected = BrowseWithLegacyDialog(title, initial);
        }

        if (selected is null) return;

        target.Text = selected;
        if (isSource) _vm.SourceDirectory = selected;
        else          _vm.OutputDirectory  = selected;
    }

    private static string? BrowseWithLegacyDialog(string title, string? initialDirectory)
    {
        using var dialog = new FolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            Description            = title,
            ShowNewFolderButton    = true,
            InitialDirectory       = initialDirectory ?? string.Empty,
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private void RefreshFileList()
    {
        _lstFiles.BeginUpdate();
        _lstFiles.Items.Clear();
        foreach (var f in _vm.InputFiles)
            _lstFiles.Items.Add(f);
        _lstFiles.EndUpdate();
    }

    private static Panel BuildBrowseRow(string label, out TextBox textBox, out Button button)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 34, Margin = new Padding(0, 2, 0, 2) };
        var lbl   = new Label  { Text = label, Left = 0, Top = 8, Width = 130, TextAlign = ContentAlignment.MiddleLeft };
        textBox   = new TextBox { Left = 134, Top = 5, Width = 420, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        button    = new Button  { Text = "Browse…", Left = 560, Top = 4, Width = 80, Anchor = AnchorStyles.Right | AnchorStyles.Top };
        panel.Controls.AddRange([lbl, textBox, button]);
        return panel;
    }
}
