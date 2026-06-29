using FlashGrab.Ocr;

namespace FlashGrab.Pipeline;

/// <summary>
/// 後處理 Pipeline 編排:OcrDocument → 還原文字 → 串接各 stage → 最終文字。
/// 預設只放「規則絕對安全」的 stage(全形英數正規化、純數字串字母修正);
/// 需語意脈絡的修正(中文標點↔ASCII、混合 O/0、箭頭等)留給 Tier 2(Phase 4)。
/// </summary>
internal sealed class TextPipeline
{
    private readonly IReadOnlyList<ITextProcessor> _stages;

    public TextPipeline(params ITextProcessor[] stages)
    {
        _stages = stages;
    }

    /// <summary>Phase 2 預設 Pipeline。</summary>
    public static TextPipeline CreateDefault() => new(
        new FullWidthNormalizer(),
        new DigitRunFixer());

    public string Run(OcrDocument document, bool reflowParagraphs)
    {
        // Tier 2 VLM 已回傳成形文字:直接採用,不經幾何重建與安全規則(避免改壞)。
        if (document.PreformattedText is not null)
        {
            return document.PreformattedText.TrimEnd();
        }

        string text = LineReconstructor.Build(document, reflowParagraphs);

        foreach (var stage in _stages)
        {
            text = stage.Process(text);
        }

        return text.TrimEnd();
    }
}
