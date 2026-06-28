using System.Text.Json;

namespace FlashGrab.App;

/// <summary>
/// 使用者設定,存於 %AppData%\FlashGrab\settings.json。
/// Phase 2 範圍:段落重排開關 + 辨識語言。
/// </summary>
internal sealed class Settings
{
    /// <summary>段落重排(把視覺軟換行併成段落);預設關閉=忠實保留行序。</summary>
    public bool ReflowParagraphs { get; set; }

    /// <summary>偏好辨識語言 BCP-47 標籤(如 "zh-Hant");null = 使用者設定檔語言。</summary>
    public string? LanguageTag { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlashGrab");
            return Path.Combine(dir, "settings.json");
        }
    }

    public static Settings Load()
    {
        try
        {
            string path = FilePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<Settings>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // 設定毀損/無法讀取時回退預設值,不影響啟動
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // 寫入失敗不致命(例如唯讀目錄),靜默忽略
        }
    }
}
