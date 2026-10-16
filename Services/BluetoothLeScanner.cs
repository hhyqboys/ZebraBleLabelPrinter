using System.Windows.Threading;
using Windows.Devices.Bluetooth.Advertisement;
using ZebraBleLabelPrinter.Models;

namespace ZebraBleLabelPrinter.Services;

/// <summary>
/// 基于 BluetoothLEAdvertisementWatcher 的持续 BLE 扫描器。
/// 自动重启 Stopped，保证一直扫描；按 MAC 地址去重并推送更新。
/// </summary>
public sealed class BluetoothLeScanner : IDisposable
{
    private BluetoothLEAdvertisementWatcher? _watcher;
    private readonly object _lock = new();
    private readonly Dictionary<ulong, BleDevice> _known = new();
    private Dispatcher? _dispatcher;

    /// <summary>新发现或 RSSI/名称更新时触发（UI 线程派发）。</summary>
    public event Action<BleDevice>? DeviceUpdated;

    public bool IsRunning { get; private set; }

    public void Start(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        if (IsRunning) return;

        _watcher = new BluetoothLEAdvertisementWatcher
        {
            // 主动扫描，拿到名称与更多广播数据
            ScanningMode = BluetoothLEScanningMode.Active,
        };

        _watcher.Received += OnAdvertisementReceived;
        _watcher.Stopped += OnWatcherStopped;

        _watcher.Start();
        IsRunning = true;
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        BleDevice device;
        lock (_lock)
        {
            if (!_known.TryGetValue(args.BluetoothAddress, out var existing))
            {
                existing = new BleDevice
                {
                    Address = args.BluetoothAddress,
                    Mac = BleDevice.FormatMac(args.BluetoothAddress),
                };
                _known[args.BluetoothAddress] = existing;
            }

            if (!string.IsNullOrWhiteSpace(args.Advertisement.LocalName))
                existing.Name = args.Advertisement.LocalName.Trim();
            existing.Rssi = args.RawSignalStrengthInDBm;
            existing.LastSeen = DateTime.Now;
            device = existing;
        }

        _dispatcher?.BeginInvoke(new Action(() => DeviceUpdated?.Invoke(device)));
    }

    private void OnWatcherStopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        IsRunning = false;
        // 持续扫描：任何原因停止都重启
        _dispatcher?.BeginInvoke(new Action(() =>
        {
            if (_watcher != null)
            {
                try { _watcher.Start(); IsRunning = true; }
                catch { /* 蓝牙关闭等情况，下一轮 Stopped 还会再试 */ }
            }
        }));
    }

    public void Stop()
    {
        IsRunning = false;
        if (_watcher != null)
        {
            _watcher.Received -= OnAdvertisementReceived;
            _watcher.Stopped -= OnWatcherStopped;
            try { _watcher.Stop(); } catch { }
            _watcher = null;
        }
        lock (_lock) _known.Clear();
    }

    public void Dispose() => Stop();
}
