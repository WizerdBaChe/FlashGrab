using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>是否已顯示過首次執行的歡迎視窗;之後啟動只剩托盤 toast,維持無感。</summary>
    public bool WelcomeShown { get; set; }

    // ── Phase 4:Tier 2 選配 AI(OpenAI 相容視覺端點),預設全關 ──

    /// <summary>是否啟用 Tier 2。啟用後仍需框選時按住 Shift 才會走 AI;預設關閉。</summary>
    public bool Tier2Enabled { get; set; }

    /// <summary>OpenAI 相容端點 base URL(如 http://localhost:11434/v1 或 Gemini 端點)。</summary>
    public string? Tier2BaseUrl { get; set; }

    /// <summary>API 金鑰;本地 Ollama 留空即可。存於本機設定檔,不進任何發行物。</summary>
    public string? Tier2ApiKey { get; set; }

    /// <summary>模型名稱(如 gemini-2.5-flash、qwen2.5vl)。</summary>
    public string? Tier2Model { get; set; }

    /// <summary>是否已同意「雲端端點會將截圖外傳」的一次性告知(本地端點不需要)。</summary>
    public bool Tier2CloudConsented { get; set; }

    /// <summary>端點是否已備齊可用(base URL + 模型)。衍生屬性,不寫入設定檔。</summary>
    [JsonIgnore]
    public bool IsTier2Configured =>
        !string.IsNullOrWhiteSpace(Tier2BaseUrl) && !string.IsNullOrWhiteSpace(Tier2Model);

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
