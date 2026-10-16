using System.IO;
using System.Text;

namespace ZebraBleLabelPrinter.Services;

/// <summary>
/// 打印记录写入 %LocalAppData%\ZebraPrinter\ZebraPrinter-yyyyMMdd.csv
/// 字段：MAC 地址, 打印时间, 是否打印成功
/// </summary>
public sealed class PrintLogService
{
    private readonly object _lock = new();

    public string GetLogFilePath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZebraPrinter");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"ZebraPrinter-{DateTime.Now:yyyyMMdd}.csv");
    }

    public void Log(string mac, bool success)
    {
        lock (_lock)
        {
            string path = GetLogFilePath();
            bool needHeader = !File.Exists(path) || new FileInfo(path).Length == 0;

            var sb = new StringBuilder();
            if (needHeader)
                sb.AppendLine("MAC,打印时间,是否打印成功");

            sb.AppendLine($"{Escape(mac)},{DateTime.Now:yyyy-MM-dd HH:mm:ss},{(success ? "成功" : "失败")}");

            // UTF-8 BOM 保证 Excel 打开不乱码
            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: needHeader));
        }
    }

    private static string Escape(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
