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

    // 輪盤:拖曳中按一下 Ctrl 叫出;選取只看方向。狀態全在 WheelSession,這裡只轉發事件與繪圖。
    private readonly IReadOnlyList<WheelItem?> _loadout;
    private WheelSession? _wheel;
    private bool _ctrlHeld;

    private bool WheelOpen => _wheel is not null;

    /// <summary>框選結果(相對快照左上角的像素矩形)。未選取時為 null。</summary>
    public Rectangle? SelectedRegion { get; private set; }

    /// <summary>在輪盤上選定的動作;null = 沒用輪盤(或在中心放開),走原本流程。</summary>
    public WheelAction? ChosenAction { get; private set; }

    /// <summary>本次框選是否要求走 Tier 2 AI(僅當 AI 可用且放開時按住 Shift)。</summary>
    public bool UseAiRequested { get; private set; }

    /// <param name="aiAvailable">Tier 2 是否已啟用且設妥;true 時顯示「Shift = AI」提示並接受該修鍵。</param>
    /// <param name="loadout">輪盤 8 格的配置;null = 預設配置。</param>
    public OverlayForm(Bitmap snapshot, bool aiAvailable = false, IReadOnlyList<WheelItem?>? loadout = null)
    {
        _aiAvailable = aiAvailable;
        _loadout = loadout ?? RadialWheel.ResolveLoadout(null, out _);

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
        if (e.Button == MouseButtons.Left && WheelOpen)
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

        if (_wheel is not null)
        {
            // 輪盤開著時框選凍結,滑鼠只用來指方向(群組格往外甩會展開二級輪盤)。
            if (_wheel.Move(e.Location))
            {
                Invalidate();
            }
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
        if (e.Button == MouseButtons.Left && _wheel is not null)
        {
            _selecting = false;
            var release = _wheel.Release(e.Location);
            if (release.Outcome == WheelOutcome.Stay)
            {
                // 空格或不能用:輪盤留著,可再點一次;不能用時中心顯示原因。
                if (_wheel.Status.Length > 0)
                {
                    System.Media.SystemSounds.Beep.Play();
                }

                Invalidate();
                base.OnMouseUp(e);
                return;
            }

            ChosenAction = release.Action;

            // 第一層中心放開 = 不選輪盤,照原本規則(含放開時按住 Shift = AI)處理。
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
            // 按一下 Ctrl:拖曳中且已有像樣的選區 → 開輪盤;二級輪盤開著 → 退回第一層;
            // 第一層開著 → 收回(回到框選)。
            if (_wheel is not null)
            {
                if (!_wheel.Back())
                {
                    _wheel = null;
                }

                Invalidate();
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
        int radius = (int)Math.Min(170 * scale, Math.Min(screen.Width, screen.Height) * 0.36);
        _wheel = new WheelSession(_loadout, UnavailableReason);
        _wheel.Open(cursor, screen, radius);
        Invalidate();
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

        if (_wheel is not null)
        {
            DrawWheel(e.Graphics, _wheel);
        }

        DrawHint(e.Graphics, client);
    }

    /// <summary>
    /// 輪盤:第一層;二級輪盤展開時第一層淡化,子輪盤疊在越過外圈的位置。
    /// </summary>
    private void DrawWheel(Graphics g, WheelSession w)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        bool sub = w.Level == 2;
        DrawRing(g, w.Center, w.Radius, w.Slots, w.Hover, dim: sub, status: sub ? "" : w.Status,
            idleHint: "朝方向移動\n放開即選\n中心放開 = 照常複製", fontScale: 1f);
        if (sub && w.Group is { } group)
        {
            using var tether = new Pen(Color.FromArgb(150, 255, 255, 255), 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawLine(tether, w.Center, w.SubCenter);
            DrawRing(g, w.SubCenter, w.SubRadius, w.SubSlots, w.SubHover, dim: false, status: w.Status,
                idleHint: $"{group.Label}\n中心放開 =\n{group.Children![0].Label}", fontScale: 0.92f);
        }
    }

    private void DrawRing(Graphics g, Point center, int radius, IReadOnlyList<WheelItem?> slots, int hoverIndex,
        bool dim, string status, string idleHint, float fontScale)
    {
        float scale = DeviceDpi / 96f;
        var c = new PointF(center.X, center.Y);
        float outer = radius, inner = (float)(radius * RadialWheel.DeadZoneRatio);
        float sweep = 360f / RadialWheel.SectorCount;
        int alpha = dim ? 110 : 225;

        using var idle = new SolidBrush(Color.FromArgb(alpha, 248, 248, 246));
        using var empty = new SolidBrush(Color.FromArgb(dim ? 60 : 120, 230, 230, 228));
        using var disabled = new SolidBrush(Color.FromArgb(dim ? 90 : 200, 225, 225, 222));
        using var hover = new SolidBrush(Color.FromArgb(dim ? 140 : 240, 214, 238, 252));
        using var divider = new Pen(Color.FromArgb(dim ? 80 : 160, 120, 120, 120), 1f);
        using var accent = new Pen(Color.FromArgb(dim ? 120 : 255, Color.DeepSkyBlue), 4f * scale);
        using var labelFont = new Font("Microsoft JhengHei UI", 10.5f * fontScale, FontStyle.Bold);
        using var centerFont = new Font("Microsoft JhengHei UI", 11f * fontScale, FontStyle.Bold);
        using var detailFont = new Font("Microsoft JhengHei UI", 9f * fontScale);
        using var ink = new SolidBrush(Color.FromArgb(dim ? 120 : 255, 34, 34, 34));
        using var mute = new SolidBrush(Color.FromArgb(dim ? 100 : 255, 130, 130, 130));
        using var warn = new SolidBrush(Color.Firebrick);
        using var marker = new SolidBrush(Color.FromArgb(dim ? 90 : 200, 90, 90, 90));
        var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        for (int i = 0; i < RadialWheel.SectorCount; i++)
        {
            var item = slots[i];
            bool isHover = i == hoverIndex && item is not null;
            bool usable = item is not null && UnavailableReason(item.DefaultAction) is null;
            // GDI+ 角度 0 = 右方、順時針;格子 0 = 上方,所以 -90。
            float start = -90f + i * sweep - sweep / 2f;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(c.X - outer, c.Y - outer, outer * 2, outer * 2, start, sweep);
            path.AddArc(c.X - inner, c.Y - inner, inner * 2, inner * 2, start + sweep, -sweep);
            path.CloseFigure();
            g.FillPath(item is null ? empty : isHover ? hover : usable ? idle : disabled, path);
            g.DrawPath(divider, path);
            if (isHover)
            {
                g.DrawArc(accent, c.X - outer + accent.Width / 2, c.Y - outer + accent.Width / 2,
                    outer * 2 - accent.Width, outer * 2 - accent.Width, start, sweep);
            }

            if (item is null)
            {
                continue;
            }

            var at = RadialWheel.Polar(c, (inner + outer) / 2, RadialWheel.SectorAngle(i));
            var labelBox = new RectangleF(at.X - (outer - inner) * 0.5f, at.Y - 22 * scale, (outer - inner), 44 * scale);
            g.DrawString(item.Label, labelFont, usable ? ink : mute, labelBox, centered);

            if (item.IsGroup)
            {
                // 群組記號:外緣一個朝外的小三角,表示「往外甩還有一層」。
                double a = RadialWheel.SectorAngle(i);
                var tip = RadialWheel.Polar(c, outer - 4 * scale, a);
                var left = RadialWheel.Polar(c, outer - 13 * scale, a - 0.07);
                var right = RadialWheel.Polar(c, outer - 13 * scale, a + 0.07);
                g.FillPolygon(marker, new[] { tip, left, right });
            }
        }

        // 中心:懸停格說明,或操作提示。
        using var hub = new SolidBrush(Color.FromArgb(dim ? 120 : 235, 255, 255, 255));
        g.FillEllipse(hub, c.X - inner + 3, c.Y - inner + 3, (inner - 3) * 2, (inner - 3) * 2);
        if (dim)
        {
            return; // 被二級輪盤蓋住時不寫中心字,避免兩處說明打架
        }

        var box = new RectangleF(c.X - inner * 0.9f, c.Y - inner * 0.75f, inner * 1.8f, inner * 1.5f);
        if (hoverIndex >= 0 && slots[hoverIndex] is { } s)
        {
            string? reason = UnavailableReason(s.DefaultAction);
            string detail = status.Length > 0 ? status
                : reason ?? (s.IsGroup ? $"往外甩展開 {s.Children!.Count} 項\n放開 = {s.Children[0].Label}" : s.Detail);
            g.DrawString(s.Label, centerFont, ink, new RectangleF(box.X, box.Y, box.Width, box.Height * 0.45f), centered);
            g.DrawString(detail, detailFont, status.Length > 0 || reason is not null ? warn : mute,
                new RectangleF(box.X, box.Y + box.Height * 0.42f, box.Width, box.Height * 0.58f), centered);
        }
        else
        {
            g.DrawString(idleHint, detailFont, mute, box, centered);
        }
    }

    /// <summary>頂端置中提示:輪盤用法;AI 可用時另提示 Shift,按住 Shift 時切換為「AI 待命」橘字。</summary>
    private void DrawHint(Graphics g, Rectangle client)
    {
        string hint = _wheel is { Level: 2 }
            ? "二級輪盤:朝格子方向放開 · 再按 Ctrl 回上一層 · Esc 取消"
            : WheelOpen
            ? "輪盤:朝格子方向移動後放開 · 有 ▲ 的格子往外甩展開 · 再按 Ctrl 收回 · Esc 取消"
            : _aiArmed
                ? "● AI 增強模式 — 放開即用 AI 辨識"
                : _aiAvailable
                    ? "拖曳中按一下 Ctrl = 輪盤 · 按住 Shift 放開 = AI 增強"
                    : "拖曳中按一下 Ctrl = 輪盤(更多輸出方式)";
        Color fg = _aiArmed && !WheelOpen ? Color.Orange : Color.White;

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
