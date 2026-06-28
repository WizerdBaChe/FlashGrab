using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace FlashGrab.Ocr;

/// <summary>
/// Tier 0 地基引擎:Windows.Media.Ocr。全機型、離線、毫秒級。
/// 回傳逐行文字(保留行結構),供 Phase 2 的後處理 Pipeline 進一步清理。
/// </summary>
internal sealed class WindowsMediaOcr : IOcrEngine
{
    /// <summary>
    /// OCR 前的放大倍率。引擎對較大的字辨識率較佳,放大可救回部分小字誤判;
    /// 1 = 不放大。屬準確度緩解手段,非根治(根治需 Tier 2 AI 校正)。
    /// </summary>
    private const float PreScale = 2f;

    public async Task<string> RecognizeAsync(Bitmap bitmap)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
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

            // 逐行重建文字以保留行結構;每行內以「CJK 感知」規則決定詞間是否加空格,
            // 修正引擎把每個中文字當成獨立詞而插入空格的問題。
            // Phase 2 會接手更高階的斷行合併/標點/全半形清理。
            var sb = new StringBuilder();
            foreach (var line in result.Lines)
            {
                sb.AppendLine(BuildLineText(line));
            }

            return sb.ToString().TrimEnd();
        }
        finally
        {
            if (!ReferenceEquals(prepared, bitmap))
            {
                prepared.Dispose();
            }
        }
    }

    /// <summary>
    /// 把一行的詞重組成文字:僅當相鄰兩詞「兩側皆非 CJK」時才插入空格。
    /// → 中文字字相連、英文單字間保留空格、中英交界不加空格(如「按Win」)。
    /// </summary>
    private static string BuildLineText(OcrLine line)
    {
        var sb = new StringBuilder();
        string? prev = null;

        foreach (var word in line.Words)
        {
            string w = word.Text;
            if (w.Length == 0)
            {
                continue;
            }

            if (prev is not null && NeedsSpace(prev, w))
            {
                sb.Append(' ');
            }

            sb.Append(w);
            prev = w;
        }

        return sb.ToString();
    }

    private static bool NeedsSpace(string prev, string next)
    {
        char a = prev[^1];
        char b = next[0];
        return !IsCjk(a) && !IsCjk(b);
    }

    /// <summary>是否為不使用空格分詞的 CJK 字元(中日韓表意/假名/全形/CJK 標點)。</summary>
    private static bool IsCjk(char c)
    {
        int u = c;
        return (u >= 0x4E00 && u <= 0x9FFF)   // CJK 統一表意文字
            || (u >= 0x3400 && u <= 0x4DBF)   // 擴充 A
            || (u >= 0x3000 && u <= 0x303F)   // CJK 標點符號
            || (u >= 0xFF00 && u <= 0xFFEF)   // 全形字元
            || (u >= 0x3040 && u <= 0x30FF)   // 日文假名
            || (u >= 0xAC00 && u <= 0xD7AF);  // 韓文音節
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
