# compose/Gallery — 组件画廊

## 状态来源

画廊的每个条目由 `Workspace.init ()` + `Workspace.update` 的消息序列跑出，再经 `Workspace.project` 投影到真实控件（`compose/Gallery/Scenes.fs` 定义消息序列与固定时钟）。画廊不另造视图状态记录。

## 时钟

关于页环境事实由 `SettingsScreen` 构造参数 `environmentInfo` 注入，画廊固定为 `Desktop (.NET gallery)` / `Gallery OS`，不把本机运行时或操作系统版本写进像素基线；生产未注入时仍显示真实环境。固定时钟 `Scenes.fixedNow = 2026-10-21T08:00:00Z`。同一条目连渲两次 PNG 逐字节相同，是像素基线门禁的前提。

## 假端口

需要宿主端口的条目（对话框、审批、模型切换）由 `FakePorts` 那一小组显式假端口驱动，逐条列在本文件《条目》表与 `Gallery.fs` 注释。

## 条目

41 条，按控件库分（每个控件库至少一条，由 `./x check` 的 `gallery_coverage` 核对；判据见根 `AGENTS.md` §5《控件库交付约定（沿用）》）：

| 控件库 | 条目 |
| --- | --- |
| `ComposerBox` | `ComposerBox.idle`（已连接、无草稿）、`ComposerBox.typing`（有草稿）、`ComposerBox.attachments`（带附件——附件已进入状态树，见《已知缺口与各自的归属》4）、`ComposerBox.generating`（生成中，发送键变停止） |
| `MessageCardView` | `MessageCardView.user`、`MessageCardView.assistant`（含思考折叠块）、`MessageCardView.toolcall`、`MessageCardView.error`、`MessageCardView.streaming` |
| `SidebarDesk` | `SidebarDesk.empty`（未连接）、`SidebarDesk.rows`（置顶 / 普通 / 归档三行）、`SidebarDesk.batch`（批量模式——**多选已收口，走真状态树消息**）、`SidebarDesk.search`（搜索中） |
| `SettingsScreen` | `SettingsScreen.providers` / `.tools` / `.generation` / `.devices` / `.appearance` / `.about`（六个分区各一条） |
| `OverlayDesk` | `OverlayDesk.dialog.confirm` / `.prompt` / `.connect` / `.shortcuts`（四种对话框） |
| `ChatEmpty` | `ChatEmpty.skeleton`（对话列骨架屏）、`ChatEmpty.empty`（新对话空态欢迎卡与起手式芯片——走真实 `ShowPrompt` 入口） |
| `ActionStrip` | `ActionStrip.actions`（消息脚注的操作条） |
| `ApprovalPanel` | `ApprovalPanel.approval`（工具审批卡——走真实 `ToolApprovalRequested` 事件） |
| `AttachmentTray` | `AttachmentTray.items`（一张已就绪的图片与一个上传中的文件） |
| `BatchBar` | `BatchBar.selection`（已选 2 项） |
| `BrandMark` | `BrandMark.tile`（没有图时退化成「万」字） |
| `ChatHeader` | `ChatHeader.header`（对话顶栏——走状态树投影 `vs.Header`） |
| `ConfirmPane` | `ConfirmPane.confirm`（破坏性操作确认） |
| `DevicesPane` | `DevicesPane.devices`（配对码、地址与已授权设备） |
| `ExportSheet` | `ExportSheet.export`（导出对话框） |
| `FieldCheck` | `FieldCheck.invalid`（字段级校验反馈） |
| `FocusOrigin` | `FocusOrigin.programmatic`（聚焦来源标记） |
| `IconSet` | `IconSet.glyphs`（Lucide 图标集） |
| `ModelSwitchPanel` | `ModelSwitchPanel.models`（模型切换浮层） |
| `PalettePanel` | `PalettePanel.palette`（命令面板） |
| `PopupMenu` | `PopupMenu.menu`（浮层菜单） |
| `ToastStrip` | `ToastStrip.toast`（删除对话后的成功提示——走真实 `DeleteConversation` 消息） |

## 深浅双主题渲染与像素基线门禁 (G3)

