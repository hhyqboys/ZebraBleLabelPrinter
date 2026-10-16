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
    // 固定打印参数（600DPI）
    private const int ModW = 4;            // ^BY 模块宽
    private const int LabelW = 780;        // 33mm × 23.62
    private const int LabelH = 780;
    private const int BarcodeH = 450;
    private const int BarcodeW = 167 * ModW; // 668

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

    public MainViewModel()
    {
        _uiDispatcher = Dispatcher.CurrentDispatcher;

        DevicesView = CollectionViewSource.GetDefaultView(AllDevices);
        DevicesView.SortDescriptions.Add(new SortDescription(nameof(BleDevice.IsPrinted), ListSortDirection.Ascending));
        DevicesView.SortDescriptions.Add(new SortDescription(nameof(BleDevice.LastSeen), ListSortDirection.Descending));
        DevicesView.Filter = FilterDevice;

        PrintCommand = new RelayCommand(DoPrint, _ => !IsBusy);
        RefreshPrintersCommand = new RelayCommand(_ => LoadPrinters());

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
        foreach (var p in _printer.ListPrinters()) Printers.Add(p);
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
            }
        }
    }

    public ImageSource? PreviewImage { get; private set; }
    public string? PreviewMac { get; private set; }
    public bool HasPreview => SelectedDevice != null;

    private void RefreshPreview()
    {
        string bare = SelectedDevice?.Mac.Replace(":", "") ?? string.Empty;
        PreviewImage = string.IsNullOrEmpty(bare)
            ? null
            : BarcodePreviewService.RenderCode128(bare, BarcodeW, BarcodeH);
        OnPropertyChanged(nameof(PreviewImage));
    }

    private void OnDeviceUpdated(BleDevice device)
    {
        if (_knownMacs.Add(device.Address))
            AllDevices.Add(device);
        _sortDirty = true;
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
