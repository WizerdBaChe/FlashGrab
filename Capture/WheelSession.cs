using System.Drawing;

namespace FlashGrab.Capture;

/// <summary>放開滑鼠時輪盤的判定結果。</summary>
internal enum WheelOutcome
{
    /// <summary>選定了一個動作(見 <see cref="WheelRelease.Action"/>)。</summary>
    Commit,

    /// <summary>在第一層中心放開:不用輪盤,照原本流程。</summary>
    Default,

    /// <summary>空格或不能用的動作:輪盤留著(原因在 Status)。</summary>
    Stay,
}

internal readonly record struct WheelRelease(WheelOutcome Outcome, WheelAction? Action = null);

/// <summary>
/// 一次輪盤操作的狀態機(純邏輯,不碰 UI,可單獨測試)。
/// 第一層 8 格;群組格沿方向越過外圈 1.3 倍時,在越過處展開二級輪盤。
/// </summary>
internal sealed class WheelSession
{
    private readonly IReadOnlyList<WheelItem?> _slots;
    private readonly Func<WheelAction, string?> _unavailable;
    private Rectangle _screen;

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
    public int Hover { get; private set; } = -1;

    /// <summary>1 = 第一層,2 = 二級輪盤展開中。</summary>
    public int Level { get; private set; } = 1;

    public WheelItem? Group { get; private set; }
    public Point SubCenter { get; private set; }
    public int SubRadius { get; private set; }
    public int SubHover { get; private set; } = -1;

    /// <summary>給使用者看的狀態訊息(不能用的原因);空字串 = 無。</summary>
    public string Status { get; private set; } = "";

    /// <summary>二級輪盤的 8 格:子項依序放 0..n-1,其餘空格。</summary>
    public WheelItem?[] SubSlots
    {
        get
        {
            var slots = new WheelItem?[RadialWheel.SectorCount];
            if (Group?.Children is { } children)
            {
                for (int i = 0; i < Math.Min(children.Count, slots.Length); i++)
                {
                    slots[i] = children[i];
                }
            }

            return slots;
        }
    }

    public void Open(Point cursor, Rectangle screen, int radius)
    {
        _screen = screen;
        Radius = radius;
        Center = RadialWheel.Place(cursor, screen, radius);
        Level = 1;
        Group = null;
        Status = "";
        Hover = -1;
        Move(cursor);
    }

    /// <summary>游標移動;回傳畫面是否需要重繪。</summary>
    public bool Move(Point p)
    {
        if (Level == 2)
        {
            int nextSub = RadialWheel.SectorAt(p.X - SubCenter.X, p.Y - SubCenter.Y, SubRadius * RadialWheel.DeadZoneRatio);
            if (nextSub == SubHover)
            {
                return false;
            }

            SubHover = nextSub;
            Status = "";
            return true;
        }

        double dx = p.X - Center.X, dy = p.Y - Center.Y;
        int next = RadialWheel.SectorAt(dx, dy, Radius * RadialWheel.DeadZoneRatio);
        bool changed = next != Hover;
        Hover = next;
        if (changed)
        {
            Status = "";
        }

        if (Hover >= 0 && _slots[Hover] is { IsGroup: true } group && RadialWheel.ShouldExpand(dx, dy, Radius, Hover))
        {
            Group = group;
            Level = 2;
            SubRadius = (int)(Radius * RadialWheel.SubRadiusRatio);
            SubCenter = RadialWheel.Place(p, _screen, SubRadius);
            SubHover = -1;
            Status = "";
            return true;
        }

        return changed;
    }

    /// <summary>二級輪盤開著時回到第一層;回傳是否真的退回。</summary>
    public bool Back()
    {
        if (Level != 2)
        {
            return false;
        }

        Level = 1;
        Group = null;
        SubHover = -1;
        Status = "";
        return true;
    }

    public WheelRelease Release(Point p)
    {
        Move(p);
        WheelItem? picked;
        if (Level == 2)
        {
            // 二級輪盤中心放開 = 群組預設子項
            picked = SubHover < 0 ? Group : SubSlots[SubHover];
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
            return new WheelRelease(WheelOutcome.Stay); // 空格不是選項
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
