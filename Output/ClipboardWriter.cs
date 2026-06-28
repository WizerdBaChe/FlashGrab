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
}
