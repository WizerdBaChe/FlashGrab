using System.Text;
using System.Text.RegularExpressions;

namespace FlashGrab.Pipeline;

/// <summary>
/// Tier 1 安全規則(B 類安全子集):僅在「明確是數字」的 token 內,
/// 把常被誤認的字母還原成數字 —— O/o→0、l/I→1。
/// 嚴格限定:token 須至少含一個真數字、且其餘字元只由 [0-9OolI] 與數字分隔符組成,
/// 修正後必須整段成為合法數字。如此「lol」「100GB」「h1」等不會被誤改。
/// 混合情境(如孤立的 O 或字母→數字反向)留給 Tier 2 AI。
/// </summary>
internal sealed partial class DigitRunFixer : ITextProcessor
{
    // 連續的 [0-9OolI] 串(前後須為非英數,確保是獨立 token 的數字核心)
    [GeneratedRegex(@"(?<![A-Za-z0-9])[0-9OolI]+(?![A-Za-z0-9])")]
    private static partial Regex DigitishToken();

    public string Process(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return DigitishToken().Replace(input, static m =>
        {
            string token = m.Value;
            if (!token.Any(char.IsDigit))
            {
                return token; // 無真數字 → 可能是 IO、lol 之類,不動
            }

            var sb = new StringBuilder(token.Length);
            foreach (char c in token)
            {
                sb.Append(c switch
                {
                    'O' or 'o' => '0',
                    'l' or 'I' => '1',
                    _ => c,
                });
            }

            return sb.ToString();
        });
    }
}
