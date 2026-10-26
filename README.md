# Meaning Overlay

A Windows application that explains anything you copy — just press **Ctrl+Shift+M**.

## Quick Start

1. **Build** the application:
   ```
   dotnet build MeaningOverlay.csproj --configuration Release
   ```

2. **Run** it:
   ```
   dotnet run
   ```
   Or double-click `bin\Release\net8.0-windows\MeaningOverlay.exe`

3. On **first launch**, a Settings window opens. Paste your **Gemini API key** and click Save.
   - Get a free key at: https://aistudio.google.com/app/apikey

4. **Use it:**
   - Select and copy any text anywhere (Ctrl+C)
   - Press **Ctrl+Shift+M**
   - An overlay appears over your screen with the AI explanation!
   - Press **Esc** or click anywhere outside to dismiss

## Features

- ✦ **Always-on-top overlay** — appears over any application
- ⚡ **Instant hotkey** — `Ctrl+Shift+M` from anywhere
- 🤖 **Gemini 2.0 Flash** — fast, free AI explanations
- 🎨 **Dark glassmorphism UI** — beautiful modern design
- 📋 **Smart responses** — single words get definitions, passages get summaries
- 🔒 **Secure key storage** — API key saved in AppData, not in code
- 🖥️ **System tray** — runs silently in background, right-click to access Settings or Exit

## Tray Icon

Right-click the tray icon (system notification area) for:
- **Settings** — update your API key
- **Exit** — quit the application

## Requirements

- Windows 10/11
- .NET 8 Runtime
- Gemini API Key (free at https://aistudio.google.com/app/apikey)
