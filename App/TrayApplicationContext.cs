using System.Drawing;
using System.Media;
using FlashGrab.Capture;
using FlashGrab.Ocr;
using FlashGrab.Output;
using FlashGrab.Trigger;

namespace FlashGrab.App;

/// <summary>
/// 無主視窗的常駐入口:系統匣圖示 + 全域快捷鍵。
/// Phase 1 打通端到端:遮罩框選 → 截圖 → Tier 0 OCR → 剪貼簿 + 音效。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly GlobalHotkey _hotkey;
    private readonly IOcrEngine _ocr = new WindowsMediaOcr();
    private bool _capturing;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
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
            Rectangle region;
            using (var overlay = new OverlayForm())
            {
                if (overlay.ShowDialog() != DialogResult.OK || overlay.SelectedRegion is not { } selected)
                {
                    return;
                }

                region = selected;
            }

            // 等待遮罩完全消失並讓桌面重繪,再擷取下方畫面
            await Task.Delay(60);

            using var bitmap = ScreenGrabber.Capture(region);
            string text = await _ocr.RecognizeAsync(bitmap);

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
            "FlashGrab v0.2(Phase 1 取字 MVP)\n\n" +
            "一鍵喚醒 Windows 原生 OCR,將螢幕上的文字與程式碼\n化為剪貼簿裡乾淨的結構化資料。\n\n快捷鍵:Win + Shift + C",
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
