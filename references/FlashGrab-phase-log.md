# FlashGrab — Phase Log

> 螢幕智慧取字桌面工具。新 session 接手請**只讀本檔**重建上下文。
> 完整設計藍圖:`C:\Users\gunda\.claude\plans\rippling-whistling-frog.md`

---

# Phase Checkpoint
- Project: FlashGrab
- Phase: Phase 0 – 骨架(喚醒層 + 常駐)
- Status: completed
- Date: 2026-06-28

## Goals
- 驗證產品概念可行性,並把基礎架構定案。
- 建立可常駐、可喚醒的 WinForms 骨架(系統匣 + 全域快捷鍵 + 結束)。

## Decisions
- **定位**:第 1~3 層(快捷鍵→遮罩→Windows OCR→剪貼簿)已被 PowerToys Text Extractor 驗證且免費;差異化集中在「第 4 層智慧後處理」+「G-Helper 級極致輕量」。使用者選「兩者並重」。
- **技術棧**:C# / .NET 8 + WinForms(WinRT OCR 互通最順、封裝最小)。
- **TFM**:`net8.0-windows10.0.19041.0`,自動啟用 WinRT 投影 → Phase 1 直接用 `Windows.Media.Ocr`,無需第三方 OCR 套件。
- **第 4 層 AI 三階分層**(已查證鎖定):
  - Tier 0 地基 = `Windows.Media.Ocr.OcrEngine`(全機型、離線)。
  - Tier 1 預設 = 純離線啟發式後處理(標準模式 + 程式碼模式用邊界框 X 座標推算縮排)。
  - Tier 2 選配 = Phi Silica / AI TextRecognizer(**強制需 Copilot+ PC/NPU**,偵測到才啟用)或自備雲端 API key(預設關,因違反「無網路」承諾)。
- **DPI**:PerMonitorV2,為 Phase 1 多螢幕框選座標正確鋪路。
- **捨棄**:Rust/WPF(較重或工期長);VS 工作負載安裝(改裝獨立 SDK,較輕)。

## Changes
- 環境:winget 安裝 .NET 8 SDK `8.0.422`(原本只有 runtime + VS2022,缺 SDK)。Windows SDK `10.0.26100` 已具備。
- `FlashGrab.csproj`: WinExe / net8.0-windows10.0.19041.0 / PerMonitorV2。
- `Program.cs`: 單一實例 Mutex + ApplicationContext 啟動。
- `Trigger/GlobalHotkey.cs`: RegisterHotKey(Win+Shift+C)+ 隱藏訊息視窗收 WM_HOTKEY,MOD_NOREPEAT 防連發。
- `App/TrayApplicationContext.cs`: 無主視窗、系統匣圖示、右鍵選單(關於/結束)、快捷鍵占位回饋。
- `.gitignore`: 忽略 bin/obj/.vs/publish。
- Git: repo 初始化於 `D:\AIWork\FlashGrab`,首個 commit `a5ff1d3` (chore: scaffold FlashGrab phase 0) on `main`。

## Verification
- `dotnet build`:0 警告 0 錯誤。
- 執行:常駐成功(RAM ~40MB debug/framework-dependent;閒置 CPU≈0)。Win+Shift+C 觸發占位氣泡正常。

## Open Questions / TODO
- **下一步 = Phase 1 取字 MVP**:透明全螢幕遮罩框選 → 多螢幕/DPI 座標 → Tier 0 `Windows.Media.Ocr` → 寫剪貼簿 + 通知音效。
- Phase 1 起的功能性工作改走 feature branch + 語意化 commit(首個 scaffold commit 已直接在 main)。
- 體積/閒置資源最佳化留待 Phase 3(self-contained + trim)。
- 待確認:預設辨識語言清單(中英)、OCR 語言包未安裝時的提示流程。

---

# Phase Checkpoint
- Project: FlashGrab
- Phase: Phase 1 – 取字 MVP(框選 → 截圖 → Tier 0 OCR → 剪貼簿)
- Status: completed(功能已實機驗證;跨螢幕/混合 DPI 待下次測)
- Date: 2026-06-28

