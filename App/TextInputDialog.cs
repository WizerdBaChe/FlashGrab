using System.Drawing;

namespace FlashGrab.App;

/// <summary>極簡單行輸入對話框(WinForms 無內建 InputBox)。用於設定 Tier 2 端點/金鑰/模型。</summary>
internal static class TextInputDialog
{
    /// <param name="mask">true 時遮蔽輸入(API 金鑰用)。</param>
    /// <returns>確定回傳輸入字串(可空字串);取消回傳 null。</returns>
    public static string? Show(string title, string prompt, string? initial = null, bool mask = false)
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            ClientSize = new Size(440, 140),
        };

        var label = new Label { Left = 12, Top = 12, Width = 416, Height = 40, Text = prompt };
        var box = new TextBox { Left = 12, Top = 60, Width = 416, Text = initial ?? string.Empty };
        if (mask)
        {
            box.UseSystemPasswordChar = true;
        }

        var ok = new Button { Text = "確定", DialogResult = DialogResult.OK, Left = 272, Width = 75, Top = 100 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 353, Width = 75, Top = 100 };

        form.Controls.AddRange(new Control[] { label, box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog() == DialogResult.OK ? box.Text : null;
    }
}
