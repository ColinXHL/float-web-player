## v1.4.3 🎉 Release Notes

**AkashaNavigator 1.4.3 正式版发布！** 本次更新修复 Profile 缺失插件无法从官方仓库正常补齐的问题，并改善多显示器和高 DPI 环境下的悬浮窗口位置恢复。

### 🧩 Profile 插件补全

- “一键安装缺失插件”现在会正确从官方插件仓库下载并安装
- 仅在插件安装成功后恢复 Profile 关联，避免失败后留下错误状态
- 增加下载进度和忙碌状态，防止重复点击发起多次安装
- 补全当前 Profile 后自动重新加载插件，无需重启程序

### 🖥️ 窗口位置修复

- 改善多显示器、不同 DPI 缩放和显示器旋转后的悬浮窗口位置恢复
- 显示器被移除或旧坐标超出屏幕时，自动将窗口恢复到可见区域

### 📥 下载

| 类型 | 下载 |
|------|------|
| 安装版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.3/AkashaNavigator.Install.1.4.3.exe" title="Windows x64 安装版"><img src="https://custom-icon-badges.demolab.com/badge/.exe-0078D6?logo=windows11&logoColor=white"/></a> |
| 便携版 | <a href="https://github.com/ColinXHL/akasha-navigator/releases/download/v1.4.3/AkashaNavigator_v1.4.3.7z" title="Portable 便携版"><img src="https://custom-icon-badges.demolab.com/badge/.7z-4CAF50?logo=7zip&logoColor=white"/></a> |

> 推荐使用 Profile 市场和“一键安装缺失插件”的用户升级到 1.4.3。
