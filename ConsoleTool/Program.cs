using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using ZebraBleLabelPrinter.Helpers;
using ZebraBleLabelPrinter.Services;
using ZXing;
using ZXing.Common;

// =====================================================================
// ZebraPrinterConsole —— 斑马条码打印参数调试工具（与 WPF 程序共用同一套打印逻辑）
//
// 用法:
//   ZebraPrinterConsole                      列出打印机与串口
//   ZebraPrinterConsole <MAC> [选项]          输出该 MAC 的打印参数与完整 ZPL
//   ZebraPrinterConsole -print <MAC> -com COM3 | -printer "名称" [选项]   直接打印测试
//   ZebraPrinterConsole -decode <图片>        用 ZXing 解码照片中的条码（验证打印质量/内容）
//
// 选项:
//   -offset <N>     水平偏移(dots)，正数=条码右移，负数=左移；1mm≈23.6 dots。
//                   左侧溢出被截 → 用正数（如 60）把条码整体右移。
//   -com <COM3>     直连串口发送（绕过打印队列，推荐）
//   -printer "名称"  走打印队列发送（自动清理卡死任务）
// =====================================================================

Console.OutputEncoding = Encoding.UTF8;

var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
string? mac = null, printer = null, com = null, decodePath = null;
int? offset = null;
bool print = false, list = false;

for (int i = 0; i < cmdArgs.Length; i++)
{
    switch (cmdArgs[i].ToLowerInvariant())
    {
        case "-list": list = true; break;
        case "-print": print = true; break;
        case "-decode":
            if (i + 1 < cmdArgs.Length) decodePath = cmdArgs[++i];
            break;
        case "-render":
            if (i + 1 < cmdArgs.Length) mac = cmdArgs[++i];
            RenderAndShow(mac!);
            return;
        case "-verify":
            if (i + 5 < cmdArgs.Length)
            {
                VerifyBarcode(cmdArgs[++i], int.Parse(cmdArgs[++i]), int.Parse(cmdArgs[++i]), int.Parse(cmdArgs[++i]), int.Parse(cmdArgs[++i]), cmdArgs[++i]);
                return;
            }
            Console.WriteLine("错误：-verify <图片> <x0> <x1> <y0> <y1> <MAC>（逐模块比对照片图案与标准渲染）");
            return;
        case "-decode3":
            if (i + 4 < cmdArgs.Length)
            {
                DecodeRect(cmdArgs[++i], int.Parse(cmdArgs[++i]), int.Parse(cmdArgs[++i]), int.Parse(cmdArgs[++i]), int.Parse(cmdArgs[++i]));
                return;
            }
            Console.WriteLine("错误：-decode3 <图片> <y0> <y1> <x0> <x1>（按已知条码矩形精确解码）");
            return;
        case "-offset":
            if (i + 1 < args.Length && int.TryParse(args[++i], out var o)) offset = o;
            else { Console.WriteLine("错误：-offset 需要一个整数（dots）"); return; }
            break;
        case "-com":
            if (i + 1 < args.Length) com = args[++i];
            break;
        case "-printer":
            if (i + 1 < args.Length) printer = args[++i];
            break;
        default:
            if (mac == null && !args[i].StartsWith("-")) mac = args[i];
            else { Console.WriteLine($"未知参数：{args[i]}"); Usage(); return; }
            break;
    }
}

if (list || (mac == null && !print && decodePath == null))
{
    Console.WriteLine("=== 打印机（走打印队列） ===");
    try { foreach (var p in PrinterSettings.InstalledPrinters) Console.WriteLine($"  {p}"); } catch { }
    Console.WriteLine("=== 串口 COM（直连，绕过打印队列，推荐） ===");
    try { foreach (var p in SerialPort.GetPortNames()) Console.WriteLine($"  {p}"); } catch { }
    Console.WriteLine();
    Usage();
    return;
}

if (decodePath != null)
{
    DecodeImage(decodePath);
    return;
}

var svc = new ZebraPrinterService();
string bare = Code128Layout.Normalize(mac!);
var layout = Code128Layout.Compute(bare);
int off = offset ?? Code128Layout.HorizontalOffsetDots;
const double dpm = 600.0 / 25.4; // 600DPI 每 mm 点数 ≈ 23.62

Console.WriteLine("=== 打印参数（600DPI，双排：单列 30×20mm，整排 81mm，列缝 2mm）===");
Console.WriteLine($"MAC（规范化）    : {bare}");
Console.WriteLine("CODE128 子集     : B（^BC 默认子集，已在 ^FD 前显式加 \">:\" 起始码强制）");
Console.WriteLine($"模块数（条码宽度）: {Code128Layout.CountModules(bare)}");
Console.WriteLine($"模块宽           : {layout.ModuleWidth} dots（{layout.ModuleWidth / dpm:F3} mm）");
Console.WriteLine($"条码宽           : {layout.BarcodeWidthDots} dots（{layout.BarcodeWidthDots / dpm:F2} mm）");
Console.WriteLine($"条码高           : {Code128Layout.BarcodeHeightDots} dots（{Code128Layout.BarcodeHeightDots / dpm:F2} mm）");
Console.WriteLine($"整排宽 ^PW       : {Code128Layout.RowW} dots（{Code128Layout.RowW / dpm:F1} mm）；单列高 ^LL={Code128Layout.ColH} dots（{Code128Layout.ColH / dpm:F1} mm）");
Console.WriteLine($"水平偏移补偿     : {off} dots（{(off >= 0 ? "右移" : "左移")} {Math.Abs(off) / dpm:F2} mm）");
for (int col = 0; col < Code128Layout.Columns; col++)
{
    int blockX = Code128Layout.ColumnBlockX(col, off);
    int barcodeX = blockX + layout.X;
    Console.WriteLine($"第 {col + 1} 列        : 列块 x={blockX}，条码 ^FO x={barcodeX}（列内居中 {layout.X}），y={layout.Y}");
}
Console.WriteLine($"可读文本         : 条码下方 y={Code128Layout.TextY(layout)}，字号 {Code128Layout.TextFontDots}，列内全宽 {Code128Layout.ColW} 居中");

