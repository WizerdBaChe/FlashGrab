# FlashGrab ⚡

🇹🇼 **[繁體中文](README.zh-TW.md)** | **English**

> **Lightning-fast screen text extraction for Windows.**
>
> **Freeze. Grab. OCR. Copy.**

`Win + Shift + C` → **Freeze Frame** → **Offline OCR** → **Smart Cleanup** → **Clipboard**

---

FlashGrab is an ultra-lightweight Windows screen text extraction tool inspired by the simplicity of G-Helper. 

Unlike traditional OCR utilities, FlashGrab freezes the screen **the moment you press the shortcut**, allowing you to capture subtitles, videos, terminals, and other dynamic content without racing against the screen.

By default, everything runs **100% offline** using Windows' built-in OCR engine. For difficult scenes, simply hold **Shift** when releasing the mouse to enable optional AI Vision enhancement.

---

## ✨ Features

* ⚡ **Instant Freeze Frame** capture
* 📴 **100% Offline** Windows OCR
* 🇹🇼 **Smart CJK** spacing restoration
* 💻 Automatic **code indentation** recovery
* 🧠 Optional **AI Vision** enhancement (Ollama / Gemini / NIM)
* 📋 Automatic clipboard copy
* 🔒 **Privacy-first** design (no background network activity)

---

## 🚀 Why FlashGrab?

Traditional OCR tools usually capture the screen *after* you finish selecting, which causes issues with moving text. 

FlashGrab captures the screen **immediately when the shortcut is pressed**, so dynamic content stays exactly as you saw it.

**Perfect for:**
* Video subtitles & Live streams
* Terminal output & Notifications
* Presentations & PDF viewers
* Documentation & Source code

---

## ⚙️ How It Works

```
Win + Shift + C
       │
       ▼
 Freeze Frame
       │
       ▼
  Offline OCR
       │
       ▼
 Smart Cleanup
       │
       ▼
   Clipboard
```
*Hold **Shift** while releasing the mouse to replace the Offline OCR stage with AI Vision.*

---

## 🧠 Recognition Pipeline

### Offline Mode (Default)
FlashGrab uses Windows' native `Windows.Media.Ocr` engine together with several post-processing stages designed specifically for developers and CJK languages.

#### 1. Smart CJK Cleanup
Windows OCR often inserts spaces between every Chinese character. FlashGrab intelligently restores:
```
這 是 一 段 文 字
```
↓
```
這是一段文字
```
*while preserving proper spacing between Chinese and English words.*

#### 2. Code Indentation Recovery
FlashGrab estimates character width and reconstructs indentation levels automatically. Highly useful for structured or monospace source code like C#, C++, Python, JavaScript, JSON, and YAML.

#### 3. Safe Token Guard
Safely normalizes common OCR character mistakes under strict verification:
* `ＯpenAI` → `OpenAI`
* `2O25` → `2025`

---

## 🤖 AI Vision (Optional)

Need even better recognition for low-contrast text or complex watermarks? Simply **hold Shift while releasing the mouse**. FlashGrab will use an OpenAI-compatible Vision model instead of the offline OCR engine.

* **Recommended Local Setup**: Ollama (e.g., `maternion/LightOnOCR-2`), LM Studio.
* **Supported Cloud Providers**: Google Gemini, NVIDIA NIM, or any OpenAI-compatible Vision endpoint.

*AI mode is completely optional. If no AI backend is configured, FlashGrab remains **100% offline**.*

---

## ⌨️ Keyboard Shortcuts

| Action | Result |
| :--- | :--- |
| **Win + Shift + C** | Freeze screen and start selection |
| **Left Mouse Drag** | Select OCR region |
| **Hold Shift while releasing** | Trigger Tier 2 AI Vision |
| **Esc** or **Right Click** | Cancel selection and exit |

---

## 📦 Releases

Two editions are available under the Releases page:

### FlashGrab.exe (Recommended)
* ~24 MB
* Requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
* Lowest memory usage & fastest startup

### FlashGrab-Portable.exe
* ~74 MB
* Self-contained (No runtime required)
* Portable and USB-friendly

> **No installer needed — just double-click the `.exe`.**
> FlashGrab starts **directly in the background** (system tray, bottom-right). A toast confirms *"Running in the background — press Win + Shift + C"*, and a one-time welcome window appears on first launch. There is **no main window**; right-click the tray icon for settings. Running it again while it's already open just shows a reminder instead of opening a second copy.

---

## ⚙️ Configuration

Configuration file is automatically created at:
```
%AppData%\FlashGrab\settings.json
```
For local Ollama setup, ensure your model is running before executing the AI grab:
```bash
ollama run maternion/LightOnOCR-2
```

---

## 🔒 Privacy

Privacy is a core design principle. By default, FlashGrab **never** uploads screenshots, **never** sends OCR data anywhere, and **never** requires internet access. Network requests only occur if you explicitly configure and opt-in to a cloud AI provider.

---

## 📊 Comparison

| Feature | FlashGrab | PowerToys Text Extractor |
| :--- | :---: | :---: |
| **Freeze Frame Capture** | ✅ | ❌ |
| **Offline OCR** | ✅ | ✅ |
| **Smart CJK Cleanup** | ✅ | ❌ |
| **Code Indentation Recovery** | ✅ | ❌ |
| **Optional AI Vision** | ✅ | ❌ |
| **Portable Version** | ✅ | ❌ |

---

## 🛠 Roadmap

* ✅ Freeze Frame capture
* ✅ Offline OCR
* ✅ Smart CJK cleanup
* ✅ Code indentation recovery
* ✅ AI Vision enhancement
* ⬜ Translation
* ⬜ Table recognition
* ⬜ Batch OCR

---

## 📄 License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**. See the `LICENSE` file for details.
