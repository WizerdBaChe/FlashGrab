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

    private Point _start;
    private Rectangle _selection;
    private bool _selecting;

    /// <summary>框選結果(相對快照左上角的像素矩形)。未選取時為 null。</summary>
    public Rectangle? SelectedRegion { get; private set; }

    public OverlayForm(Bitmap snapshot)
    {
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

        base.OnKeyDown(e);
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

            using var pen = new Pen(Color.DeepSkyBlue, 2);
            e.Graphics.DrawRectangle(pen, s);
        }
        else
        {
            // 尚未框選:整個畫面均勻變暗
            e.Graphics.FillRectangle(veil, client);
        }
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
