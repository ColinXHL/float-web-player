# SandronePlayer - 悬浮网页播放器设计文档

## 项目概述

**SandronePlayer** 是一个基于 C# WPF + WebView2 的悬浮网页播放器，主要用于游戏时观看攻略视频（如B站）。支持全局快捷键控制、透明度调节、鼠标穿透等功能。

---

## 功能需求

### 1. 双窗口架构

#### 窗口1：URL控制栏（ControlBarWindow）

| 属性 | 值 |
|------|-----|
| 位置 | 屏幕顶部，水平居中 |
| 宽度 | 屏幕宽度的 1/3 |
| 显示逻辑 | 默认隐藏；鼠标进入屏幕顶部1/4区域→显示细线→移到线上→展开；移开后400ms延迟隐藏；URL输入框聚焦时不隐藏 |
| Alt+Tab | 使用 WS_EX_TOOLWINDOW 样式从 Alt+Tab 中隐藏 |
| 元素 | `[◀后退][▶前进][🔄刷新][URL地址栏][☆收藏][≡菜单]` |
| 拖动 | 顶部拖动条，仅限水平移动 |

**布局示意图：**
```
┌──────────────────────────────────────────────────────────────────┐
│                        ═══════════                               │ ← 拖动条（仅水平移动）
│ [◀][▶] [🔄] [地址栏 URL________________] [☆收藏] [≡菜单]          │
└──────────────────────────────────────────────────────────────────┘
         ↑ 鼠标移到屏幕顶部1/8区域时显示细线，移到线上展开
```

#### 窗口2：播放器窗口（PlayerWindow）

| 属性 | 值 |
|------|-----|
| 默认位置 | 屏幕左下角 |
| 默认大小 | 屏幕宽度的 1/4，16:9 比例 |
| 边框 | 自定义 2px 细边框，8方向可拖拽调整大小 |
| 控制栏 | 通过 JS 注入覆盖（Overlay）在 WebView2 上层 |
| 控制栏显示逻辑 | 默认隐藏；鼠标进入 WebView 区域→显示；离开→隐藏 |
| 控制栏元素 | `[—最小化][□最大化][×关闭]`（14px 圆形按钮，2px 间距） |
| 拖动区域 | WebView 顶部 10px 区域，鼠标按下触发拖动 |
| 边缘吸附 | 窗口接近屏幕边缘时自动吸附（阈值 15px） |

**布局示意图：**
```
┌─ 自定义2px细边框（可拖拽调整大小）───────────┐
│ ══════════════════════════════════════════│ ← 顶部10px拖动区域（透明）
│                                 [−][□][×] │ ← 右上角控制按钮（JS注入）
│                                           │
│               WebView2                    │
│              (全区域)                      │
│                                           │
└───────────────────────────────────────────┘
```

**控制栏实现方案（JS 注入）：**

1. **控制按钮**：
   - 位置：`position: fixed; top: 2px; right: 2px`
   - 尺寸：14px × 14px 圆形按钮
   - 间距：2px
   - 样式：半透明黑色背景，hover 时放大 1.2 倍
   - 显示逻辑：鼠标进入 document 时显示，离开时隐藏

2. **拖动区域**：
   - 位置：`position: fixed; top: 0; left: 0; right: 0; height: 10px`
   - 功能：mousedown 事件发送 `postMessage('drag')` 到 C#
   - C# 端使用 Win32 API `WM_NCLBUTTONDOWN + HTCAPTION` 实现拖动

3. **消息通信**：
   - `window.chrome.webview.postMessage()` 发送命令
   - C# 端 `WebMessageReceived` 事件处理 `minimize`、`maximize`、`close`、`drag` 命令

---

### 脚本注入架构

采用开源项目最佳实践设计，实现 WebView2 脚本注入的关注点分离。

#### 设计原则

| 原则 | 说明 |
|------|------|
| **单一职责** | `ScriptInjector` 专门负责脚本注入管理 |
| **关注点分离** | CSS/JS 脚本与 C# 代码完全分离 |
| **使用官方 API** | 使用 `AddScriptToExecuteOnDocumentCreatedAsync` 替代手动事件监听 |
| **嵌入资源** | 脚本作为嵌入资源编译，支持 IDE 语法高亮 |