画廊支持确定性的深浅（`Light` / `Dark`）双主题渲染与像素比对门禁（`Wanxiang2.Gallery.Baseline`，`compose/Gallery/Baseline.fs`）：

- **深浅两态切换**：`Gallery.setMode PaletteMode.Light` / `PaletteMode.Dark`，不仅切换万象自身活画笔，同时驱动 `Application.Current.RequestedThemeVariant` 同步切换 FluentTheme 原生控件模板主题，确保两套主题在同一画布尺寸（720 × 900）下具有确定性渲染。
- **基线契约**：基线必须由 DevOps 在实机环境中真实执行生成（`bash tools/gallery-baseline.sh --generate <目录>`），严禁伪造假图进仓库。
- **像素格式契约**：PNG 解码位图与渲染目标均经 Avalonia 原生 `CopyPixels(ILockedFramebuffer)` 转为 BGRA8888 / Premul 后再比较，不把源格式假定为 BGRA。固定色 PNG 往返必须保持通道值；单像素篡改必须报告准确坐标。新增回归已复现红蓝通道交换；修复后本机 Gallery 38/38 通过，单像素篡改准确定位到 (100,100)。**基线生成与核验平台：优先 Linux 本机（与 CI `gallery-baseline` 作业的 ubuntu-latest runner 同款环境），退而用 macOS 本机；不使用容器环境。** 2026-10-24 的 82/82 逐像素核验在本口径确立前于容器环境完成（证据见 docs/evidence/2026-10-24-verified-release/ 与根 AGENTS.md 2026-10-24 节），平台口径自本条起改为本机优先。
- **见红门禁**：`bash tools/gallery-baseline.sh` 执行时，缺少基线文件、位图尺寸不匹配或像素发生任何改变，必须以非零退出码（1）失败并红灯报错；差异时自动在 `<基线目录>/diff/` 输出高亮差异图（相同像素压暗，不同像素品红高亮）。
- **当前覆盖**：仓库内 `compose/Gallery/baselines/` 已包含全部 41 个条目 × 深浅两态 = 82 张基线（生成与核验口径见上条：Linux 本机优先，macOS 本机次之）。

## 真实控件树无障碍遍历与原生 Peer 审查 (G5)

2026-10-23 修复契约：附件托盘与用户气泡的非交互容器不进入 Tab 焦点序列；审批条目直接呈现审批面板，不以空浮层根替代；字段示例提供真实可访问名称。品牌标、图标集与骨架是纯展示条目，仅豁免「必须至少有一个交互控件」，不豁免子树缺陷扫描。新增五种交互条目回归已实跑失败（5 失败、38 通过）；修复后 Gallery 43/43 通过。审批面板由独立 ApprovalDesk 实例承载，避免复用已归 ChatScreen 所有的控件。2026-10-23 本机 G5 遍历 41 条目、268 个控件，0 缺陷；正式生成入口更新审批画面后 G3 82/82 通过。Linux CI 与真机读屏仍未由这些结果证明。

针对全画廊真实控件树的系统性无障碍审查（`Wanxiang2.Gallery.A11yAudit`，`compose/Gallery/A11yAudit.fs`）：

- **全树遍历**：挂入 Headless 窗口完成首帧布局与渲染后，递归遍历全可视树，甄别所有可见的可聚焦（`Focusable = true`）与实体交互控件（`Button`, `TextBox`, `ToggleSwitch`, `CheckBox`, `MenuItem`, `ComboBox` 等）。
- **原生 Peer 角色与名称**：必须通过 `ControlAutomationPeer.CreatePeerForElement` 获取原生 peer，校验 `peer.GetAutomationControlType() <> AutomationControlType.None` 且 `peer.GetName()` 具有非空白有效名称。
- **拒绝只读属性冒充**：严禁以只读 `AutomationProperties.GetName` 绕过 peer（原生按钮靠 Content 派生可访问名称，只读属性为 null 但读屏能真实获取；反之仅挂属性而不具备有效 peer 属于虚假无障碍）。
- **无名负例反例守护**：针对无内容无名称按钮、无提示输入框、可聚焦无名自定义元素及纯空白字符，均有明确单测证明审查会精确亮红灯拒收。
- 与 `compose/AppShell/tests/A11yTraversalTests.fs`（AppShell 各屏幕的整树遍历断言）分工：画廊审查覆盖画廊条目，AppShell 测试覆盖真实屏幕；两者都是「真实控件树」口径，不互相替代。

