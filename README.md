# FlashGrab ⚡️

> **G-Helper 級極致輕量、無安裝、開機即用的螢幕智慧取字桌面工具。**

`Win+Shift+C` 框選 ➡️ 智慧定格 ➡️ 離線 Windows OCR / AI 視覺雙階清理 ➡️ 乾淨文字秒進剪貼簿。

---

## 🚀 核心特色

### 1. 雙階智慧辨識架構 (Tiered OCR Pipeline)
- **Tier 0 + Tier 1 (預設離線模式)**：採用 Windows 原生 `Windows.Media.Ocr` 引擎，搭配專為亞洲語系打造的智慧清理 Pipeline。
  - **CJK 智慧空格消除**：根治微軟 OCR 中文逐字插空格的通病，自動識別中英文邊界，還原乾淨無瑕的中文段落。
  - **程式碼縮排還原**：自動計算畫面字寬中位數，精準還原程式碼的左側縮排層級（Indent）。
  - **嚴格 token 守衛**：自動修正全形英數為半形，並在純數字情境下安全修正 `O ➡️ 0` 或 `l ➡️ 1`，絕不誤改變數名稱。
- **Tier 2 (選配 AI 增強模式)**：框選放開時**按住 `Shift` 鍵**觸發。採用 OpenAI 相容通用視覺引擎：
  - **離線私密**：直連本地 **Ollama / LM Studio**（預設推薦 `LightOnOCR-2` 模型），100% 離線安全。
  - **雲端高速**：支援 **Gemini / NVIDIA NIM** 等 API 接頭，金鑰僅儲存於本地 `%AppData%`。
  - **完美根治**：專治低對比度、雜亂背景、影片黑邊白字、或是規則無法還原的標點符號與特殊箭頭。

### 2. 極致效能與 UX 體驗
- **定格截圖 (Freeze Frame)**：按鍵瞬間立刻凍結螢幕快照，徹底解決即時字幕、動態影片因框選時間差導致的殘影或掉字問題。
- **極輕量純淨常駐**：事件驅動設計，閒置時 CPU 使用率為 0%，常駐記憶體僅約 40MB~54MB。
- **無網路承諾**：AI 功能完全採取 Opt-in（主動加入）制，不設定 API Key 或不啟動 Ollama 時，100% 純離線運行，絕不暗中外傳隱私。

---

## 🛠 快捷鍵與操作說明

| 動作 | 觸發效果 | 適用場景 |
| :--- | :--- | :--- |
| **`Win + Shift + C`** | 觸發螢幕定格，進入變暗遮罩選區 | 開始取字 |
| **滑鼠左鍵拖曳** | 劃定取字範圍，放開即完成辨識 | 標準流程 |
| **滑鼠放開時「按住 `Shift`」** | 畫面出現橘框提示，改用 **Tier 2 AI 視覺模型** | 低對比、複雜字幕、程式碼 |
| **`Esc` 或 滑鼠右鍵** | 取消本次擷取 | 隨時退出 |

---

## 📦 產出版本說明 (Releases)

本工具免安裝，下載單一 `.exe` 檔案即可開箱即用。對齊開源輕量標竿，體積遠比動輒 300MB 的 PowerToys Text Extractor 更精簡：

1. **`FlashGrab.exe` (約 24MB，推薦)**
   - **依賴**：需安裝 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。
   - **優點**：與系統共用 Runtime，最省記憶體（Working Set ~57MB）。
2. **`FlashGrab-Portable.exe` (約 74MB)**
   - **依賴**：免任何前置環境，純單檔自含（Self-contained）。
   - **優點**：隨身碟隨插即用，適合封閉或無網路安裝環境。

---

## ⚙️ 設定與 AI 組態

首次啟動後，設定檔會自動生成於：
`%AppData%\FlashGrab\settings.json`

### Ollama 本地模型推薦設定
若要啟用本地免金鑰的 Tier 2 增強，請先確保本地 Ollama 已啟動，並安裝視覺模型：
```bash
ollama run maternion/LightOnOCR-2
