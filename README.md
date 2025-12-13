# 🎬 FloatWebPlayer

<p align="center">
  <img src="assets/float-web-player-logo.png" alt="Float Web Player Logo" width="128">
</p>

简体中文 | [English](README-en.md)

**Windows 悬浮网页播放器，专为游戏时观看攻略视频设计。**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)

---

## ✨ 功能特性

- **🪟 始终置顶** - 悬浮窗口始终显示在其他应用上方
- **⌨️ 全局快捷键** - 无需切换窗口即可控制播放（支持自定义）
- **👻 鼠标穿透** - 开启后可与播放器下方的应用交互
- **🎚️ 透明度调节** - 支持 20% 到 100% 透明度设置
- **🎯 边缘吸附** - 窗口自动吸附到屏幕边缘
- **🍪 Cookie 持久化** - 保持网站登录状态
- **🎨 简洁界面** - 无边框设计，自定义控制按钮
- **📚 历史与收藏** - 浏览历史记录，收藏常用页面
- **⚙️ 设置窗口** - 可视化配置界面

---

## 📸 截图

> *即将添加...*

---

## 🖥️ 系统要求

- **操作系统**: Windows 10/11
- **运行时**: [.NET 8.0 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)
- **浏览器引擎**: [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)（Windows 10/11 通常已预装）
- **权限**: 需要管理员权限（用于在游戏中使用全局快捷键）

---

## 📥 安装

### 方式一：下载发布版

> *发布版即将推出...*

### 方式二：从源码构建

```powershell
# 克隆仓库
git clone https://github.com/ColinXHL/float-web-player.git
cd float-web-player

# 构建
dotnet build -c Release

# 运行
dotnet run --project FloatWebPlayer
```

---

## ⌨️ 默认快捷键

| 按键 | 功能 |
|------|------|
| `` ` `` | 播放 / 暂停 |
| `5` | 后退 5 秒 |
| `6` | 前进 5 秒 |
| `7` | 降低透明度 |
| `8` | 增加透明度 |
| `0` | 切换鼠标穿透 |

> 💡 快捷键可在设置中自定义，支持组合键（Ctrl、Alt、Shift）。

---

## 🛠️ 技术栈

| 组件 | 技术 |
|------|------|
| 框架 | .NET 8.0 + WPF |
| 浏览器引擎 | Microsoft WebView2 |
| 全局快捷键 | Win32 API（低级键盘钩子） |
| 鼠标穿透 | Win32 API (WS_EX_TRANSPARENT) |
| 窗口控制 | Win32 API (SendMessage) |

---

## 📁 项目结构

```
FloatWebPlayer/
├── Views/              # WPF 窗口（播放器、控制栏、OSD、历史、收藏、设置）
├── Services/           # HotkeyService、ProfileManager、DataService、WindowStateService
├── Helpers/            # Win32Helper、ScriptInjector、AnimatedWindow
├── Models/             # AppConfig、GameProfile、HotkeyBinding 等
├── Scripts/            # WebView2 注入的 JS/CSS
└── docs/               # 设计文档
```

---

## 🚧 开发状态

- [x] WebView2 基础播放器窗口
- [x] 悬浮控制栏（屏幕顶部）
- [x] 全局快捷键支持（可自定义）
- [x] 透明度调节
- [x] 鼠标穿透模式
- [x] OSD 操作提示
- [x] 边缘吸附
- [x] 历史记录与收藏夹
- [x] 设置窗口
- [ ] 游戏内鼠标指针检测（自动透明度）
- [ ] 进程检测 + Profile 自动切换
- [ ] 外部工具启动

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

---

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE) 开源。

---

## 🙏 致谢

- [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- [Bilibili](https://www.bilibili.com/) - 主要使用场景灵感来源
