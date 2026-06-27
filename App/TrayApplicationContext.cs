using System.Drawing;
using FlashGrab.Trigger;

namespace FlashGrab.App;

/// <summary>
/// 無主視窗的常駐入口:系統匣圖示 + 全域快捷鍵。
/// Phase 0 僅驗證喚醒層;Phase 1 將以透明遮罩 + OCR 取代占位行為。
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly GlobalHotkey _hotkey;

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

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        // Phase 0 占位:證明喚醒層運作。Phase 1 將開啟透明遮罩進行框選 + OCR。
        _trayIcon.ShowBalloonTip(
            1500, "FlashGrab",
            "已喚醒(Phase 0 占位)。擷取與 OCR 將於 Phase 1 接入。",
            ToolTipIcon.Info);
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "FlashGrab v0.1(Phase 0 骨架)\n\n" +
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
