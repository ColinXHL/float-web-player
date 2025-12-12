# FloatWebPlayer - 悬浮网页播放器设计文档

## 项目概述

**FloatWebPlayer** 是一个基于 C# WPF + WebView2 的悬浮网页播放器，主要用于游戏时观看攻略视频（如B站）。支持全局快捷键控制、透明度调节、鼠标穿透等功能。

---

## 功能需求

### 1. 双窗口架构

#### 窗口1：URL控制栏（ControlBarWindow）

| 属性 | 值 |
|------|-----|
| 位置 | 屏幕顶部，水平居中 |
| 宽度 | 屏幕宽度的 1/3 |
| 显示逻辑 | 默认隐藏；鼠标进入屏幕顶部1/8区域→显示细线→移到线上→展开；移开后延迟隐藏 |
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
| 默认大小 | 屏幕大小的 1/16 |
| 边框 | 自定义 2px 细边框，8方向可拖拽调整大小 |
| 控制栏 | 通过 JS 注入覆盖（Overlay）在 WebView2 上层 |
| 控制栏显示逻辑 | 默认隐藏；鼠标进入 WebView 区域→显示；离开→隐藏 |
| 控制栏元素 | `[—最小化][□最大化][×关闭]`（14px 圆形按钮，2px 间距） |
| 拖动区域 | WebView 顶部 10px 区域，鼠标按下触发拖动 |
| 边缘吸附 | 窗口接近屏幕边缘时自动吸附（阈值 10px） |

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

使用 Win32 API `RegisterHotKey` 实现系统级全局快捷键。

| 按键 | 功能 | 备注 |
|------|------|------|
| `5` | 视频倒退 | 默认5秒，可配置 |
| `6` | 视频前进 | 默认5秒，可配置 |
| `` ` `` (OEM3/波浪键) | 播放/暂停 | |
| `7` | 降低透明度 | 每次 -10%，最低 20% |
| `8` | 增加透明度 | 每次 +10%，最高 100% |
| `0` | 切换鼠标穿透模式 | 开启时自动降至最低透明度 |

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

### 4. 操作提示（OSD）

- **触发条件**：所有快捷键操作
- **显示位置**：屏幕正中央
- **显示样式**：半透明背景 + 白色文字 + 图标
- **消失方式**：1-2秒后自动淡出
- **实现方式**：独立透明窗口，Topmost

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

| 数据类型 | 存储方式 | 说明 |
|----------|---------|------|
| 应用配置 | JSON (`config.json`) | 快捷键、透明度默认值、快进秒数等 |
| 窗口状态 | JSON (`window_state.json`) | 位置、大小、透明度 |
| 历史记录 | SQLite (`data.db`) | URL、标题、访问时间 |
| 收藏夹 | SQLite (`data.db`) | URL、标题、添加时间 |
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
| 数据库 | Microsoft.Data.Sqlite | 最新 |
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
  <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
</ItemGroup>
```

### 项目创建命令

```powershell
# 创建解决方案
dotnet new sln -n FloatWebPlayer

# 创建 WPF 项目
dotnet new wpf -n FloatWebPlayer -f net8.0-windows

# 添加项目到解决方案
dotnet sln add FloatWebPlayer/FloatWebPlayer.csproj

# 添加 NuGet 包
cd FloatWebPlayer
dotnet add package Microsoft.Web.WebView2
dotnet add package Microsoft.Data.Sqlite
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
    <AssemblyName>FloatWebPlayer</AssemblyName>
    <RootNamespace>FloatWebPlayer</RootNamespace>
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
// 推荐路径：AppData/Local/FloatWebPlayer/WebView2Data
var userDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "FloatWebPlayer",
    "WebView2Data"
);
```

### 调试配置

在 `launchSettings.json` 中可配置调试选项：

```json
{
  "profiles": {
    "FloatWebPlayer": {
      "commandName": "Project",
      "nativeDebugging": true
    }
  }
}
```

---

## 项目结构

```
FloatWebPlayer/
├── FloatWebPlayer.sln
├── FloatWebPlayer/
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
│   │   ├── DatabaseService.cs             # SQLite 数据库服务
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
14. 实现 SQLite 数据库服务（历史记录、收藏夹 CRUD）
15. 实现 JSON 配置存储（窗口状态、用户设置）
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

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| v0.1 | 2025-12-12 | 初始设计文档 |
| v0.2 | 2025-12-12 | 更新脚本注入架构，CSS/JS 分离，添加 DOM 就绪检测机制 |