## 控件库显式清单与覆盖规则

详见 `docs/ux/widget-coverage.md`。覆盖核对由 `./x check` 内置的 `gallery_coverage` 完成（每个实体控件库至少一条画廊条目），不再使用独立脚本。

## 命令

```
bash tools/gallery.sh --list                                                      列出全部条目
bash tools/gallery.sh --render <条目名> <输出.png> [--theme <light|dark>]          渲染一个状态成 PNG（默认浅色）
bash tools/gallery.sh --check-baselines <基线目录> [--theme <light|dark|both>]     执行像素基线核查（门禁 G3）
bash tools/gallery.sh --generate-baselines <输出目录> [--theme <light|dark|both>]  生成像素基线（DevOps 命令）
bash tools/gallery.sh --a11y-audit                                                遍历全画廊真实控件树审查无障碍（门禁 G5）
bash tools/gallery.sh --window                                                    开交互窗口（开发用法）
bash tools/gallery-baseline.sh [基线目录]                                          基线门禁独立脚本（缺基线或像素变化必红）
```

- `--render` 在 Avalonia headless（Skia，`UseHeadlessDrawing = false`）下进行：
  不连主机、不读本机文件、不写仓库里的任何东西。未知条目名以非零码退出（退出码 2）。
  画布尺寸固定 720 × 900，条目之间可比。
- `--check-baselines` / `gallery-baseline.sh` 消费 `artifacts/feed` 的交付包真实渲染比对，**不放进 `./x check`**（避免日常检查反复构建）；门禁接入点由 DevOps 按轮次执行。
- `--a11y-audit` 同样消费真实渲染的控件树。
- `--window` 是**开发用法**（真实桌面窗口，`Avalonia.Desktop` + `UsePlatformDetect`）：
  左侧是条目表，右侧是当前条目的真实控件，点条目即切换。
  改一个控件库 → 只重建那个库 → 重开窗口就看到新样子，不需要连主机、不需要走旅程、不需要打整个 feed。
- 都用**隔离的包缓存** `artifacts/gallery-cache`（`tools/gallery.sh` 里设 `RestorePackagesPath`）：
  `artifacts/packages` 是 `./x pack` / `./x compose` 的缓存，同一时刻只允许一个写者。

### 跑本仓组合者测试时的两个陈旧缓存坑（都实测踩过）

1. **全局 NuGet 缓存会留下旧的本仓包。** `~/.nuget/packages/wanxiang2.*` 里存着**旧版本**的本仓包
   （实测 84 个 `wanxiang2.*` 目录）。跑本仓组合者测试若**不带**
   `-p:RestorePackagesPath="$PWD/artifacts/packages"`，会解析到那些旧程序集，
   表现为「**我明明改了却不生效**」。所以本仓的测试一律带这个参数。
2. **`bin/` 里的陈旧同名 dll 会让 F# 编译器解析到错误的程序集。** 包合并之后，
   输出目录里可能还留着合并前的独立 dll（例如合并 `ComposerSendState` 时输出目录里留着旧的
   `ComposerSendState.dll`），编译器就以为它还是独立程序集，于是报一堆「类型找不到」。
   合并后先清掉输出目录再构建。

## LEMMA

1. **画廊里的状态与真实界面的状态是同一个状态。** [根 AXIOM: 5] 投影只有一条来源（`Workspace.project`），
   画廊不另造视图状态记录；改了状态树的投影，画廊立刻跟着变。
2. **每条状态都能被单独看见。** [根 AXIOM: 3] 一个状态不依赖整条用户旅程才可达——
   画廊用一串消息直接把它造出来。
3. **视觉基线具有确定性，缺基线与像素变化必须见红。** [根 AXIOM: 1, 4] 锁定时钟与尺寸，两套主题像素不漂移，任何偏差敏锐告警。
4. **无障碍信息由原生 Peer 担保。** [根 AXIOM: 5] 拒绝只读属性遮羞布，凡可聚焦/交互控件必有真实角色与非空名称。

