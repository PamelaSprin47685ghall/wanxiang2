# 界面专业化重构 · 第一阶段：现状审计与改造方案

日期：2026-10-30。范围：桌面端（Avalonia）与 PWA 共享界面。本阶段只做诊断与方案，未批量修改界面样式。

> 方法：读根 `AGENTS.md` 与 `docs/ux/` 全部 9 篇现行文档；核对主题令牌（`libs/ThemePalette`、`libs/ThemeMetrics`、`compose/Theme`、`libs/SpacingRamp`）与主要组合者（`compose/ShellDesk`、`SidebarDesk`、`ChatScreen`、`SettingsScreen`、`ModelSwitchPanel`、`ModelSwitchDesk`）的实现；运行真实产品采集基准（命令与结果见 §6）。截图在 `docs/evidence/ui-refactor-baseline/`。
>
> 结论标注口径：**已确认**＝代码与截图能直接支持；**待验证**＝有线索但未核实；**方案**＝需要前后对比或拍板后才能实施。

## 1. 现状基线

产品主线与信息架构（对话优先、侧栏找回、输入区发起、设置维护）经此前多轮走查已成立，本轮未发现架构级问题。视觉体系已有完整令牌：暖纸/墨夜双主题、Fluent 2 间距刻度（`SpacingRamp`）、133 项度量（`ThemeMetrics`）、WCAG 对比度机械门禁。这是本次重构的底座，不是要推翻的对象。

基准运行证据（macOS 本机，Debug 构建）：

- 桌面冒烟（`tools/smoke_desktop.sh`，隔离临时数据）：退出码 0，`startup.firstFrame` 2745ms、`listReady` 2959ms、`catalogReady` 2966ms，存储文件与锁释放断言全部通过。
- PWA：`run-pwa.sh` 构建成功、静态服务 HTTP 200；真实浏览器视口 573×866、DPR 2 下首屏渲染正常（见 `pwa-home-light-dpr2.png`）。
- 画廊基准：8 个关键条目浅色 + 3 个深色，见 `docs/evidence/ui-refactor-baseline/`。

## 2. 问题清单（按影响排序）

| 编号 | 级别 | 位置 | 问题 | 证据 | 方案 | 验收 |
| --- | --- | --- | --- | --- | --- | --- |
| R0 | P1（已修） | `app/Pwa/Program.fs` | 未提交改动中缺 `open System.Threading`，PWA 入口无法编译（FS0039） | 构建失败输出；补引用后 0 警告 0 错误 | 已补一行 open，无行为改动 | `run-pwa.sh` 构建通过；`./x check` 通过 |
| A1 | A | `libs/ThemeMetrics/ThemeMetrics.fs`（FontCaption 11.5 / Small 12.5 / Body 13.5 / Reading 14.5） | 相邻字号级差仅 1px，正文与辅助文字、标签与正文的层级区分弱；长对话与设置表单里「哪里是正文、哪里是说明」要靠颜色猜 | `ThemeMetrics.fs:326-333`；画廊基准图可见 | 方案：在独立分支试 1.25 比率（如 Body 14、Reading 15.5、Caption 12），出前后对比截图后再定。**不直接全局放大**（任务约束） | 前后截图对比 + 画廊基线重生成 + `./x compose` 全绿；需负责人确认数值 |
| A2 | A | `libs/ModelSwitchPanel/ModelSwitchPanel.fs`、`compose/AppShell/AppShell.fs` | 模型切换浮层：探活状态只有文字 tag（"已探测/未探测"），无颜色语义；行高 32、行圆角 4（低于控件默认 8 的规范档）；服务名与模型名同灰度同字号，层级平 | `ModelSwitchPanel.fs` 的 `tag`/`row`（MinHeight 32、CornerRadius 4、muted tag）；`ModelSwitchPanel_models.png` | 对照参考设计（列表 + 状态点 + 标签 + 备注）：探针状态加色点（成功 Success / 失败 Danger / 未探测 TextFaint），行高提到 36、圆角对齐 `Radius.md`，服务名作为弱化第二行。状态事实仍来自 `probeNote` 字符串归类，不新增状态源 | 画廊条目更新 + 像素基线重生成 + `ModelSwitchPanel` headless 测试补状态点断言；作为第一批判试点先出前后对比 |
| A3 | A | `compose/SidebarDesk/SidebarDesk.fs`（`previewFor`） | 会话行第二行把「归档/分叉前缀 + 模型 · 时间（+ 搜索片段）」拼成一条同灰度文本，扫读时时间与模型不可分 | `SidebarDesk.fs` 的 previewFor 实现；`SidebarDesk_rows.png` | 第二行拆层：时间右对齐弱化，模型名弱化居左，归档/分叉改小 tag；命中片段保持独立一行 | `SidebarDesk` headless 测试 + `SidebarDesk_rows` 基线重生成 + 窄屏（compact 断点 720 以下）不溢出 |
| A4 | B | `compose/SettingsScreen/SettingsScreen.fs`（surface 固定 Width=720） | 大窗口下设置页居中 720，两侧留白偏大；信息密度偏低 | `SettingsScreen.fs` 的 surface 定义；`SettingsScreen_providers.png` | 观察项：先在第一批改造后重新评估，若仍显空再议（涉及 SettingsFrame 布局，不在第一批判动） | 暂无（观察项） |
| A5 | 待验证 | `compose/MessageCardView` 操作条 | 消息操作（复制/编辑等）在无头画廊基准图中呈常驻；真实界面是否 hover 显示未能从静态基准确认（旧仓库 r19 曾改为非常驻） | `MessageCardView_assistant.png`；旧账 r18#6 | 待验证：读 MessageCardView 实现与 headless 用例确认常驻策略，再决定是否列入第二批判 | 核实后补记 |
| R-b | 边界 | PWA 真机 | 中文输入法、触屏滚动/键盘、真实模型链路未走查（headless 无法替代） | 历轮记录一致 | 保留为人工走查项；本轮新增浏览器工具具备条件后可补自动化交互冒烟 | 真机走查记录 |

