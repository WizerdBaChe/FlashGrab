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

/// <summary>輸出層:把框選畫面存成 PNG 到「圖片\FlashGrab」,回傳完整路徑。</summary>
internal static class ImageSaver
{
    public static string SavePng(System.Drawing.Bitmap bitmap)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "FlashGrab");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"FlashGrab_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }
}
