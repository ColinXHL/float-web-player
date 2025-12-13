# 🎬 FloatWebPlayer

English | [简体中文](README-zh_CN.md)

**A floating web player for Windows, designed for watching tutorial videos while gaming.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)

---

## ✨ Features

- **🪟 Always on Top** - Floating window stays above other applications
- **⌨️ Global Hotkeys** - Control playback without leaving your game (customizable)
- **👻 Mouse Click-Through** - Interact with apps behind the player
- **🎚️ Adjustable Opacity** - Set transparency from 20% to 100%
- **🎯 Edge Snapping** - Window snaps to screen edges
- **🍪 Cookie Persistence** - Stay logged in to websites
- **🎨 Minimal UI** - Clean borderless design with custom controls
- **📚 History & Bookmarks** - Track browsing history and save favorites
- **⚙️ Settings Window** - Visual configuration interface

---

## 📸 Screenshots

> *Coming soon...*

---

## 🖥️ System Requirements

- **OS**: Windows 10/11
- **Runtime**: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Browser Engine**: [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre-installed on Windows 10/11)
- **Permissions**: Administrator privileges (required for global hotkeys in games)

---

## 📥 Installation

### Option 1: Download Release

> *Release builds coming soon...*

### Option 2: Build from Source

```powershell
# Clone the repository
git clone https://github.com/ColinXHL/float-web-player.git
cd float-web-player

# Build
dotnet build -c Release

# Run
dotnet run --project FloatWebPlayer
```

---

## ⌨️ Default Hotkeys

| Key | Function |
|-----|----------|
| `` ` `` | Play / Pause |
| `5` | Seek Backward (5s) |
| `6` | Seek Forward (5s) |
| `7` | Decrease Opacity |
| `8` | Increase Opacity |
| `0` | Toggle Click-Through |

> 💡 Hotkeys are fully customizable in Settings. Modifier keys (Ctrl, Alt, Shift) are supported.

---

## 🛠️ Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8.0 + WPF |
| Browser Engine | Microsoft WebView2 |
| Global Hotkeys | Win32 API (Low-level Keyboard Hook) |
| Click-Through | Win32 API (WS_EX_TRANSPARENT) |
| Window Control | Win32 API (SendMessage) |

---

## 📁 Project Structure

```
FloatWebPlayer/
├── Views/              # WPF Windows (Player, ControlBar, OSD, History, Bookmark, Settings)
├── Services/           # HotkeyService, ProfileManager, DataService, WindowStateService
├── Helpers/            # Win32Helper, ScriptInjector, AnimatedWindow
├── Models/             # AppConfig, GameProfile, HotkeyBinding, etc.
├── Scripts/            # Injected JS/CSS for WebView2
└── docs/               # Design documents
```

---

## 🚧 Development Status

- [x] Basic player window with WebView2
- [x] Floating control bar (top of screen)
- [x] Global hotkey support (customizable)
- [x] Opacity adjustment
- [x] Mouse click-through mode
- [x] OSD notifications
- [x] Edge snapping
- [x] History & Bookmarks
- [x] Settings window
- [ ] Auto cursor detection (auto opacity)
- [ ] Process detection + Profile auto-switch
- [ ] External tools launcher

---

## 🤝 Contributing

Issues and Pull Requests are welcome!

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## 🙏 Acknowledgments

- [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- [Bilibili](https://www.bilibili.com/) - Primary use case inspiration
