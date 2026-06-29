using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FlashGrab.Ocr;

/// <summary>
/// Tier 2(選配,預設關):OpenAI 相容 /chat/completions「視覺」端點的 OCR 引擎。
/// 一套程式碼涵蓋全部後端,差別只在 {baseUrl, apiKey, model}:
///   • 本地 Ollama / LM Studio(http://localhost:.../v1) → 離線、私密、跑在使用者 GPU。
///   • 免費/付費雲端(Gemini、NVIDIA NIM、Mistral…) → 不需 GPU,根治低對比/糊字誤讀。
/// 把截圖以 base64 送出,要求「忠實逐字轉錄」,回傳成形文字(PreformattedText);
/// Pipeline 會略過幾何行重建,原樣採用。
/// </summary>
internal sealed class OpenAiCompatibleVisionOcr : IOcrEngine
{
    private const string Prompt =
        "You are a precise OCR engine. Transcribe ALL text visible in the image exactly as it appears. " +
        "Preserve the original line breaks, reading order, and leading indentation (use spaces). " +
        "Do NOT translate, summarize, explain, or wrap the output in markdown or code fences. " +
        "Output only the raw transcribed text. If the image contains no text, output nothing.";

    // 共用單一 HttpClient(避免 socket 耗盡);取字屬偶發、低頻,逾時設寬一點容納雲端/本地冷啟。
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _endpoint;
    private readonly string? _apiKey;
    private readonly string _model;

    public OpenAiCompatibleVisionOcr(string baseUrl, string? apiKey, string model)
    {
        _endpoint = baseUrl.TrimEnd('/') + "/chat/completions";
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _model = model;
    }

    public async Task<OcrDocument> RecognizeAsync(Bitmap bitmap)
    {
        string dataUrl = "data:image/png;base64," + ToBase64Png(bitmap);

        var payload = new
        {
            model = _model,
            temperature = 0,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = Prompt },
                        new { type = "image_url", image_url = new { url = dataUrl } },
                    },
                },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        if (_apiKey is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var response = await Http.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AI 端點回應 {(int)response.StatusCode}:{Truncate(body, 300)}");
        }

        string text = StripFences(ExtractContent(body));
        return new OcrDocument(Array.Empty<OcrTextLine>()) { PreformattedText = text };
    }

    private static string ToBase64Png(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>解析 OpenAI 相容回應的 choices[0].message.content(可為字串或分段陣列)。</summary>
    private static string ExtractContent(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content");

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var t))
                {
                    sb.Append(t.GetString());
                }
            }
            return sb.ToString();
        }

        return string.Empty;
    }

    /// <summary>模型偶爾仍會把輸出包進 ``` 圍欄,移除首尾圍欄行(保守:僅當整段被圍欄包住)。</summary>
    private static string StripFences(string text)
    {
        string trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var lines = trimmed.Split('\n');
        if (lines.Length < 2 || !lines[^1].TrimEnd().EndsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        // 去掉第一行(```lang)與最後一行(```)
        return string.Join('\n', lines[1..^1]);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
