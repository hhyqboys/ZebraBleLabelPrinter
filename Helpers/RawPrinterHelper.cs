using System.Runtime.InteropServices;
using System.Text;

namespace ZebraBleLabelPrinter.Helpers;

/// <summary>
/// 通过 Windows 打印后台处理程序直接向打印机发送原始字节（ZPL）。
/// 适用于 ZDesigner 驱动通过 USB 安装的 Zebra 打印机。
/// </summary>
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName = "Zebra BLE Label";
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType = "RAW";
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static void SendBytesToPrinter(string printerName, byte[] data)
    {
        IntPtr hPrinter = IntPtr.Zero;
        IntPtr pUnmanagedBytes = IntPtr.Zero;
        try
        {
            if (!OpenPrinter(printerName.Normalize(), out hPrinter, IntPtr.Zero))
                throw new InvalidOperationException($"OpenPrinter 失败（{printerName}），错误码 {Marshal.GetLastWin32Error()}");

            var di = new DOCINFOA();
            if (!StartDocPrinter(hPrinter, 1, di))
                throw new InvalidOperationException($"StartDocPrinter 失败，错误码 {Marshal.GetLastWin32Error()}");
            if (!StartPagePrinter(hPrinter))
                throw new InvalidOperationException($"StartPagePrinter 失败，错误码 {Marshal.GetLastWin32Error()}");

            pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
            Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);

            if (!WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out int written))
                throw new InvalidOperationException($"WritePrinter 失败，错误码 {Marshal.GetLastWin32Error()}");
            if (written != data.Length)
                throw new InvalidOperationException($"WritePrinter 写入不完整：{written}/{data.Length}");

            EndPagePrinter(hPrinter);
            EndDocPrinter(hPrinter);
        }
        finally
        {
            if (pUnmanagedBytes != IntPtr.Zero) Marshal.FreeCoTaskMem(pUnmanagedBytes);
            if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter);
        }
    }

        public static void SendStringToPrinter(string printerName, string s)
        {
            // ZPL 用 CodePage 850 / ASCII 即可；MAC 全是 ASCII
            var bytes = Encoding.ASCII.GetBytes(s);
            SendBytesToPrinter(printerName, bytes);
        }
}
