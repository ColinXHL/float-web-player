## v1.4.2 🎉 Release Notes

**AkashaNavigator 1.4.2 正式版发布！** 本次更新为 Companion 插件加入独立资源更新能力，插件可在不重新发布安装包的情况下同步黑名单等数据文件。

### 🔄 插件资源更新

- 已订阅并安装的插件可在程序启动时自动检查资源更新
- 支持从 GitHub 或 CNB 下载，并校验文件大小与 SHA-256
- 资源采用内容寻址存储和原子状态切换，下载失败时继续使用上一个有效版本
- 插件设置页新增手动检查资源更新入口
- 新增“启动时自动更新插件资源”选项，默认开启

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.2/AkashaNavigator.Install.1.4.2.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.2/AkashaNavigator_v1.4.2.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 升级到 1.4.2 后，Akasha 原神自动化插件可以独立同步 BetterGI 黑名单；资源更新失败不会影响插件继续运行。
