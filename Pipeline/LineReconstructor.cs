using System.Text;
using FlashGrab.Ocr;

namespace FlashGrab.Pipeline;

/// <summary>
/// 把結構化 <see cref="OcrDocument"/> 還原成文字。
/// 預設「忠實模式」:先把引擎切碎的同列片段併回同一行,再保留行序與斷行、
/// CJK 感知詞間空格、用邊界框 X 座標還原縮排。
/// 選配「段落重排」:把視覺軟換行併成段落(僅適合抄純文章)。
/// </summary>
internal static class LineReconstructor
{
    /// <summary>縮排判定門檻:位移需達一個字寬的此比例才算縮排,避免雜訊。</summary>
    private const float IndentThreshold = 0.6f;

    /// <summary>
    /// 縮排空格上限。跨區域框選(例如同時含左右兩塊面板)時,右側區塊的 X 位移本來就很大,
    /// 上限壓低可讓假縮排維持在可讀範圍,同時不影響程式碼截圖的正常縮排層級。
    /// </summary>
    private const int MaxIndentSpaces = 16;

    /// <summary>
    /// 同列判定門檻:兩段文字的垂直重疊需達「較矮者高度」的此比例才算同一視覺列。
    /// Windows.Media.Ocr 會把同列但水平間距大的文字(例如 UI 標籤列)切成多個 OcrLine,
    /// 不併回同列的話,每段的 X 位移都會被當成縮排,貼上就變成階梯狀的假巢狀結構。
    /// </summary>
    private const float RowOverlapThreshold = 0.5f;

    public static string Build(OcrDocument doc, bool reflowParagraphs)
    {
        var rows = GroupRows(doc.Lines);
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        float baseLeft = MinLeft(rows);
        float charWidth = EstimateCharWidth(doc);

        var rendered = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            string body = JoinRow(row);
            if (body.Length == 0)
            {
                continue;
            }

            int indent = IndentSpaces(RowLeft(row), baseLeft, charWidth);
            rendered.Add(new string(' ', indent) + body);
        }

