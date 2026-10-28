using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ZebraBleLabelPrinter.Models;
using ZebraBleLabelPrinter.Services;

namespace ZebraBleLabelPrinter.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    // 标签尺寸/条码排版统一由 Code128Layout 计算（600DPI，双排 30×20mm，整排 81mm），预览与实际打印一致

    private readonly BluetoothLeScanner _scanner = new();
    private readonly ZebraPrinterService _printer = new();
    private readonly PrintLogService _log = new();
    private readonly Dispatcher _uiDispatcher;
    private readonly HashSet<ulong> _knownMacs = new();
    private DispatcherTimer? _sortTimer;
    private bool _sortDirty;

    public ObservableCollection<BleDevice> AllDevices { get; } = new();
    public ICollectionView DevicesView { get; }
    public ObservableCollection<string> Printers { get; } = new();

    public RelayCommand PrintCommand { get; }
    public RelayCommand RefreshPrintersCommand { get; }
    public RelayCommand CopyMacCommand { get; }

    public MainViewModel()
    {
        _uiDispatcher = Dispatcher.CurrentDispatcher;

        DevicesView = CollectionViewSource.GetDefaultView(AllDevices);
        DevicesView.SortDescriptions.Add(new SortDescription(nameof(BleDevice.IsPrinted), ListSortDirection.Ascending));
        DevicesView.SortDescriptions.Add(new SortDescription(nameof(BleDevice.LastSeen), ListSortDirection.Descending));
        DevicesView.Filter = FilterDevice;

        PrintCommand = new RelayCommand(DoPrint, _ => !IsBusy);
        RefreshPrintersCommand = new RelayCommand(_ => LoadPrinters());
        CopyMacCommand = new RelayCommand(DoCopyMac, _ => SelectedDevice != null);

        LoadPrinters();
        _scanner.DeviceUpdated += OnDeviceUpdated;
        _scanner.Start(_uiDispatcher);

        _sortTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(500) };
        _sortTimer.Tick += (_, _) =>
        {
            if (!_sortDirty) return;
            _sortDirty = false;
            DevicesView.Refresh();
        };
        _sortTimer.Start();

        StatusText = "扫描中…";
    }

    private void LoadPrinters()
    {
        Printers.Clear();
        foreach (var p in _printer.ListPrinters())
        {
            // COM 口标注“直连串口”：直写打印机、绕过 Windows 打印队列，打印最稳
            Printers.Add(ZebraPrinterService.ComPortRegex.IsMatch(p) ? $"{p}（直连串口）" : p);
        }
        var def = _printer.GuessDefaultZebraPrinter(Printers);
        if (def != null) SelectedPrinter = def;
    }

    private bool FilterDevice(object obj)
    {
        if (obj is not BleDevice d) return false;
        if (string.IsNullOrWhiteSpace(d.Name)) return false;
        if (string.IsNullOrWhiteSpace(_filterText)) return true;
        return d.Name.Contains(_filterText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private string _filterText = "sm";
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value ?? string.Empty))
                DevicesView.Refresh();
        }
    }

    private string? _selectedPrinter;
    public string? SelectedPrinter
    {
        get => _selectedPrinter;
        set => SetProperty(ref _selectedPrinter, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                PrintCommand.RaiseCanExecuteChanged();
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private BleDevice? _selectedDevice;
    public BleDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                PreviewMac = value?.Mac;
                OnPropertyChanged(nameof(PreviewMac));
                OnPropertyChanged(nameof(HasPreview));
                RefreshPreview();
                CopyMacCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ImageSource? PreviewImage { get; private set; }
    public string? PreviewMac { get; private set; }
    public bool HasPreview => SelectedDevice != null;

    private void RefreshPreview()
    {
        string bare = SelectedDevice?.Mac ?? string.Empty;
        if (string.IsNullOrEmpty(bare))
        {
            PreviewImage = null;
        }
        else
        {
            var layout = Code128Layout.Compute(bare);
            PreviewImage = BarcodePreviewService.RenderCode128(
                Code128Layout.Normalize(bare),
                layout.ImageWidthDots,
                Code128Layout.BarcodeHeightDots);
        }
        OnPropertyChanged(nameof(PreviewImage));
    }

    private void OnDeviceUpdated(BleDevice device)
    {
        if (_knownMacs.Add(device.Address))
            AllDevices.Add(device);
        _sortDirty = true;
    }

    /// <summary>复制选中设备的 MAC 到剪切板（去掉冒号，12 位十六进制）。</summary>
    private void DoCopyMac(object? param)
    {
        string? mac = SelectedDevice?.Mac;
        if (string.IsNullOrWhiteSpace(mac)) return;

        string bare = mac.Replace(":", string.Empty);
        System.Windows.Clipboard.SetText(bare);
        StatusText = $"已复制 MAC（去冒号）：{bare}";
    }

    private async void DoPrint(object? param)
    {
        if (param is not BleDevice device) return;
        if (!ReferenceEquals(SelectedDevice, device))
            SelectedDevice = device;

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            StatusText = "请先在右上角选择 Zebra 打印机。";
            return;
        }

        if (device.IsPrinted)
        {
            var r = System.Windows.MessageBox.Show(
                $"设备 {device.Mac} 已经打印过，是否再次打印？",
                "二次打印确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes)
            {
                StatusText = "已取消二次打印。";
                return;
            }
        }

        IsBusy = true;
        StatusText = $"正在打印 {device.Mac} …";
        bool ok = false;
        try
        {
            await Task.Run(() => _printer.PrintMacBarcode(SelectedPrinter!, device.Mac));
            ok = true;
            device.IsPrinted = true;
            StatusText = $"已发送打印任务：{device.Mac} -> {SelectedPrinter}";
        }
        catch (Exception ex)
        {
            StatusText = $"打印失败：{ex.Message}";
        }
        finally
        {
            _log.Log(device.Mac, ok);
            IsBusy = false;
        }
    }

    public void Shutdown() => _scanner.Dispose();
}