已排除的怀疑：术语与文案（lint 门禁全绿）、对比度（A11y 机械回归全绿）、焦点/读屏语义（整树遍历 + macOS VoiceOver 实测已收口）——这些不再列问题。

## 3. 设计定位与原则

延续「对话优先」与现有令牌体系，目标是**层级更清楚、状态更可读、密度更均衡**，不做视觉推倒重来：

1. 每个改动先在代表页（画廊条目 + 真实截图）出前后对比，再推广；
2. 只用现有令牌与原生控件；确需新令牌（如状态点尺寸）先落 `ThemeMetrics`/`SpacingRamp` 的既有档位，不发明档外值；
3. 状态用互斥语义表达（探活三态），不堆布尔；
4. 每批一个清晰提交节点，可独立回退；
5. 门禁全程生效：`./x check`（含格式与画廊覆盖）、`./x hygiene`、`./x lint`、G3 像素基线、G5 遍历、`./x compose`。

## 4. 实施批次

| 批次 | 内容 | 涉及 |
| --- | --- | --- |
| 第一批（试点） | A2 模型切换浮层（含参考设计对齐）+ A3 侧栏行层级 | `libs/ModelSwitchPanel`、`compose/SidebarDesk`、画廊条目与基线 |
| 第二批 | 对话阅读区与输入区：消息卡层级（含 A5 结论）、正文宽度与间距核对 | `compose/ChatScreen`、`compose/MessageCardView`、`compose/ComposerDesk` |
| 第三批 | 设置页与浮层（命令面板、审批、弹窗）统一尺寸与对齐；评估 A4 | `compose/SettingsScreen`、`compose/OverlayDesk` 等 |
| 第四批 | 全局字号/间距收敛（A1，需负责人确认数值后执行） | `libs/ThemeMetrics`、`compose/Theme` |

### 第四批前置试验结论（2026-10-31，试验分支 `trial/a1-type-scale`，未合并）

按 §5「必须独立分支 + 前后对比」执行了一次 1.25 比率试验，得出两条**与 A1 原假设不同**的事实：

