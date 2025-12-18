# Profile 创建与发布指南

本文档介绍如何创建和发布 SandronePlayer Profile。

## 什么是 Profile

Profile 是针对特定游戏或使用场景的配置集合，包含：

- 窗口位置和大小设置
- 推荐的插件列表
- 插件的预设配置

用户可以通过 Profile 市场一键安装完整的配置方案。

## 创建 Profile

### 1. 目录结构

```
User/Data/Profiles/
└── my-profile/
    ├── profile.json       # Profile 配置（必需）
    ├── icon.png           # Profile 图标（可选，64x64）
    └── plugins/           # 插件目录
        └── my-plugin/
            ├── plugin.json
            └── main.js
```

### 2. profile.json 配置

```json
{
  "id": "genshin",
  "name": "原神",
  "icon": "🎮",
  "description": "原神游戏配置，包含方向标记插件",
  "author": "Your Name",
  "version": "1.0.0",
  "homepage": "https://github.com/yourname/genshin-profile",
  "plugins": [
    "genshin-direction-marker"
  ],
  "windowState": {
    "x": 100,
    "y": 100,
    "width": 400,
    "height": 300,
    "opacity": 0.8
  }
}
```

**字段说明：**

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| id | string | ✅ | 唯一标识符 |
| name | string | ✅ | 显示名称 |
| icon | string | ❌ | Emoji 图标 |
| description | string | ❌ | 简短描述 |
| author | string | ❌ | 作者 |
| version | string | ❌ | 版本号 |
| homepage | string | ❌ | 主页地址 |
| plugins | string[] | ❌ | 推荐插件 ID 列表 |
| windowState | object | ❌ | 默认窗口状态 |

### 3. 添加插件

将插件放入 `plugins/` 目录：

```
my-profile/
└── plugins/
    ├── plugin-a/
    │   ├── plugin.json
    │   └── main.js
    └── plugin-b/
        ├── plugin.json
        └── main.js
```

或者在 `profile.json` 的 `plugins` 字段中引用市场上的插件 ID，安装 Profile 时会自动下载。

## 发布 Profile

### 方式一：提交到官方仓库

1. Fork 官方仓库
2. 在 `profiles/` 目录下创建你的 Profile 文件夹
3. 提交 Pull Request
4. 等待审核通过

### 方式二：自托管

1. 将 Profile 打包为 ZIP 文件
2. 上传到你的服务器或 GitHub Releases
3. 在 `profiles/registry.json` 中添加 Profile 信息
4. 提交 PR 更新索引

## Profile 打包

### 打包结构

```
my-profile.zip
├── profile.json
├── icon.png
└── plugins/
    └── my-plugin/
        ├── plugin.json
        └── main.js
```

### 打包命令

```powershell
# 进入 Profile 目录
cd User/Data/Profiles/my-profile

# 打包
Compress-Archive -Path * -DestinationPath ../my-profile.zip
```

## 索引文件格式

### profiles/registry.json

```json
{
  "version": 1,
  "updated": "2025-12-15",
  "profiles": [
    {
      "id": "genshin",
      "name": "原神",
      "icon": "🎮",
      "version": "1.0.0",
      "author": "ColinXHL",
      "description": "原神游戏配置，包含方向标记插件",
      "tags": ["原神", "米哈游", "开放世界"],
      "plugins": ["genshin-direction-marker"],
      "downloads": 256,
      "stars": 89,
      "downloadUrl": "https://github.com/.../genshin-profile.zip"
    }
  ]
}
```

**索引字段说明：**

| 字段 | 说明 |
|------|------|
| id | Profile 唯一标识符 |
| name | 显示名称 |
| icon | Emoji 图标 |
| version | 版本号 |
| author | 作者 |
| description | 描述 |
| tags | 搜索标签 |
| plugins | 包含的插件 ID 列表 |
| downloads | 下载次数 |
| stars | 收藏数 |
| downloadUrl | 下载地址 |

## 最佳实践

### 1. 提供合理的默认配置

```json
{
  "windowState": {
    "x": 100,
    "y": 100,
    "width": 400,
    "height": 300,
    "opacity": 0.8
  }
}
```

### 2. 只包含必要的插件

不要包含过多插件，让用户根据需要自行添加。

### 3. 编写清晰的描述

描述应该让用户一眼就知道这个 Profile 适合什么场景。

### 4. 使用有意义的图标

选择能代表游戏或场景的 Emoji 图标。

## 版本更新

更新 Profile 时：

1. 更新 `profile.json` 中的 `version` 字段
2. 更新索引中的版本号和下载地址
3. 提交 PR

## 常见问题

### Q: Profile 和插件的关系是什么？

Profile 是配置容器，可以包含多个插件。一个插件可以被多个 Profile 使用。

### Q: 用户安装 Profile 后可以修改吗？

可以。用户可以自由添加、删除插件，修改配置。

### Q: 如何更新已安装的 Profile？

用户可以在 Profile 市场中检查更新，选择更新或保留当前配置。

---

相关文档：
- [插件 API 文档](plugin-api.md)
- [插件发布指南](plugin-marketplace.md)