## SPEC

1. [LEMMA: 1] `tests/Tests.fs`（18 条，全是**状态差异**断言，不是「没抛异常 / 不是空板 / 清单对得上」那类烟雾）。
   **当前 18 条全过**（实测 `dotnet test --project compose/Gallery/tests/Gallery.Tests.fsproj -p:RestorePackagesPath="$PWD/artifacts/packages"` → `total: 18, failed: 0`）。附件那条此前是红的（状态树给不出附件），状态树补上处理分支后已转绿（见《已知缺口与各自的归属》4）。
   - `输入区：空闲、输入中、带附件、生成中是四个不同的状态`（读投影：草稿文本、托盘、发送键状态、禁用位）；
   - `输入区：未连接时禁用并给出原因`（`DisabledReason` 与侧栏空态）；
   - `对话列：空态、有消息、流式是三个不同的状态`（流式那条走**真的** `generation.delta`，
     断言流式正文文字「正在逐段补齐的回复……」真的在投影里，不是「有个空的流式位」）；
   - `对话列：助手消息带思维链，工具调用卡带参数`（读 `MessageRowVS` 的 `Reasoning` 与 `ToolCalls`）；
   - `对话列：错误是真实存在的错误，有 kind 与可重试标记`（渲染真的错误卡，断言卡片上有错误文案 + 「重试」+「继续生成」）；
   - `侧栏：空态、有行、搜索无命中是三个不同的状态`（读投影里的行数、置顶位、归档位、连接位、查询词）；
   - `侧栏：批量模式真的画出了批量条`（断言批量条的字：已选 / 全选 / 删除）；
   - `设置页：切到哪个分区，就真的看见那个分区的面板`（断的是**界面**：切分区后那个分区的面板标题
     真的出现在控件树里——不是断「我发的消息在模型里的键对不对」那种接线）；
   - `对话框：确认框真的把标题、正文与确认键画了出来`；
   - `骨架屏：真的处于显示态且带呼吸类`；
   - `画廊的每个条目都由状态树驱动：投影真的变了`（钉住「画廊不是手搓记录」这个前提本身）。
   - `批量条：计数真的跟着选中项走`（断言「已选 2 项」与「归档」真的在控件里）；
   - `确认框与导出表：标题与状态真的画了出来`（断言「删除对话」/「取消」/「正在生成 Markdown」）；
   - `浮层菜单：条目字与危险项真的画了出来`（断言「复制」/「删除对话」）；
   - `字段校验：出错的输入壳真的标了 invalid`（断言错误文案与 `invalid` 类）；
   - `提示条：删除对话后真的出现「已删除对话」`（断言投影里的 toast 与控件树里的字）；
   - `品牌标与图标集：字与图标真的画了出来`（断言「万」与 4 个图标控件）；

   **断言从状态树与真实控件读事实，不抄控件树的字符串**：抄来的期望值换个控件实现就假了，
   而状态差异是画廊存在的理由。

2. [LEMMA: 3] `tests/BaselineTests.fs`（5 条基线比对引擎断言，`[待验证]` 合并后动态运行）：
   - `缺少基线文件时核查必须红（MissingBaseline）`；
   - `同图比较必定 100% 匹配（Match）`；
   - `像素改变时核查必须红并指出坐标（PixelMismatch）`；
   - `尺寸不匹配时核查必须红（SizeMismatch）`；
   - `深浅两套主题渲染出的位图背景与像素真实不同`。

3. [LEMMA: 4] `tests/A11yAuditTests.fs`（11 条原生 peer 与无障碍遍历断言，`[待验证]` 合并后动态运行）：
   - 输入区、对话列、侧栏、设置页、对话框真实控件树遍历通过断言；
   - `原生 peer 角色与名称证明：peer.GetName() 优于只读 AutomationProperties`；
   - 4 条无名负例反例判定（无内容按钮、无提示输入框、可聚焦无名元素、纯空白名称）；
   - `负例审查失败判定：存在缺陷时 assertClean 必须抛出异常亮红灯`。