#### 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                    PlayerWindow.xaml.cs                      │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ InitializeWebView()                                     ││
│  │   └── await ScriptInjector.InjectAllAsync(WebView)      ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    ScriptInjector.cs                         │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ InjectAllAsync(WebView2 webView)                        ││
│  │   ├── 1. BuildCssInjectionScript()                      ││
│  │   │      └── 读取 InjectedStyles.css，包装为 JS         ││
│  │   │      └── AddScriptToExecuteOnDocumentCreatedAsync() ││
│  │   └── 2. GetEmbeddedResource("InjectedScripts.js")      ││
│  │          └── AddScriptToExecuteOnDocumentCreatedAsync() ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌────────────────────────────────┐  ┌────────────────────────────────┐
│ Scripts/InjectedStyles.css     │  │ Scripts/InjectedScripts.js     │
│ (嵌入资源)                     │  │ (嵌入资源)                     │
│ ┌────────────────────────────┐ │  │ ┌────────────────────────────┐ │
│ │ • 自定义滚动条样式         │ │  │ │ • 控制按钮创建             │ │
│ │ • 控制按钮样式             │ │  │ │ • 顶部拖动区域             │ │
│ │ • 拖动区域样式             │ │  │ │ • 鼠标悬停显示/隐藏        │ │
│ └────────────────────────────┘ │  │ │ • postMessage 通信         │ │
└────────────────────────────────┘  │ └────────────────────────────┘ │
                                    └────────────────────────────────┘
```

#### 关键 API：`AddScriptToExecuteOnDocumentCreatedAsync`

**为什么使用此 API？**

| 对比项 | NavigationCompleted 事件 | AddScriptToExecuteOnDocumentCreatedAsync |
|--------|-------------------------|------------------------------------------|
| 调用时机 | 页面加载完成后 | 文档创建时（更早） |
| 事件管理 | 需要手动注册/取消 | 一次调用，自动处理所有导航 |
| iframe 支持 | 需要额外处理 | 自动应用于所有 frame |
| 代码复杂度 | 较高 | 较低 |

**官方文档**：[AddScriptToExecuteOnDocumentCreatedAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.addscripttoexecuteondocumentcreatedasync)

#### DOM 就绪检测机制

由于 `AddScriptToExecuteOnDocumentCreatedAsync` 在文档创建非常早期执行，此时 `document.head` 和 `document.body` 可能尚未创建。因此所有注入脚本都实现了 DOM 就绪等待逻辑：

```javascript
function injectContent() {
    var target = document.head || document.documentElement;
    if (!target) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', injectContent);
        } else {
            setTimeout(injectContent, 10);
        }
        return;
    }
    // ... 执行实际注入
}
injectContent();
```

**设计要点**：
- 检测 `document.head || document.documentElement` 是否可用
- 若不可用且 `readyState === 'loading'`，监听 `DOMContentLoaded`
- 否则使用 `setTimeout` 短暂延迟后重试
- 所有脚本都有防重复注入检测（通过元素 ID）

#### 文件说明

| 文件 | 职责 |
|------|------|
| `Scripts/InjectedStyles.css` | 所有注入的 CSS 样式，作为嵌入资源 |
| `Scripts/InjectedScripts.js` | 所有注入的 JS 代码（DOM 操作、事件处理），作为嵌入资源 |
| `Helpers/ScriptInjector.cs` | 读取嵌入资源，调用 WebView2 API 注入 |
| `Views/PlayerWindow.xaml.cs` | 仅调用 `ScriptInjector.InjectAllAsync()` |

#### 两窗口关联

- URL 控制栏输入地址后，回车在播放器窗口加载
- 播放器窗口内点击链接跳转时，URL 控制栏同步更新
- 双向实时同步

---

### 2. 全局快捷键

使用低级键盘钩子（`SetWindowsHookEx` + `WH_KEYBOARD_LL`）实现系统级全局快捷键。

#### 默认快捷键

| 按键 | 功能 | 备注 |
|------|------|------|
| `5` | 视频倒退 | 默认5秒，可配置 |
| `6` | 视频前进 | 默认5秒，可配置 |
| `` ` `` (OEM3/波浪键) | 播放/暂停 | |
| `7` | 降低透明度 | 每次 -10%，最低 20% |
| `8` | 增加透明度 | 每次 +10%，最高 100% |
| `0` | 切换鼠标穿透模式 | 开启时自动降至最低透明度 |

#### 快捷键自定义架构

