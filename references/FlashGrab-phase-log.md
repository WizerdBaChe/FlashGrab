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

---

# Phase Checkpoint
- Project: FlashGrab
- Phase: Phase 2 – 智慧後處理層(結構化 OCR + Pipeline + 定格截圖)
- Status: completed(實機驗證通過)
- Date: 2026-06-29

## Goals
- 把 Tier 0 逐行文字升級成乾淨的結構化資料,預設忠實保留畫面結構。
- OCR 引擎只暴露幾何,文字成形全移到可插拔 Pipeline。
- 解決即時字幕/影片瞬間擷取。

## Decisions
- **反轉模式框架(第一性原理)**:預設「忠實模式」(保留行序/斷行 + 縮排還原 + 安全清理);「段落重排」降為罕用選配(tray 勾選)。理由:畫面行序通常即理解順序,而「合併斷行」是唯一會猜錯、會破壞資訊的動作 → 不該綁在每次取字。等於原「程式碼模式」變成預設,不需切換。
- **結構化 OCR**:IOcrEngine 改回傳 OcrDocument(行→詞→邊界框,原始像素)。CJK 重組從 adapter 上移到 Pipeline(修正 Phase 1 「重建在 adapter」的決策)。
- **定格截圖**:按鍵瞬間凍結 virtual screen,遮罩疊在快照上、選區外變暗、從快照裁切。解決即時字幕(擷取結果與框選耗時無關)+ 消除 60ms 延遲 + 消殘影。遮罩 client 座標即快照像素,免 PointToScreen。
- **Tier 1 只放「絕對安全」規則**:全形英數→半形(**不碰** CJK 標點,否則破壞中文)、純數字串 O→0/l→1(嚴格 token 守衛:須含真數字且整段可成合法數字)。需語意脈絡者(中文標點↔ASCII、混合 O/0、箭頭)一律留 Tier 2,不過度承諾。
- **縮排還原**:以所有詞「每字平均寬度」中位數估字寬;首詞左緣相對 baseLeft 換算空格數(門檻 0.6 字寬、上限 60)。左對齊文字縮排=0(無害),程式碼自然得縮排。

## Changes
- `Ocr/OcrModels.cs`: OcrDocument / OcrTextLine / OcrTextWord。
- `Ocr/IOcrEngine.cs`: 回傳 OcrDocument。
- `Ocr/WindowsMediaOcr.cs`: 結構化輸出、邊界框除以 2× 還原原始座標、可指定辨識語言(TryCreateFromLanguage,退回設定檔)。
- `Pipeline/`: TextRules(CJK/終結標點)、ITextProcessor、LineReconstructor(CJK 空格+縮排+段落重排)、FullWidthNormalizer、DigitRunFixer、TextPipeline。
- `Capture/ScreenGrabber.cs`: CaptureVirtualScreen + Crop。
- `Capture/OverlayForm.cs`: 吃快照當背景、選區外變暗、回傳快照座標矩形。
- `App/Settings.cs`: %AppData%\FlashGrab\settings.json(重排開關 + 語言標籤)。
- `App/TrayApplicationContext.cs`: 串接流程、重排勾選、語言子選單、定格擷取。
- Git: branch `feat/phase2-pipeline`,commit `f27b0ce`。

## Verification
- `dotnet build`:0 警告 0 錯誤。常駐 RAM ~54MB(debug/framework-dependent)。
- 實機(使用者驗收):定格字幕擷取正確、變暗 UX、縮排還原、純數字 O/0 修正(且 lol/100GB/變數不誤改)、段落重排開關、語言自動偵測皆通過。

## Open Questions / TODO
- **下一步 = Phase 3 輕量封裝**:self-contained 單檔 + trim,量測 exe 體積與閒置 CPU/RAM,對齊「G-Helper 級」。Trim 可能裁掉 WinRT 互通型別 → 先 framework-dependent 驗證再逐步開 trim + TrimmerRootDescriptor。
- **確認的邊界(Tier 0 天花板,非 bug)**:低對比 / 雜亂背景(尤其影片黑邊白字壓灰底)辨識掉字。根治留 Phase 4 Tier 2 AI;**不**在 Tier 1 塞脆弱影像前處理(對雜背景無效且傷乾淨文字)。
- **已知小邊界**:置中文字各行左緣不一,縮排還原可能產生假縮排(忠實座標副作用)。
- **跨螢幕 / 混合 DPI**:仍待多螢幕環境驗證。

---

# Phase Checkpoint
- Project: FlashGrab
- Phase: Phase 3 – 輕量封裝(發行成品)
- Status: completed
- Date: 2026-06-29

## Goals
- 兌現「G-Helper 級、無安裝、開機即用」:產出單一 exe 發行檔,量測體積與閒置資源。

## Decisions
- **trim 出局**:WinForms 啟用 trim 直接被 SDK 硬擋(NETSDK1175,官方不支援);強開要用未支援 hack 且極可能弄壞 WinRT OCR 載入。故自含體積極限就是壓縮後 ~74MB。
- **兩種成品都出,fd 為主推**:G-Helper 本身就是 framework-dependent(官方拒出自含,理由=體積暴增、.NET 8 LTS 多已安裝)。對齊之,主推 fd;另附自含給無 runtime 者。
  - `FlashGrab.exe`:framework-dependent 單檔,24.4MB,需 .NET 8 Desktop Runtime。
  - `FlashGrab-Portable.exe`:自含 + 壓縮 + IncludeNativeLibrariesForSelfExtract → 真・單一 exe,74.3MB,無前置。
- **模型永不打包**:Tier 2 為能力偵測/外掛(Phi Silica 屬 Windows、雲端僅 API key)。主程式不因功能膨脹(未來加的是 KB 級程式碼)。**未來模型或有「非內建資源」可用 → 後續討論。**
- 對手定位:PowerToys 安裝包約 270–343MB(整套件),本工具兩版皆遠輕(1/4 ~ 1/14)。

## Changes
- `publish.ps1`: 一鍵產出兩種單檔成品到 publish\dist(DebugType=none 去 pdb)。
- Git: branch `feat/phase3-packaging`,commit `1fe4263`。

## Verification
- `dotnet publish` 兩版皆 0 錯誤;產出真・單一 exe。
- 實機啟動:兩版皆正常常駐,閒置 CPU≈0(事件驅動)。
  - fd:Working Set ~57MB / Private ~11MB(共用系統 runtime,最省)。
  - portable:Working Set ~145MB / Private ~51MB(單檔解壓到記憶體所致)。
- 單一實例 mutex 正常(同時啟動第二個會乾淨退出 0)。

## Open Questions / TODO
- **下一步 = Phase 4 選配 AI(Tier 2)**:能力偵測;Copilot+ 走 Phi Silica/AI OCR 根治低對比與字元錯誤,其他機型提供雲端 API key(預設關)。
- **模型「非內建資源」方案**:後續討論(可能透過工具推送下載依賴,而非內建)。
- **跨螢幕 / 混合 DPI**:仍待多螢幕環境驗證。
- 發行檔未簽章 → 首次執行可能觸發 SmartScreen;簽章為將來發佈議題。
