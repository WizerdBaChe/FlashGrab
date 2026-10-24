using System.Drawing;

namespace FlashGrab.Capture;

/// <summary>輪盤上可執行的輸出動作。</summary>
internal enum WheelAction
{
    CopyText,      // 依設定複製文字
    AiText,        // AI 辨識(需已設定 Tier 2)
    CopyOneLine,   // 合併成一行
    CopyImage,     // 複製框選圖片
    SaveImage,     // 存成 PNG
    CopyKeepLines, // 保留原本換行
    SaveJpg,       // 存成 JPG
}

/// <summary>
/// 功能庫裡的一個項目:葉節點(Action 有值)或群組(Children 有值,往外甩展開成二級輪盤)。
/// 群組本身也可直接選:等於它的第一個子項(預設子項)。
/// </summary>
internal sealed record WheelItem(string Id, string Label, string Detail, WheelAction? Action = null, IReadOnlyList<WheelItem>? Children = null)
{
    public bool IsGroup => Children is { Count: > 0 };

    /// <summary>選這一格最終會執行的動作:葉節點是自己,群組是預設子項。</summary>
    public WheelAction DefaultAction => IsGroup ? Children![0].DefaultAction : Action!.Value;
}

/// <summary>
/// 輪盤模型與幾何(純函式,不碰 UI,可單獨測試)。
/// 第一層固定 8 格(0 = 正上方,順時針);格子不會因項目增減而重新均分,
/// 沒放東西的格子就是空格。放不下的項目留在功能庫,由設定畫面換上輪盤。
/// 選取只看方向:超出中心死區後,角度決定格子,沒有外圈上限。
/// 例外是群組格:沿它的方向到達外圈時,子項以扇形色帶從該格往外散出(同一個圓心),
/// 退回外圈 0.9 倍以內自動收合。群組最多 <see cref="MaxGroupChildren"/> 個子項。
/// </summary>
internal static class RadialWheel
{
    public const int SectorCount = 8;

    /// <summary>死區半徑佔輪盤半徑的比例;移回這裡放開 = 不選任何格。</summary>
    public const double DeadZoneRatio = 0.42;

    /// <summary>群組格的扇面:游標沿群組方向到達外圈(這個倍數)時展開。</summary>
    public const double FanOpenRatio = 1.0;

    /// <summary>扇面展開後,游標退回到外圈這個倍數以內才收合(緩衝帶,避免在邊界忽開忽關)。</summary>
    public const double FanCollapseRatio = 0.9;

    /// <summary>扇面色帶外緣相對外圈半徑的倍數。</summary>
    public const double FanOuterRatio = 1.55;

    /// <summary>扇面裡每個子項佔的角度。</summary>
    public const double FanChildDegrees = 36;

    /// <summary>群組最多幾個子項(5 × 36° = 180°,再多就壓過太多鄰格方向)。</summary>
    public const int MaxGroupChildren = 5;

    /// <summary>輪盤含群組時,擺位要讓扇面外緣也留在螢幕內:以外圈的這個倍數當保留半徑。</summary>
    public const double FanPlaceReachRatio = 1.6;

    private static readonly WheelItem CopyText = new("text.copy", "複製文字", "內建 OCR,照設定處理段落", WheelAction.CopyText);
    private static readonly WheelItem OneLine = new("text.oneline", "合成一行", "去掉換行,貼進搜尋框或表單", WheelAction.CopyOneLine);
    private static readonly WheelItem KeepLines = new("text.keeplines", "保留換行", "照畫面逐行複製", WheelAction.CopyKeepLines);
    private static readonly WheelItem AiText = new("ai.text", "AI 辨識", "改用視覺模型(較慢)", WheelAction.AiText);
    private static readonly WheelItem CopyImage = new("image.copy", "複製圖片", "不辨識,直接複製框選畫面", WheelAction.CopyImage);
    private static readonly WheelItem SavePng = new("image.png", "存成 PNG", "存到 圖片\\FlashGrab", WheelAction.SaveImage);
    private static readonly WheelItem SaveJpg = new("image.jpg", "存成 JPG", "檔案較小,存到 圖片\\FlashGrab", WheelAction.SaveJpg);

    /// <summary>功能庫:所有可放上輪盤的項目(葉節點與群組)。新動作只要加進這裡。</summary>
    public static readonly IReadOnlyList<WheelItem> Library = new[]
    {
        new WheelItem("group.text", "文字", "往外甩:複製/一行/保留換行", Children: new[] { CopyText, OneLine, KeepLines }),
        new WheelItem("group.image", "圖片", "往外甩:PNG/複製/JPG", Children: new[] { SavePng, CopyImage, SaveJpg }),
        CopyText, OneLine, KeepLines, AiText, CopyImage, SavePng, SaveJpg,
    };

