using System.Drawing;

namespace FlashGrab.Ocr;

/// <summary>
/// 結構化 OCR 結果。引擎只忠實暴露「文字 + 幾何座標」,
/// 所有文字成形(空格、縮排、斷行、正規化)交給 Pipeline 處理。
/// </summary>
internal sealed record OcrDocument(IReadOnlyList<OcrTextLine> Lines)
{
    public static readonly OcrDocument Empty = new(Array.Empty<OcrTextLine>());

    /// <summary>
    /// 非幾何引擎(Tier 2 VLM)直接回傳的「已成形文字」。
    /// 設定時 Pipeline 略過幾何行重建與安全規則,原樣輸出(VLM 已忠實成形,額外規則反而可能改壞)。
    /// </summary>
    public string? PreformattedText { get; init; }
}

/// <summary>一行 OCR 結果:依閱讀順序排列的詞。</summary>
internal sealed record OcrTextLine(IReadOnlyList<OcrTextWord> Words);

/// <summary>
/// 一個 OCR 詞:文字 + 邊界框(原始點陣圖像素座標,已還原放大倍率)。
/// 邊界框供 Pipeline 推算縮排層級。
/// </summary>
internal sealed record OcrTextWord(string Text, RectangleF Bounds);
