# FloatWebPlayer - 开发任务清单

## Phase 1: 基础框架

- [x] 1. 创建解决方案和项目结构
- [x] 2. 实现 PlayerWindow 基础框架（无边框、自定义细边框、拖拽调整大小）
- [x] 3. 集成 WebView2 并实现 Cookie 持久化
- [x] 4. 实现 PlayerWindow 控制栏 Overlay（显示/隐藏动画）+ 细滚动条样式

## Phase 2: 控制栏窗口

- [x] 5. 实现 ControlBarWindow（URL栏、导航按钮、收藏按钮、菜单按钮）
- [x] 6. 实现 ControlBarWindow 显示/隐藏逻辑（屏幕顶部触发）
- [x] 7. 实现两窗口 URL 双向同步

## Phase 3: 快捷键与控制

- [x] 8. 实现全局快捷键服务（RegisterHotKey）
- [x] 9. 实现视频控制 JS 注入（播放/暂停、快进/倒退）
- [x] 10. 实现透明度调节
- [x] 11. 实现鼠标穿透模式
- [x] 12. 实现 OSD 操作提示窗口

## Phase 4: 数据与设置

- [x] 13. 实现边缘吸附功能
- [ ] 14. 实现 JSON 数据服务（历史记录、收藏夹 CRUD，按 Profile 隔离）
- [ ] 15. 实现 ProfileManager 服务（加载/切换 Profile，Default 兜底）+ JSON 配置存储
- [ ] 16. 实现历史记录 UI 窗口
- [ ] 17. 实现收藏夹 UI
- [ ] 18. 实现设置窗口

## Phase 5: 测试与优化

- [ ] 19. 功能测试与 Bug 修复
- [ ] 20. 性能优化
- [ ] 21. 打包发布

---

## 进度记录

| 日期 | 完成任务 | 备注 |
|------|----------|------|
| 2025-12-12 | 项目初始化 | 创建设计文档、todo.md、.gitignore |
| 2025-12-12 | PlayerWindow 基础框架 | 无边框窗口、2px细边框、8方向拖拽调整大小 |
| 2025-12-12 | WebView2 集成 | Cookie 持久化、默认加载 B站 |
| 2025-12-12 | 控制栏 Overlay | 显示/隐藏动画、窗口按钮、细滚动条样式 |
| 2025-12-12 | ControlBarWindow | URL栏、导航按钮、收藏/菜单按钮、水平拖动条、双窗口事件绑定 |
| 2025-12-12 | ControlBarWindow 显示/隐藏逻辑 | 屏幕顶部1/8触发区域、细线→展开→延迟隐藏 |
| 2025-12-12 | 全局快捷键服务 | HotkeyService.cs，支持 5/6/`/7/8/0 键，视频控制方法 |
| 2025-12-12 | 透明度调节和鼠标穿透 | 7/8 键调节透明度(20%-100%)，0 键切换穿透模式 |
| 2025-12-12 | OSD 操作提示窗口 | 屏幕居中半透明窗口，1s 自动淡出，输入模式不触发快捷键，输入框聚焦时控制栏不隐藏 |
| 2025-12-12 | 代码整理重构 | 统一 Win32 API 到 Win32Helper.cs，创建 AppConstants.cs 集中管理常量，创建 Models 目录和数据模型（AppConfig/WindowState/HistoryItem/BookmarkItem），HotkeyService 添加输入模式检测，移除 UseWindowsForms 依赖 |
| 2025-12-12 | 快捷键自定义架构 | HotkeyBinding/HotkeyProfile/HotkeyConfig 模型，ActionDispatcher 动作分发器，支持组合键 (Ctrl/Alt/Shift) 和进程过滤，预留多 Profile 和脚本扩展 |
| 2025-12-12 | 边缘吸附功能 | WM_MOVING/WM_SIZING 消息钩子，10px 阈值吸附到屏幕工作区边缘，使用鼠标意图位置算法确保拖动跟手，支持 DPI 缩放 |