采用配置驱动 + ActionDispatcher 模式，支持组合键和进程过滤。

**数据模型**：

```
HotkeyConfig (配置根)
├── Profiles: List<HotkeyProfile>     // 多配置 Profile
├── ActiveProfileName: string          // 当前激活的 Profile
└── AutoSwitchProfile: bool            // 是否根据进程自动切换

HotkeyProfile (配置 Profile)
├── Name: string                       // Profile 名称
├── ActivationProcesses: List<string>? // 自动激活的进程列表
└── Bindings: List<HotkeyBinding>      // 快捷键绑定列表

HotkeyBinding (快捷键绑定)
├── Key: uint                          // 虚拟键码 (VK_xxx)
├── Modifiers: ModifierKeys            // 修饰键 (Ctrl|Alt|Shift)
├── Action: string                     // 动作标识符
├── ProcessFilters: List<string>?      // 进程过滤（仅特定进程生效）
└── IsEnabled: bool                    // 是否启用
```

**架构图**：

```
┌─────────────────────────────────────────────────────┐
│                  HotkeyService                       │
│  ┌────────────────┐    ┌──────────────────────────┐ │
│  │ HotkeyConfig   │ -> │ ActionDispatcher         │ │
│  │ (配置驱动)     │    │ (Action -> 处理器映射)   │ │
│  └────────────────┘    └──────────────────────────┘ │
│          │                       │                   │
│          ▼                       ▼                   │
│  ┌────────────────┐    ┌──────────────────────────┐ │
│  │ Profile 匹配   │    │ 内置 Actions             │ │
│  │ + 进程过滤     │    │ SeekBackward, TogglePlay │ │
│  └────────────────┘    └──────────────────────────┘ │
│                              │ 扩展点               │
│                              ▼                      │
│                      ┌──────────────────────────┐   │
│                      │ 自定义脚本 (Script:xxx)  │   │
│                      │ (预留，后续实现)          │   │
│                      └──────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

**修饰键支持**：

| 标志 | 值 | 说明 |
|------|-----|------|
| None | 0 | 无修饰键 |
| Alt | 1 | Alt 键 |
| Ctrl | 2 | Ctrl 键 |
| Shift | 4 | Shift 键 |
| Ctrl+Alt | 3 | 组合 (1+2) |
| Ctrl+Shift | 6 | 组合 (2+4) |

**进程过滤**：

- `ProcessFilters` 为 `null` 或空列表：全局生效
- `ProcessFilters` 包含进程名：仅当前台进程匹配时生效
- 匹配时不区分大小写，不含路径和扩展名（如 `game` 匹配 `Game.exe`）

---

### 3. 鼠标穿透模式

- **开启方式**：按 `0` 键切换
- **开启时行为**：
  - 窗口透明度自动降至最低（20%）
  - 鼠标事件穿透到下层窗口
  - 屏幕中央显示 OSD 提示"鼠标穿透已开启"
- **关闭时行为**：
  - 恢复之前的透明度
  - 屏幕中央显示 OSD 提示"鼠标穿透已关闭"
- **实现方式**：Win32 API `SetWindowLong` + `WS_EX_TRANSPARENT`

---

### 游戏内鼠标指针检测（自动透明度）

**应用场景**：原神等游戏中，行走/战斗时无鼠标指针，打开菜单/活动界面时显示指针。当指针显示时自动降低 WebView 透明度，方便同时查看游戏 UI 和攻略视频。

#### 检测方案

使用 `GetCursorInfo` Win32 API 检测系统光标是否显示：

```csharp
[DllImport("user32.dll")]
private static extern bool GetCursorInfo(out CURSORINFO pci);

[StructLayout(LayoutKind.Sequential)]
public struct CURSORINFO
{
    public int cbSize;
    public int flags;        // 0 = 隐藏, 1 = 显示
    public IntPtr hCursor;
    public POINT ptScreenPos;
}

public const int CURSOR_SHOWING = 0x00000001;

public static bool IsCursorVisible()
{
    var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
    if (GetCursorInfo(out ci))
    {
        return (ci.flags & CURSOR_SHOWING) != 0;
    }
    return true; // 默认显示
}
```

#### 触发逻辑

| 条件 | 行为 |
|------|------|
| 鼠标指针显示 | WebView 降至最低透明度（如 20%） |
| 鼠标指针隐藏 | WebView 恢复正常透明度 |
| 非目标进程前台 | 不执行检测 |

#### 实现方式

**定时器轮询**（推荐，简单可靠）：

```csharp
private DispatcherTimer _cursorCheckTimer;
private double _normalOpacity = 0.8;
private double _minOpacity = 0.2;

