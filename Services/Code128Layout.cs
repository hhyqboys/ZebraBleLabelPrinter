using System;

namespace ZebraBleLabelPrinter.Services;

/// <summary>
/// CODE128 条码在 600DPI、33×33mm 标签上的统一排版计算（ZPL ^BC 原生条码方案，不使用位图打印）。
/// 模块宽固定 3 dots（用户指定），坐标按条码实体宽精确居中；
/// 水平偏移由 HorizontalOffsetDots 补偿（左溢→调大右移）。
/// </summary>
public static class Code128Layout
{
    // 600DPI：1mm ≈ 23.62 dots；60mm ≈ 780 dots535C8B540D36
    public const int LabelW = 1420;              // 标签宽 dots（60mm）
    public const int LabelH = 945;              // 标签高 dots（60mm）
    public const int BarcodeHeightDots = 450;   // 条码高 dots（≈19mm，垂直居中）

    /// <summary>CODE128 标准静区 = 窄条宽（模块宽）的 10 倍。^BC 不自动加静区，由居中式留白保证。</summary>
    public const int QuietModules = 10;

    /// <summary>模块宽固定 3 dots（600DPI 下 0.127mm/5mil），用户指定。</summary>
    public const int ModuleWidth = 5;

    /// <summary>
    /// 水平偏移补偿（dots）：打印机介质/传感器/存储偏移导致条码整体左偏（左侧溢出被截）时调大（条码右移），
    /// 右偏时调小。默认 0 = 无补偿。1mm ≈ 23.6 dots。
    /// 可用控制台工具 ZebraPrinterConsole 的 -offset 参数快速试值。
    /// </summary>
    public const int HorizontalOffsetDots = 0;

    /// <summary>
    /// 排版结果：模块宽（dots）、条码实体宽（dots）、含静区宽度（dots，仅供屏幕预览）、居中坐标 X/Y（dots）。
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
    /// 打印宽度相同（均为 CODE B），因此统一按 CODE B 计算，避免按 CODE C 估宽导致位置偏右。
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

    /// <summary>
    /// 计算排版坐标。模块宽固定 3 dots；X 按条码实体宽居中（^BC 无位图静区）。
    /// 所有 MAC 统一按 CODE B（167 模块 → 条码 501 dots）计算，x=139，保证任意 MAC
    /// 打印位置完全一致（不再出现"纯数字与带字母位置不同"）。
    /// </summary>
    public static Layout Compute(string content, int labelW = LabelW, int labelH = LabelH, int barcodeHeight = BarcodeHeightDots)
    {
        int modules = CountModules(content);
        int module = ModuleWidth;
        int barcodeWidth = modules * module;                      // 条码实体宽
        int imageWidth = (modules + 2 * QuietModules) * module;   // 含左右静区宽度（仅屏幕预览用）
        int x = Math.Max(0, (labelW - barcodeWidth) / 2);
        int y = Math.Max(0, (labelH - barcodeHeight) / 2);
        return new Layout(module, barcodeWidth, imageWidth, x, y);
    }
}
