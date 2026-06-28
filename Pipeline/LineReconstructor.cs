using System.Text;
using FlashGrab.Ocr;

namespace FlashGrab.Pipeline;

/// <summary>
/// 把結構化 <see cref="OcrDocument"/> 還原成文字。
/// 預設「忠實模式」:保留行序與斷行、CJK 感知詞間空格、用邊界框 X 座標還原縮排。
/// 選配「段落重排」:把視覺軟換行併成段落(僅適合抄純文章)。
/// </summary>
internal static class LineReconstructor
{
    /// <summary>縮排判定門檻:位移需達一個字寬的此比例才算縮排,避免雜訊。</summary>
    private const float IndentThreshold = 0.6f;

    /// <summary>縮排空格上限,避免異常座標產生超長空白。</summary>
    private const int MaxIndentSpaces = 60;

    public static string Build(OcrDocument doc, bool reflowParagraphs)
    {
        if (doc.Lines.Count == 0)
        {
            return string.Empty;
        }

        float baseLeft = MinLeft(doc);
        float charWidth = EstimateCharWidth(doc);

        var rendered = new List<string>(doc.Lines.Count);
        foreach (var line in doc.Lines)
        {
            if (line.Words.Count == 0)
            {
                continue;
            }

            int indent = IndentSpaces(line.Words[0].Bounds.Left, baseLeft, charWidth);
            string body = JoinWords(line.Words);
            rendered.Add(new string(' ', indent) + body);
        }

        return reflowParagraphs ? Reflow(rendered) : string.Join("\n", rendered);
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

    /// <summary>把每行第一個詞的左緣換算成縮排空格數(以字寬為單位)。</summary>
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

    private static float MinLeft(OcrDocument doc)
    {
        float min = float.MaxValue;
        foreach (var line in doc.Lines)
        {
            if (line.Words.Count > 0)
            {
                min = Math.Min(min, line.Words[0].Bounds.Left);
            }
        }

        return min == float.MaxValue ? 0f : min;
    }

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