4. [LEMMA: 2] `bash tools/gallery.sh --list` 列出 41 条；
   `bash tools/gallery.sh --render MessageCardView.toolcall /tmp/g.png` 产出一张
   `PNG image data, 720 x 900`（实测 18510 字节，40 条时代的实测值；41 条全量逐条渲染 `[待验证]`）。
5. [LEMMA: 2] `bash tools/gallery.sh --window` 开交互窗口：左侧条目表、右侧真实控件，
   点条目即切换。改一个控件库 → 只重建那个库 → 重开窗口就看到新样子。
   实测：窗口标题「万象组件画廊」，左侧条目表（选中项高亮），右侧是选中条目的真实控件。

## 已知缺口与各自的归属

画廊只画**状态树真的到得了**的状态。逐条查过之后，下面这些事各有各的归属与结论；
画廊不自己造投影绕过它们：该走真实控件入口的走控件入口（对话框、错误卡），
状态树到不了的就在这里写清楚。

1. **侧栏多选（批量模式）—— 已收口。**
   多选现在只有一个事实来源：
   `SidebarUserMsg.EnterBatch` / `SidebarUserMsg.ToggleBatch` 直接进 `WorkspaceModel`
   的侧栏多选状态；`Workspace.project` 从它投影出 `SidebarVS.Selection`，
   `SidebarDesk` 据此渲染批量条。画廊条目 `SidebarDesk.batch` 走真状态树消息（`Scenes` 里发
   `User(Sidebar(SidebarUserMsg.EnterBatch))` + 两条 `ToggleBatch`），不手搓视图状态。
2. **对话框与浮层的「打开」状态 —— 分工不同，不改。**
   对话框的「打开」是桌内分工（`DialogsDesk` 自己的 `Open` 记录），不在 `WorkspaceModel` 里。
   画廊里对话框条目因此走真实控件入口（`ctx.Dialogs.Connect(...)` 等），
   不经过状态树——这是分工，不是缺口；状态树只记「有对话框在场」的相位。
3. **错误卡 —— 已收成单一归属。**
   错误状态在状态树里是 `ChatVS.Error`（由 `Workspace.project` 从 `RunState.Idle` + 错误事实投影），
   渲染走卡片组的 `ErrorCard` 单一入口。画廊条目 `MessageCardView.error` 用真实入口渲染，
   不另造错误卡视图。
4. **输入区的附件 —— 已修。**
   状态树已补齐 `ComposerUserMsg.Attach` / `.RemoveAttachment` 与
   `InternalMsg.UploadProgress` / `.UploadFinished` / `.UploadFailed` 分支；
   画廊条目 `ComposerBox.attachments` 走真实消息序列（`Scenes.composerAttach`），
   发送时附件元数据随发件出去（`Send` 丢附件的 bug 已修并有用例钉住）。
5. **像素基线覆盖 25/41 条目 —— 待 DevOps 实机补齐。**
   见《深浅双主题渲染与像素基线门禁 (G3)》的当前覆盖一节。缺基线期间门禁按契约红灯，
   不伪造基线图。

## 与旧实现的差异

无（旧仓库没有画廊这一物）。

## 合并后实测更正（2026-10-23）

上述「25/41 条目有基线，其余待补齐」是合并中间状态，已由本节取代。合并远端后从源码重新 `./x pack`，清理画廊旧包缓存，再实际生成全部 41 个条目的深浅主题基线，共 82 张；独立进程逐像素复核 82 项全部通过。这证明本机渲染可重复，不代表人工视觉审查或跨平台像素一致。

- `bash tools/gallery.sh --list`：41 条，实际运行成功。
- `dotnet test --project compose/Gallery/tests/Gallery.Tests.fsproj -p:RestorePackagesPath="$PWD/artifacts/gallery-cache"`：37 条通过、0 失败。
- AppShell 原生 Tab/Esc/删除邻位交互用例：1 条通过、0 失败。
- `./x check`：109 个库、71 个组合者、17 个控件库覆盖检查通过。
- 实际渲染暴露并修正两个重复挂载：ChatHeader 条目改用独立真实控件；审批条目呈现所属 OverlayRoot，不再把已有父节点的审批视图重复挂入画布。
