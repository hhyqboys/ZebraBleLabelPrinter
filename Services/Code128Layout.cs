using System;

namespace ZebraBleLabelPrinter.Services;

/// <summary>
/// CODE128 条码在 600DPI、双排标签（单列 30×20mm，整排 81mm，列缝 2mm）上的统一排版计算。
/// ZPL ^BC 原生条码方案（不使用位图打印），模块宽固定 3 dots；
/// 一排两列，同一 MAC 打两份，每列内条码+可读文本垂直居中。
/// 水平偏移由 HorizontalOffsetDots 补偿（左溢→调大右移）。
/// </summary>
public static class Code128Layout
{
    // 600DPI：1mm ≈ 23.622 dots（600 / 25.4）
    public const double Dpm = 600.0 / 25.4;

    // —— 双排标签纸物理尺寸（600DPI 换算 dots）——
    public const int RowW = 1913;   // 整排宽 dots（81mm，^PW）
    public const int ColW = 709;    // 单列宽 dots（30mm）
    public const int ColH = 472;    // 单列高 dots（20mm，^LL）
    public const int ColGap = 47;  // 两列之间的缝 dots（2mm）
    public const int Columns = 2;   // 一排两列

    // —— 单列内部排版 ——
    public const int BarcodeHeightDots = 300;   // 条码高 dots（≈12.7mm）
    public const int TextFontDots = 36;        // 可读文本字号 dots（≈1.52mm）
    public const int TextGapDots = 10;         // 条码底到文本顶的间隙 dots

    // 兼容旧引用（LabelW/LabelH）：LabelW=整排宽，LabelH=单列高
    public const int LabelW = RowW;
    public const int LabelH = ColH;

    /// <summary>CODE128 标准静区 = 窄条宽（模块宽）的 10 倍。^BC 不自动加静区，由居中式留白保证。</summary>
    public const int QuietModules = 10;

    /// <summary>模块宽固定 3 dots（600DPI 下 0.127mm/5mil），用户指定。</summary>
    public const int ModuleWidth = 3;

    /// <summary>
    /// 水平偏移补偿（dots）：打印机介质/传感器/存储偏移导致条码整体左偏（左侧溢出被截）时调大（条码右移），
    /// 右偏时调小。默认 0 = 无补偿。1mm ≈ 23.6 dots。
    /// 可用控制台工具 ZebraPrinterConsole 的 -offset 参数快速试值。
    /// </summary>
    public const int HorizontalOffsetDots = 0;

    /// <summary>
    /// 排版结果：模块宽（dots）、条码实体宽（dots）、含静区宽度（dots，仅供屏幕预览）、
    /// 列内居中坐标 X、列内块顶 Y（dots）。
    /// </summary>
    public sealed record Layout(int ModuleWidth, int BarcodeWidthDots, int ImageWidthDots, int X, int Y);

    /// <summary>规范化 MAC：只保留十六进制字符、统一大写。</summary>
    public static string Normalize(string mac)
    {
        var sb = new System.Text.StringBuilder(mac.Length);
        foreach (char c in mac)
            if (Uri.IsHexDigit(c))
                sb.Append(char.ToUpperInvariant(c));
        return sb.ToString();
    }

    /// <summary>
    /// CODE128 实际模块数（与打印机 ^BC 行为保持一致）：
    /// Zebra ^BC 默认模式（m=N、^FD 无起始码）一律使用 Subset B——实测纯数字与带字母 MAC
    /// 打印宽度相同（均为 CODE B），因此统一按 CODE B 计算。
    /// 总模块数 = 起始 11 + 数据字符×11 + 校验 11 + 终止 13。
    /// 12 位 MAC（CODE B）= 11 + 132 + 11 + 13 = 167 模块。
    /// </summary>
    public static int CountModules(string content)
    {
        string bare = Normalize(content);
        // 恒按 Subset B：每字符 1 个符号（Zebra ^BC 默认行为，已实测确认）
        int symbols = bare.Length;
        return 11 * (symbols + 3) + 2;
    }

    /// <summary>第 colIndex 列块在整排坐标系里的左起点 x（dots），叠加水平偏移。</summary>
    public static int ColumnBlockX(int colIndex, int off = 0)
        => colIndex * (ColW + ColGap) + off;

    /// <summary>
    /// 计算单列内排版坐标。模块宽固定 3 dots；X 按条码实体宽在单列内居中；
    /// Y = 条码+文本整体在单列高度内垂直居中后的块顶位置。
    /// </summary>
    public static Layout Compute(string content, int labelW = ColW, int labelH = ColH, int barcodeHeight = BarcodeHeightDots)
    {
        int modules = CountModules(content);
        int module = ModuleWidth;
        int barcodeWidth = modules * module;                      // 条码实体宽
        int imageWidth = (modules + 2 * QuietModules) * module;   // 含左右静区宽度（仅屏幕预览用）
        int x = Math.Max(0, (labelW - barcodeWidth) / 2);
        // 条码 + 间隙 + 文本 整体垂直居中
        int blockH = barcodeHeight + TextGapDots + TextFontDots;
        int y = Math.Max(0, (labelH - blockH) / 2);
        return new Layout(module, barcodeWidth, imageWidth, x, y);
    }

    /// <summary>可读文本在列内的 y（条码块顶 Y + 条码高 + 文本间隙）。</summary>
    public static int TextY(Layout layout) => layout.Y + BarcodeHeightDots + TextGapDots;
}
