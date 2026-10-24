using System.Drawing;

namespace FlashGrab.Capture;

/// <summary>放開滑鼠時輪盤的判定結果。</summary>
internal enum WheelOutcome
{
    /// <summary>選定了一個動作(見 <see cref="WheelRelease.Action"/>)。</summary>
    Commit,

    /// <summary>在第一層中心放開:不用輪盤,照原本流程。</summary>
    Default,

    /// <summary>空格、扇面外或不能用的動作:輪盤留著(不能用時原因在 Status)。</summary>
    Stay,
}

internal readonly record struct WheelRelease(WheelOutcome Outcome, WheelAction? Action = null);

/// <summary>
/// 一次輪盤操作的狀態機(純邏輯,不碰 UI,可單獨測試)。
/// 第一層 8 格;游標沿群組格方向到達外圈時,該群組的子項以扇形從那一格往外散出
/// (同一個圓心),退回外圈 0.9 倍以內自動收合。
/// </summary>
internal sealed class WheelSession
{
    private readonly IReadOnlyList<WheelItem?> _slots;
    private readonly Func<WheelAction, string?> _unavailable;

    public WheelSession(IReadOnlyList<WheelItem?> slots, Func<WheelAction, string?> unavailable)
    {
        if (slots.Count != RadialWheel.SectorCount)
        {
            throw new ArgumentException($"輪盤必須剛好 {RadialWheel.SectorCount} 格", nameof(slots));
        }

        _slots = slots;
        _unavailable = unavailable;
    }

    public IReadOnlyList<WheelItem?> Slots => _slots;
    public Point Center { get; private set; }
    public int Radius { get; private set; }

    /// <summary>第一層游標所在格;扇面打開時固定為該群組格。</summary>
    public int Hover { get; private set; } = -1;

    /// <summary>扇面所屬的群組格索引;-1 = 扇面收合。</summary>
    public int FanSlot { get; private set; } = -1;

    /// <summary>扇面裡游標所在的子項索引;-1 = 不在任何子項上(仍在外圈內,或在扇面角度外)。</summary>
    public int FanHover { get; private set; } = -1;

    /// <summary>給使用者看的狀態訊息(不能用的原因);空字串 = 無。</summary>
    public string Status { get; private set; } = "";

    public bool FanOpen => FanSlot >= 0;

    /// <summary>扇面裡實際顯示的子項(最多 5 個)。</summary>
    public IReadOnlyList<WheelItem> FanChildren =>
        FanOpen && _slots[FanSlot]?.Children is { } c ? c.Take(RadialWheel.MaxGroupChildren).ToList() : Array.Empty<WheelItem>();

    public void Open(Point cursor, Rectangle screen, int radius)
    {
        Radius = radius;
        // 有群組時多留扇面的寬度,靠近螢幕邊緣也不會被切掉。
        bool hasGroup = _slots.Any(s => s is { IsGroup: true });
        int reach = hasGroup ? (int)Math.Ceiling(radius * RadialWheel.FanPlaceReachRatio) : radius;
        Center = RadialWheel.Place(cursor, screen, reach);
        FanSlot = -1;
        FanHover = -1;
        Hover = -1;
        Status = "";
        Move(cursor);
    }

    /// <summary>游標移動;回傳畫面是否需要重繪。</summary>
    public bool Move(Point p)
    {
        var before = (Hover, FanSlot, FanHover);
        double dx = p.X - Center.X, dy = p.Y - Center.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (FanOpen && dist < Radius * RadialWheel.FanCollapseRatio)
        {
            FanSlot = -1; // 拉回外圈內:收合
            FanHover = -1;
        }

        if (FanOpen)
        {
            Hover = FanSlot;
            FanHover = dist >= Radius * RadialWheel.FanOpenRatio
                ? RadialWheel.FanHit(dx, dy, FanSlot, FanChildren.Count)
                : -1; // 0.9R–1.0R:還指著群組格本身
        }
        else
        {
            Hover = RadialWheel.SectorAt(dx, dy, Radius * RadialWheel.DeadZoneRatio);
            if (Hover >= 0 && _slots[Hover] is { IsGroup: true } && dist >= Radius * RadialWheel.FanOpenRatio)
            {
                FanSlot = Hover;
                FanHover = RadialWheel.FanHit(dx, dy, FanSlot, FanChildren.Count);
            }
        }

        bool changed = before != (Hover, FanSlot, FanHover);
        if (changed)
        {
            Status = "";
        }

        return changed;
    }

    public WheelRelease Release(Point p)
    {
        Move(p);
        WheelItem? picked;
        if (FanOpen)
        {
            double dx = p.X - Center.X, dy = p.Y - Center.Y;
            bool inBand = dx * dx + dy * dy >= Math.Pow(Radius * RadialWheel.FanOpenRatio, 2);
            // 扇面內 = 該子項;還在外圈內 = 群組預設子項;扇面角度外 = 不選(輪盤留著)
            picked = !inBand ? _slots[FanSlot] : FanHover >= 0 ? FanChildren[FanHover] : null;
        }
        else
        {
            if (Hover < 0)
            {
                return new WheelRelease(WheelOutcome.Default);
            }

            picked = _slots[Hover]; // 沒展開的群組 = 預設子項
        }

        if (picked is null)
        {
            return new WheelRelease(WheelOutcome.Stay);
        }

        var action = picked.DefaultAction;
        if (_unavailable(action) is string reason)
        {
            Status = reason;
            return new WheelRelease(WheelOutcome.Stay);
        }

        return new WheelRelease(WheelOutcome.Commit, action);
    }
}