        return reflowParagraphs ? Reflow(rendered) : string.Join("\n", rendered);
    }

    /// <summary>
    /// 把引擎回傳的行依垂直位置分群成「視覺列」:垂直重疊夠多的行屬同一列,
    /// 列內再依左緣由左至右排序;列序由上而下。
    /// </summary>
    private static List<List<OcrTextLine>> GroupRows(IReadOnlyList<OcrTextLine> lines)
    {
        var ordered = new List<OcrTextLine>(lines.Count);
        foreach (var line in lines)
        {
            if (line.Words.Count > 0)
            {
                ordered.Add(line);
            }
        }

        ordered.Sort((a, b) =>
        {
            int byCenter = VerticalCenter(a).CompareTo(VerticalCenter(b));
            return byCenter != 0 ? byCenter : Left(a).CompareTo(Left(b));
        });

        var rows = new List<List<OcrTextLine>>();
        float rowTop = 0f;
        float rowBottom = 0f;

        foreach (var line in ordered)
        {
            float top = Top(line);
            float bottom = Bottom(line);

            if (rows.Count > 0 && SameRow(rowTop, rowBottom, top, bottom))
            {
                rows[^1].Add(line);
                rowTop = Math.Min(rowTop, top);
                rowBottom = Math.Max(rowBottom, bottom);
            }
            else
            {
                rows.Add(new List<OcrTextLine> { line });
                rowTop = top;
                rowBottom = bottom;
            }
        }

        foreach (var row in rows)
        {
            row.Sort((a, b) => Left(a).CompareTo(Left(b)));
        }

        return rows;
    }

    /// <summary>垂直重疊比例是否達門檻(以較矮者的高度為分母,避免高片段把整列吃掉)。</summary>
    private static bool SameRow(float aTop, float aBottom, float bTop, float bBottom)
    {
        float overlap = Math.Min(aBottom, bBottom) - Math.Max(aTop, bTop);
        if (overlap <= 0f)
        {
            return false;
        }

        float shorter = Math.Min(aBottom - aTop, bBottom - bTop);
        return shorter > 0f && overlap / shorter >= RowOverlapThreshold;
    }

    /// <summary>同一視覺列的各片段之間固定以一個空格分隔(片段之間必然存在可見間距)。</summary>
    private static string JoinRow(IReadOnlyList<OcrTextLine> row)
    {
        var sb = new StringBuilder();
        foreach (var fragment in row)
        {
            string body = JoinWords(fragment.Words);
            if (body.Length == 0)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(body);
        }

        return sb.ToString();
    }

    /// <summary>一行內把詞接起來:相鄰兩詞交界皆非 CJK 才加空格。</summary>
    private static string JoinWords(IReadOnlyList<OcrTextWord> words)
    {
        var sb = new StringBuilder();
        string? prev = null;
        foreach (var word in words)
        {
            string w = word.Text;
            if (w.Length == 0)
            {
                continue;
            }

            if (prev is not null && TextRules.NeedsSpaceBetween(prev[^1], w[0]))
            {
                sb.Append(' ');
            }

            sb.Append(w);
            prev = w;
        }

        return sb.ToString();
    }

    /// <summary>把每列最左片段的左緣換算成縮排空格數(以字寬為單位)。</summary>
    private static int IndentSpaces(float left, float baseLeft, float charWidth)
    {
        if (charWidth <= 0f)
        {
            return 0;
        }

        float offset = left - baseLeft;
        if (offset < charWidth * IndentThreshold)
        {
            return 0;
        }

        int spaces = (int)Math.Round(offset / charWidth);
        return Math.Clamp(spaces, 0, MaxIndentSpaces);
    }

    private static float MinLeft(IReadOnlyList<List<OcrTextLine>> rows)
    {
        float min = float.MaxValue;
        foreach (var row in rows)
        {
            min = Math.Min(min, RowLeft(row));
        }

        return min == float.MaxValue ? 0f : min;
    }

    /// <summary>一列的左緣 = 最左片段的首詞左緣(列內已依左緣排序)。</summary>
    private static float RowLeft(IReadOnlyList<OcrTextLine> row) => Left(row[0]);

    private static float Left(OcrTextLine line) => line.Words[0].Bounds.Left;

    private static float Top(OcrTextLine line)
    {
        float top = float.MaxValue;
        foreach (var word in line.Words)
        {
            top = Math.Min(top, word.Bounds.Top);
        }

        return top;
    }

    private static float Bottom(OcrTextLine line)
    {
        float bottom = float.MinValue;
        foreach (var word in line.Words)
        {
            bottom = Math.Max(bottom, word.Bounds.Bottom);
        }

        return bottom;
    }

    private static float VerticalCenter(OcrTextLine line) => (Top(line) + Bottom(line)) / 2f;

    /// <summary>以所有詞的「每字平均寬度」中位數估一個字寬,作為縮排換算單位。</summary>
    private static float EstimateCharWidth(OcrDocument doc)
    {
        var widths = new List<float>();
        foreach (var line in doc.Lines)
        {
            foreach (var word in line.Words)
            {
                int len = word.Text.Length;
                if (len > 0 && word.Bounds.Width > 0f)
                {
                    widths.Add(word.Bounds.Width / len);
                }
            }
        }

        if (widths.Count == 0)
        {
            return 0f;
        }

        widths.Sort();
        return widths[widths.Count / 2];
    }

    /// <summary>
    /// 段落重排:把相鄰行併入同段,除非前一行以終結標點結尾或目前行為空。
    /// 併接時依交界字元決定是否加空格(CJK 不加、拉丁文加)。
    /// </summary>
    private static string Reflow(IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        foreach (string line in lines)
        {
            string trimmed = line.TrimEnd();
            if (sb.Length == 0)
            {
                sb.Append(trimmed);
                continue;
            }

            char prevChar = sb[^1];
            bool breakHere = trimmed.Length == 0
                || TextRules.IsSentenceTerminator(prevChar);

            if (breakHere)
            {
                sb.Append('\n').Append(trimmed);
            }
            else
            {
                string head = trimmed.TrimStart();
                if (head.Length > 0 && TextRules.NeedsSpaceBetween(prevChar, head[0]))
                {
                    sb.Append(' ');
                }

                sb.Append(head);
            }
        }

        return sb.ToString();
    }
}
