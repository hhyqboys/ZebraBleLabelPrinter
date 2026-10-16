using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace ZebraBleLabelPrinter.Services;

/// <summary>
/// 用 ZXing 渲染 CODE128 条形码到 WPF BitmapSource，供界面预览。
/// 与 ZPL ^BC 打印的内容一致（都是 CODE128），仅作视觉预览。
/// </summary>
public static class BarcodePreviewService
{
    public static BitmapSource RenderCode128(string content, int widthPx = 440, int heightPx = 250)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = widthPx,
                Height = heightPx,
                Margin = 0,
                PureBarcode = true,   // 不画人眼可读文本
            }
        };

        var pd = writer.Write(content);
        // pd.Pixels 是 BGRA 字节数组，长度 = Width * Height * 4
        var stride = pd.Width * 4;
        var bmp = BitmapSource.Create(
            pd.Width, pd.Height, 96, 96,
            PixelFormats.Bgra32, null,
            pd.Pixels, stride);
        bmp.Freeze();
        return bmp;
    }
}
