using System.Drawing;
using System.Drawing.Imaging;

namespace FlashGrab.Capture;

/// <summary>
/// 擷取層:依螢幕(物理像素)矩形,從桌面複製點陣。
/// 程序為 PerMonitorV2 DPI-aware,故 client/螢幕座標皆等於物理像素,
/// CopyFromScreen 取到的即是原解析度影像,利於 OCR 辨識率。
/// </summary>
internal static class ScreenGrabber
{
    public static Bitmap Capture(Rectangle screenRect)
    {
        var bmp = new Bitmap(screenRect.Width, screenRect.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(
            screenRect.Location,
            Point.Empty,
            screenRect.Size,
            CopyPixelOperation.SourceCopy);
        return bmp;
    }
}