private void StartCursorMonitor()
{
    _cursorCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _cursorCheckTimer.Tick += (s, e) =>
    {
        // 仅当目标进程（如原神）在前台时检测
        var processName = Win32Helper.GetForegroundWindowProcessName();
        if (!IsTargetProcess(processName)) return;

        var visible = Win32Helper.IsCursorVisible();
        if (visible && _currentOpacity > _minOpacity)
            SetOpacity(_minOpacity);
        else if (!visible && _currentOpacity < _normalOpacity)
            SetOpacity(_normalOpacity);
    };
    _cursorCheckTimer.Start();
}
```

#### 配置项

在 `profile.json` 中支持配置：

```json
{
  "cursorDetection": {
    "enabled": true,
    "minOpacity": 0.2,
    "checkIntervalMs": 200
  }
}
```

#### 注意事项

- **前台进程判断**：只在配置的目标游戏前台时启用检测
- **Debounce 防抖**：添加短暂延迟避免频繁切换导致闪烁
- **游戏兼容性**：某些游戏可能使用自定义光标，需实测验证

---

### 4. 操作提示（OSD）

- **触发条件**：所有快捷键操作（输入模式下不触发）
- **显示位置**：屏幕正中央
- **显示样式**：半透明背景（#B0000000）+ 白色文字 + 图标
- **消失方式**：淡入 200ms → 停留 1s → 淡出 300ms
- **实现方式**：独立透明窗口（`OsdWindow`），Topmost
- **输入模式检测**：当焦点在 TextBox/PasswordBox/ComboBox 等输入控件时，快捷键不触发

---

### 5. 透明度调节

| 属性 | 值 |
|------|-----|
| 范围 | 20% - 100% |
| 步进 | 10% |
| 快捷键 | `7` 降低，`8` 增加 |
| 持久化 | 保存到配置文件 |

---

### 6. 数据存储

采用 **JSON + Profile 目录结构**，便于插件扩展和数据隔离。

#### 存储结构

```
Data/
├── config.json              # 全局配置（非 Profile 相关）
├── window_state.json        # 窗口状态
└── Profiles/
    └── Default/
        ├── profile.json     # Profile 配置（必需）
        ├── hotkeys.json     # 快捷键绑定（可选，不存在则用默认）
        ├── bookmarks.json   # 收藏夹（可选，用户添加后创建）
        └── history.json     # 历史记录（系统自动生成）
```

#### 数据类型说明

| 数据类型 | 存储方式 | 说明 |
|----------|---------|------|
| 全局配置 | JSON (`config.json`) | 透明度默认值、快进秒数、边缘吸附等全局设置 |
| 窗口状态 | JSON (`window_state.json`) | 位置、大小、透明度 |
| Profile 配置 | JSON (`Profiles/{name}/profile.json`) | 名称、激活进程、快速链接、工具等 |
| 快捷键 | JSON (`Profiles/{name}/hotkeys.json`) | 可选，不存在则继承 Default |
| 历史记录 | JSON (`Profiles/{name}/history.json`) | URL、标题、访问时间，按 Profile 隔离 |
| 收藏夹 | JSON (`Profiles/{name}/bookmarks.json`) | URL、标题、添加时间，按 Profile 隔离 |
| Cookie | WebView2 UserDataFolder | 自动持久化 |

---

### 7. 菜单功能

点击 `≡菜单` 按钮弹出下拉菜单：

- **历史记录**：打开历史记录窗口
- **设置**：打开设置窗口
  - 快捷键配置
  - 默认透明度
  - 快进/倒退秒数
  - 边缘吸附开关
- **关于**：版本信息

---

### 8. 收藏夹功能

- 点击 `☆收藏` 按钮：
  - 当前页面未收藏 → 添加收藏
  - 当前页面已收藏 → 弹出收藏列表
- 收藏列表支持：搜索、删除、点击跳转

---

## 技术选型

| 组件 | 技术 | 版本 |
|------|------|------|
| 运行时 | .NET | 8.0 |
| UI 框架 | WPF | - |
| 浏览器引擎 | Microsoft.Web.WebView2 | 最新 |
| JSON 处理 | System.Text.Json | 内置 |
| 全局快捷键 | Win32 API `RegisterHotKey` | - |
| 鼠标穿透 | Win32 API `SetWindowLong` | - |
| 窗口操作 | Win32 API `SendMessage` | - |

---

## 开发环境配置

### 必需软件

| 软件 | 版本 | 下载地址 |
|------|------|----------|
| Visual Studio 2022 | 17.8+ | https://visualstudio.microsoft.com/ |
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| WebView2 Runtime | Evergreen | https://developer.microsoft.com/en-us/microsoft-edge/webview2/ |

### Visual Studio 工作负载

安装以下工作负载：
- **.NET 桌面开发**（包含 WPF）

### NuGet 包依赖

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2420.47" />
</ItemGroup>
```

