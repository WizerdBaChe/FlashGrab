using System.Drawing;

namespace FlashGrab.Capture;

/// <summary>輪盤上可選的輸出動作。順序 = 扇區索引(0 = 正上方,順時針)。</summary>
internal enum WheelAction
{
    CopyText,      // N  依設定複製文字
    AiText,        // NE AI 辨識(需已設定 Tier 2)
    CopyOneLine,   // E  合併成一行
    CopyImage,     // SE 複製框選圖片
    SaveImage,     // S  存成 PNG
    CopyKeepLines, // SW 保留原本換行
}

/// <summary>一格扇區的內容;Action 為 null 代表空格(畫出來但不可選)。</summary>
internal sealed record WheelSlot(WheelAction? Action, string Label, string Detail);

/// <summary>
/// 輪盤幾何(純函式,不碰 UI,可單獨測試)。
/// 選取只看方向:超出中心死區後,角度決定扇區,沒有外圈上限 —— 甩過頭仍算選中。
/// </summary>
internal static class RadialWheel
{
    public const int SectorCount = 8;

    /// <summary>死區半徑佔輪盤半徑的比例;移回這裡放開 = 不選任何格。</summary>
    public const double DeadZoneRatio = 0.42;

    public static readonly IReadOnlyList<WheelSlot> Slots = new WheelSlot[]
    {
        new(WheelAction.CopyText, "複製文字", "內建 OCR,照設定處理段落"),
        new(WheelAction.AiText, "AI 辨識", "改用視覺模型(較慢)"),
        new(WheelAction.CopyOneLine, "合成一行", "去掉換行,貼進搜尋框或表單"),
        new(WheelAction.CopyImage, "複製圖片", "不辨識,直接複製框選畫面"),
        new(WheelAction.SaveImage, "存成 PNG", "存到 圖片\\FlashGrab"),
        new(WheelAction.CopyKeepLines, "保留換行", "照畫面逐行複製"),
        new(null, "", ""),
        new(null, "", ""),
    };

    /// <summary>
    /// 由游標相對中心的位移求扇區索引;落在死區回 -1。
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

    /// <summary>扇區中心角(弧度,0 = 上,順時針)。</summary>
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
