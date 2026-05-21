using System.ComponentModel;
using DdjToPng.Core.ViewModels;
using Microsoft.Win32;

namespace DdjToPng.App.Views;

public sealed class MainForm : Form
{
    private readonly MainViewModel _vm;

    private readonly Button    _btnSelectSource;
    private readonly Button    _btnSelectOutput;
    private readonly CheckBox  _chkRecursive;
    private readonly Button    _btnScan;
    private readonly Button    _btnConvert;
    private readonly Button    _btnCancel;
    private readonly ListBox   _lstFiles;
    private readonly ProgressBar _progressBar;
    private readonly Label     _lblStatus;

    private const string PlaceholderSource = "Click to select source folder…";
    private const string PlaceholderOutput = "Click to select output folder…";

    public MainForm(MainViewModel viewModel)
    {
        _vm = viewModel;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        Text            = "DDJ to PNG Converter";
        Size            = new Size(700, 560);
        MinimumSize     = new Size(560, 500);
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
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // source row
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // output row
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // options row
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // scan button
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // file list
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // progress
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // status + action buttons

        Controls.Add(mainPanel);

        // Source directory row
        _btnSelectSource = new Button();
        mainPanel.Controls.Add(BuildFolderRow("Source directory:", _btnSelectSource, PlaceholderSource));
        _btnSelectSource.Click += (_, _) => BrowseDirectory(isSource: true);

        // Output directory row
        _btnSelectOutput = new Button();
        mainPanel.Controls.Add(BuildFolderRow("Output directory:", _btnSelectOutput, PlaceholderOutput));
        _btnSelectOutput.Click += (_, _) => BrowseDirectory(isSource: false);

        // Options row
        _chkRecursive = new CheckBox { Text = "Search subdirectories recursively", AutoSize = true, Margin = new Padding(0, 6, 0, 4) };
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
        if (string.IsNullOrWhiteSpace(_vm.SourceDirectory))
        {
            MessageBox.Show("Select a source directory first.", "DDJ to PNG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _vm.ScanFiles();
        RefreshFileList();
    }

    private async void OnConvertClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.SourceDirectory))
        {
            MessageBox.Show("Select a source directory first.", "DDJ to PNG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_vm.OutputDirectory))
        {
            MessageBox.Show("Select an output directory first.", "DDJ to PNG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_vm.InputFiles.Count == 0)
        {
            MessageBox.Show("No DDJ files found. Click 'Scan for DDJ Files' first.", "DDJ to PNG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
                _btnSelectSource.Enabled = !_vm.IsBusy;
                _btnSelectOutput.Enabled = !_vm.IsBusy;
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

    private void BrowseDirectory(bool isSource)
    {
        var btn      = isSource ? _btnSelectSource : _btnSelectOutput;
        var current  = isSource ? _vm.SourceDirectory : _vm.OutputDirectory;
        var dialog   = new OpenFolderDialog
        {
            Title            = isSource ? "Select source directory" : "Select output directory",
            InitialDirectory = string.IsNullOrWhiteSpace(current) ? null : current,
        };

        if (dialog.ShowDialog() == true)
        {
            btn.Text = dialog.FolderName;
            if (isSource) _vm.SourceDirectory = dialog.FolderName;
            else          _vm.OutputDirectory  = dialog.FolderName;
        }
    }

    private void RefreshFileList()
    {
        _lstFiles.BeginUpdate();
        _lstFiles.Items.Clear();
        foreach (var f in _vm.InputFiles)
            _lstFiles.Items.Add(f);
        _lstFiles.EndUpdate();
    }

    private static TableLayoutPanel BuildFolderRow(string label, Button folderButton, string placeholder)
    {
        var row = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 1,
            AutoSize    = true,
            Margin      = new Padding(0, 2, 0, 2),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lbl = new Label
        {
            Text      = label,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize  = false,
        };

        folderButton.Text      = placeholder;
        folderButton.Dock      = DockStyle.Fill;
        folderButton.Height    = 30;
        folderButton.TextAlign = ContentAlignment.MiddleLeft;
        folderButton.Margin    = new Padding(0, 0, 0, 4);
        // Truncate long paths with ellipsis on the left
        folderButton.AutoEllipsis = true;

        row.Controls.Add(lbl, 0, 0);
        row.Controls.Add(folderButton, 1, 0);
        return row;
    }
}