    /// <summary>
    /// 預設配置。位置沿用第一版(N 複製文字、NE AI、E 一行、SE 複製圖片、S 存 PNG、SW 保留換行),
    /// 已練熟的方向不變;N 與 S 換成同位置的群組,直接放開仍是原本的動作。
    /// </summary>
    public static readonly IReadOnlyList<string?> DefaultLoadout = new string?[]
    {
        "group.text", "ai.text", "text.oneline", "image.copy", "group.image", "text.keeplines", null, null,
    };

    public static WheelItem? Find(string? id) =>
        id is null ? null : Library.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// 把設定檔裡的配置轉成 8 格。null/長度不對 = 預設;找不到的 id 變空格並回報在 dropped。
    /// </summary>
    public static WheelItem?[] ResolveLoadout(IReadOnlyList<string?>? stored, out List<string> dropped)
    {
        dropped = new List<string>();
        var source = stored is { Count: SectorCount } ? stored : DefaultLoadout;
        var slots = new WheelItem?[SectorCount];
        for (int i = 0; i < SectorCount; i++)
        {
            slots[i] = Find(source[i]);
            if (slots[i] is null && source[i] is { } bad)
            {
                dropped.Add(bad);
            }
        }

        return slots;
    }

    /// <summary>
    /// 由游標相對中心的位移求格子索引;落在死區回 -1。
    /// 螢幕座標 y 向下,所以「上」是 dy &lt; 0。
    /// </summary>
    public static int SectorAt(double dx, double dy, double deadZoneRadius)
    {
        if (dx * dx + dy * dy < deadZoneRadius * deadZoneRadius)
        {
            return -1;
        }

        double angle = Math.Atan2(dx, -dy); // 0 = 上,順時針為正
        double step = 2 * Math.PI / SectorCount;
        int index = (int)Math.Floor((angle + step / 2) / step);
        return ((index % SectorCount) + SectorCount) % SectorCount;
    }

    /// <summary>
    /// 扇面子項相對群組方向的偏移角(度):預設子項(第 0 個)正對群組方向,
    /// 之後順時針、逆時針交替:0, +36, −36, +72, −72。
    /// </summary>
    public static double[] FanOffsets(int count)
    {
        var offsets = new double[Math.Min(count, MaxGroupChildren)];
        for (int i = 0; i < offsets.Length; i++)
        {
            int step = (i + 1) / 2;
            offsets[i] = (i % 2 == 1 ? 1 : -1) * step * FanChildDegrees;
        }

        return offsets;
    }

    /// <summary>
    /// 扇面打開時,依游標相對中心的角度求子項索引;不在任何子項的 36° 楔形內回 -1。
    /// 只看角度不看距離(呼叫端負責「距離 ≥ 外圈」的判斷),所以扇面內沒有外緣上限。
    /// </summary>
    public static int FanHit(double dx, double dy, int groupSlot, int childCount)
    {
        double angle = Math.Atan2(dx, -dy) * 180 / Math.PI;
        double offset = angle - SectorAngle(groupSlot) * 180 / Math.PI;
        offset = ((offset + 180) % 360 + 360) % 360 - 180; // 收進 −180..180
        var offsets = FanOffsets(childCount);
        for (int i = 0; i < offsets.Length; i++)
        {
            if (offset >= offsets[i] - FanChildDegrees / 2 && offset < offsets[i] + FanChildDegrees / 2)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>格子中心角(弧度,0 = 上,順時針)。</summary>
    public static double SectorAngle(int index) => index * 2 * Math.PI / SectorCount;

    /// <summary>極座標轉螢幕座標。</summary>
    public static PointF Polar(PointF center, double radius, double angle) =>
        new((float)(center.X + Math.Sin(angle) * radius), (float)(center.Y - Math.Cos(angle) * radius));

    /// <summary>
    /// 決定輪盤中心:盡量在游標處,但整圈必須留在游標所在螢幕內(留邊 margin)。
    /// 螢幕比輪盤還小時置中於該螢幕。
    /// </summary>
    public static Point Place(Point cursor, Rectangle screen, int radius, int margin = 16)
    {
        int reach = radius + margin;
        return new Point(Clamp(cursor.X, screen.Left, screen.Right, reach), Clamp(cursor.Y, screen.Top, screen.Bottom, reach));
    }

    private static int Clamp(int value, int low, int high, int reach)
    {
        if (high - low < reach * 2)
        {
            return (low + high) / 2;
        }

        return Math.Clamp(value, low + reach, high - reach);
    }

    /// <summary>
    /// 把多行文字合成一行:換行處兩側都是 CJK 時直接相接,否則補一個空格。
    /// </summary>
    public static string JoinLines(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (string line in lines)
        {
            if (sb.Length > 0 && !(IsCjk(sb[^1]) && IsCjk(line[0])))
            {
                sb.Append(' ');
            }

            sb.Append(line);
        }

        return sb.ToString();
    }

    private static bool IsCjk(char c) =>
        c is >= '　' and <= '〿'   // CJK 標點
          or >= '㐀' and <= '鿿'   // 漢字
          or >= '＀' and <= '￯';  // 全形
}
