namespace FlashGrab.Output;

/// <summary>
/// 輸出層:寫入剪貼簿。剪貼簿偶爾被其他程式鎖定,故重試數次。
/// 須於 STA 執行緒呼叫(WinForms UI 執行緒即是)。
/// </summary>
internal static class ClipboardWriter
{
    public static void Write(string text)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                Thread.Sleep(40);
            }
        }
    }

    /// <summary>寫入圖片(輪盤「複製圖片」);同樣對剪貼簿鎖定重試。</summary>
    public static void WriteImage(System.Drawing.Image image)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
                Thread.Sleep(40);
            }
        }
    }
}

/// <summary>輸出層:把框選畫面存成 PNG/JPG 到「圖片\FlashGrab」,回傳完整路徑。</summary>
internal static class ImageSaver
{
    public static string SavePng(System.Drawing.Bitmap bitmap) => Save(bitmap, "png", System.Drawing.Imaging.ImageFormat.Png);

    public static string SaveJpg(System.Drawing.Bitmap bitmap)
    {
        // JPG 沒有透明度:先鋪白底,避免透明像素變黑。
        using var flat = new System.Drawing.Bitmap(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = System.Drawing.Graphics.FromImage(flat))
        {
            g.Clear(System.Drawing.Color.White);
            g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }

        return Save(flat, "jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
    }

    private static string Save(System.Drawing.Bitmap bitmap, string ext, System.Drawing.Imaging.ImageFormat format)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "FlashGrab");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"FlashGrab_{DateTime.Now:yyyyMMdd_HHmmss_fff}.{ext}");
        bitmap.Save(path, format);
        return path;
    }
}
