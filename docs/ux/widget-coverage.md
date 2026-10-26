# 控件库显式清单与画廊覆盖规则 (Widget Library Coverage)

> **历史快照 + 现状对照。** 本文件写于 2026-10-23，原 §2/§4 记载 15 个控件库 / 26 条画廊条目，但早先的 `ccefc35` 已新增覆盖条目，不能将这些旧数字认定为该日准确快照。下方旧表仅供追溯，不作现行清单或历史数量证明。2026-10-26 按当前树实测：**17 个控件库**、**41 条画廊条目**、**71 个组合者**。
>
> 权威源与再生成方法：
>
> | 事实 | 权威源 | 取法 |
> | --- | --- | --- |
> | 什么算控件库、当前有几个 | 根 `AGENTS.md`《控件库交付约定（沿用）》＋`tools/avalonia-controls.txt` | 逐库 `grep` 生产源码（下表给出一行命令）；CI 上 `./x check` 同样输出 `17 control librar(y|ies)` |
> | 画廊条目清单 | `compose/Gallery/Gallery.fs` 的 `entries` | `bash tools/gallery.sh --list`（一条一行：条目名 / 控件库 / 说明）。**不要用 `Widget` 去重**：那是条目所归属的控件类，多条条目可共用同一个 `Widget`（如 `composerEntry` 生成的条目全挂在 `ComposerBox` 下），去重后只有 22 个，不是条目数。无 `dotnet` 时退用基线文件名：`ls compose/Gallery/baselines/*.png \| sed -E 's#.*/([^/]+)\.(dark\|light)\.png#\1#' \| sort -u \| wc -l`（当前 41） |
> | 像素基线 | `compose/Gallery/baselines/` | `ls compose/Gallery/baselines/*.png \| sed -E 's#.*/([^/]+)\.(dark\|light)\.png#\1#' \| sort -u \| wc -l` |
>
> 控件库逐库重算（不依赖一次把全部路径交给 `find`）：
>
> ```bash
> pattern=$({ grep -v '^#' tools/avalonia-controls.txt; } | grep -v '^[[:space:]]*$' | paste -sd'|' -)
> for d in libs/*/; do
>   name=$(basename "$d"); hit=""
>   while IFS= read -r f; do
>     grep -qE "(^|[^.[:alnum:]_])($pattern)\(" "$f" 2>/dev/null && { hit=1; break; }
>   done < <(find "$d" \( -name '*.fs' -o -name '*.cs' \) -not -path '*/tests/*' -not -path '*/obj/*' -not -path '*/bin/*')
>   [ -n "$hit" ] && echo "$name"
> done
> ```
>
> 本规范的**规则部分**（§1 判据、§5 覆盖契约）仍然有效；**清单部分**（§2、§4）是快照，与上面的权威源冲突时以权威源为准。

> 审计与生效日期：2026-10-23。承接《展望.md》§3 G3/G5 自动化与画廊覆盖要求。
> 本规范明确全仓「哪些库算控件库」、显式清单、状态覆盖规则、无障碍（G5）与像素基线（G3）门禁契约。

---

## 1. 控件库资格认定规则

依据根 `AGENTS.md`《控件库交付约定》及决策提交 `ccefc35`：引用 Avalonia 本身不足以认定控件库；生产源码构造 Avalonia 控件才触发覆盖要求。画廊提供真实呈现与交互的检查入口，不能用库名或“附加行为”标签豁免实际构造控件的代码。

### 1.1 控件库认定条件

位于 `libs/<Name>/`，且生产源码（不含 `tests/`、`obj/`、`bin/`）构造了至少一个 Avalonia 控件类型，即须有画廊条目。类型清单见 `tools/avalonia-controls.txt`；`./x check` 执行覆盖核对，不另设人工豁免名单。

### 1.2 不构造控件的库

只做数据、资源或行为附加且不构造控件的库，不触发本覆盖要求，例如 `CopyButton`、`ImeShield`、`InputSense`。其自身契约仍需相应测试。

`FieldCheck`、`FocusOrigin`、`IconSet` 构造控件，不能按职责标签豁免，分别由 `FieldCheck.invalid`、`FocusOrigin.programmatic`、`IconSet.glyphs` 提供画廊入口。有条目只证明存在检查入口，不证明全部状态、无障碍行为或像素已验证。

