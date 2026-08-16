using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlashGrab.App;

/// <summary>
/// 使用者設定,存於 %AppData%\FlashGrab\settings.json。
/// Phase 2 範圍:段落重排開關 + 辨識語言。
///
/// Tier 2 的 API 金鑰是付費/雲端供應商的 bearer 憑證,先經 DPAPI(CurrentUser
/// 範圍)加密才落地,設定檔裡只有密文。
///
/// 這道保護的實際範圍要講清楚:它擋得住「設定檔被複製到別台機器、被備份、被
/// 同步軟體帶走」與「同機其他本機帳號直接讀這個檔」。它擋不住「以同一個
/// Windows 使用者身分執行的程式碼」——那種情況下解密所需的一切本來就在手上。
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

    /// <summary>DPAPI 密文的 Base64,絕不是金鑰本身。空字串 = 未設定。</summary>
    public string Tier2ApiKeyProtected { get; set; } = "";

    /// <summary>
    /// 僅供遷移:v0.4.1 以前的設定檔把金鑰以明文寫在 "Tier2ApiKey" 欄位。
    /// Load() 讀到就轉存成密文並清空本欄,使用者不必重新輸入金鑰。
    /// </summary>
    [JsonPropertyName("Tier2ApiKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPlaintextApiKey { get; set; }

    /// <summary>
    /// 明文金鑰,只存在於記憶體;null = 未設定(本地 Ollama 留空即可)。
    /// 讀取時即時解密,寫入時即時加密,不寫進設定檔。
    /// </summary>
    [JsonIgnore]
    public string? Tier2ApiKey
    {
        get => string.IsNullOrEmpty(LegacyPlaintextApiKey)
            ? NullIfEmpty(Unprotect(Tier2ApiKeyProtected))
            : LegacyPlaintextApiKey;
        set
        {
            Tier2ApiKeyProtected = Protect(value);
            LegacyPlaintextApiKey = null;
        }
    }

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
        string path = FilePath;
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<Settings>(json);
                if (loaded is not null)
                {
                    loaded.MigrateLegacyApiKey();
                    return loaded;
                }

                // 檔案在、內容卻反序列化成 null(整個檔是 "null" 或空白)。
                SecurityLog.Write(
                    "settings.json 存在但沒有可用內容,本次啟動改用預設值;"
                    + "若原本設過 Tier 2 金鑰,需要重新輸入。");
            }
        }
        catch (Exception ex)
        {
            // 「檔案不存在」是首次執行的正常狀態,上面的 File.Exists 已經把它擋在外面;
            // 走到這裡代表檔案在、卻讀不動或解析不了,等於使用者的設定(含加密後的
            // API 金鑰)當場歸零。這種事不留痕跡就會變成無聲的資料遺失,所以記一行。
            SecurityLog.Write(
                $"settings.json 讀取或解析失敗({ex.GetType().Name}),本次啟動改用預設值;"
                + "若原本設過 Tier 2 金鑰,需要重新輸入。");
        }

        return new Settings();
    }

    /// <summary>
    /// 舊版明文金鑰就地改為加密存放。金鑰曾以明文躺在磁碟上,所以不是「改完就沒事」:
    /// 留一則 security log 明講這件事,並建議去供應商後台重新產生一組。
    /// </summary>
    private void MigrateLegacyApiKey()
    {
        if (string.IsNullOrEmpty(LegacyPlaintextApiKey))
        {
            return;
        }

        string plaintext = LegacyPlaintextApiKey;
        Tier2ApiKey = plaintext;   // 加密進 Tier2ApiKeyProtected,並清空明文欄位

        if (string.IsNullOrEmpty(Tier2ApiKeyProtected))
        {
            // DPAPI 加密失敗(極罕見)。此時清掉明文等於直接弄丟使用者的金鑰,
            // 所以原樣還原:功能照舊可用,下次啟動再試一次遷移。
            LegacyPlaintextApiKey = plaintext;
            SecurityLog.Write(
                "偵測到舊版明文 Tier 2 API 金鑰,但 DPAPI 加密失敗,暫時維持明文存放;"
                + "下次啟動會再試一次。");
            return;
        }

        Save();
        SecurityLog.Write(
            "偵測到舊版明文 Tier 2 API 金鑰,已改為 DPAPI 加密存放。"
            + "該金鑰曾以明文寫在 settings.json,建議到 AI 供應商後台重新產生一組。");
    }

    /// <summary>
    /// 先寫到同目錄的 .tmp 再改名蓋回去。改名在 NTFS 上是原子操作,所以任何時刻的
    /// 讀者看到的要嘛是完整的舊檔、要嘛是完整的新檔,不會是寫到一半的殘檔。
    ///
    /// 直接 File.WriteAllText(path, ...) 不行:它在寫入第一個位元組之前就把原檔截斷,
    /// 程序若在中途死掉(當機、強制斷電、防毒/EDR 中斷),磁碟上就留下一個解析不了的
    /// settings.json,Load() 只會靜默回退預設值——連剛加密好的 Tier2ApiKeyProtected
    /// 一起丟掉,使用者得重新輸入金鑰。MigrateLegacyApiKey() 會在啟動時自動呼叫本方法,
    /// 這條寫入路徑沒有人在旁邊看著,所以它必須自己是安全的。
    /// </summary>
    public void Save()
    {
        string path = FilePath;
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // 不指定 encoding:與改動前同樣寫出不含 BOM 的 UTF-8(縮排換行由
            // JsonOptions 決定,在 Windows 上是 CRLF),避免順手改掉檔案格式。
            File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // 寫入失敗不致命(例如唯讀目錄),靜默忽略。原檔完好——改名是唯一會碰到
            // 真正路徑的步驟,沒走到就等於什麼都沒發生;剩下的暫存檔順手清掉。
            TryDelete(temp);
        }
    }

    /// <summary>清掉半途而廢的暫存檔;連這步都失敗也不該把原本的錯誤蓋掉。</summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 檔案被鎖住或權限不足。留一個 .tmp 在旁邊不影響任何功能,下次存檔會覆蓋它。
        }
    }

    // ── DPAPI 輔助 ──

    private static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return "";
        }

        try
        {
            byte[] bytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch (CryptographicException)
        {
            return "";
        }
    }

    private static string Unprotect(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return "";
        }

        try
        {
            byte[] bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(encoded), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception)
        {
            // 從別台機器/別的帳號複製過來,或被手改過。一律視為未設定。
            return "";
        }
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
