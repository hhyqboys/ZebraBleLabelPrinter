using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraBleLabelPrinter.Models;

/// <summary>
/// 一个被扫描到的 BLE 广播设备。RSSI 会随新广播不断更新，因此实现 INotifyPropertyChanged。
/// </summary>
public sealed class BleDevice : INotifyPropertyChanged
{
    /// <summary>蓝牙地址（MAC），原始 48 位。</summary>
    public ulong Address { get; init; }

    /// <summary>按 AA:BB:CC:DD:EE:FF 格式化的 MAC 字符串。</summary>
    public string Mac { get; init; } = string.Empty;

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private int _rssi;
    public int Rssi
    {
        get => _rssi;
        set
        {
            if (_rssi == value) return;
            _rssi = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RssiBar));
        }
    }

    /// <summary>界面信号条像素宽度。-30dBm≈220px, -100dBm≈0。</summary>
    public double RssiBar
    {
        get
        {
            double v = (Rssi + 100) / 70.0 * 220.0;
            if (v < 0) v = 0;
            if (v > 220) v = 220;
            return v;
        }
    }

    public DateTime LastSeen { get; set; }

    private bool _isPrinted;
    public bool IsPrinted
    {
        get => _isPrinted;
        set
        {
            if (_isPrinted == value) return;
            _isPrinted = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(未命名设备)" : Name;

    public static string FormatMac(ulong address)
    {
        byte[] b = BitConverter.GetBytes(address);
        // BitConverter 是小端，BLE 地址按网络序展示
        return $"{b[5]:X2}:{b[4]:X2}:{b[3]:X2}:{b[2]:X2}:{b[1]:X2}:{b[0]:X2}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
