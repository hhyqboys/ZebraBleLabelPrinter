using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace ZebraBleLabelPrinter.Services;

/// <summary>
/// 用 ZXing 渲染 CODE128 条形码到 WPF BitmapSource，供界面预览与 ^GFA 位图打印。
/// 位图宽度 = 条码实体 + 两侧 10 模块静区（Margin 单位为模块数），
/// 打印内容与屏幕预览完全一致。
/// </summary>
public static class BarcodePreviewService
{
    public static BitmapSource RenderCode128(string content, int imageWidthPx, int heightPx)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = imageWidthPx,
                Height = heightPx,
                Margin = Code128Layout.QuietModules, // 每侧 10 模块静区，画进位图内
                PureBarcode = true,                  // 不画人眼可读文本
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
