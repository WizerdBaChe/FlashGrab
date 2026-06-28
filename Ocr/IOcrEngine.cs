using System.Drawing;

namespace FlashGrab.Ocr;

/// <summary>
/// OCR 引擎抽象。讓 Tier 0(Windows.Media.Ocr)與未來 Tier 2(Copilot+ AI OCR)
/// 可互換,後處理 Pipeline 不需知道底層是哪個引擎。
/// </summary>
internal interface IOcrEngine
{
    /// <summary>對點陣圖辨識,回傳保留行結構的純文字。</summary>
    Task<string> RecognizeAsync(Bitmap bitmap);
}
