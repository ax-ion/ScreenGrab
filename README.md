# ScreenGrab

Select any text on your screen via OCR overlay. Like Apple Live Text for Windows.

Press a hotkey, drag to highlight any visible text (images, videos, apps, PDFs, thumbnails), and copy it to your clipboard.

## How It Works

1. **Ctrl+Shift+T** captures a screenshot of your current monitor
2. EasyOCR detects all text and their positions
3. A fullscreen overlay appears (looks identical to your screen) with an invisible selectable text layer
4. **Drag to highlight** the text you want
5. **Ctrl+C** copies selected text and closes the overlay
6. **Escape** closes without copying

## Prerequisites

- Windows 10+ (build 19041 or later)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Python 3.10+
- EasyOCR: `pip install easyocr`

## Setup

```bash
git clone https://github.com/ax-ion/ScreenGrab.git
cd ScreenGrab
pip install easyocr
dotnet build
```

## Usage

```bash
dotnet run
```

The app runs in the system tray. First launch downloads OCR models (~30MB, cached after that).

## Project Structure

```
ScreenGrab/
├── App.xaml / .cs           # Application entry point, system tray, lifecycle
├── ScreenGrab.csproj        # Project configuration
├── ScreenGrab.sln           # Solution file
├── Windows/
│   └── OverlayWindow.xaml / .cs  # Fullscreen overlay with drag-select
├── Services/
│   ├── CaptureService.cs    # Multi-monitor screenshot capture
│   ├── HotkeyService.cs     # Global Ctrl+Shift+T hotkey via Win32
│   └── OcrService.cs        # Python sidecar process management
└── Scripts/
    └── ocr_server.py        # Persistent EasyOCR server (stdin/stdout)
```

## Architecture

The app is a C#/WPF system tray application that communicates with a persistent Python process for OCR:

- **C# (WPF)** handles the UI: global hotkey, screen capture, overlay rendering, text selection
- **Python (EasyOCR)** handles OCR: the process stays warm to avoid cold starts, communicates via stdin/stdout JSON

EasyOCR uses CRAFT text detection + CRNN recognition (deep learning), which handles diverse fonts, symbols, and small text significantly better than Windows built-in OCR or Tesseract.
