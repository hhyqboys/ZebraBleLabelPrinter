using System.Drawing.Printing;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using ZebraBleLabelPrinter.Helpers;

namespace ZebraBleLabelPrinter.Services;

public sealed class ZebraPrinterService
{
    // 固定 600DPI，33×33mm 标签
    private const int LabelW = 780;
    private const int LabelH = 780;
    private const int ModW = 3;
    private const int BarcodeH = 450;
    private const int BarcodeW = 167 * ModW; // 668

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

    public string BuildMacBarcodeZpl(string mac)
    {
        string bare = mac.Replace(":", "");
        int x = (LabelW - BarcodeW) / 2;
        int y = (LabelH - BarcodeH) / 2;

        var sb = new StringBuilder();
        sb.AppendLine("^XA");
        sb.AppendLine("^LH0,0");
        sb.AppendLine("^LT0");
        sb.AppendLine($"^FO0,0^LL{LabelH}");
        sb.AppendLine($"^PW{LabelW}");
        sb.AppendLine("^CI27");
        sb.AppendLine($"^BY{ModW},3.0");
        sb.AppendLine($"^FO{x},{y}^BCN,{BarcodeH},N,N,N");
        sb.AppendFormat("^FD{0}^FS\r\n", bare);
        sb.AppendLine("^PQ1,0,1,Y");
        sb.AppendLine("^XZ");
        return sb.ToString();
    }

    private static readonly Regex ComPortRegex = new(@"^COM\d+\s*:?$", RegexOptions.IgnoreCase);

    public void PrintMacBarcode(string printerName, string mac)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new InvalidOperationException("尚未选择打印机/串口。");

        string zpl = BuildMacBarcodeZpl(mac);

        if (ComPortRegex.IsMatch(printerName.Trim()))
        {
            using var sp = new SerialPort(printerName.Trim().TrimEnd(':'), 9600, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 5000, WriteTimeout = 5000, Handshake = Handshake.None,
            };
            try { sp.Open(); sp.Write(zpl); }
            catch (Exception ex) { throw new InvalidOperationException($"串口发送失败：{ex.Message}", ex); }
        }
        else
        {
            try { RawPrinterHelper.SendStringToPrinter(printerName, zpl); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"通过打印机队列 \"{printerName}\" 发送失败：{ex.Message}。" +
                    "如果打印机是 USB 转串口，请在下拉里选对应的 COM 口。", ex);
            }
        }
    }
}
