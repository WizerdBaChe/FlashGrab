using System.Text;

namespace FlashGrab.App;

/// <summary>
/// 只記錄「安全上有決定性」的事件(目前僅金鑰儲存格式遷移),寫到
/// %AppData%\FlashGrab\security.log,每行以 [security] 開頭方便 grep。
///
/// FlashGrab 本身沒有(也不需要)一般用途的 log:一個閒置零負擔的托盤工具
/// 沒有東西值得長期記錄。但金鑰曾以明文落地這件事若不留痕跡,使用者就永遠
/// 不會知道該去重新產生金鑰——那才是這個檔案存在的理由。
/// </summary>
internal static class SecurityLog
{
    private const long MaxBytes = 256_000;
    private static readonly object Gate = new();

    private static string Directory_ => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlashGrab");

    internal static string FilePath => Path.Combine(Directory_, "security.log");

    internal static void Write(string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [security] {Sanitize(message)}";
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Directory_);
                string path = FilePath;
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Move(path, path + ".1", overwrite: true);
                }

                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
                // 寫不進 log 不該讓程式起不來
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>去掉控制字元,避免夾帶換行的外部字串偽造出整行 log。</summary>
    private static string Sanitize(string message)
    {
        var builder = new StringBuilder(message.Length);
        foreach (char ch in message)
        {
            builder.Append(char.IsControl(ch) ? ' ' : ch);
        }

        return builder.ToString();
    }
}
