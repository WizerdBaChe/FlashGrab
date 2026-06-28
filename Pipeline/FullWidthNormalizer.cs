using System.Text;

namespace FlashGrab.Pipeline;

/// <summary>
/// Tier 1 安全規則(A 類):全形英數字 → 半形。
/// 只轉換全形的「數字、大小寫拉丁字母、空白」——這些幾乎必為 OCR 雜訊;
/// **不碰**全形 CJK 標點(。、「」等),因其在中文是正當字元,轉成 ASCII 會破壞原意。
/// </summary>
internal sealed class FullWidthNormalizer : ITextProcessor
{
    public string Process(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            sb.Append(Normalize(c));
        }

        return sb.ToString();
    }

    private static char Normalize(char c)
    {
        // 全形空白 → 半形空白
        if (c == '　')
        {
            return ' ';
        }

        // 全形數字 ０-９ / 大寫 Ａ-Ｚ / 小寫 ａ-ｚ → ASCII(偏移 0xFEE0)
        bool isFullWidthAlnum =
            (c >= '０' && c <= '９') ||
            (c >= 'Ａ' && c <= 'Ｚ') ||
            (c >= 'ａ' && c <= 'ｚ');

        return isFullWidthAlnum ? (char)(c - 0xFEE0) : c;
    }
}
