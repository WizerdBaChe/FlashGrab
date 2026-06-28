namespace FlashGrab.Pipeline;

/// <summary>跨 stage 共用的字元判斷規則。</summary>
internal static class TextRules
{
    /// <summary>是否為不使用空格分詞的 CJK 字元(中日韓表意/假名/全形/CJK 標點)。</summary>
    public static bool IsCjk(char c)
    {
        int u = c;
        return (u >= 0x4E00 && u <= 0x9FFF)   // CJK 統一表意文字
            || (u >= 0x3400 && u <= 0x4DBF)   // 擴充 A
            || (u >= 0x3000 && u <= 0x303F)   // CJK 標點符號
            || (u >= 0xFF00 && u <= 0xFFEF)   // 全形字元
            || (u >= 0x3040 && u <= 0x30FF)   // 日文假名
            || (u >= 0xAC00 && u <= 0xD7AF);  // 韓文音節
    }

    /// <summary>相鄰兩段文字之間是否需要空格:僅當交界兩側皆非 CJK 時才需要。</summary>
    public static bool NeedsSpaceBetween(char left, char right)
    {
        return !IsCjk(left) && !IsCjk(right);
    }

    /// <summary>句末終結標點(中英),用於段落重排時判斷是否該保留換行。</summary>
    public static bool IsSentenceTerminator(char c)
    {
        return c is '。' or '!' or '?' or '.' or '!' or '?'
            or '：' or ':' or '；' or ';' or '…';
    }
}