string zpl = svc.BuildMacBarcodeZpl(bare, offset);
Console.WriteLine();
Console.WriteLine($"=== ZPL 指令（{Encoding.ASCII.GetByteCount(zpl)} 字节）===");
Console.WriteLine(zpl);

if (print)
{
    string target = !string.IsNullOrWhiteSpace(com) ? com! : printer!;
    if (string.IsNullOrWhiteSpace(target))
    {
        Console.WriteLine("错误：-print 需要指定 -com COM3 或 -printer \"打印机名称\"");
        return;
    }
    try
    {
        svc.SendZpl(target, zpl);
        Console.WriteLine($"\n✔ 已发送到 {target}，请观察标签：左侧溢出→增大 -offset；右侧留白多→减小 -offset");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n✘ 发送失败：{ex.Message}");
    }
}

static LuminanceSource ToLuminance(Bitmap bmp)
{
    var w = bmp.Width; var h = bmp.Height;
    var pixels = new byte[w * h * 4];
    var rect = new Rectangle(0, 0, w, h);
    var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try { Marshal.Copy(bd.Scan0, pixels, 0, pixels.Length); }
    finally { bmp.UnlockBits(bd); }
    return new RGBLuminanceSource(pixels, w, h, RGBLuminanceSource.BitmapFormat.BGRA32);
}

static Bitmap ToBinary(Bitmap src, int threshold)
{
    var bmp = new Bitmap(src.Width, src.Height);
    using var g = Graphics.FromImage(bmp);
    g.DrawImage(src, 0, 0);
    for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
            var c = bmp.GetPixel(x, y);
            int lum = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
            bmp.SetPixel(x, y, lum < threshold ? Color.Black : Color.White);
        }
    return bmp;
}

static void DecodeImage(string path)
{
    Console.WriteLine($"=== 解码条码：{path} ===");
    if (!File.Exists(path)) { Console.WriteLine("错误：文件不存在"); return; }

    using var src = new Bitmap(path);
    Console.WriteLine($"图片尺寸：{src.Width} x {src.Height}");

    var attempts = new List<(string Name, Bitmap Bmp)>();
    attempts.Add(("原图", src));

    for (int thr = 60; thr <= 200; thr += 40)
        attempts.Add(($"二值化@阈值{thr}", ToBinary(src, thr)));

    var scaled = new Bitmap(src, src.Width * 2, src.Height * 2);
    attempts.Add(("放大2x", scaled));
    for (int thr = 60; thr <= 200; thr += 40)
        attempts.Add($"放大2x+二值化@阈值{thr}", ToBinary(scaled, thr));

    bool any = false;
    foreach (var (name, bmp) in attempts)
    {
        try
        {
            var reader = new BarcodeReaderGeneric
            {
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.CODE_128, BarcodeFormat.CODE_39 }
                }
            };
            var lum = ToLuminance(bmp);
            var r = reader.Decode(lum);
            if (r != null)
            {
                Console.WriteLine($"✔ [{name}] 解出：{r.Text}（格式 {r.BarcodeFormat}，{r.ResultPoints?.Length ?? 0} 个定位点）");
                any = true;
            }
        }
        catch { /* 单次失败继续尝试 */ }
        if (bmp != src && bmp != scaled) bmp.Dispose();
    }
    scaled.Dispose();

    if (!any) Console.WriteLine("✘ 所有预处理方式均无法解码——条码质量或内容存在问题");
    Console.WriteLine();
}

static void RenderAndShow(string mac)
{
    string bare = Code128Layout.Normalize(mac);
    var writer = new BarcodeWriterGeneric
    {
        Format = BarcodeFormat.CODE_128,
        Options = new EncodingOptions { PureBarcode = true, Height = 60, Width = 0, Margin = 0 }
    };
    var matrix = writer.Encode(bare);
    Console.WriteLine($"=== ZXing 标准渲染 {bare}（{matrix.Width} 模块）===");
    int y = matrix.Height / 2;
    var sb = new StringBuilder();
    for (int x = 0; x < matrix.Width; x++) sb.Append(matrix[x, y] ? '1' : '0');
    Console.WriteLine(sb.ToString());
    Console.WriteLine($"总模块数：{matrix.Width}");
    Console.WriteLine();
}

static void Usage()
{
    Console.WriteLine("用法：");
    Console.WriteLine("  ZebraPrinterConsole <MAC> [-offset N]            输出打印参数与 ZPL");
    Console.WriteLine("  ZebraPrinterConsole -print <MAC> -com COM3 [-offset N]   直连串口打印测试");
    Console.WriteLine("  ZebraPrinterConsole -print <MAC> -printer \"名称\" [-offset N]  打印队列打印测试");
    Console.WriteLine("  -offset N：水平偏移(dots)，正数右移，负数左移，1mm≈23.6 dots；左溢→正数");
}
