using System.Drawing;

namespace FlashGrab.Capture;

/// <summary>
/// 擷取層 UI:覆蓋整個 virtual desktop 的 topmost 遮罩,背景是按鍵瞬間的凍結快照。
/// 十字準心拖曳橡皮筋框選;選區外變暗、選區維持原亮度。
/// 放開回傳「快照座標」的矩形(因遮罩 Bounds = VirtualScreen,client 座標即快照像素)。
/// Esc / 右鍵取消本次框選。
/// </summary>
internal sealed class OverlayForm : Form
{
    private static readonly Color VeilColor = Color.FromArgb(110, 0, 0, 0);

    private readonly bool _aiAvailable;

    private Point _start;
    private Rectangle _selection;
    private bool _selecting;
    private bool _aiArmed; // 目前是否按住 Shift(AI 待命),用於即時視覺回饋

    /// <summary>框選結果(相對快照左上角的像素矩形)。未選取時為 null。</summary>
    public Rectangle? SelectedRegion { get; private set; }

    /// <summary>本次框選是否要求走 Tier 2 AI(僅當 AI 可用且放開時按住 Shift)。</summary>
    public bool UseAiRequested { get; private set; }

    /// <param name="aiAvailable">Tier 2 是否已啟用且設妥;true 時顯示「Shift = AI」提示並接受該修鍵。</param>
    public OverlayForm(Bitmap snapshot, bool aiAvailable = false)
    {
        _aiAvailable = aiAvailable;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;

        // 凍結快照當背景由框架繪製(1:1、左上對齊),OnPaint 只疊變暗與邊框
        BackgroundImage = snapshot;
        BackgroundImageLayout = ImageLayout.None;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _selecting = true;
            _start = e.Location;
            _selection = new Rectangle(e.Location, Size.Empty);
        }
        else if (e.Button == MouseButtons.Right)
        {
            CancelAndClose();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        RefreshArmed();

        if (_selecting)
        {
            _selection = Normalize(_start, e.Location);
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _selecting)
        {
            _selecting = false;

            if (_selection.Width > 3 && _selection.Height > 3)
            {
                SelectedRegion = _selection;
                // 放開瞬間判定:AI 可用且按住 Shift → 本次走 Tier 2。
                UseAiRequested = _aiAvailable && (ModifierKeys & Keys.Shift) == Keys.Shift;
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }

            Close();
        }

        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CancelAndClose();
        }

        RefreshArmed();
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        RefreshArmed();
        base.OnKeyUp(e);
    }

    /// <summary>依目前 Shift 狀態更新 AI 待命旗標,有變才重繪。</summary>
    private void RefreshArmed()
    {
        bool armed = _aiAvailable && (ModifierKeys & Keys.Shift) == Keys.Shift;
        if (armed != _aiArmed)
        {
            _aiArmed = armed;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var client = ClientRectangle;
        using var veil = new SolidBrush(VeilColor);

        if (_selection.Width > 0 && _selection.Height > 0)
        {
            // 選區外四塊變暗,選區維持原亮度
            var s = _selection;
            e.Graphics.FillRectangle(veil, 0, 0, client.Width, s.Top);
            e.Graphics.FillRectangle(veil, 0, s.Bottom, client.Width, client.Height - s.Bottom);
            e.Graphics.FillRectangle(veil, 0, s.Top, s.Left, s.Height);
            e.Graphics.FillRectangle(veil, s.Right, s.Top, client.Width - s.Right, s.Height);

            using var pen = new Pen(_aiArmed ? Color.Orange : Color.DeepSkyBlue, 2);
            e.Graphics.DrawRectangle(pen, s);
        }
        else
        {
            // 尚未框選:整個畫面均勻變暗
            e.Graphics.FillRectangle(veil, client);
        }

        if (_aiAvailable)
        {
            DrawAiHint(e.Graphics, client);
        }
    }

    /// <summary>AI 可用時於頂端置中提示修鍵用法;按住 Shift 時即時切換為「AI 待命」橘字。</summary>
    private void DrawAiHint(Graphics g, Rectangle client)
    {
        string hint = _aiArmed
            ? "● AI 增強模式 — 放開即用 AI 辨識"
            : "按住 Shift 放開 = AI 增強(否則用內建 OCR)";
        Color fg = _aiArmed ? Color.Orange : Color.White;

        using var font = new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold);
        var size = g.MeasureString(hint, font);
        float x = (client.Width - size.Width) / 2f;
        float y = 24f;

        using var back = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        g.FillRectangle(back, x - 12, y - 6, size.Width + 24, size.Height + 12);
        using var fore = new SolidBrush(fg);
        g.DrawString(hint, font, fore, x, y);
    }

    private void CancelAndClose()
    {
        SelectedRegion = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private static Rectangle Normalize(Point a, Point b)
    {
        return new Rectangle(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X),
            Math.Abs(a.Y - b.Y));
    }
}