### 项目创建命令

```powershell
# 创建解决方案
dotnet new sln -n SandronePlayer

# 创建 WPF 项目
dotnet new wpf -n SandronePlayer -f net8.0-windows

# 添加项目到解决方案
dotnet sln add SandronePlayer/SandronePlayer.csproj

# 添加 NuGet 包
cd SandronePlayer
dotnet add package Microsoft.Web.WebView2
```

### 项目配置（.csproj）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ApplicationIcon>Resources\app.ico</ApplicationIcon>
    <AssemblyName>SandronePlayer</AssemblyName>
    <RootNamespace>SandronePlayer</RootNamespace>
  </PropertyGroup>
</Project>
```

### 目录结构初始化

```powershell
# 在项目根目录下执行
mkdir Views, Services, Helpers, Models, Resources
```

### WebView2 UserDataFolder 配置

为实现 Cookie 持久化，需指定固定的 UserDataFolder：

```csharp
// 推荐路径：AppData/Local/SandronePlayer/WebView2Data
var userDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SandronePlayer",
    "WebView2Data"
);
```

### 调试配置

在 `launchSettings.json` 中可配置调试选项：

```json
{
  "profiles": {
    "SandronePlayer": {
      "commandName": "Project",
      "nativeDebugging": true
    }
  }
}
```

---

## 项目结构

```
SandronePlayer/
├── SandronePlayer.sln
├── SandronePlayer/
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── Views/
│   │   ├── PlayerWindow.xaml              # 播放器窗口
│   │   ├── PlayerWindow.xaml.cs
│   │   ├── ControlBarWindow.xaml          # URL 控制栏窗口
│   │   ├── ControlBarWindow.xaml.cs
│   │   ├── OsdWindow.xaml                 # OSD 操作提示窗口
│   │   ├── OsdWindow.xaml.cs
│   │   ├── HistoryWindow.xaml             # 历史记录窗口
│   │   ├── HistoryWindow.xaml.cs
│   │   ├── SettingsWindow.xaml            # 设置窗口
│   │   └── SettingsWindow.xaml.cs
│   ├── Scripts/
│   │   ├── InjectedStyles.css             # WebView2 注入样式（嵌入资源）
│   │   └── InjectedScripts.js             # WebView2 注入脚本（嵌入资源）
│   ├── Services/
│   │   ├── HotkeyService.cs               # 全局快捷键服务
│   │   ├── ConfigService.cs               # 配置管理服务
│   │   ├── ProfileManager.cs              # Profile 管理服务（加载/切换/Default 兜底）
│   │   ├── DataService.cs                 # JSON 数据服务（历史/收藏 CRUD）
│   │   └── WindowStateService.cs          # 窗口状态保存服务
│   ├── Helpers/
│   │   ├── Win32Helper.cs                 # Win32 API 封装
│   │   ├── ScriptInjector.cs              # WebView2 脚本注入管理
│   │   └── WebViewHelper.cs               # WebView2 JS 注入辅助
│   ├── Models/
│   │   ├── AppConfig.cs                   # 应用配置模型
│   │   ├── HistoryItem.cs                 # 历史记录模型
│   │   └── BookmarkItem.cs                # 收藏夹模型
│   └── Resources/
│       └── Styles.xaml                    # 全局样式资源
├── docs/
│   └── design.md                          # 设计文档（本文件）
└── README.md                              # 项目说明
```

---

## 开发计划

### Phase 1: 基础框架
1. 创建解决方案和项目结构
2. 实现 PlayerWindow 基础框架（无边框、自定义细边框、拖拽调整大小）
3. 集成 WebView2 并实现 Cookie 持久化
4. 实现 PlayerWindow 控制栏 Overlay（显示/隐藏动画）

### Phase 2: 控制栏窗口
5. 实现 ControlBarWindow（URL栏、导航按钮、收藏按钮、菜单按钮）
6. 实现 ControlBarWindow 显示/隐藏逻辑（屏幕顶部触发）
7. 实现两窗口 URL 双向同步

### Phase 3: 快捷键与控制
8. 实现全局快捷键服务（RegisterHotKey）
9. 实现视频控制 JS 注入（播放/暂停、快进/倒退）
10. 实现透明度调节
11. 实现鼠标穿透模式
12. 实现 OSD 操作提示窗口

### Phase 4: 数据与设置
13. 实现边缘吸附功能
14. 实现 JSON 数据服务（历史记录、收藏夹 CRUD，按 Profile 隔离）
15. 实现 ProfileManager 服务（加载/切换 Profile，Default 兜底）+ JSON 配置存储
16. 实现历史记录 UI 窗口
17. 实现收藏夹 UI
18. 实现设置窗口

### Phase 5: 测试与优化
19. 功能测试与 Bug 修复
20. 性能优化
21. 打包发布

---

## 视频控制 JS 脚本（B站适配）

```javascript
// 获取视频元素
const video = document.querySelector('video');

