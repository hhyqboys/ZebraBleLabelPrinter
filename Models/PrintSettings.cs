using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZebraBleLabelPrinter.Models;

/// <summary>
/// 打印参数配置，持久化到 %LocalAppData%\ZebraPrinter\settings.json
/// </summary>
public sealed class PrintSettings : INotifyPropertyChanged
{
    private int _moduleWidth = 4;
    private double _labelWidthMm = 60;
    private double _labelHeightMm = 40;
    private int _barcodeHeightDots = 450;
    private int? _barcodeX = null;
    private int? _barcodeY = null;

    /// <summary>ZPL ^BY 模块宽度，1-10 dots。</summary>
    public int ModuleWidth
    {
        get => _moduleWidth;
        set { _moduleWidth = Math.Clamp(value, 1, 10); OnPropertyChanged(); }
    }

    /// <summary>标签宽度 mm。</summary>
    public double LabelWidthMm
    {
        get => _labelWidthMm;
        set { _labelWidthMm = value; OnPropertyChanged(); }
    }

    /// <summary>标签高度 mm。</summary>
    public double LabelHeightMm
    {
        get => _labelHeightMm;
        set { _labelHeightMm = value; OnPropertyChanged(); }
    }

    /// <summary>条码高度 dots（600DPI）。</summary>
    public int BarcodeHeightDots
    {
        get => _barcodeHeightDots;
        set { _barcodeHeightDots = value; OnPropertyChanged(); }
    }

    /// <summary>条码 X 坐标 dots；null = 水平居中。</summary>
    public int? BarcodeX
    {
        get => _barcodeX;
        set { _barcodeX = value; OnPropertyChanged(); }
    }

    /// <summary>条码 Y 坐标 dots；null = 垂直居中。</summary>
    public int? BarcodeY
    {
        get => _barcodeY;
        set { _barcodeY = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
