using System.Drawing;
using FlashGrab.Ocr;
using Windows.Globalization;

namespace FlashGrab.App;

/// <summary>
/// 正規設定視窗(沿用歡迎視窗的版型/字體/留白),取代原本「彈出原生托盤選單」的入口。
/// 以 TableLayoutPanel + AutoSize 排版:每列依實際字高自動撐開、欄寬自動對齊,
/// 避免固定像素座標在不同 DPI / CJK 字高下發生文字遮擋或區塊重疊。
/// </summary>
internal static class SettingsForm
{
    private static readonly Color HintColor = Color.FromArgb(110, 110, 110);
    private static readonly Color LineColor = Color.Gainsboro;
    private static readonly Font TitleFont = new("Segoe UI", 14F, FontStyle.Bold);
    private static readonly Font SectionFont = new("Segoe UI", 10.5F, FontStyle.Bold);
    private static readonly Font BodyFont = new("Segoe UI", 9.75F);

    private sealed record LangItem(string? Tag, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record PresetItem(string Display, string? BaseUrl, string? Model)
    {
        public override string ToString() => Display;
    }

    public static void Show(Settings settings, IReadOnlyList<Language> languages, Icon? icon, Action onApplied)
    {
        using var form = new Form
        {
            Text = "FlashGrab 設定",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = true,
            TopMost = true,
            AutoScaleMode = AutoScaleMode.Font,
            Font = BodyFont,
        };
        if (icon is not null)
        {
            form.Icon = icon;
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(22, 18, 22, 14),
            Width = 524,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 384));

        int r = 0;

        void AddFull(Control c, Padding margin)
        {
            c.Margin = margin;
            root.Controls.Add(c, 0, r);
            root.SetColumnSpan(c, 2);
            r++;
        }

        void AddRow(string labelText, Control field)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 10, 6),
            };
            field.Margin = new Padding(0, 4, 0, 4);
            field.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            root.Controls.Add(label, 0, r);
            root.Controls.Add(field, 1, r);
            r++;
        }

        Panel Line() => new() { Height = 1, Dock = DockStyle.Fill, BackColor = LineColor, Margin = new Padding(0, 12, 0, 12) };

        Label Header(string text) => new() { Text = text, Font = SectionFont, AutoSize = true, Margin = new Padding(0, 12, 0, 6) };

        // ── 標題 ──
        AddFull(new Label { Text = "FlashGrab 設定", Font = TitleFont, AutoSize = true }, new Padding(0, 0, 0, 2));
        AddFull(new Label { Text = "右鍵系統匣圖示也可隨時開啟此頁。", AutoSize = true, ForeColor = HintColor }, new Padding(0, 0, 0, 6));

        // ── 一般 ──
        AddFull(Header("一般"), new Padding(0, 6, 0, 6));
        var reflow = new CheckBox
        {
            Text = "段落重排(把視覺軟換行併成段落,抄文章用)",
            AutoSize = true,
        };
        reflow.Checked = settings.ReflowParagraphs;
        AddFull(reflow, new Padding(0, 2, 0, 2));

        var langCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        langCombo.Items.Add(new LangItem(null, "自動(系統語言)"));
        foreach (var lang in languages)
        {
            langCombo.Items.Add(new LangItem(lang.LanguageTag, lang.DisplayName));
        }
        langCombo.SelectedIndex = 0;
        for (int i = 1; i < langCombo.Items.Count; i++)
        {
            if (((LangItem)langCombo.Items[i]!).Tag is { } t &&
                string.Equals(t, settings.LanguageTag, StringComparison.OrdinalIgnoreCase))
            {
                langCombo.SelectedIndex = i;
                break;
            }
        }
        AddRow("辨識語言", langCombo);

        AddFull(Line(), new Padding(0, 0, 0, 0));

        // ── AI 增強 (Tier 2) ──
        AddFull(Header("AI 增強 (Tier 2) · 選配"), new Padding(0, 4, 0, 6));
        var aiEnable = new CheckBox
        {
            Text = "啟用(框選放開時按住 Shift 才改用 AI)",
            AutoSize = true,
            Checked = settings.Tier2Enabled,
        };
        AddFull(aiEnable, new Padding(0, 2, 0, 4));

        var srcCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        var presets = new[]
        {
            new PresetItem("本地 Ollama(離線、私密)", TrayApplicationContext.OllamaBaseUrl, TrayApplicationContext.DefaultLocalModel),
            new PresetItem("Google Gemini(雲端)", TrayApplicationContext.GeminiBaseUrl, "gemini-3.1-flash-lite"),
            new PresetItem("NVIDIA NIM(雲端)", TrayApplicationContext.NimBaseUrl, "meta/llama-3.2-90b-vision-instruct"),
            new PresetItem("自訂…", null, null),
        };
        foreach (var p in presets)
        {
            srcCombo.Items.Add(p);
        }
        AddRow("服務來源", srcCombo);

        var urlBox = new TextBox { Text = settings.Tier2BaseUrl ?? string.Empty };
        AddRow("Base URL", urlBox);

        // 模型列:文字框 + 「偵測」按鈕(本地用),以子 Panel 容納兩者
        var modelBox = new TextBox();
        var detectBtn = new Button { Text = "偵測…", Width = 88, Dock = DockStyle.Right, Margin = new Padding(8, 0, 0, 0) };
        var modelHost = new Panel { Height = 27, Margin = new Padding(0, 4, 0, 4), Anchor = AnchorStyles.Left | AnchorStyles.Right };
        modelBox.Dock = DockStyle.Fill;
        modelBox.Text = settings.Tier2Model ?? string.Empty;
        modelHost.Controls.Add(modelBox);   // Fill 先加 → 佔滿剩餘
        modelHost.Controls.Add(detectBtn);  // Right 後加 → 靠右
        modelBox.BringToFront();
        AddRow("模型", modelHost);

        var keyBox = new TextBox { UseSystemPasswordChar = true, Text = settings.Tier2ApiKey ?? string.Empty };
        AddRow("API 金鑰", keyBox);

        var status = new Label { AutoSize = true, ForeColor = HintColor };
        AddFull(status, new Padding(0, 8, 0, 2));

        // ── 行為 ──
        void RefreshLocalState()
        {
            bool local = IsLocal(urlBox.Text);
            keyBox.Enabled = !local;
            detectBtn.Enabled = local;
            status.Text = string.IsNullOrWhiteSpace(urlBox.Text) || string.IsNullOrWhiteSpace(modelBox.Text)
                ? "狀態:尚未設定端點(選一個服務來源並填模型)。"
                : $"狀態:{(local ? "本地離線" : "雲端")} · {modelBox.Text.Trim()}";
        }

        int presetIndex = presets.Length - 1;
        for (int i = 0; i < presets.Length - 1; i++)
        {
            if (string.Equals(presets[i].BaseUrl, settings.Tier2BaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                presetIndex = i;
                break;
            }
        }
        srcCombo.SelectedIndex = presetIndex;

        srcCombo.SelectedIndexChanged += (_, _) =>
        {
            if (srcCombo.SelectedItem is PresetItem { BaseUrl: { } url } p)
            {
                urlBox.Text = url;
                if (p.Model is { } m)
                {
                    modelBox.Text = m;
                }
            }
            RefreshLocalState();
        };
        urlBox.TextChanged += (_, _) => RefreshLocalState();
        modelBox.TextChanged += (_, _) => RefreshLocalState();

        detectBtn.Click += async (_, _) =>
        {
            detectBtn.Enabled = false;
            string prev = detectBtn.Text;
            detectBtn.Text = "偵測中…";
            try
            {
                var models = await TrayApplicationContext.ProbeOllamaModelsAsync();
                if (models is null)
                {
                    MessageBox.Show(form,
                        "偵測不到本地 Ollama(http://localhost:11434)。\n請先啟動 Ollama 後再試。",
                        "Ollama 未啟動", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (models.Contains(TrayApplicationContext.DefaultLocalModel))
                {
                    modelBox.Text = TrayApplicationContext.DefaultLocalModel;
                }
                else if (models.Count > 0)
                {
                    string list = string.Join("\n - ", models);
                    string? pick = TextInputDialog.Show("選擇本地模型",
                        "已安裝模型:\n - " + list + "\n\n輸入要使用的模型名稱:", models[0]);
                    if (!string.IsNullOrWhiteSpace(pick))
                    {
                        modelBox.Text = pick.Trim();
                    }
                }
                else
                {
                    MessageBox.Show(form,
                        "Ollama 已啟動,但尚未安裝任何模型。\n可執行: ollama pull maternion/LightOnOCR-2",
                        "無已安裝模型", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            finally
            {
                detectBtn.Text = prev;
                RefreshLocalState();
            }
        };

        RefreshLocalState();

        AddFull(Line(), new Padding(0, 0, 0, 0));

        // ── 底部按鈕 ──
        var save = new Button { Text = "儲存並關閉", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
        var cancel = new Button { Text = "取消", AutoSize = true, Padding = new Padding(14, 4, 14, 4), DialogResult = DialogResult.Cancel, Margin = new Padding(8, 0, 0, 0) };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 14, 0, 0),
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        AddFull(buttons, new Padding(0, 6, 0, 0));

        save.Click += (_, _) =>
        {
            string? newUrl = Trimmed(urlBox.Text);
            string? newModel = Trimmed(modelBox.Text);
            bool configured = newUrl is not null && newModel is not null;

            if (aiEnable.Checked && !configured)
            {
                MessageBox.Show(form,
                    "已勾選啟用 AI,但尚未填妥 Base URL 與模型。\n請補齊,或取消勾選後再儲存。",
                    "AI 設定未完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!string.Equals(newUrl, settings.Tier2BaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                settings.Tier2CloudConsented = false; // 換端點需重新確認雲端外傳同意
            }

            settings.ReflowParagraphs = reflow.Checked;
            settings.LanguageTag = ((LangItem)langCombo.SelectedItem!).Tag;
            settings.Tier2BaseUrl = newUrl;
            settings.Tier2Model = newModel;
            settings.Tier2ApiKey = IsLocal(urlBox.Text) ? null : Trimmed(keyBox.Text);
            settings.Tier2Enabled = aiEnable.Checked && configured;
            settings.Save();

            onApplied();
            form.DialogResult = DialogResult.OK;
            form.Close();
        };

        form.Controls.Add(root);
        form.AcceptButton = save;
        form.CancelButton = cancel;
        form.ClientSize = new Size(524, root.PreferredSize.Height);
        form.ShowDialog();
    }

    private static string? Trimmed(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool IsLocal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || url.Contains("127.0.0.1", StringComparison.Ordinal)
            || url.Contains("0.0.0.0", StringComparison.Ordinal);
    }
}
