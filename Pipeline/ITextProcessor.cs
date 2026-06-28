namespace FlashGrab.Pipeline;

/// <summary>
/// 後處理 Pipeline 的一個 stage(文字 → 文字)。
/// 可串接,Tier 2 的 AI 校正將來也只是其中一個 stage。
/// </summary>
internal interface ITextProcessor
{
    string Process(string input);
}
