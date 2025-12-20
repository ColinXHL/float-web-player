# AkashaNavigator 插件仓库

官方和社区验证的 AkashaNavigator 插件集合。

## 目录结构

```
plugins/
├── README.md                    # 本文件
├── registry.json                # 插件索引（供插件市场使用）
└── genshin-direction-marker/    # 原神方向标记插件
    ├── plugin.json              # 插件清单
    ├── main.js                  # 插件入口
    └── README.md                # 插件说明
```

## 可用插件

| 插件 | 版本 | 描述 |
|------|------|------|
| [genshin-direction-marker](./genshin-direction-marker/) | 2.0.0 | 识别攻略视频中的方位词，在小地图上显示方向标记 |

## 安装插件

### 方式一：通过插件市场（推荐）

1. 打开 AkashaNavigator
2. 进入设置 → 插件管理
3. 浏览并安装所需插件

### 方式二：手动安装

1. 下载插件文件夹
2. 复制到 `User/Data/Profiles/{profile}/plugins/` 目录
3. 重启应用或重新加载 Profile

## 开发插件

请参阅 [插件开发文档](../docs/plugin-api.md)

## 贡献插件

1. Fork 本仓库
2. 在 `plugins/` 下创建插件目录
3. 提交 Pull Request

### 插件要求

- 必须包含 `plugin.json` 清单文件
- 必须包含 `README.md` 说明文档
- 代码需通过基本审核

## 许可证

各插件遵循其自身的许可证，默认为 MIT。