// 播放/暂停
function togglePlay() {
    if (video.paused) {
        video.play();
    } else {
        video.pause();
    }
}

// 快进/倒退（秒）
function seek(seconds) {
    video.currentTime += seconds;
}

// 获取当前状态
function getStatus() {
    return {
        paused: video.paused,
        currentTime: video.currentTime,
        duration: video.duration
    };
}
```

---

## 注意事项

1. **WebView2 运行时**：用户需安装 WebView2 Runtime，或打包时包含 Evergreen Standalone Installer
2. **Cookie 持久化**：设置 `WebView2.CreationProperties.UserDataFolder` 到固定目录
3. **全局快捷键冲突**：某些按键可能与系统或其他软件冲突，需提供自定义配置
4. **多显示器支持**：当前版本暂不考虑，后续可扩展
5. **管理员权限**：鼠标穿透功能在某些情况下可能需要管理员权限
6. **链接打开行为**：默认点击链接会弹出新窗口，后续通过 `NewWindowRequested` 事件拦截并在当前窗口打开

---

## 插件系统设计（Game Profile）

### 概述

插件系统允许用户为不同游戏创建独立的配置 Profile，实现：
- **独立收藏夹**：每个游戏的攻略收藏互不干扰
- **快速链接菜单**：常用攻略网站一键跳转
- **外部工具启动**：一键启动游戏辅助程序
- **自动 Profile 切换**：检测游戏进程自动加载对应配置
- **个性化设置**：每个游戏可有不同的透明度、快捷键等

### 设计原则

| 原则 | 说明 |
|------|------|
| **配置驱动** | 使用 JSON 文件定义 Profile，无需修改代码 |
| **渐进增强** | 先实现核心功能，逐步添加可视化配置界面 |
| **向后兼容** | 默认 Profile 保证无配置时正常使用 |
| **可扩展** | 预留插件仓库订阅机制 |

---

### Profile 目录结构

```
Profiles/
├── Default/
│   ├── profile.json          # 默认配置（必需）
│   ├── hotkeys.json          # 快捷键绑定（可选）
│   ├── bookmarks.json        # 收藏夹（系统生成）
│   └── history.json          # 历史记录（系统生成）
├── Genshin/
│   ├── profile.json          # 原神配置（必需）
│   ├── hotkeys.json          # 可选，不存在则继承 Default
│   ├── bookmarks.json        # 独立收藏夹（系统生成）
│   └── custom.js             # 可选：网页注入脚本
├── Minecraft/
│   └── profile.json          # 最简配置，只需这一个文件
└── ...
```

#### 文件必需性说明

| 文件 | 必需性 | 说明 |
|------|--------|------|
| `profile.json` | ✅ **必需** | 核心配置（ID、名称、激活进程、快速链接等） |
| `hotkeys.json` | ❌ 可选 | 不提供则继承 Default 的快捷键 |
| `bookmarks.json` | ❌ 可选 | 用户添加收藏后系统自动创建 |
| `history.json` | ❌ 可选 | 系统自动生成，开发者无需编写 |

---

### Profile 配置文件格式

**`profile.json` 完整示例**：

```json
{
  "id": "genshin",
  "name": "原神",
  "icon": "🎮",
  "version": 1,
  
  "activation": {
    "processes": ["GenshinImpact", "YuanShen"],
    "autoSwitch": true
  },
  
  "defaults": {
    "url": "https://search.bilibili.com/all?keyword=原神攻略",
    "opacity": 0.5,
    "seekSeconds": 10
  },
  
  "quickLinks": [
    { "label": "📋 深渊配队推荐", "url": "https://..." },
    { "label": "🎲 祈愿分析", "url": "https://paimon.moe" },
    { "label": "📖 原神Wiki", "url": "https://wiki.biligame.com/ys" },
    { "separator": true },
    { "label": "📁 打开截图目录", "type": "folder", "path": "D:\\Screenshots\\Genshin" }
  ],
  
  "tools": [
    {
      "label": "🛠️ 原神工具箱",
      "path": "D:\\Games\\GenshinTools\\ToolBox.exe",
      "args": "",
      "runAsAdmin": false
    },
    {
      "label": "📊 伤害计算器",
      "path": "D:\\Games\\GenshinTools\\DamageCalc.exe"
    }
  ],
  
  "hotkeys": {
    "inherit": true,
    "bindings": []
  },
  
  "customScript": "custom.js"
}
```

---

### 数据模型

```csharp
public class GameProfile
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; } = "🎮";
    public int Version { get; set; } = 1;
    
    public ProfileActivation Activation { get; set; }
    public ProfileDefaults Defaults { get; set; }
    public List<QuickLink> QuickLinks { get; set; }
    public List<ExternalTool> Tools { get; set; }
    public ProfileHotkeys? Hotkeys { get; set; }
    public string? CustomScript { get; set; }
}

