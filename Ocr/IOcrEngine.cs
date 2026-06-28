using System.Drawing;

namespace FlashGrab.Ocr;

/// <summary>
/// OCR 引擎抽象。讓 Tier 0(Windows.Media.Ocr)與未來 Tier 2(Copilot+ AI OCR)
/// 可互換,後處理 Pipeline 不需知道底層是哪個引擎。
/// 回傳結構化 <see cref="OcrDocument"/>(文字 + 幾何),不做文字成形。
/// </summary>
internal interface IOcrEngine
{
    /// <summary>對點陣圖辨識,回傳含邊界框的結構化結果。</summary>
    Task<OcrDocument> RecognizeAsync(Bitmap bitmap);
}
