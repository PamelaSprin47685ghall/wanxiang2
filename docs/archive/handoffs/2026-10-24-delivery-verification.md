# 落地起点与交付验证收口证据（2026-10-21 / 2026-10-24）

> 历史账目，只读。原载于根 `AGENTS.md`，归档于此；根文档只留指针。
> 原文出自 `30eb138:AGENTS.md` 第 152–177 行，未改动内容。

## W7 收口证据（2026-10-21）

- 单入口投影：SidebarDesk、SettingsScreen、ComposerDesk 与 ErrorPanel 的生产调用方使用完整视图状态；命令与插槽按 Apply/Wire/Slot 区分。生产源码中的自有公开 `Set*` 成员已清理（历史 tools/Old* 不计）。
- 外观：控件外观订阅迁至 DynamicResource 与主题派生资源键；主题字典由入口壳挂入 Application，SettingsScreen 不重复挂载同一字典，避免 ResourceDictionary 多 owner 启动异常。
- 接线：谱系来自列表快照；引导状态回报模型；搜索命中经统一滚动 Effect 消费；检查主机通过真实 doctor.request/report 往返，包含请求关联、断线和超时处理。
- 退役：删除两个无生产消费者的组合者；CardReconcile 包改为 CardBits；ModelVision 并入 HostCatalog，消除新增的库间依赖边。纯界面复刻金样改行为断言，线协议与字节格式等外部契约向量保留。
- 实测（基线历史）：`./x all` 通过：118 个库测试工程、66 个组合者测试工程、9 波打包、仓库外提交后重开得到 `Used 1UL`；`./x app` 双入口通过；`bash tools/smoke_desktop.sh` 全部断言通过（firstFrame 1695 ms、listReady 1892 ms、catalogReady 1926 ms，退出码 0，无未处理异常，存储锁释放）。这是所运行路径的证据，不代表所有用户旅程均已验证。**（2026-10-21 复核：上面两个工程数是那次运行时的数字，当前树上是 109 个库测试工程、67 个组合者测试工程——`./x check` 现在报 `109 libs with no sibling references; 71 composer(s) using packages only`；组合者用例 979 条，库与组合者合计 1906 条 `[<Fact>]`。）**
- 终局状态（Wave 0 审计执行后）：库合并收敛至 109 个（8 组合并、删死包 AguiMessageMap、收回 DraftSaveGate）、组合者 71 个（67 个测试工程），全部工程通过依赖闭包与无兄弟引用检查；已包含 PWA 根字体设置（消除中文豆腐块）与输入上屏三缺陷修复。已在提交 `14e4625` 全量提交入库。

## 2026-10-24 本轮交付验证收口证据（已实测通过）

- 状态基线与改动收口：在初始快照（HEAD `14e4625`）及后续基线提交（`0074101`）基础上，完成全部门禁、桌面端与 PWA 发布产物端到端冒烟验证及独立审查修复：
  - 生产源码关键修复：
    1. `compose/AppShell/AppShell.fs` 中的 `ShellCommands.IsDialogOpen` 与 `IsPopupOpen` 直接委托查询真实控件层 `views().Overlay.Layer.IsDialogOpen` 与 `views().Overlay.Layer.HasActivePopup()`，根除状态树 `model.Overlay` 无生产者恒为 false 导致弹窗/菜单打开时全局快捷键意外穿透的缺陷。
    2. `app/Desktop/Program.fs`、`app/Pwa/Program.fs` 与 `compose/AppShell/tests/Rig.fs` 中的 `SettingsHostVisible` 改为 `root.Children.Insert(0, settings.View)` 置于底层工作区槽位，确保设置页处于 `OverlayDesk` 浮层（scrim、dialogCards、popupCards、toast）之下，不遮挡设置页内部拉起的弹窗与菜单。
    3. `app/Desktop/Program.fs` 将 `Palette.Run` 的 `PaletteRecentFile.saveRecent` 订阅移至 `shell.Build()` 之后并在 `FlushDrafts` 中同步落盘，确保最近执行命令在状态树更新后立即持久化，不落后一步。
  - 门禁与实测证据（2026-10-24 全部动态跑通，退出码均为 0）：
    - `./x check`、`./x hygiene`、`./x lint`、`./x all`（109 个库测试工程、67 个组合者测试工程、9 波打包、仓库外 `consume` 验证）、`./x app`（Desktop 与 PWA 双入口构建）全部通过。
    - 画廊像素基线门禁（G3）：`compose/Gallery/baselines/` 覆盖全部 41 个条目 × 深浅双主题 = 82 张 PNG 基线，在 Linux 容器环境（`wx2-linux-gallery-env:local`）下 `bash tools/gallery-baseline.sh` 82/82 逐像素核验通过（基线平台口径 2026-10-24 起改为 Linux 本机优先、macOS 本机次之，不再使用容器；见 compose/Gallery/AGENTS.md G3 节）；画廊无障碍遍历（G5）41 条目 268 个控件 0 缺陷通过。
    - 桌面端真实冒烟（`bash tools/smoke_desktop.sh`）：全部断言通过（`startup.firstFrame` 2125 ms、`startup.listReady` 2332 ms、`startup.catalogReady` 2380 ms，退出码 0，无异常，数据锁释放）。
    - PWA 发布产物端到端冒烟（`bash tools/smoke_published.sh artifacts/publish/osx-arm64`）：28/28 项断言全部通过（同时通过 macOS Vision OCR `tools/smoke-ocr.swift` 与 `WX_SMOKE_OCR=tesseract` 双引擎验证，区分核验 ToolTip、消息卡 ContextMenu、输入框 MenuFlyout 三类弹出层中文词条，以及命令面板执行、设置开关切换、主题轮转与草稿往返）。
  - 环境事实（见 `docs/evidence/2026-10-23-handoff/initial-state.md`）：宿主无 orca/speech-dispatcher/at-spi-bus-launcher，adb 无连接设备，真机读屏与手机物理设备仍保持未验证。

从一个真实问题开始：补明人在乎的结果，选出最可能推翻设计的边界，按 LEMMA 2 准备下一小步。由具体场景落实最简独立职责及真实组合，再逐个加强。证明填入相应 SPEC，尚未成立的保留 `[TODO]`；不必先设计全系统，也不必先填完这份模板才动手。
