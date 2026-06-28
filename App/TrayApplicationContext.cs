using System.Drawing;
using System.Media;
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

    public TrayApplicationContext()
    {
        _ocr = new WindowsMediaOcr(_settings.LanguageTag);

        var menu = new ContextMenuStrip();

        var reflowItem = new ToolStripMenuItem("段落重排(抄文章用)")
        {
            CheckOnClick = true,
            Checked = _settings.ReflowParagraphs,
            ToolTipText = "開啟:把視覺軟換行併成段落。預設關閉=忠實保留畫面行序。",
        };
        reflowItem.CheckedChanged += (_, _) =>
        {
            _settings.ReflowParagraphs = reflowItem.Checked;
            _settings.Save();
        };
        menu.Items.Add(reflowItem);

        menu.Items.Add(BuildLanguageMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("關於 FlashGrab", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("結束", null, (_, _) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "FlashGrab — 螢幕智慧取字 (Win+Shift+C)",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _hotkey = new GlobalHotkey();
        _hotkey.HotkeyPressed += OnHotkeyPressed;

        if (!_hotkey.Register(ModifierKeys.Win | ModifierKeys.Shift, Keys.C))
        {
            _trayIcon.ShowBalloonTip(
                3000, "FlashGrab",
                "全域快捷鍵 Win+Shift+C 註冊失敗(可能已被其他程式佔用)。",
                ToolTipIcon.Warning);
        }
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

            Rectangle region;
            using (var overlay = new OverlayForm(snapshot))
            {
                if (overlay.ShowDialog() != DialogResult.OK || overlay.SelectedRegion is not { } selected)
                {
                    return;
                }

                region = selected;
                overlay.BackgroundImage = null; // 快照由本方法持有/釋放,避免 Form 連帶處理
            }

            using var bitmap = ScreenGrabber.Crop(snapshot, region);
            var document = await _ocr.RecognizeAsync(bitmap);
            string text = _pipeline.Run(document, _settings.ReflowParagraphs);

            if (string.IsNullOrWhiteSpace(text))
            {
                _trayIcon.ShowBalloonTip(1500, "FlashGrab", "未辨識到文字。", ToolTipIcon.Info);
                return;
            }

            ClipboardWriter.Write(text);
            SystemSounds.Asterisk.Play();
            _trayIcon.ShowBalloonTip(
                1200, "FlashGrab", $"已複製 {text.Length} 個字元到剪貼簿。", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(3000, "FlashGrab", $"取字失敗:{ex.Message}", ToolTipIcon.Error);
        }
        finally
        {
            _capturing = false;
        }
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "FlashGrab v0.3(Phase 2 智慧後處理)\n\n" +
            "一鍵喚醒 Windows 原生 OCR,將螢幕上的文字與程式碼\n化為剪貼簿裡乾淨的結構化資料。\n\n" +
            "快捷鍵:Win + Shift + C",
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
        }

        base.Dispose(disposing);
    }
}