---

## 2. 全仓控件库显式清单

**采集时点（2026-10-23）：全仓 `libs/` 下共有且仅有 15 个实体控件库。当前（2026-10-26）按同一机械判据重算为 17 个，但名单不只是「下表 15 个 + 2」：下表里的 `CopyButton` 现在只附加行为、不构造 Avalonia 控件，按根 `AGENTS.md`《控件库交付约定》的机械判据已不算控件库；新增的 `FieldCheck`、`FocusOrigin`、`IconSet` 三个都构造了控件。以文首的逐库重算命令为准（当前实际为 `ActionStrip`、`ApprovalPanel`、`AttachmentTray`、`BatchBar`、`BrandMark`、`ChatEmpty`、`ChatHeader`、`ConfirmPane`、`DevicesPane`、`ExportSheet`、`FieldCheck`、`FocusOrigin`、`IconSet`、`ModelSwitchPanel`、`PalettePanel`、`PopupMenu`、`ToastStrip`）。下表保留 2026-10-23 的时段记录。**

| 序号 | 控件库名称 | 导出的核心控件/视图类型 | 职责范围说明 |
|:---:|:---|:---|:---|
| 1 | `ActionStrip` | `ActionStrip`, `ToolCallCard`, `AttachmentChip`, `ErrorPanel`, `FoldHeader`, `RevealPane` | 消息卡附属操作条、工具调用卡、附件芯片、错误卡、思考折叠头、延迟揭示面板 |
| 2 | `ApprovalPanel` | `ApprovalPanel` | 工具调用审批面板（参数预览、批准/拒绝操作） |
| 3 | `AttachmentTray` | `AttachmentTray`, `ComposerBox`, `ModelChip` | 输入框底栏、附件待上传托盘、模型切换芯片 |
| 4 | `BatchBar` | `BatchBar`, `SidebarSearch`, `SidebarBrand`, `HostSwitcher`, `ConversationRows`, `ListEmptyCard` | 侧栏多选批量条、搜索栏、列表行、空态卡、主机切换器 |
| 5 | `BrandMark` | `ShellBrand` | 应用品牌 Logo 矢量渲染控件 |
| 6 | `ChatEmpty` | `ChatEmpty`, `SkeletonList`, `ChatScroller`, `ExchangeNav` | 对话区空态欢迎页、起手式芯片、骨架屏、问答导航浮标 |
| 7 | `ChatHeader` | `ChatHeader` | 对话顶栏（标题、会话操作菜单、模型标识） |
| 8 | `ConfirmPane` | `ConfirmPane`, `PromptPane`, `ConnectPane`, `TextEditPane`, `SessionPrefsPane`, `ShortcutHelpPane` | 模态对话框内部面板（确认、单行输入、连接配置、多行文本编辑、快捷键帮助） |
| 9 | `CopyButton` | `CopyButton` | 带复制中/复制成功微动效与读屏反馈的原生按钮 |
| 10 | `DevicesPane` | `SettingsFrame`, `ProvidersPane`, `ToolsPane`, `GeneralPane`, `DevicesPane`, `OnboardingPane` | 设置页各分区面板（模型服务、工具、常规、设备、引导配置） |
| 11 | `ExportSheet` | `ExportSheet`, `ImportSheet` | 会话导出选项与导入进度交互抽屉面板 |
| 12 | `ModelSwitchPanel` | `ModelSwitchPanel` | 模型服务与模型列表切换下拉气泡面板 |
| 13 | `PalettePanel` | `PalettePanel` | 命令面板（全局动作检索与快速定位） |
| 14 | `PopupMenu` | `PopupMenu` | 悬浮菜单原生气泡面板（右键上下文菜单、更多操作菜单） |
| 15 | `ToastStrip` | `ToastStrip` | 界面底部悬浮气泡通知条（成功、警告、错误通知） |

---

## 3. 全仓 UI 组合者 (UI Desks)

