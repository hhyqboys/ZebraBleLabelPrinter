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

    [StructLayout(LayoutKind.Sequential)]
    private struct PRINTER_DEFAULTS
    {
        public IntPtr pDataType;
        public IntPtr pDevMode;
        public int DesiredAccess;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JOB_INFO_1
    {
        public int JobId;
        public IntPtr pPrinterName;
        public IntPtr pMachineName;
        public IntPtr pUserName;
        public IntPtr pDocument;
        public IntPtr pDatatype;
        public IntPtr pStatus;
        public int Status;
        public int Priority;
        public int Position;
        public int TotalPages;
        public int PagesPrinted;
    }

    private const int PRINTER_ALL_ACCESS = 0x000F0000;
    private const int JOB_CONTROL_DELETE = 5;

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool OpenPrinterW(string szPrinter, out IntPtr hPrinter, ref PRINTER_DEFAULTS pd);

    [DllImport("winspool.Drv", EntryPoint = "EnumJobsW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool EnumJobsW(IntPtr hPrinter, int FirstJob, int NoJobs, int Level, IntPtr pJob, int cbBuf, out int pcbNeeded, out int pcReturned);

    [DllImport("winspool.Drv", EntryPoint = "SetJobW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool SetJobW(IntPtr hPrinter, int JobId, int Level, IntPtr pJob, int Command);

    /// <summary>
    /// 清空指定打印机的排队任务（含“错误-正在打印-正在重新启动”的卡死任务）。
    /// 发送新任务前调用，避免旧任务阻塞/冲突。打不开队列时静默跳过，不影响打印。
    /// </summary>
    public static void PurgePrinterQueue(string printerName)
    {
        IntPtr h = IntPtr.Zero;
        try
        {
            var pd = new PRINTER_DEFAULTS { DesiredAccess = PRINTER_ALL_ACCESS };
            if (!OpenPrinterW(printerName.Normalize(), out h, ref pd) || h == IntPtr.Zero) return;

            for (int pass = 0; pass < 3; pass++)
            {
                int needed = 0, returned = 0;
                if (!EnumJobsW(h, 0, int.MaxValue, 1, IntPtr.Zero, 0, out needed, out returned)) return;
                if (needed <= 0) return;

                IntPtr buf = Marshal.AllocHGlobal(needed);
                try
                {
                    if (!EnumJobsW(h, 0, int.MaxValue, 1, buf, needed, out _, out returned)) return;
                    if (returned <= 0) return;
                    IntPtr p = buf;
                    for (int i = 0; i < returned; i++)
                    {
                        var job = Marshal.PtrToStructure<JOB_INFO_1>(p);
                        SetJobW(h, job.JobId, 0, IntPtr.Zero, JOB_CONTROL_DELETE);
                        p = IntPtr.Add(p, Marshal.SizeOf<JOB_INFO_1>());
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
        }
        catch { /* 清理失败不阻塞打印流程 */ }
        finally
        {
            if (h != IntPtr.Zero) ClosePrinter(h);
        }
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