## Goals
- 打通端到端最短資料路徑:Win+Shift+C → 遮罩框選 → 截圖 → Tier 0 OCR → 剪貼簿 + 音效。
- 從一開始就用 `IOcrEngine` 介面隔離 OCR,讓 Tier 2 將來可替換。

## Decisions
- **OCR 文字重建在引擎 adapter 做、清理留給 Pipeline**:adapter(WindowsMediaOcr)負責「忠實還原文字」(CJK 感知空格),Phase 2 的 Pipeline 才做高階美化(標點/斷行合併/全半形)。職責分離。
- **CJK 空格修正**:`Windows.Media.Ocr` 對每個「詞」插空格,中文逐字成詞 → 字字有空格。改為逐詞重組,僅「相鄰兩詞兩側皆非 CJK」才加空格。修正中文空格、且不影響 `Win+Shift+C`、`按Win` 交界。
- **OCR 前放大 2×**(HighQualityBicubic):緩解小字誤判,非根治。
- **座標**:程序 PerMonitorV2 DPI-aware → client/螢幕座標=物理像素;`PointToScreen` + `VirtualScreen` 取框。單一 DPI 正確;混合 DPI 跨螢幕單一遮罩為 WinForms 已知難點,**留待下次有多螢幕環境驗證**。
- **截圖前延遲 60ms** 讓遮罩消失再 `CopyFromScreen`,避免截到殘影。
- **Tier 1/Tier 2 分界經實測確認**:殘留錯誤分三類——A 全半形(規則可安全修)、B 數字↔字母 O/0/l/1(僅純數字或純字母串可安全修,混合情境需 AI)、C 資訊遺失型(箭頭、引號被認成 J,規則無法還原)。**B 混合與 C 類明確歸 Tier 2 AI(Phase 4),不對 Tier 1 過度承諾。**

## Changes
- `Ocr/IOcrEngine.cs`: OCR 抽象介面(Tier 可插拔)。
- `Ocr/WindowsMediaOcr.cs`: Tier 0 引擎;Bitmap→SoftwareBitmap 走 PNG+InMemoryRandomAccessStream(.NET Core 無 AsRandomAccessStream);CJK 感知空格 + 2× 放大。
- `Capture/OverlayForm.cs`: virtual-desktop 半透明遮罩、十字準心橡皮筋、Esc/右鍵取消、回傳螢幕座標。
- `Capture/ScreenGrabber.cs`: `CopyFromScreen` 物理像素截圖。
- `Output/ClipboardWriter.cs`: 剪貼簿寫入 + 鎖定重試。
- `App/TrayApplicationContext.cs`: 串接流程 + 防重入(`_capturing`)。
- Git: branch `feat/phase1-capture-ocr`,commit `e356564` (feat(capture): phase 1 pipeline)。

## Verification
- `dotnet build`:0 警告 0 錯誤。常駐 RAM ~40MB、閒置 CPU≈0。
- 實機:變暗/框選/音效/氣泡/貼上皆正常;Esc/右鍵取消正常;瀏覽器 500% 放大截圖通過;CJK 空格問題已消除。
- 殘留(非 Phase 1 bug,屬 Tier 0 天花板):B 混合 O/0、C 類箭頭與引號→J。

## Open Questions / TODO
- **下一步 = Phase 2 討論**:Tier 1 安全規則(全半形正規化 + 純數字串 O→0/l→1)+ 程式碼模式(邊界框 X 推算縮排 + 規則符號修正);設定頁切換模式/語言。
- **跨螢幕 / 混合 DPI 截取**:留待下次有多螢幕環境驗證;若錯位,改用 P/Invoke 強制物理像素定位。
- C 類與混合 B 類字元錯誤的根治 = Phase 4 Tier 2 AI(本機 Phi Silica 需 Copilot+;雲端 API 須 opt-in)。