**采集时点快照（14 个）。** 当前 `compose/` 共 71 个目录，其中 67 个有测试工程；下面是当时挑出的 14 个万亿级装配面，不代表 `compose/` 的全量。全量以 `ls -d compose/*/` 为准。

1. `ComposerDesk`（输入区工作台）
2. `SidebarDesk`（侧栏工作台）
3. `ChatScreen`（聊天屏组合根）
4. `ChatPane`（消息流展示与卡片重排）
5. `MessageCardView`（消息卡组装）
6. `SettingsScreen`（设置中心全屏）
7. `DialogsDesk`（对话框调度）
8. `ApprovalDesk`（审批流调度）
9. `ModelSwitchDesk`（模型切换气泡调度）
10. `PaletteDesk`（命令面板调度）
11. `TransferDesk`（导入导出流转调度）
12. `OverlayDesk`（全局浮层、模态遮罩与弹窗栈调度）
13. `ShellDesk`（窗口根脚手架与响应式布局）
14. `MarkdownView`（Markdown 渲染器，含代码着色与公式嵌入）

---

## 4. 画廊状态矩阵覆盖映射表

**采集时点快照。** 组件画廊（`compose/Gallery`）当时是 26 个独立条目，对应上述 15 个控件库与 14 个组合者。当前（2026-10-26）是 **41 条条目**（`compose/Gallery/baselines/` 去重后与 `Gallery.fs` 的 `entries` 同为 41）。

下表 26 行里，25 行仍存在，`ApprovalDesk.request` 已被 `ApprovalPanel.approval` 取代。表中未列出的 16 条为后补：`ActionStrip.actions`、`ApprovalPanel.approval`、`AttachmentTray.items`、`BatchBar.selection`、`BrandMark.tile`、`ChatHeader.header`、`ConfirmPane.confirm`、`DevicesPane.devices`、`ExportSheet.export`、`FieldCheck.invalid`、`FocusOrigin.programmatic`、`IconSet.glyphs`、`ModelSwitchPanel.models`、`PalettePanel.palette`、`PopupMenu.menu`、`ToastStrip.toast`。**条目清单以 `compose/Gallery/Gallery.fs` 为准**（取法见文首表），下表保留为历史映射说明。

