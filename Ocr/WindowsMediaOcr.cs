using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace FlashGrab.Ocr;

/// <summary>
/// Tier 0 地基引擎:Windows.Media.Ocr。全機型、離線、毫秒級。
/// 回傳結構化文字 + 邊界框(已還原放大倍率至原始像素座標),
/// 文字成形(空格/縮排/正規化)一律交給後處理 Pipeline。
/// </summary>
internal sealed class WindowsMediaOcr : IOcrEngine
{
    /// <summary>
    /// OCR 前的放大倍率。引擎對較大的字辨識率較佳,放大可救回部分小字誤判;
    /// 1 = 不放大。屬準確度緩解手段,非根治(根治需 Tier 2 AI 校正)。
    /// </summary>
    private const float PreScale = 2f;

    private readonly string? _languageTag;

    /// <param name="languageTag">
    /// 指定辨識語言的 BCP-47 標籤(如 "zh-Hant"、"en");null 則用使用者設定檔語言。
    /// </param>
    public WindowsMediaOcr(string? languageTag = null)
    {
        _languageTag = languageTag;
    }

    /// <summary>目前系統可用的 OCR 辨識語言。供設定 UI 列出選項。</summary>
    public static IReadOnlyList<Language> AvailableLanguages => OcrEngine.AvailableRecognizerLanguages;

    public async Task<OcrDocument> RecognizeAsync(Bitmap bitmap)
    {
        OcrEngine? engine = CreateEngine();
        if (engine is null)
        {
            throw new InvalidOperationException(
                "找不到可用的 OCR 語言包。請於「設定 → 時間與語言 → 語言」新增中文/英文語言包。");
        }

        Bitmap prepared = PreScale > 1f ? Upscale(bitmap, PreScale) : bitmap;
        try
        {
            using var softwareBitmap = await ToSoftwareBitmapAsync(prepared);
            var result = await engine.RecognizeAsync(softwareBitmap);

            var lines = new List<OcrTextLine>(result.Lines.Count);
            foreach (var line in result.Lines)
            {
                var words = new List<OcrTextWord>(line.Words.Count);
                foreach (var word in line.Words)
                {
                    if (word.Text.Length == 0)
                    {
                        continue;
                    }

                    var r = word.BoundingRect;
                    // 邊界框是「放大後」座標,除以倍率還原至原始點陣圖像素
                    var bounds = new RectangleF(
                        (float)(r.X / PreScale),
                        (float)(r.Y / PreScale),
                        (float)(r.Width / PreScale),
                        (float)(r.Height / PreScale));
                    words.Add(new OcrTextWord(word.Text, bounds));
                }

                if (words.Count > 0)
                {
                    lines.Add(new OcrTextLine(words));
                }
            }

            return new OcrDocument(lines);
        }
        finally
        {
            if (!ReferenceEquals(prepared, bitmap))
            {
                prepared.Dispose();
            }
        }
    }

    private OcrEngine? CreateEngine()
    {
        if (_languageTag is not null)
        {
            var lang = new Language(_languageTag);
            var engine = OcrEngine.TryCreateFromLanguage(lang);
            if (engine is not null)
            {
                return engine;
            }
            // 指定語言不可用時退回使用者設定檔語言,避免直接失敗
        }

        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    /// <summary>以高品質雙三次內插放大,提升小字辨識率。</summary>
    private static Bitmap Upscale(Bitmap source, float factor)
    {
        int w = (int)(source.Width * factor);
        int h = (int)(source.Height * factor);
        var dest = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(dest);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.DrawImage(source, 0, 0, w, h);

        return dest;
    }

    /// <summary>
    /// System.Drawing.Bitmap → WinRT SoftwareBitmap。
    /// 走 PNG → InMemoryRandomAccessStream → BitmapDecoder,避免依賴
    /// .NET Core 已移除的 AsRandomAccessStream 互通擴充。
    /// </summary>
    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        byte[] bytes = ms.ToArray();

        var ras = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(ras))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        ras.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(ras);
        return await decoder.GetSoftwareBitmapAsync();
    }
}
