using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ZebraBleLabelPrinter.Services;

/// <summary>
/// 把 WPF BitmapSource（黑白条码）编码成 ZPL ^GFA 位图指令。
/// 打印出来的内容与屏幕预览完全一致。
/// </summary>
public static class BarcodeBitmapEncoder
{
    /// <summary>
    /// 生成完整的 ^GFA 段（不含 ^XA/^XZ）。
    /// 坐标 x,y 由外部 ^FO 指定。
    /// </summary>
    public static string ToZplGfa(BitmapSource bmp)
    {
        // 转成 1bpp 黑白
        var mono = new FormatConvertedBitmap(bmp, PixelFormats.BlackWhite, null, 0.0);

        int w = mono.PixelWidth;
        int h = mono.PixelHeight;
        int rowBytes = (w + 7) / 8;
        // BlackWhite stride 按 4 字节对齐
        int stride = (rowBytes + 3) / 4 * 4;

        byte[] src = new byte[h * stride];
        mono.CopyPixels(src, stride, 0);

        // 1bpp MSB 优先：bit=1 表示白，bit=0 表示黑。ZPL ^GF 里 bit=1=打印黑点。
        // 所以需要逐位取反。
        var sb = new StringBuilder(h * rowBytes * 2);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < rowBytes; x++)
            {
                byte b = (byte)~src[y * stride + x];
                sb.Append(b.ToString("X2"));
            }
        }

        int total = h * rowBytes;
        // ^GFA<totalByteCount>,<totalByteCount>,<rowByteCount>,<data>
        return $"^GFA{total:X},{total:X},{rowBytes:X},{sb}";
    }
}