1. **A1 的影响面比文档预估小得多。** 把 `FontMicro`…`FontDisplay` 八档整体上调（10.5→11、11.5→12、12.5→13、13.5→14、14.5→15.5、16→17、19→20、25→26）后重打 feed 并重渲画廊，四条代表条目里只有两条像素变化：
   - `SettingsScreen.providers` 变化（过滤字节差异 1.74%）、`SidebarDesk.rows` 变化（0.13%）；
   - **`MessageCardView.assistant` 与 `ChatEmpty.empty` 逐字节完全相同**。
   原因：阅读列正文字号不取自 `Metric.FontReading`，而取自用户偏好 `UiPrefs.fontScale`（默认 14.5，设置页可调 12–19，见 `libs/WorkspaceState/UiPrefs.fs` 与 `compose/AppShell/AppShell.fs` 的 `FontSize = model.Prefs.fontScale`）。所以「全局放大字号」改不到主要阅读面，A1 想解决的「正文与说明层级弱」在阅读区需另找手段（调 `fontScale` 默认值，或改卡片内部层级色），不能靠改度量档解决。
2. **`Metric.FontReading` 是死档。** 全仓零消费者（`grep -rn FontReading` 只命中 `ThemeMetrics.fs` 自身的 `all` / `initial` 两处）。历史原因是 clean-break 重构（`ad22bf0`）把输入区字号从 `FontReading` 改为 `FontBody`，旧消费点删除后登记档没跟着删。它仍在公开 `ThemeMetrics.all` 里、仍参与主题推导，属于「保持着一项无人使用的公开承诺」。

**处置**：A1 数值仍未拍板，本试验只产出上述事实，不改生产数值。`Metric.FontReading` 的删除或改用途属公开契约变更，需一并决定——若确认阅读列不打算回到度量档（现状由用户偏好直接驱动，是合理设计），则应删除该死档，避免下一个人误以为改它能改阅读正文。

第一批先行理由：A2 风险最小（单控件库 + 画廊条目 + 基线重生成），能最快建立「前后对比 → 门禁 → 提交」的完整回路。

## 5. 风险与回归计划

- **像素基线平台口径**：画廊基线历史上存在 Ubuntu CI 与本机两种口径的反复（见 `compose/Gallery/AGENTS.md`）。改视觉的批次必须同步重生成基线，并在 CI 上复验后再合入。
- **字号改动（A1）影响面大**：涉及全部 82 张基线与多语言换行，必须独立分支 + 前后对比，不与其他批混提交。
- **回归面**：每批跑 `./x check` + `./x hygiene` + `./x lint` + 相关库测试 + `./x compose`；交付前桌面冒烟 + `tools/smoke_published.sh`。
- **不做的事**：不更换技术栈、不引入 React/HTML 改造、不删功能、不为美观加渐变阴影动画、不改协议与存储。

## 6. 本阶段验证记录

- `./x compose`：67 个组合工程全部通过（2026-10-30 00:51，234s）。
- `./x check`：104 库零兄弟依赖、71 组合者、17 控件库覆盖、格式门禁（96 文件）通过。
- `./x hygiene` / `./x lint`：519 个测试文件、1864 个文案串，全部通过。
- `tools/smoke_desktop.sh`：全部断言通过（firstFrame 2745ms）。
- PWA：修复 FS0039 后构建 0 警告 0 错误，静态服务 HTTP 200，真实浏览器渲染正常。
- 画廊基准渲染：11 张 PNG 生成成功（8 浅 + 3 深），已归档 `docs/evidence/ui-refactor-baseline/`。
- 未做：PWA 真机交互走查（R-b）；A5 的实现核实。

## 7. 待负责人确认

1. ~~A1 字号方案是否立项~~ → **已执行前置试验，结论见 §4《第四批前置试验结论》**：改度量档影响不到阅读正文（阅读字号走 `UiPrefs.fontScale`），且 `Metric.FontReading` 是零消费者死档。仍需拍板两件事：
   - 阅读区层级是否要做（若做，手段是调 `fontScale` 默认值或改卡片内部层级，不是改度量档）；
   - `Metric.FontReading` 删除，还是改作他用。
2. A2 模型浮层按参考图改造（状态点 + 行高 + 圆角）是否按第一批判试点执行。
3. A4（设置页宽度）是否保持观察、暂不动 —— 第三批已把宽度与圆角接回刻度（`Metric.SettingsSurfaceWidth` / `CornerRadius.RadiusXl`），数值未变，后续调整只需改一个登记值。
