using System.Drawing.Printing;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using ZebraBleLabelPrinter.Helpers;

namespace ZebraBleLabelPrinter.Services;

public sealed class ZebraPrinterService
{
    // 标签尺寸/条码排版统一由 Code128Layout 计算（600DPI，双排纸，只打第二列）
    public List<string> ListPrinters()
    {
        var list = new List<string>();
        try { foreach (string p in PrinterSettings.InstalledPrinters) list.Add(p); }
        catch { }
        try { foreach (string port in SerialPort.GetPortNames()) list.Add(port); }
        catch { }
        return list;
    }

    public string? GuessDefaultZebraPrinter(IEnumerable<string> printers)
    {
        var list = printers.ToList();
        return list.FirstOrDefault(p => p.Contains("Zebra", StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(p => p.Contains("110Xi", StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault();
    }

    public string BuildMacBarcodeZpl(string mac, int? offsetOverride = null)
    {
        string bare = Code128Layout.Normalize(mac);
        var layout = Code128Layout.Compute(bare);

        // 双排标签纸：只在第二列（右列）打印，第一列空白。整排 ^PW=RowW，^LL=ColH。
        // 用 ZPL ^BC 原生条码（几百字节，任何连接方式都快），模块宽由 Code128Layout 指定，列内居中。
        // ^LS0 中和打印机内存储的水平偏移（label shift），^LH0,0 中和存储的标签原点。
        // offsetOverride：控制台工具试偏移用；默认取 Code128Layout.HorizontalOffsetDots（左溢→正数右移）。
        int off = offsetOverride ?? Code128Layout.HorizontalOffsetDots;
        int textY = Code128Layout.TextY(layout);

        var sb = new StringBuilder();
        sb.AppendLine("^XA");
        sb.AppendLine("^LH0,0");
        sb.AppendLine("^LS0");
        sb.AppendLine("^LT0");
        sb.AppendLine($"^FO0,0^LL{Code128Layout.ColH}");
        sb.AppendLine($"^PW{Code128Layout.RowW}");
        sb.AppendLine($"^BY{layout.ModuleWidth},3.0");

        // 只打印第二列（右列，printCol=1），第一列位置完全空白、不发任何条码/文本指令。
        // 列块起点 = colIndex*(ColW+ColGap)+off，条码在列内居中（+layout.X）。
        const int printCol = 1; // 0=第一列(左)，1=第二列(右)
        {
            int blockX = Code128Layout.ColumnBlockX(printCol, off);
            int barcodeX = blockX + layout.X;
            // ">:" = CODE128 Subset B 起始码（官方建议显式指定，杜绝固件子集歧义）。
            // 不发送 ^CI27：110XiIII 老固件字符集映射曾导致含字母数据编码异常（纯数字不受影响）。
            sb.AppendLine($"^FO{barcodeX},{layout.Y}^BCN,{Code128Layout.BarcodeHeightDots},N,N,N");
            sb.AppendFormat("^FD>:{0}^FS\r\n", bare);
            // 可读文本：条码下方，列内全宽（ColW）居中，与该列条码同心。
            sb.AppendLine($"^FO{blockX},{textY}^A0N,{Code128Layout.TextFontDots},{Code128Layout.TextFontDots}^FB{Code128Layout.ColW},1,0,C^FD{bare}^FS");
        }
        sb.AppendLine("^PQ1,0,1,Y");
        sb.AppendLine("^XZ");
        return sb.ToString();
    }

    /// <summary>是否 COM 串口（支持 “COM3（直连串口）” 这类带后缀的显示名）。</summary>
    internal static readonly Regex ComPortRegex = new(@"^COM\d+", RegexOptions.IgnoreCase);

    public void PrintMacBarcode(string printerName, string mac, int? offsetOverride = null)
        => SendZpl(printerName, BuildMacBarcodeZpl(mac, offsetOverride));

    /// <summary>把 ZPL 发送到打印机：COM 口直连串口；打印机名走队列（发送前自动清理卡死任务）。</summary>
    public void SendZpl(string printerName, string zpl)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException("尚未选择打印机/串口。");

        var com = ComPortRegex.Match(printerName.Trim());
        if (com.Success)
        {
            // 直连串口：绕过 Windows 打印队列，不依赖 spooler，最稳
            using var sp = new SerialPort(com.Value, 9600, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 5000, WriteTimeout = 5000, Handshake = Handshake.None,
            };
            try { sp.Open(); sp.Write(zpl); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"串口 {com.Value} 发送失败：{ex.Message}。请确认下拉里选的是打印机 USB 对应的 COM 口。", ex);
            }
        }
        else
        {
            // 打印队列方式：先清掉卡死的旧任务（“错误-正在打印-正在重新启动”），再发送
            try
            {
                RawPrinterHelper.PurgePrinterQueue(printerName);
                RawPrinterHelper.SendStringToPrinter(printerName, zpl);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"通过打印机队列 \"{printerName}\" 发送失败：{ex.Message}。" +
                    "建议：1) 下拉里改选对应的 COM 口（直连串口，绕过打印队列）；"
                    + "2) 断电重启打印机后再试。", ex);
            }
        }
    }
}