public class ProfileActivation
{
    public List<string> Processes { get; set; }
    public bool AutoSwitch { get; set; } = true;
}

public class ProfileDefaults
{
    public string? Url { get; set; }
    public double Opacity { get; set; } = 1.0;
    public int SeekSeconds { get; set; } = 5;
}

public class QuickLink
{
    public string Label { get; set; }
    public string? Url { get; set; }
    public string? Type { get; set; }      // "url" | "folder" | "action"
    public string? Path { get; set; }
    public bool Separator { get; set; }
}

public class ExternalTool
{
    public string Label { get; set; }
    public string Path { get; set; }
    public string? Args { get; set; }
    public bool RunAsAdmin { get; set; }
}
```

---

### 架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          ProfileManager                                   │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │ LoadProfiles()          // 启动时加载所有 Profile                   │  │
│  │ SwitchProfile(id)       // 手动切换                                │  │
│  │ CurrentProfile          // 当前激活的 Profile                       │  │
│  │ OnProfileChanged        // Profile 切换事件                         │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
                                     │
         ┌───────────────────────────┼───────────────────────────┐
         ▼                           ▼                           ▼
┌─────────────────┐        ┌─────────────────┐        ┌─────────────────┐
│  ControlBar     │        │  BookmarkService │        │  HotkeyService  │
│  - 动态菜单     │        │  - 按 Profile    │        │  - 按 Profile   │
│  - 插件按钮     │        │    切换数据源    │        │    切换绑定     │
└─────────────────┘        └─────────────────┘        └─────────────────┘
         │                           │                           │
         ▼                           ▼                           ▼
┌─────────────────┐        ┌─────────────────┐        ┌─────────────────┐
│ ContextMenu     │        │ bookmarks.json  │        │ profile.json    │
│ (动态生成)      │        │ (Profile 独立)  │        │ (hotkeys 配置)  │
└─────────────────┘        └─────────────────┘        └─────────────────┘
```

---

### 控制栏集成

在现有控制栏新增一个**插件按钮**：

```
┌─────────────────────────────────────────────────────────────────────┐
│ [◀][▶][🔄] [URL________________________] [☆收藏] [🎮] [≡菜单]      │
└─────────────────────────────────────────────────────────────────────┘
                                                      ↑
                                              插件按钮（显示当前 Profile 图标）
```

**点击插件按钮弹出菜单**：

