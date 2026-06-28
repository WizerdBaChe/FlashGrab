using System.Drawing;
using System.Drawing.Imaging;

namespace FlashGrab.Capture;

/// <summary>
/// 擷取層:截取桌面點陣。程序為 PerMonitorV2 DPI-aware,client/螢幕座標皆等於物理像素,
/// CopyFromScreen 取到的即是原解析度影像,利於 OCR 辨識率。
///
/// Phase 2 改採「定格截圖」:按下快捷鍵的瞬間先凍結整個 virtual screen,
/// 框選只是從這張快照裁切。如此擷取到的永遠是「按鍵那一刻」的畫面,
/// 與框選耗時無關(解決即時字幕),且無需等遮罩淡出的延遲。
/// </summary>
internal static class ScreenGrabber
{
    /// <summary>凍結整個 virtual desktop(所有螢幕)為一張快照。</summary>
    public static Bitmap CaptureVirtualScreen()
    {
        Rectangle vs = SystemInformation.VirtualScreen;
        var bmp = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(vs.Location, Point.Empty, vs.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    /// <summary>從快照裁切一塊區域(座標為相對於快照左上角的像素)。</summary>
    public static Bitmap Crop(Bitmap snapshot, Rectangle region)
    {
        var clamped = Rectangle.Intersect(region, new Rectangle(Point.Empty, snapshot.Size));
        var bmp = new Bitmap(clamped.Width, clamped.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.DrawImage(snapshot, new Rectangle(Point.Empty, clamped.Size), clamped, GraphicsUnit.Pixel);
        return bmp;
    }
}
