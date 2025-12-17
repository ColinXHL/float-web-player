# FloatWebPlayer 插件 API 文档

本文档描述 FloatWebPlayer 插件系统的 API 接口。

## 目录

- [快速开始](#快速开始)
- [插件结构](#插件结构)
- [生命周期](#生命周期)
- [权限系统](#权限系统)
- [API 参考](#api-参考)
  - [Core API](#core-api)
  - [Config API](#config-api)
  - [Subtitle API](#subtitle-api)
  - [Overlay API](#overlay-api)
  - [Player API](#player-api)
  - [Window API](#window-api)
  - [Storage API](#storage-api)
  - [HTTP API](#http-api)
  - [Event API](#event-api)
  - [Speech API](#speech-api)

---

## 快速开始

### 最小插件示例

```javascript
// main.js
function onLoad(api) {
    api.log("Hello from my plugin!");
}

function onUnload(api) {
    api.log("Goodbye!");
}
```

```json
// plugin.json
{
  "id": "my-plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "main": "main.js",
  "description": "一个简单的示例插件",
  "author": "Your Name",
  "permissions": []
}
```

---

## 插件结构

```
my-plugin/
├── plugin.json    # 插件清单（必需）
├── main.js        # 插件入口（必需）
├── config.json    # 用户配置（自动生成）
├── README.md      # 插件说明（推荐）
└── storage/       # 数据存储目录（自动生成）
```

### plugin.json 字段说明

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| id | string | ✅ | 插件唯一标识符 |
| name | string | ✅ | 插件显示名称 |
| version | string | ✅ | 版本号（语义化版本） |
| main | string | ✅ | 入口文件路径 |
| description | string | ❌ | 插件描述 |
| author | string | ❌ | 作者 |
| homepage | string | ❌ | 主页/仓库地址 |
| permissions | string[] | ❌ | 所需权限列表 |
| profiles | string[] | ❌ | 推荐的 Profile 列表 |
| defaultConfig | object | ❌ | 默认配置 |

---

## 生命周期

```
┌─────────────┐
│  加载插件   │
└──────┬──────┘
       ▼
┌─────────────┐
│  onLoad()   │  ← 初始化、注册监听器
└──────┬──────┘
       ▼
┌─────────────┐
│   运行中    │  ← 响应事件、执行逻辑
└──────┬──────┘
       ▼
┌─────────────┐
│ onUnload()  │  ← 清理资源、移除监听器
└──────┬──────┘
       ▼
┌─────────────┐
│  插件卸载   │
└─────────────┘
```

### onLoad(api)

插件加载时调用，用于初始化。

```javascript
function onLoad(api) {
    // 初始化代码
    api.log("插件已加载");
    
    // 注册事件监听
    api.subtitle.onChanged(function(subtitle) {
        // 处理字幕变化
    });
}
```

### onUnload(api)

插件卸载时调用，用于清理资源。

**重要：** 插件卸载时，系统会自动清理所有 API 资源（包括事件监听器、覆盖层窗口等），但建议在 `onUnload` 中显式清理，以确保资源及时释放。

```javascript
function onUnload(api) {
    // 清理代码
    api.subtitle.removeAllListeners();
    api.speech.removeAllListeners();
    api.overlay.hide();
    api.overlay.clear();
    api.log("插件已卸载");
}
```

**自动清理的资源：**
- 所有事件监听器（subtitle、speech、event）
- 覆盖层窗口和绘图元素
- HTTP 请求连接
- 存储 API 缓存

**注意：** 即使 `onUnload` 中没有清理代码，系统也会在插件卸载后自动清理所有资源，防止内存泄漏。

---

## 权限系统

插件需要在 `plugin.json` 中声明所需权限：

| 权限 | 说明 | API |
|------|------|-----|
| `subtitle` | 访问视频字幕 | api.subtitle |
| `overlay` | 显示覆盖层 | api.overlay |
| `player` | 控制视频播放 | api.player |
| `window` | 控制窗口状态 | api.window |
| `storage` | 数据持久化 | api.storage |
| `network` | HTTP 网络请求 | api.http |
| `events` | 应用事件监听 | api.event |
| `audio` | 语音识别 | api.speech |

**无需权限的 API：**
- `api.core` - 日志、版本信息
- `api.config` - 插件配置读写
- `api.profile` - Profile 信息（只读）
- `api.log()` - 日志快捷方法

---

## API 参考

### Core API

无需权限，提供基础功能。

#### api.core.version

获取主程序版本号。

```javascript
var version = api.core.version;
api.log("主程序版本: " + version);
```

#### api.core.log(message)

输出普通日志。

```javascript
api.core.log("这是一条日志");
// 或使用快捷方法
api.log("这是一条日志");
```

#### api.core.warn(message)

输出警告日志。

```javascript
api.core.warn("这是一条警告");
```

#### api.core.error(message)

输出错误日志。

```javascript
api.core.error("这是一条错误");
```

---

### Config API

无需权限，管理插件配置。

#### api.config.get(key, defaultValue)

获取配置值，支持点号路径。

```javascript
var x = api.config.get("overlay.x", 100);
var name = api.config.get("name", "default");
```

#### api.config.set(key, value)

设置配置值，自动保存到文件。

```javascript
api.config.set("overlay.x", 200);
api.config.set("enabled", true);
```

#### api.config.has(key)

检查配置键是否存在。

```javascript
if (api.config.has("customSetting")) {
    // ...
}
```

#### api.config.remove(key)

移除配置键。

```javascript
api.config.remove("oldSetting");
```

---

### Subtitle API

需要 `subtitle` 权限。

#### api.subtitle.hasSubtitles

检查是否有字幕数据。

```javascript
if (api.subtitle.hasSubtitles) {
    api.log("字幕已加载");
}
```

#### api.subtitle.getCurrent(timeInSeconds)

根据时间戳获取当前字幕。

```javascript
var subtitle = api.subtitle.getCurrent(30.5);
if (subtitle) {
    api.log("字幕内容: " + subtitle.content);
}
```

**返回值：**
```javascript
{
    from: 30.0,      // 开始时间（秒）
    to: 33.5,        // 结束时间（秒）
    content: "字幕文本"
}
```

#### api.subtitle.getAll()

获取所有字幕。

```javascript
var subtitles = api.subtitle.getAll();
subtitles.forEach(function(s) {
    api.log(s.from + " - " + s.to + ": " + s.content);
});
```

#### api.subtitle.onLoaded(callback)

监听字幕加载事件。

```javascript
api.subtitle.onLoaded(function(subtitleData) {
    api.log("字幕已加载，共 " + subtitleData.body.length + " 条");
});
```

#### api.subtitle.onChanged(callback)

监听当前字幕变化。

```javascript
api.subtitle.onChanged(function(subtitle) {
    if (subtitle) {
        api.log("当前字幕: " + subtitle.content);
    }
});
```

#### api.subtitle.onCleared(callback)

监听字幕清除事件。

```javascript
api.subtitle.onCleared(function() {
    api.log("字幕已清除");
});
```

#### api.subtitle.removeAllListeners()

移除所有字幕监听器。

```javascript
api.subtitle.removeAllListeners();
```

---

### Overlay API

需要 `overlay` 权限。

#### api.overlay.setPosition(x, y)

设置覆盖层位置（逻辑像素）。

```javascript
api.overlay.setPosition(100, 100);
```

#### api.overlay.setSize(width, height)

设置覆盖层大小。

```javascript
api.overlay.setSize(200, 200);
```

#### api.overlay.getRect()

获取覆盖层位置和大小。

```javascript
var rect = api.overlay.getRect();
api.log("位置: " + rect.x + ", " + rect.y);
api.log("大小: " + rect.width + " x " + rect.height);
```

#### api.overlay.show()

显示覆盖层。

```javascript
api.overlay.show();
```

#### api.overlay.hide()

隐藏覆盖层。

```javascript
api.overlay.hide();
```

#### api.overlay.showMarker(direction, duration)

显示方向标记。

```javascript
// 显示北方向标记，常驻
api.overlay.showMarker("north", 0);

// 显示东方向标记，3秒后消失
api.overlay.showMarker("east", 3000);
```

**支持的方向：**
- `north`, `n`, `up`
- `northeast`, `ne`
- `east`, `e`, `right`
- `southeast`, `se`
- `south`, `s`, `down`
- `southwest`, `sw`
- `west`, `w`, `left`
- `northwest`, `nw`

#### api.overlay.clearMarkers()

清除所有方向标记。

```javascript
api.overlay.clearMarkers();
```

#### api.overlay.drawText(text, x, y, options)

绘制文本，返回元素 ID。

```javascript
var id = api.overlay.drawText("Hello", 10, 10, {
    fontSize: 16,
    color: "#FFFFFF",
    backgroundColor: "#000000",
    opacity: 0.8,
    duration: 3000  // 3秒后消失，0为常驻
});
```

#### api.overlay.drawRect(x, y, width, height, options)

绘制矩形，返回元素 ID。

```javascript
var id = api.overlay.drawRect(10, 10, 100, 50, {
    fill: "#FF0000",
    stroke: "#FFFFFF",
    strokeWidth: 2,
    opacity: 0.5,
    cornerRadius: 5
});
```

#### api.overlay.drawImage(path, x, y, options)

绘制图片，返回元素 ID。

```javascript
// 相对于插件目录的路径
var id = api.overlay.drawImage("icon.png", 10, 10, {
    width: 32,
    height: 32,
    opacity: 1.0
});
```

#### api.overlay.removeElement(elementId)

移除指定绘图元素。

```javascript
api.overlay.removeElement(id);
```

#### api.overlay.clear()

清除所有绘图元素。

```javascript
api.overlay.clear();
```

#### api.overlay.enterEditMode()

进入编辑模式（可拖拽调整位置和大小）。

```javascript
api.overlay.enterEditMode();
```

#### api.overlay.exitEditMode()

退出编辑模式，自动保存配置。

```javascript
api.overlay.exitEditMode();
```

---

### Player API

需要 `player` 权限。

#### api.player.play()

开始播放。

```javascript
api.player.play();
```

#### api.player.pause()

暂停播放。

```javascript
api.player.pause();
```

#### api.player.togglePlay()

切换播放/暂停状态。

```javascript
api.player.togglePlay();
```

#### api.player.seek(seconds)

跳转到指定时间。

```javascript
api.player.seek(60);  // 跳转到 1 分钟
```

#### api.player.seekRelative(seconds)

相对跳转。

```javascript
api.player.seekRelative(10);   // 前进 10 秒
api.player.seekRelative(-5);   // 后退 5 秒
```

#### api.player.getCurrentTime()

获取当前播放时间（秒）。

```javascript
var time = api.player.getCurrentTime();
api.log("当前时间: " + time + " 秒");
```

#### api.player.getDuration()

获取视频总时长（秒）。

```javascript
var duration = api.player.getDuration();
api.log("总时长: " + duration + " 秒");
```

#### api.player.setPlaybackRate(rate)

设置播放速度。

```javascript
api.player.setPlaybackRate(1.5);  // 1.5 倍速
api.player.setPlaybackRate(0.5);  // 0.5 倍速
```

#### api.player.getPlaybackRate()

获取当前播放速度。

```javascript
var rate = api.player.getPlaybackRate();
```

#### api.player.setVolume(volume)

设置音量（0.0 - 1.0）。

```javascript
api.player.setVolume(0.5);  // 50% 音量
```

#### api.player.getVolume()

获取当前音量。

```javascript
var volume = api.player.getVolume();
```

#### api.player.setMuted(muted)

设置静音状态。

```javascript
api.player.setMuted(true);
```

#### api.player.isMuted()

获取静音状态。

```javascript
var muted = api.player.isMuted();
```

---

### Window API

需要 `window` 权限。

#### api.window.setOpacity(opacity)

设置窗口透明度（0.2 - 1.0）。

```javascript
api.window.setOpacity(0.5);
```

#### api.window.getOpacity()

获取当前透明度。

```javascript
var opacity = api.window.getOpacity();
```

#### api.window.setClickThrough(enabled)

设置鼠标穿透模式。

```javascript
api.window.setClickThrough(true);
```

#### api.window.isClickThrough()

获取穿透模式状态。

```javascript
var clickThrough = api.window.isClickThrough();
```

#### api.window.setTopmost(topmost)

设置窗口置顶状态。

```javascript
api.window.setTopmost(true);
```

#### api.window.isTopmost()

获取置顶状态。

```javascript
var topmost = api.window.isTopmost();
```

#### api.window.getBounds()

获取窗口位置和大小。

```javascript
var bounds = api.window.getBounds();
api.log("位置: " + bounds.x + ", " + bounds.y);
api.log("大小: " + bounds.width + " x " + bounds.height);
```

---

### Storage API

需要 `storage` 权限。

数据存储在 `{插件目录}/storage/{key}.json`。

#### api.storage.save(key, data)

保存数据。

```javascript
api.storage.save("settings", { theme: "dark", fontSize: 14 });
api.storage.save("history", [1, 2, 3]);
```

#### api.storage.load(key)

加载数据。

```javascript
var settings = api.storage.load("settings");
if (settings) {
    api.log("主题: " + settings.theme);
}
```

#### api.storage.delete(key)

删除数据。

```javascript
api.storage.delete("oldData");
```

#### api.storage.exists(key)

检查数据是否存在。

```javascript
if (api.storage.exists("cache")) {
    // ...
}
```

#### api.storage.list()

列出所有存储的键名。

```javascript
var keys = api.storage.list();
keys.forEach(function(key) {
    api.log("存储键: " + key);
});
```

---

### HTTP API

需要 `network` 权限。

#### api.http.get(url, options)

发起 GET 请求。

```javascript
var result = api.http.get("https://api.example.com/data", {
    headers: { "Authorization": "Bearer token" },
    timeout: 10000
});

if (result.success) {
    api.log("状态码: " + result.status);
    api.log("数据: " + result.data);
} else {
    api.log("错误: " + result.error);
}
```

#### api.http.post(url, body, options)

发起 POST 请求。

```javascript
var result = api.http.post("https://api.example.com/submit", 
    { name: "test", value: 123 },
    {
        headers: { "Content-Type": "application/json" },
        timeout: 10000
    }
);
```

**返回值：**
```javascript
{
    success: true,           // 请求是否成功
    status: 200,             // HTTP 状态码
    data: "响应内容",         // 响应体
    headers: { ... },        // 响应头
    error: null              // 错误信息（失败时）
}
```

---

### Event API

需要 `events` 权限。

#### api.event.on(eventName, callback)

注册事件监听器。

```javascript
api.event.on("playStateChanged", function(data) {
    api.log("播放状态: " + (data.playing ? "播放中" : "已暂停"));
});

api.event.on("opacityChanged", function(data) {
    api.log("透明度: " + data.opacity);
});
```

#### api.event.off(eventName, callback)

取消事件监听。

```javascript
// 移除特定监听器
api.event.off("playStateChanged", myCallback);

// 移除该事件的所有监听器
api.event.off("playStateChanged");
```

**支持的事件：**

| 事件名 | 数据 | 说明 |
|--------|------|------|
| `playStateChanged` | `{ playing: boolean }` | 播放状态变化 |
| `timeUpdate` | `{ currentTime: number }` | 播放时间更新 |
| `opacityChanged` | `{ opacity: number }` | 透明度变化 |
| `clickThroughChanged` | `{ enabled: boolean }` | 穿透模式变化 |
| `urlChanged` | `{ url: string }` | URL 变化 |
| `profileChanged` | `{ profileId: string }` | Profile 切换 |

---

### Speech API

需要 `audio` 权限。

#### api.speech.isAvailable()

检查语音识别是否可用。

```javascript
if (api.speech.isAvailable()) {
    api.log("语音识别可用");
}
```

#### api.speech.onKeyword(keywords, callback)

监听特定关键词。

```javascript
api.speech.onKeyword(["东", "西", "南", "北"], function(keyword, fullText) {
    api.log("识别到关键词: " + keyword);
    api.log("完整文本: " + fullText);
});
```

#### api.speech.onText(callback)

监听所有识别文本。

```javascript
api.speech.onText(function(text) {
    api.log("识别文本: " + text);
});
```

#### api.speech.removeAllListeners()

移除所有语音监听器。

```javascript
api.speech.removeAllListeners();
```

---

## Profile 信息

无需权限，只读访问当前 Profile 信息。

```javascript
api.log("Profile ID: " + api.profile.id);
api.log("Profile 名称: " + api.profile.name);
api.log("Profile 目录: " + api.profile.directory);
```

---

## 最佳实践

### 1. 始终在 onUnload 中清理资源

```javascript
function onUnload(api) {
    api.subtitle.removeAllListeners();
    api.speech.removeAllListeners();
    api.overlay.hide();
    api.overlay.clear();
}
```

### 2. 使用配置而非硬编码

```javascript
// ❌ 不推荐
var x = 43;

// ✅ 推荐
var x = api.config.get("overlay.x", 43);
```

### 3. 处理 API 不可用的情况

```javascript
if (!api.subtitle) {
    api.log("警告：字幕 API 不可用，请检查权限");
    return;
}
```

### 4. 使用有意义的日志

```javascript
api.log("插件名 v1.0.0 已加载");
api.log("识别到方向: " + direction + " (字幕: " + text + ")");
```

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0.0 | 2025-12-15 | 初始版本 |
