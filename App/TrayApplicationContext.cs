using System.Drawing;
using System.Media;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using FlashGrab.Capture;
using FlashGrab.Ocr;
using FlashGrab.Output;
using FlashGrab.Pipeline;
using FlashGrab.Trigger;

namespace FlashGrab.App;

/// <summary>
/// 無主視窗的常駐入口:系統匣圖示 + 全域快捷鍵。
/// Phase 2:定格截圖 → Tier 0 OCR(結構化)→ 後處理 Pipeline → 剪貼簿 + 音效。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly GlobalHotkey _hotkey;
    private readonly TextPipeline _pipeline = TextPipeline.CreateDefault();
    private readonly Settings _settings = Settings.Load();

    private IOcrEngine _ocr;
    private bool _capturing;
    private bool _hotkeyOk;

    // 第二實例喚醒第一實例用的具名事件 + 用來把該通知 marshal 回 UI 執行緒的同步內容
    private readonly EventWaitHandle _showSignal;
    private SynchronizationContext? _ui;

    // 設定視窗儲存後需同步勾選狀態的托盤項目
    private ToolStripMenuItem _reflowItem = null!;
    private ToolStripMenuItem _languageRoot = null!;
    private ToolStripMenuItem _aiEnableItem = null!;
    private ToolStripMenuItem _aiStatusItem = null!;
    private ToolStripMenuItem _presetsRoot = null!;

    internal const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai";
    internal const string NimBaseUrl = "https://integrate.api.nvidia.com/v1";
    internal const string OllamaBaseUrl = "http://localhost:11434/v1";
    internal const string DefaultLocalModel = "maternion/LightOnOCR-2:latest";

    public TrayApplicationContext(EventWaitHandle showSignal)
    {
        _showSignal = showSignal;
        _ocr = new WindowsMediaOcr(_settings.LanguageTag);

        var menu = new ContextMenuStrip();

        menu.Items.Add("設定…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());

        _reflowItem = new ToolStripMenuItem("段落重排(抄文章用)")
        {
            CheckOnClick = true,
            Checked = _settings.ReflowParagraphs,
            ToolTipText = "開啟:把視覺軟換行併成段落。預設關閉=忠實保留畫面行序。",
        };
        _reflowItem.CheckedChanged += (_, _) =>
        {
            _settings.ReflowParagraphs = _reflowItem.Checked;
            _settings.Save();
        };
        menu.Items.Add(_reflowItem);

        _languageRoot = BuildLanguageMenu();
        menu.Items.Add(_languageRoot);
        menu.Items.Add(BuildAiMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("關於 FlashGrab", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("結束", null, (_, _) => ExitApp());

		_trayIcon = new NotifyIcon
        {
            Icon = (Environment.ProcessPath != null) 
                ? Icon.ExtractAssociatedIcon(Environment.ProcessPath) 
                : SystemIcons.Application,
            Text = "FlashGrab — 螢幕智慧取字 (Win+Shift+C)",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _hotkey = new GlobalHotkey();
        _hotkey.HotkeyPressed += OnHotkeyPressed;
        _hotkeyOk = _hotkey.Register(ModifierKeys.Win | ModifierKeys.Shift, Keys.C);

        // 啟動反饋(toast / 首次歡迎)需在訊息迴圈就緒後才能可靠顯示並捕捉 UI 同步內容,
        // 故用一次性 Timer 延到迴圈啟動後執行(建構子此刻迴圈尚未開始)。
        var startup = new System.Windows.Forms.Timer { Interval = 1 };
        startup.Tick += (_, _) =>
        {
            startup.Stop();
            startup.Dispose();
            _ui = SynchronizationContext.Current;
            OnStarted();
        };
        startup.Start();

        StartSecondInstanceListener();
    }

    /// <summary>訊息迴圈就緒後執行一次:啟動 toast + 首次歡迎視窗。</summary>
    private void OnStarted()
    {
        if (!_hotkeyOk)
        {
            _trayIcon.ShowBalloonTip(
                4000, "FlashGrab",
                "已在背景執行,但全域快捷鍵 Win+Shift+C 註冊失敗(可能被其他程式佔用)。",
                ToolTipIcon.Warning);
        }
        else
        {
            _trayIcon.ShowBalloonTip(
                2500, "FlashGrab",
                "已在背景執行 · 按 Win + Shift + C 開始取字", ToolTipIcon.Info);
        }

        if (!_settings.WelcomeShown)
        {
            _settings.WelcomeShown = true;
            _settings.Save();
            WelcomeForm.Show(_trayIcon.Icon, OpenSettings);
        }
    }

    /// <summary>第二實例透過具名事件喚醒時,在第一實例跳「已在執行中」氣泡。</summary>
    private void StartSecondInstanceListener()
    {
        var thread = new Thread(() =>
        {
            try
            {
                while (true)
                {
                    _showSignal.WaitOne();
                    _ui?.Post(_ => ShowAlreadyRunning(), null);
                }
            }
            catch
            {
                // 程序結束、handle 釋放時的例外屬正常退出,忽略。
            }
        })
        {
            IsBackground = true,
            Name = "FlashGrab.SignalListener",
        };
        thread.Start();
    }

    private void ShowAlreadyRunning()
    {
        _trayIcon.ShowBalloonTip(
            2000, "FlashGrab",
            "已在執行中 — 看右下角系統匣圖示,或直接按 Win + Shift + C。",
            ToolTipIcon.Info);
    }

    /// <summary>開啟正規設定視窗(供首次歡迎視窗與托盤「設定…」共用)。</summary>
    private void OpenSettings()
    {
        SettingsForm.Show(_settings, WindowsMediaOcr.AvailableLanguages, _trayIcon.Icon, ApplySettingsChanges);
    }

    /// <summary>設定視窗儲存後:重建 OCR 引擎並同步托盤選單的勾選狀態。</summary>
    private void ApplySettingsChanges()
    {
        _ocr = new WindowsMediaOcr(_settings.LanguageTag);
        _reflowItem.Checked = _settings.ReflowParagraphs;

        foreach (ToolStripItem item in _languageRoot.DropDownItems)
        {
            if (item is ToolStripMenuItem mi)
            {
                mi.Checked = (mi.Tag as string) == _settings.LanguageTag
                    || (mi.Tag is null && _settings.LanguageTag is null);
            }
        }

        _aiEnableItem.Checked = _settings.Tier2Enabled && _settings.IsTier2Configured;
        UpdateAiStatus();
    }

    private ToolStripMenuItem BuildLanguageMenu()
    {
        var root = new ToolStripMenuItem("辨識語言");

        var auto = new ToolStripMenuItem("自動(系統語言)")
        {
            Checked = _settings.LanguageTag is null,
            CheckOnClick = false,
        };
        auto.Click += (_, _) => SetLanguage(root, null);
        root.DropDownItems.Add(auto);

        foreach (var lang in WindowsMediaOcr.AvailableLanguages)
        {
            string tag = lang.LanguageTag;
            var item = new ToolStripMenuItem(lang.DisplayName)
            {
                Checked = string.Equals(_settings.LanguageTag, tag, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false,
                Tag = tag,
            };
            item.Click += (_, _) => SetLanguage(root, tag);
            root.DropDownItems.Add(item);
        }

        return root;
    }

    private void SetLanguage(ToolStripMenuItem root, string? tag)
    {
        _settings.LanguageTag = tag;
        _settings.Save();
        _ocr = new WindowsMediaOcr(tag);

        foreach (ToolStripItem item in root.DropDownItems)
        {
            if (item is ToolStripMenuItem mi)
            {
                mi.Checked = (mi.Tag as string) == tag || (mi.Tag is null && tag is null);
            }
        }
    }

    private ToolStripMenuItem BuildAiMenu()
    {
        var root = new ToolStripMenuItem("AI 增強 (Tier 2)");

        _aiEnableItem = new ToolStripMenuItem("啟用(框選時按住 Shift 用 AI)")
        {
            CheckOnClick = true,
            Checked = _settings.Tier2Enabled,
            ToolTipText = "啟用後,一般快捷鍵仍用內建 OCR;框選放開時按住 Shift 才走 AI。",
        };
        _aiEnableItem.CheckedChanged += (_, _) =>
        {
            if (_aiEnableItem.Checked && !_settings.IsTier2Configured)
            {
                MessageBox.Show("請先在「端點預設」選一個來源(或自訂 Base URL + 模型)。",
                    "尚未設定 AI 端點", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _aiEnableItem.Checked = false;
                return;
            }

            _settings.Tier2Enabled = _aiEnableItem.Checked;
            _settings.Save();
            UpdateAiStatus();
        };
        root.DropDownItems.Add(_aiEnableItem);

        root.DropDownItems.Add(new ToolStripSeparator());

        _presetsRoot = new ToolStripMenuItem("端點預設");
        _presetsRoot.DropDownItems.Add(new ToolStripMenuItem("本地 Ollama(離線、私密)", null,
            async (_, _) => await BindLocalOllamaAsync()) { Tag = OllamaBaseUrl, CheckOnClick = false });
        _presetsRoot.DropDownItems.Add(new ToolStripMenuItem("Google Gemini(免費雲端)", null,
            (_, _) => ApplyCloudPreset(GeminiBaseUrl, "gemini-3.1-flash-lite")) { Tag = GeminiBaseUrl, CheckOnClick = false });
        _presetsRoot.DropDownItems.Add(new ToolStripMenuItem("NVIDIA NIM(免費雲端)", null,
            (_, _) => ApplyCloudPreset(NimBaseUrl, "meta/llama-3.2-90b-vision-instruct")) { Tag = NimBaseUrl, CheckOnClick = false });
        root.DropDownItems.Add(_presetsRoot);

        root.DropDownItems.Add(new ToolStripMenuItem("設定 API 金鑰…", null, (_, _) => EditApiKey()));
        root.DropDownItems.Add(new ToolStripMenuItem("設定模型…", null, (_, _) => EditModel()));
        root.DropDownItems.Add(new ToolStripMenuItem("自訂 Base URL…", null, (_, _) => EditBaseUrl()));

        root.DropDownItems.Add(new ToolStripSeparator());
        _aiStatusItem = new ToolStripMenuItem(string.Empty) { Enabled = false };
        root.DropDownItems.Add(_aiStatusItem);
        UpdateAiStatus();

        return root;
    }

    /// <summary>雲端預設:設端點+模型,清除舊同意,接著要求輸入金鑰。</summary>
    private void ApplyCloudPreset(string baseUrl, string model)
    {
        _settings.Tier2BaseUrl = baseUrl;
        _settings.Tier2Model = model;
        _settings.Tier2CloudConsented = false; // 換端點需重新確認外傳同意
        _settings.Save();
        UpdateAiStatus();
        EditApiKey();
    }

    /// <summary>本地 Ollama 防呆:偵測服務與模型,沒有就引導 pull 或改用已安裝模型。</summary>
    private async Task BindLocalOllamaAsync()
    {
        var models = await ProbeOllamaModelsAsync();
        if (models is null)
        {
            MessageBox.Show(
                "偵測不到本地 Ollama(http://localhost:11434)。\n請先啟動 Ollama 後再試一次。",
                "Ollama 未啟動", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? chosen;
        if (models.Contains(DefaultLocalModel))
        {
            chosen = DefaultLocalModel;
        }
        else
        {
            // 沒有預設模型:問是否拉取,否則讓使用者從已安裝(優先視覺類)中挑
            var pull = MessageBox.Show(
                $"未偵測到視覺模型「{DefaultLocalModel}」。\n\n" +
                "按「是」顯示 pull 指令;按「否」改用已安裝的其他模型。",
                "本地模型", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (pull == DialogResult.Cancel)
            {
                return;
            }

            if (pull == DialogResult.Yes)
            {
                MessageBox.Show(
                    "請在終端機執行(僅 1.5GB):\n\n    ollama pull maternion/LightOnOCR-2\n\n完成後再回來選「本地 Ollama」。",
                    "拉取模型", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var vision = models
                .Where(m => Regex.IsMatch(m, "ocr|vl|vision|llava|minicpm|moondream|gemma", RegexOptions.IgnoreCase))
                .ToList();
            string list = models.Count == 0 ? "(無)" : string.Join("\n - ", models);
            string? pick = TextInputDialog.Show("選擇本地模型",
                "已安裝模型:\n - " + list + "\n\n輸入要使用的模型名稱:",
                vision.FirstOrDefault() ?? models.FirstOrDefault());

            if (string.IsNullOrWhiteSpace(pick))
            {
                return;
            }

            chosen = pick.Trim();
            if (!models.Contains(chosen) &&
                MessageBox.Show($"清單中沒有「{chosen}」,仍要使用嗎?",
                    "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
        }

        _settings.Tier2BaseUrl = OllamaBaseUrl;
        _settings.Tier2Model = chosen;
        _settings.Tier2ApiKey = null;       // 本地不需金鑰
        _settings.Tier2CloudConsented = false;
        _settings.Save();
        UpdateAiStatus();
        MessageBox.Show($"已設定本地離線辨識:{chosen}", "本地 Ollama",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>查詢本地 Ollama 已安裝模型;服務不可達回 null。</summary>
    internal static async Task<List<string>?> ProbeOllamaModelsAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            string json = await http.GetStringAsync("http://localhost:11434/api/tags");
            using var doc = JsonDocument.Parse(json);

            var names = new List<string>();
            foreach (var m in doc.RootElement.GetProperty("models").EnumerateArray())
            {
                if (m.TryGetProperty("name", out var n) && n.GetString() is { } s)
                {
                    names.Add(s);
                }
            }

            return names;
        }
        catch
        {
            return null;
        }
    }

    private void EditApiKey()
    {
        string? key = TextInputDialog.Show("設定 API 金鑰",
            "貼上 API 金鑰(本地 Ollama 可留空)。僅存於本機設定檔。",
            _settings.Tier2ApiKey, mask: true);
        if (key is null)
        {
            return;
        }

        _settings.Tier2ApiKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        _settings.Save();
        UpdateAiStatus();
    }

    private void EditModel()
    {
        string? model = TextInputDialog.Show("設定模型",
            "模型名稱(如 gemini-3.1-flash-lite、qwen2.5vl)。", _settings.Tier2Model);
        if (model is null || string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        _settings.Tier2Model = model.Trim();
        _settings.Save();
        UpdateAiStatus();
    }

    private void EditBaseUrl()
    {
        string? url = TextInputDialog.Show("自訂 Base URL",
            "OpenAI 相容端點(到 /v1 為止,不含 /chat/completions)。", _settings.Tier2BaseUrl);
        if (url is null || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        _settings.Tier2BaseUrl = url.Trim();
        _settings.Tier2CloudConsented = false;
        _settings.Save();
        UpdateAiStatus();
    }

    private void UpdateAiStatus()
    {
        bool configured = _settings.IsTier2Configured;
        bool on = configured && _settings.Tier2Enabled;
        string route = configured
            ? (IsLocalEndpoint(_settings.Tier2BaseUrl!) ? "本地" : "雲端")
            : "—";

        _aiStatusItem.Text = configured
            ? $"狀態:{(on ? "● 開啟" : "○ 關閉")} · {route} · {_settings.Tier2Model}"
            : "狀態:未設定";

        // 端點預設打勾(radio 樣式):反映目前選的是哪條路線
        foreach (ToolStripItem item in _presetsRoot.DropDownItems)
        {
            if (item is ToolStripMenuItem mi && mi.Tag is string url)
            {
                mi.Checked = configured &&
                    string.Equals(url, _settings.Tier2BaseUrl, StringComparison.OrdinalIgnoreCase);
            }
        }

        // 托盤提示文字反映目前是否 AI 增強模式與路線(_trayIcon 在建構子稍後才建立)
        if (_trayIcon is not null)
        {
            _trayIcon.Text = on
                ? $"FlashGrab — AI 增強:{route}(框選按住 Shift)"
                : "FlashGrab — 螢幕智慧取字 (Win+Shift+C)";
        }
    }

    /// <summary>建立 Tier 2 引擎;未設定回 null。</summary>
    private IOcrEngine? CreateAiEngine()
    {
        if (!_settings.IsTier2Configured)
        {
            return null;
        }

        return new OpenAiCompatibleVisionOcr(
            _settings.Tier2BaseUrl!, _settings.Tier2ApiKey, _settings.Tier2Model!);
    }

    private static bool IsLocalEndpoint(string baseUrl)
    {
        return baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("127.0.0.1", StringComparison.Ordinal)
            || baseUrl.Contains("0.0.0.0", StringComparison.Ordinal);
    }

    /// <summary>雲端端點首次使用前的一次性外傳告知;同意後記住。回傳是否可繼續。</summary>
    private bool EnsureCloudConsent()
    {
        if (_settings.Tier2CloudConsented || IsLocalEndpoint(_settings.Tier2BaseUrl!))
        {
            return true;
        }

        var result = MessageBox.Show(
            "AI 增強(雲端)會把這次的截圖傳送到外部服務進行辨識。\n\n" +
            "若需完全離線/私密,請改用「本地 Ollama」端點。\n\n要繼續使用雲端嗎?",
            "雲端外傳告知", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

        if (result == DialogResult.OK)
        {
            _settings.Tier2CloudConsented = true;
            _settings.Save();
            return true;
        }

        return false;
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        // 防止遮罩開啟時再次觸發快捷鍵造成多層遮罩
        if (_capturing)
        {
            return;
        }

        _capturing = true;
        try
        {
            // 定格:按鍵瞬間凍結整個畫面,後續從快照裁切(與框選耗時無關)
            using var snapshot = ScreenGrabber.CaptureVirtualScreen();

            bool aiAvailable = _settings.Tier2Enabled && _settings.IsTier2Configured;

            Rectangle region;
            bool useAi;
            using (var overlay = new OverlayForm(snapshot, aiAvailable))
            {
                if (overlay.ShowDialog() != DialogResult.OK || overlay.SelectedRegion is not { } selected)
                {
                    return;
                }

                region = selected;
                useAi = overlay.UseAiRequested;
                overlay.BackgroundImage = null; // 快照由本方法持有/釋放,避免 Form 連帶處理
            }

            // 雲端外傳一次性告知:不同意則本次取消(不退回 Tier 0,以免誤把該保密的內容外傳)
            if (useAi && !EnsureCloudConsent())
            {
                return;
            }

            IOcrEngine engine = useAi ? CreateAiEngine() ?? _ocr : _ocr;
            bool usedAi = useAi && !ReferenceEquals(engine, _ocr);

            // AI 路徑通常數秒,先給即時回饋(避免使用者以為沒反應)
            if (usedAi)
            {
                _trayIcon.Text = "FlashGrab — AI 辨識中…";
                _trayIcon.ShowBalloonTip(1500, "FlashGrab", "AI 辨識中…(雲端可能數秒)", ToolTipIcon.Info);
            }

            using var bitmap = ScreenGrabber.Crop(snapshot, region);
            var document = await engine.RecognizeAsync(bitmap);
            string text = _pipeline.Run(document, _settings.ReflowParagraphs);

            if (string.IsNullOrWhiteSpace(text))
            {
                _trayIcon.ShowBalloonTip(1500, "FlashGrab", "未辨識到文字。", ToolTipIcon.Info);
                return;
            }

            ClipboardWriter.Write(text);
            SystemSounds.Asterisk.Play();
            string tag = usedAi ? " (AI)" : string.Empty;
            _trayIcon.ShowBalloonTip(
                1200, "FlashGrab", $"已複製 {text.Length} 個字元到剪貼簿。{tag}", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(3000, "FlashGrab", $"取字失敗:{ex.Message}", ToolTipIcon.Error);
        }
        finally
        {
            _capturing = false;
            UpdateAiStatus(); // 還原托盤文字(辨識中 → 一般狀態)
        }
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "FlashGrab v0.4.1(Phase 4 選配 AI 增強)\n\n" +
            "一鍵喚醒 Windows 原生 OCR,將螢幕上的文字與程式碼\n化為剪貼簿裡乾淨的結構化資料。\n\n" +
            "快捷鍵:Win + Shift + C\n" +
            "AI 增強(選配):框選時按住 Shift,改用視覺模型\n(本地 Ollama 離線,或免費/付費雲端)。",
            "關於 FlashGrab",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkey.Dispose();
            _trayIcon.Dispose();
            _showSignal.Dispose(); // 監聽執行緒為背景緒,程序結束時隨之終止
        }

        base.Dispose(disposing);
    }
}
