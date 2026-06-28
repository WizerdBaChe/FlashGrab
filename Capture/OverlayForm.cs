using System.Drawing;

namespace FlashGrab.Capture;

/// <summary>
/// 擷取層 UI:覆蓋整個 virtual desktop 的半透明 topmost 遮罩。
/// 十字準心拖曳橡皮筋框選;放開回傳螢幕座標矩形,Esc / 右鍵取消。
/// </summary>
internal sealed class OverlayForm : Form
{
    private Point _start;
    private Rectangle _selection;
    private bool _selecting;

    /// <summary>框選結果(螢幕物理像素)。未選取時為 null。</summary>
    public Rectangle? SelectedRegion { get; private set; }

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        BackColor = Color.Black;
        Opacity = 0.35;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;
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
            var client = Normalize(_start, e.Location);

            if (client.Width > 3 && client.Height > 3)
            {
                var origin = PointToScreen(client.Location);
                SelectedRegion = new Rectangle(origin, client.Size);
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

        if (_selection.Width > 0 && _selection.Height > 0)
        {
            // 選取區提亮,讓使用者看清楚框到的內容
            using var brush = new SolidBrush(Color.FromArgb(48, Color.White));
            e.Graphics.FillRectangle(brush, _selection);

            using var pen = new Pen(Color.DeepSkyBlue, 2);
            e.Graphics.DrawRectangle(pen, _selection);
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
