# FlashGrab ⚡

**繁體中文** | 🇺🇸 **[English](README.md)**

> **極輕量、免安裝的 Windows 螢幕智慧取字工具。**
>
> **Freeze. Grab. OCR. Copy.**

`Win + Shift + C` → 定格畫面 → Windows 原生 OCR → 智慧清理 → 剪貼簿

FlashGrab 是一款專注於速度、準確度與隱私的 Windows 螢幕取字工具。

按下快捷鍵的瞬間立即凍結畫面，透過 Windows 原生 OCR 完成辨識，再經過智慧後處理，將乾淨文字直接複製到剪貼簿。

## ✨ 功能特色

* ⚡ 瞬間定格畫面（Freeze Frame）
* 📴 Windows 原生離線 OCR
* 🇹🇼 CJK 中文智慧空格修正
* 💻 程式碼縮排自動還原
* 🧠 選配 AI Vision 增強辨識
* 📋 自動複製到剪貼簿
* 🔒 預設完全離線運作

## 🚀 為什麼選擇 FlashGrab？

與傳統 OCR 工具不同，FlashGrab 在按下快捷鍵的瞬間就完成畫面定格，因此不必擔心字幕、終端機輸出或通知訊息在框選過程中消失。

適用於：

* 影片字幕
* 線上會議
* Live Stream
* Terminal
* PDF
* 程式碼
* 技術文件

## ⌨️ 操作方式

| 操作              | 功能        |
| --------------- | --------- |
| Win + Shift + C | 定格畫面並開始框選 |
| 滑鼠拖曳            | OCR 辨識    |
| 放開滑鼠時按住 Shift   | AI Vision |
| Esc / 右鍵        | 取消        |

## 📦 發行版本

### FlashGrab.exe（推薦）

* 約 24 MB
* 需安裝 .NET 8 Desktop Runtime
* 記憶體占用最低

### FlashGrab-Portable.exe

* 約 74 MB
* Self-contained
* 解壓即用

> **免安裝 —— 雙擊 `.exe` 即可。**
> FlashGrab 會**直接進入背景執行**(常駐系統匣，右下角)。啟動時右下角會跳一則 toast 通知「已在背景執行 · 按 Win + Shift + C」，首次執行還會出現一次性的歡迎視窗。本程式**沒有主視窗**，所有設定都在「右鍵托盤圖示」選單裡。若已在執行中又再次雙擊，只會跳出提示而不會開出第二個程式。

## 🔒 隱私承諾

預設完全離線。

除非使用者自行設定 AI Provider，否則不會連線、不會上傳畫面，也不會收集任何資料。

### API 金鑰的存放方式

若你設定了雲端 AI Provider，金鑰會先經 **Windows DPAPI（`CurrentUser` 範圍）** 加密才寫入
`%AppData%\FlashGrab\settings.json`；檔案裡只有密文，欄位是 `Tier2ApiKeyProtected`。

這道保護的範圍要講清楚，不要高估：

* ✅ 設定檔被複製到別台機器、別的 Windows 帳號、備份或雲端同步資料夾，拿到的只是一團無法解開的
  密文；FlashGrab 會直接當成「未設定金鑰」。
* ✅ 同一台電腦上的其他本機帳號無法從檔案讀出金鑰。
* ❌ **擋不住以你自己這個 Windows 使用者身分執行的程式碼。** 任何用你的帳號跑起來的程式，都能呼叫
  跟你一樣的解密 API。DPAPI 保護的是「檔案落地後的靜態資料」，不是本機惡意程式。

v0.4.1 以前的設定檔是以明文存放金鑰，首次啟動會自動遷移：就地改為加密、清掉明文欄位，**你不需要
重新輸入金鑰**。但因為該金鑰確實曾以明文躺在磁碟上，遷移會記錄在
`%AppData%\FlashGrab\security.log`，並**建議你到供應商後台重新產生一組**。

## 📄 授權

本專案採用 GNU General Public License v3.0（GPL-3.0）。