```
┌────────────────────────────────┐
│ 📋 深渊配队推荐                 │  ← quickLinks
│ 🎲 祈愿分析                     │
│ 📖 原神Wiki                     │
├────────────────────────────────┤
│ ▶️ 启动：原神工具箱             │  ← tools
│ ▶️ 启动：伤害计算器             │
├────────────────────────────────┤
│ ⚙️ 原神设置                     │  ← Profile 设置
│ 📂 打开配置文件夹               │
│ 🔄 切换 Profile...              │  ← 切换到其他游戏
└────────────────────────────────┘
```

---

### 可视化配置 - 分阶段计划

| 阶段 | 配置方式 | 说明 |
|------|----------|------|
| **V1** | 手动编辑 JSON | 快速验证设计，提供"打开配置文件夹"菜单 |
| **V2** | 简单表单 | 添加链接/工具的弹窗对话框 |
| **V3** | 完整编辑器 | Profile 管理窗口，完整可视化编辑 |

**V1 - 基础功能**：
- 菜单项：📂 打开配置文件夹
- 菜单项：🔄 重新加载配置
- 文档说明配置格式

**V2 - 简单表单**：
```
[添加快速链接]
┌─────────────────────────────┐
│ 名称：[_______________]      │
│ URL： [_______________]      │
│           [确定] [取消]      │
└─────────────────────────────┘

[添加外部工具]
┌─────────────────────────────┐
│ 名称：[_______________]      │
│ 路径：[___________] [浏览]   │
│ □ 以管理员身份运行           │
│           [确定] [取消]      │
└─────────────────────────────┘
```

**V3 - 完整编辑器**：
- Profile 列表管理（创建/删除/复制）
- 完整的可视化编辑界面
- 导入/导出 Profile

---

### 未来扩展：插件仓库

**目标**：建立社区共享的 Profile 仓库，用户可一键订阅/更新。

**仓库结构**（GitHub）：

```
sandrone-player-profiles/
├── registry.json              # 插件索引
├── genshin/
│   ├── manifest.json          # 插件元数据
│   ├── profile.json
│   ├── bookmarks.json
│   └── README.md
├── starrail/
│   └── ...
└── minecraft/
    └── ...
```

**manifest.json**：

```json
{
  "id": "genshin",
  "name": "原神攻略 Profile",
  "author": "ColinXHL",
  "version": "1.0.0",
  "description": "原神攻略快速链接、常用工具启动",
  "tags": ["米哈游", "原神", "攻略"],
  "updated": "2025-12-12"
}
```

**订阅机制**：

```
菜单 → 插件仓库 →
┌─────────────────────────────────────────────────────────┐
│ 🔍 搜索插件...                                          │
├─────────────────────────────────────────────────────────┤
│ 🎮 原神攻略 Profile         v1.0.0   ⭐ 128   [已安装] │
│ 🚀 崩坏：星穹铁道            v1.2.0   ⭐ 86    [安装]  │
│ 🧱 Minecraft 综合            v2.0.0   ⭐ 64    [安装]  │
│ ...                                                    │
└─────────────────────────────────────────────────────────┘
```

**工作流**：
1. 用户点击"安装"
2. 从仓库下载 Profile 文件到本地 `Profiles/` 目录
3. 自动加载并可切换使用
4. 检测更新时提示用户

---

### 实施路线图

| 阶段 | 功能 | 优先级 |
|------|------|--------|
| Phase 1 | ProfileManager 基础服务 + 配置加载 | P0 |
| Phase 2 | 控制栏插件按钮 + 动态菜单 | P0 |
| Phase 3 | 独立收藏夹（按 Profile 隔离） | P0 |
| Phase 4 | 外部工具启动 | P1 |
| Phase 5 | 进程检测 + 自动切换 | P1 |
| Phase 6 | 可视化配置表单（V2） | P2 |
| Phase 7 | 完整 Profile 编辑器（V3） | P2 |
| Phase 8 | 插件仓库订阅机制 | P3 |

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| v0.1 | 2025-12-12 | 初始设计文档 |
| v0.2 | 2025-12-12 | 更新脚本注入架构，CSS/JS 分离，添加 DOM 就绪检测机制 |
| v0.3 | 2025-12-12 | 新增插件系统设计（Game Profile），含配置结构、架构图、分阶段计划、仓库订阅机制 |
| v0.4 | 2025-12-13 | 新增游戏内鼠标指针检测功能设计，支持自动透明度调节 |
