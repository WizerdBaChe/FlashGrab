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

    // 輪盤:拖曳中按一下 Ctrl 叫出;選取只看方向。
    private bool _wheelOpen;
    private Point _wheelCenter;
    private int _wheelRadius;
    private int _wheelHover = -1;
    private string _wheelStatus = "";
    private bool _ctrlHeld;

    /// <summary>框選結果(相對快照左上角的像素矩形)。未選取時為 null。</summary>
    public Rectangle? SelectedRegion { get; private set; }

    /// <summary>在輪盤上選定的動作;null = 沒用輪盤(或在中心放開),走原本流程。</summary>
    public WheelAction? ChosenAction { get; private set; }

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
        if (e.Button == MouseButtons.Left && _wheelOpen)
        {
            // 輪盤開著時按下只是準備點選扇區,不重開框選。
        }
        else if (e.Button == MouseButtons.Left)
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

        if (_wheelOpen)
        {
            // 輪盤開著時框選凍結,滑鼠只用來指方向。
            UpdateWheelHover(e.Location);
        }
        else if (_selecting)
        {
            _selection = Normalize(_start, e.Location);
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _wheelOpen)
        {
            _selecting = false;
            UpdateWheelHover(e.Location);
            if (_wheelHover >= 0)
            {
                var slot = RadialWheel.Slots[_wheelHover];
                if (slot.Action is not { } action)
                {
                    base.OnMouseUp(e);
                    return; // 空格不是選項:輪盤留著,可再點一次
                }

                if (UnavailableReason(action) is string reason)
                {
                    _wheelStatus = reason;
                    Invalidate();
                    System.Media.SystemSounds.Beep.Play();
                    base.OnMouseUp(e);
                    return; // 不能用:說明原因,輪盤留著讓使用者改選
                }

                ChosenAction = action;
            }

            // 中心放開 = 不選輪盤,照原本規則(含放開時按住 Shift = AI)處理。
            SelectedRegion = _selection;
            UseAiRequested = ChosenAction is null && _aiAvailable && (ModifierKeys & Keys.Shift) == Keys.Shift;
            DialogResult = DialogResult.OK;
            Close();
            base.OnMouseUp(e);
            return;
        }

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
        else if (e.KeyCode == Keys.ControlKey && !_ctrlHeld)
        {
            _ctrlHeld = true; // 按住不放時鍵盤會連發 KeyDown,只認第一下
            // 按一下 Ctrl:拖曳中且已有像樣的選區 → 開輪盤;輪盤開著 → 收回(回到框選)。
            if (_wheelOpen)
            {
                CloseWheel();
            }
            else if (_selecting && _selection.Width > 3 && _selection.Height > 3)
            {
                OpenWheel(PointToClient(Cursor.Position));
            }

            e.Handled = true;
        }

        RefreshArmed();
        base.OnKeyDown(e);
    }

    private void OpenWheel(Point cursor)
    {
        float scale = DeviceDpi / 96f;
        var screen = RectangleToClient(Screen.FromPoint(PointToScreen(cursor)).Bounds);
        _wheelRadius = (int)Math.Min(170 * scale, Math.Min(screen.Width, screen.Height) * 0.36);
        _wheelCenter = RadialWheel.Place(cursor, screen, _wheelRadius);
        _wheelOpen = true;
        _wheelStatus = "";
        UpdateWheelHover(cursor);
        Invalidate();
    }

    private void CloseWheel()
    {
        _wheelOpen = false;
        _wheelHover = -1;
        _wheelStatus = "";
        Invalidate();
    }

    private void UpdateWheelHover(Point p)
    {
        int next = RadialWheel.SectorAt(p.X - _wheelCenter.X, p.Y - _wheelCenter.Y, _wheelRadius * RadialWheel.DeadZoneRatio);
        if (next != _wheelHover)
        {
            _wheelHover = next;
            _wheelStatus = "";
            Invalidate();
        }
    }

    /// <summary>目前不能用的動作回傳原因(給使用者看);可用回 null。</summary>
    private string? UnavailableReason(WheelAction action) =>
        action == WheelAction.AiText && !_aiAvailable ? "AI 增強尚未設定:到托盤選單「設定」開啟" : null;

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.ControlKey)
        {
            _ctrlHeld = false;
        }

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

        if (_wheelOpen)
        {
            DrawWheel(e.Graphics);
        }

        DrawHint(e.Graphics, client);
    }

    /// <summary>
    /// 輪盤:淺色半透明扇區,懸停格用框線色強調;中心寫懸停格的名稱與說明。
    /// </summary>
    private void DrawWheel(Graphics g)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        float scale = DeviceDpi / 96f;
        var c = new PointF(_wheelCenter.X, _wheelCenter.Y);
        float outer = _wheelRadius, inner = (float)(_wheelRadius * RadialWheel.DeadZoneRatio);
        float sweep = 360f / RadialWheel.SectorCount;

        using var idle = new SolidBrush(Color.FromArgb(225, 248, 248, 246));
        using var empty = new SolidBrush(Color.FromArgb(120, 230, 230, 228));
        using var disabled = new SolidBrush(Color.FromArgb(200, 225, 225, 222));
        using var hover = new SolidBrush(Color.FromArgb(240, 214, 238, 252));
        using var divider = new Pen(Color.FromArgb(160, 120, 120, 120), 1f);
        using var accent = new Pen(Color.DeepSkyBlue, 4f * scale);
        using var labelFont = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold);
        using var centerFont = new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold);
        using var detailFont = new Font("Microsoft JhengHei UI", 9f);
        using var ink = new SolidBrush(Color.FromArgb(34, 34, 34));
        using var mute = new SolidBrush(Color.FromArgb(130, 130, 130));
        var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < RadialWheel.SectorCount; i++)
        {
            var slot = RadialWheel.Slots[i];
            bool isHover = i == _wheelHover && slot.Action is not null;
            bool usable = slot.Action is { } a && UnavailableReason(a) is null;
            // GDI+ 角度 0 = 右方、順時針;扇區 0 = 上方,所以 -90。
            float start = -90f + i * sweep - sweep / 2f;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(c.X - outer, c.Y - outer, outer * 2, outer * 2, start, sweep);
            path.AddArc(c.X - inner, c.Y - inner, inner * 2, inner * 2, start + sweep, -sweep);
            path.CloseFigure();
            g.FillPath(slot.Action is null ? empty : isHover ? hover : usable ? idle : disabled, path);
            g.DrawPath(divider, path);
            if (isHover)
            {
                g.DrawArc(accent, c.X - outer + accent.Width / 2, c.Y - outer + accent.Width / 2,
                    outer * 2 - accent.Width, outer * 2 - accent.Width, start, sweep);
            }

            if (slot.Action is not null)
            {
                var at = RadialWheel.Polar(c, (inner + outer) / 2, RadialWheel.SectorAngle(i));
                g.DrawString(slot.Label, labelFont, usable ? ink : mute, at, centered);
            }
        }

        // 中心:懸停格說明,或操作提示。
        using var hub = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
        g.FillEllipse(hub, c.X - inner + 3, c.Y - inner + 3, (inner - 3) * 2, (inner - 3) * 2);
        var box = new RectangleF(c.X - inner * 0.9f, c.Y - inner * 0.75f, inner * 1.8f, inner * 1.5f);
        if (_wheelHover >= 0 && RadialWheel.Slots[_wheelHover] is { Action: { } act } s)
        {
            string detail = _wheelStatus.Length > 0 ? _wheelStatus : UnavailableReason(act) ?? s.Detail;
            g.DrawString(s.Label, centerFont, ink, new RectangleF(box.X, box.Y, box.Width, box.Height * 0.45f), centered);
            using var warn = new SolidBrush(Color.Firebrick);
            g.DrawString(detail, detailFont, _wheelStatus.Length > 0 || UnavailableReason(act) is not null ? warn : mute,
                new RectangleF(box.X, box.Y + box.Height * 0.42f, box.Width, box.Height * 0.58f), centered);
        }
        else
        {
            g.DrawString("朝方向移動\n放開即選\n中心放開 = 照常複製", detailFont, mute, box, centered);
        }
    }

    /// <summary>頂端置中提示:輪盤用法;AI 可用時另提示 Shift,按住 Shift 時切換為「AI 待命」橘字。</summary>
    private void DrawHint(Graphics g, Rectangle client)
    {
        string hint = _wheelOpen
            ? "輪盤:朝格子方向移動後放開 · 再按 Ctrl 收回 · Esc 取消"
            : _aiArmed
                ? "● AI 增強模式 — 放開即用 AI 辨識"
                : _aiAvailable
                    ? "拖曳中按一下 Ctrl = 輪盤 · 按住 Shift 放開 = AI 增強"
                    : "拖曳中按一下 Ctrl = 輪盤(更多輸出方式)";
        Color fg = _aiArmed && !_wheelOpen ? Color.Orange : Color.White;

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
        ChosenAction = null;
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
