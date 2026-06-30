using System.Drawing;

namespace FlashGrab.App;

/// <summary>
/// 首次執行才出現一次的歡迎/說明視窗(以 Settings.WelcomeShown 控制)。
/// 之後每次啟動僅靠托盤 toast 提示,維持 G-Helper 級「無感融入」的輕量哲學。
/// </summary>
internal static class WelcomeForm
{
    /// <param name="appIcon">視窗圖示(取自托盤圖示);可為 null。</param>
    /// <param name="openSettings">按「開啟設定選單」時呼叫(由呼叫端彈出托盤右鍵選單)。</param>
    public static void Show(Icon? appIcon, Action openSettings)
    {
        using var form = new Form
        {
            Text = "歡迎使用 FlashGrab",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = true,
            TopMost = true,
            ClientSize = new Size(470, 268),
        };
        if (appIcon is not null)
        {
            form.Icon = appIcon;
        }

        var title = new Label
        {
            Left = 22,
            Top = 18,
            Width = 426,
            Height = 30,
            Text = "FlashGrab 已在背景執行 ⚡",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
        };

        var body = new Label
        {
            Left = 24,
            Top = 56,
            Width = 424,
            Height = 150,
            Text =
                "一鍵把螢幕上看得見的文字 / 程式碼,變成剪貼簿裡乾淨的文字。\n\n" +
                "‧ 取字快捷鍵: Win + Shift + C  → 框選一塊區域即可\n" +
                "‧ 程式常駐於系統匣(右下角),閒置不耗資源\n" +
                "‧ 右鍵托盤圖示可設定:辨識語言、段落重排、AI 增強(選配)\n" +
                "‧ AI 增強:框選放開的瞬間按住 Shift,改用視覺模型辨識\n\n" +
                "(此視窗只在第一次執行時出現一次)",
            Font = new Font("Segoe UI", 9.75F),
        };

        var settingsBtn = new Button
        {
            Text = "開啟設定選單…",
            Left = 24,
            Width = 150,
            Top = 220,
            Height = 32,
        };
        var okBtn = new Button
        {
            Text = "開始使用",
            Left = 358,
            Width = 90,
            Top = 220,
            Height = 32,
            DialogResult = DialogResult.OK,
        };

        settingsBtn.Click += (_, _) =>
        {
            form.Close();
            openSettings();
        };

        form.Controls.AddRange(new Control[] { title, body, settingsBtn, okBtn });
        form.AcceptButton = okBtn;
        form.ShowDialog();
    }
}
