# FlashGrab ⚡

<<<<<<< HEAD
🇹🇼 **[繁體中文](README.zh-TW.md)** | **English**

> **Lightning-fast screen text extraction for Windows.**
>
> **Freeze. Grab. OCR. Copy.**
=======
> **Lightning-fast screen text extraction for Windows.**
>
> **Freeze. Grab. OCR. Copy.**

<p align="center">
  <!-- Replace with your banner later -->
  <img src="docs/banner.png" alt="FlashGrab Banner" width="900">
</p>

`Win + Shift + C` → **Freeze Frame** → **Offline OCR** → **Smart Cleanup** → **Clipboard**

FlashGrab is an ultra-lightweight Windows screen text extraction tool inspired by the simplicity of G-Helper.

Unlike traditional OCR utilities, FlashGrab freezes the screen **the moment you press the shortcut**, allowing you to capture subtitles, videos, terminals and other dynamic content without racing against the screen.

By default, everything runs **100% offline** using Windows' built-in OCR engine. For difficult scenes, simply hold **Shift** when releasing the mouse to enable optional AI Vision enhancement.
>>>>>>> ac2ea3cc99890db9fcdc6e8d0892a166b820ba6e

`Win + Shift + C` → Freeze Frame → Offline OCR → Smart Cleanup → Clipboard

<<<<<<< HEAD
FlashGrab is an ultra-lightweight Windows screen text extraction tool that freezes the screen instantly, extracts text using Windows' built-in OCR, restores clean formatting, and copies the result directly to your clipboard.

## ✨ Features

* ⚡ Instant Freeze Frame
* 📴 Offline Windows OCR
* 🇹🇼 Smart CJK spacing restoration
* 💻 Code indentation recovery
* 🧠 Optional AI Vision
* 📋 Automatic clipboard copy
* 🔒 Privacy-first (offline by default)
=======
## ✨ Features

* ⚡ Instant Freeze Frame capture
* 📴 100% Offline Windows OCR
* 🇹🇼 Smart CJK spacing restoration
* 💻 Automatic code indentation recovery
* 🧠 Optional AI Vision enhancement
* 📋 Automatic clipboard copy
* 🔒 Privacy-first design (no background network activity)
>>>>>>> ac2ea3cc99890db9fcdc6e8d0892a166b820ba6e

## 🚀 Why FlashGrab?

<<<<<<< HEAD
Unlike traditional OCR tools, FlashGrab freezes the screen **the moment you press the shortcut**.

Perfect for:
=======
## 🚀 Why FlashGrab?

Traditional OCR tools usually capture the screen **after** you finish selecting.

FlashGrab captures the screen **immediately when the shortcut is pressed**, so dynamic content stays exactly as you saw it.

Perfect for:

* Video subtitles
* Live streams
* Terminal output
* Presentations
* Notifications
* PDF viewers
* Documentation
* Source code
>>>>>>> ac2ea3cc99890db9fcdc6e8d0892a166b820ba6e

* Video subtitles
* Live streams
* Terminal output
* Presentations
* Documentation
* Source code

<<<<<<< HEAD
## ⌨️ Usage

| Action                     | Result            |
| -------------------------- | ----------------- |
| Win + Shift + C            | Freeze screen     |
| Drag                       | OCR selected area |
| Hold Shift while releasing | AI Vision         |
| Esc / Right Click          | Cancel            |

## 📦 Releases
=======
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

Hold **Shift** while releasing the mouse to replace the Offline OCR stage with AI Vision.
>>>>>>> ac2ea3cc99890db9fcdc6e8d0892a166b820ba6e

### FlashGrab.exe (Recommended)

<<<<<<< HEAD
* ~24 MB
* Requires .NET 8 Desktop Runtime
* Lowest memory usage

### FlashGrab-Portable.exe

* ~74 MB
* Self-contained
* No installation required

## 🔒 Privacy

FlashGrab is offline by default.

No screenshots, OCR results or user data are uploaded unless an AI provider is explicitly configured.

## 📄 License

Licensed under the GNU General Public License v3.0 (GPL-3.0).
=======
## 🧠 Recognition Pipeline

### Offline Mode (Default)

FlashGrab uses Windows' native `Windows.Media.Ocr` engine together with several post-processing stages designed specifically for developers and CJK languages.

### Smart CJK Cleanup

Windows OCR often inserts spaces between every Chinese character.

FlashGrab intelligently restores:

```
這 是 一 段 文 字
```

into

```
這是一段文字
```

while preserving proper spacing between Chinese and English.

---

### Code Indentation Recovery

FlashGrab estimates character width and reconstructs indentation levels automatically.

Useful for:

* C#
* C++
* Python
* JavaScript
* JSON
* YAML

and most monospace source code.

---

### Safe Token Guard

Safely normalizes OCR mistakes.

Examples:

```
ＯpenAI
```

↓

```
OpenAI
```

```
2O25
```

↓

```
2025
```

Corrections are only applied when they are considered safe, preventing accidental changes to identifiers or variable names.

---

## 🤖 AI Vision (Optional)

Need even better recognition?

Simply **hold Shift while releasing the mouse**.

FlashGrab will use an OpenAI-compatible Vision model instead of the offline OCR engine.

Recommended local setup:

* Ollama
* LM Studio

Supported cloud providers:

* OpenAI-compatible APIs
* Google Gemini
* NVIDIA NIM

AI mode is completely optional.

If no AI backend is configured, FlashGrab remains **100% offline**.

---

## ⌨️ Keyboard Shortcuts

| Action                         | Result                            |
| ------------------------------ | --------------------------------- |
| **Win + Shift + C**            | Freeze screen and start selection |
| Left Mouse Drag                | Select OCR region                 |
| Hold **Shift** while releasing | Use AI Vision                     |
| **Esc**                        | Cancel                            |
| Right Mouse Button             | Cancel                            |

---

## 📦 Releases

Two editions are available.

### FlashGrab.exe (Recommended)

* ~24 MB
* Requires .NET 8 Desktop Runtime
* Lowest memory usage
* Fastest startup

---

### FlashGrab-Portable.exe

* ~74 MB
* Self-contained
* No runtime required
* Portable and USB-friendly

---

## ⚙️ Configuration

Configuration file:

```
%AppData%\FlashGrab\settings.json
```

Example Ollama setup:

```bash
ollama run maternion/LightOnOCR-2
```

Once configured, simply hold **Shift** after selecting an area to invoke AI Vision.

---

## 🔒 Privacy

Privacy is a core design principle.

By default FlashGrab:

* Never uploads screenshots
* Never sends OCR data anywhere
* Never connects to the Internet
* Never requires an API key

Network access only occurs if you explicitly enable an AI provider.

---

## 📊 Comparison

| Feature                   | FlashGrab | PowerToys Text Extractor |
| ------------------------- | :-------: | :----------------------: |
| Freeze Frame Capture      |     ✅     |             ❌            |
| Offline OCR               |     ✅     |             ✅            |
| Smart CJK Cleanup         |     ✅     |             ❌            |
| Code Indentation Recovery |     ✅     |             ❌            |
| Optional AI Vision        |     ✅     |             ❌            |
| Portable Version          |     ✅     |             ❌            |

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

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**.

See the `LICENSE` file for details.
>>>>>>> ac2ea3cc99890db9fcdc6e8d0892a166b820ba6e