| 画廊条目名称 | 所属控件库/组合者 | 驱动场景与状态特征 | 覆盖的底层控件库 |
|:---|:---|:---|:---|
| `ComposerBox.idle` | `ComposerBox` / `ComposerDesk` | 已认证、无草稿输入、空闲就绪 | `AttachmentTray`, `BrandMark` |
| `ComposerBox.typing` | `ComposerBox` / `ComposerDesk` | 有草稿正文输入、发送按钮亮起 | `AttachmentTray` |
| `ComposerBox.attachments` | `ComposerBox` / `ComposerDesk` | 包含待上传与已就绪图片附件 | `AttachmentTray` |
| `ComposerBox.generating` | `ComposerBox` / `ComposerDesk` | 流式生成中，发送键转为停止键 | `AttachmentTray` |
| `MessageCardView.user` | `MessageCardView` | 用户提问卡片（含气泡与操作条） | `ActionStrip`, `CopyButton` |
| `MessageCardView.assistant` | `MessageCardView` | 助手回复（含思考链折叠块、正文） | `ActionStrip`, `CopyButton` |
| `MessageCardView.toolcall` | `MessageCardView` | 工具调用卡（含状态、参数详情展开） | `ActionStrip`, `CopyButton` |
| `MessageCardView.error` | `MessageCardView` | 生成失败卡（错误文案、重试与继续键） | `ActionStrip` (`ErrorPanel`) |
| `MessageCardView.streaming` | `MessageCardView` | 正在增量流式补齐的消息卡片 | `ActionStrip` |
| `SidebarDesk.empty` | `SidebarDesk` | 未连接主机态、侧栏空态与提示 | `BatchBar`, `BrandMark` |
| `SidebarDesk.rows` | `SidebarDesk` | 正常会话列表（置顶、普通、归档行） | `BatchBar` (`ConversationRows`) |
| `SidebarDesk.batch` | `SidebarDesk` | 多选批量管理模式（批量条、选择计数） | `BatchBar` (`BatchBar`) |
| `SidebarDesk.search` | `SidebarDesk` | 会话搜索中（搜索栏激活、无命中卡） | `BatchBar` (`SidebarSearch`, `ListEmptyCard`) |
| `SettingsScreen.providers` | `SettingsScreen` | 模型服务管理分区面板 | `DevicesPane` (`ProvidersPane`) |
| `SettingsScreen.tools` | `SettingsScreen` | MCP 与本机内置工具管理分区 | `DevicesPane` (`ToolsPane`) |
| `SettingsScreen.generation` | `SettingsScreen` | 对话默认生成参数配置分区 | `DevicesPane` (`GeneralPane`) |
| `SettingsScreen.devices` | `SettingsScreen` | 已配对设备列表与安全凭据管理 | `DevicesPane` (`DevicesPane`) |
| `SettingsScreen.appearance` | `SettingsScreen` | 外观主题、动效与布局配置分区 | `DevicesPane` (`SettingsFrame`) |
| `SettingsScreen.about` | `SettingsScreen` | 关于版本与系统诊断信息分区 | `DevicesPane` (`OnboardingPane`) |
| `OverlayDesk.dialog.confirm` | `OverlayDesk` / `DialogsDesk` | 模态确认删除对话框（标题/警告/确认） | `ConfirmPane` (`ConfirmPane`) |
| `OverlayDesk.dialog.prompt` | `OverlayDesk` / `DialogsDesk` | 单行输入对话框（重命名会话） | `ConfirmPane` (`PromptPane`) |
| `OverlayDesk.dialog.connect` | `OverlayDesk` / `DialogsDesk` | 主机连接配置对话框（地址与令牌） | `ConfirmPane` (`ConnectPane`) |
| `OverlayDesk.dialog.shortcuts` | `OverlayDesk` / `DialogsDesk` | 键盘快捷键帮助对话框 | `ConfirmPane` (`ShortcutHelpPane`) |
| `ApprovalDesk.request` | `ApprovalDesk` | 工具调用安全审批卡片（参数/批准/拒绝） | `ApprovalPanel` |
| `ChatEmpty.skeleton` | `ChatEmpty` | 会话加载中骨架屏与呼吸动画 | `ChatEmpty` (`SkeletonList`) |
| `ChatEmpty.empty` | `ChatEmpty` | 对话区起点欢迎页与起手式快捷芯片 | `ChatEmpty` (`ChatEmpty`) |

---

## 5. 覆盖规则契约

1. **真实状态驱动**：所有条目的视图状态一律通过真实 `Workspace.update` 消息处理或真实线协议快照注入，经 `Workspace.project` 投影，严禁手搓假的 `WorkspaceViewState`。
2. **像素基线比较（门禁 G3）**：
   - 画廊支持 Light 与 Dark 双主题确定性位图渲染；
   - 基线文件由 DevOps 在标准环境下执行 `bash tools/gallery-baseline.sh --generate artifacts/gallery-baselines` 生成，严禁伪造；
   - 门禁脚本 `bash tools/gallery-baseline.sh` 执行时，缺少基线文件或像素发生变化**必须红灯退出**（退出码 1），并输出高亮 diff 图片。
3. **无障碍全树遍历（门禁 G5）**：
   - 遍历画廊条目的真实可视控件树，审查所有可聚焦（`Focusable = true`）或实体交互控件（`Button`, `TextBox`, `ToggleSwitch`, `MenuItem` 等）；
   - 必须通过原生 `ControlAutomationPeer.CreatePeerForElement` 获取真实的 `peer`，检验角色不为 `None` 且 `peer.GetName()` 具有非空白有效名称；
   - 严禁仅靠只读 `AutomationProperties.GetName` 绕过 peer；
   - 包含明确的无名负例测试集，确保无名控件能敏锐引发红灯。
4. **覆盖核对命令**：
   - `./x check` 的 `gallery_coverage` 自动化检查控件库与画廊条目的一致性（缺条目即 `FAIL <库名>: control library has no gallery entry`）；`bash tools/gallery.sh --list` 列出全部条目。仓库里没有独立的 `tools/gallery-coverage.sh`，历史文档提到过它，但它从未存在。
