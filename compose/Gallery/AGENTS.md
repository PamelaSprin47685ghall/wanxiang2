# Gallery（组合者，组件画廊）

组件画廊：把每个控件库的**状态矩阵**显式列出来，每种状态渲染成一个可视条目。
它是「不需要连主机、不需要走整条旅程」就能看到界面真容的地方（`docs/frontend-dev-process.md` §3 的 G2）。

- 协作者（包）：`WorkspaceState`（状态树）、`Theme` / `ThemePalette` / `ThemeMetrics`（外观）、`IconSet`、`UiPrefs`、
  `OverlayDesk`、`SidebarDesk`、`ComposerDesk`、`ChatScreen`、`ChatPane`、`MessageCardView`、`SettingsScreen`、
  `DialogsDesk`、`ApprovalDesk`、`MarkdownView`、`CodeTint`、`ChatEmpty`、`Wire`。
- 本组合者只经交付包使用库与组合者，不引用任何源码目录。
- 命名空间 `Wanxiang2.Gallery`。

## 时钟

`Scenes.run` 用**固定的现在**（`Scenes.fixedNow = 2026-10-21T08:00:00Z`），不是 `DateTimeOffset.UtcNow`。
用墙钟的话同一场景每次跑出的时间戳都不同（渲染里那个时刻会漂），输出就不可复现、像素基线也没法比。
实测：同一条目连渲两次，PNG **逐字节相同**（md5 一致）。

## 状态来源

**每个条目的视图状态都由 `Workspace.init ()` + `Workspace.update` 的消息序列跑出来，再经 `Workspace.project` 投影。**

**两条例外，走真实控件入口**（不是手搓投影，是那条控件真实的入口）：
- `MessageCardView.error` → 走卡片组的 `ctx.Cards.ErrorCard(...)`（错误卡的状态已收成单一归属，见《已知缺口与各自的归属》3）；
- `SidebarDesk.batch` → 走**真状态树消息** `SidebarUserMsg.EnterBatch` + `SidebarUserMsg.ToggleBatch`（多选已收口，见《已知缺口与各自的归属》1）。

没有任何一个条目手搓 `WorkspaceViewState` 记录。手搓记录是画廊与真实界面漂移的唯一途径：
投影里少一个字段、门控条件写错，画廊照样绿，而真实界面是错的。所以画廊只允许两类输入：

1. **线协议事件**（`Scenes.Wire`）：主机真实下发的字节形状——`AuthAccepted`、`CatalogSnapshot`、
   `ConversationListSnapshot`、`ConversationSnapshot`、`GenerationStarted`。这些 JSON 是字面量，
   但走的正是真实解析路径（`Catalog.parse` / `ConversationSummary.parseList` / `MessageView.ofSnapshotItem`）。
2. **用户/宿主消息**（`Scenes.Msg`）：`ComposerUserMsg.TextChanged` / `.Attach`、`SidebarUserMsg.SearchTextChanged`、
   `SettingsUserMsg.OpenSection`。

`Scenes.run` 是唯一的跑法：喂一串 `Step`，返回 `(WorkspaceModel, WorkspaceViewState)`。

## 假端口

需要宿主才能动的几根线（网络、剪贴板、文件系统、计时器）由 `FakePorts.fs` 给**显式的、无副作用的**实现。
它们不忽略输入：每个端口都读自己的参数并记进 `Calls`（发出去的线事件、复制的内容、删除的对话 id……），
不写「假装成功但吞掉一切」的桩。

用到假端口的条目：

| 条目 | 用到的端口 | 为什么 |
| --- | --- | --- |
| `OverlayDesk.dialog.confirm` / `.prompt` / `.connect` / `.shortcuts` | `DialogsDesk` 的回调（`onConfirm` / `onConnect` / `onSubmitCode`） | 对话框面板要一个「确认后做什么」的出口；画廊不真的删对话、不真的连主机 |
| `MessageCardView.*` | `CardActions`（复制 / 重新生成 / 分叉 / 删除 / 下载附件 / 打开链接） | 消息卡的动作出口 |
| `ComposerBox.*` | `ComposerDeskActions`（提交 / 停止 / 选附件 / 打开模型选择器 / 粘贴） | 输入区的动作出口 |
| `SidebarDesk.*` | `SidebarDeskActions`（新建 / 打开 / 重命名 / 删除 / 置顶 / 导出 / 打开设置……） | 侧栏的动作出口 |
| `SettingsScreen.*` | `SettingsHooks`（写配置 / 探活 / 保存偏好） | 设置页写配置要走主机；画廊只记一笔 |

**这些假端口只影响「点了之后会发生什么」，不影响「界面长什么样」。** 画廊的每一条断言都落在渲染结果上。

## 条目

40 条，按控件库分（每个控件库至少一条，由 `./x check` 核对；判据见根 `AGENTS.md` §5《控件库交付约定（沿用）》）：

| 控件库 | 条目 |
| --- | --- |
| `ComposerBox` | `ComposerBox.idle`（已连接、无草稿）、`ComposerBox.typing`（有草稿）、`ComposerBox.attachments`（带附件——附件已进入状态树，见《已知缺口与各自的归属》4）、`ComposerBox.generating`（生成中，发送键变停止） |
| `MessageCardView` | `MessageCardView.user`、`MessageCardView.assistant`（含思考折叠块）、`MessageCardView.toolcall`、`MessageCardView.error`、`MessageCardView.streaming` |
| `SidebarDesk` | `SidebarDesk.empty`（未连接）、`SidebarDesk.rows`（置顶 / 普通 / 归档三行）、`SidebarDesk.batch`（批量模式——**多选已收口，走真状态树消息**）、`SidebarDesk.search`（搜索中） |
| `SettingsScreen` | `SettingsScreen.providers` / `.tools` / `.generation` / `.devices` / `.appearance` / `.about`（六个分区各一条） |
| `OverlayDesk` | `OverlayDesk.dialog.confirm` / `.prompt` / `.connect` / `.shortcuts`（四种对话框） |
| `ChatEmpty` | `ChatEmpty.skeleton`（对话列骨架屏） |
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

## 命令

```
bash tools/gallery.sh --list                             列出全部条目
bash tools/gallery.sh --render <条目名> <输出.png>       渲染一个状态成 PNG
bash tools/gallery.sh --window                            开交互窗口（开发用法）
```

- `--render` 在 Avalonia headless（Skia，`UseHeadlessDrawing = false`）下进行：
  不连主机、不读本机文件、不写仓库里的任何东西。未知条目名以非零码退出（退出码 2）。
  画布尺寸固定 720 × 900，条目之间可比。
- `--window` 是**开发用法**（真实桌面窗口，`Avalonia.Desktop` + `UsePlatformDetect`）：
  左侧是条目表，右侧是当前条目的真实控件，点条目即切换。
  改一个控件库 → 只重建那个库 → 重开窗口就看到新样子，不需要连主机、不需要走旅程、不需要打整个 feed。
- 两者都用**隔离的包缓存** `artifacts/gallery-cache`（`tools/gallery.sh` 里设 `RestorePackagesPath`）：
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

2. [LEMMA: 2] `bash tools/gallery.sh --list` 列出 40 条；
   `bash tools/gallery.sh --render MessageCardView.toolcall /tmp/g.png` 产出一张
   `PNG image data, 720 x 900`（实测 18510 字节）。40 条逐条渲染全部成功（实测）。
3. [LEMMA: 2] `bash tools/gallery.sh --window` 开交互窗口：左侧条目表、右侧真实控件，
   点条目即切换。改一个控件库 → 只重建那个库 → 重开窗口就看到新样子。
   实测：窗口标题「万象组件画廊」，左侧 40 条条目表（选中项高亮），右侧是选中条目的真实控件。

## 已知缺口与各自的归属

画廊只画**状态树真的到得了**的状态。逐条查过之后，下面四件事各有各的归属与结论；
画廊不自己造投影绕过它们：该走真实控件入口的走控件入口（对话框、错误卡），
状态树到不了的就在这里写清楚。

1. **侧栏多选（批量模式）—— 已收口。**
   多选现在只有一个事实来源：
   - `libs/WorkspaceState` 的 `Chrome.BatchMode` 与 `Chrome.SelectedRowIds` 是仅有的两份事实；
   - 四条入口消息：`SidebarUserMsg.EnterBatch` / `ToggleBatch` / `ExitBatch` / `ToggleBatchAllVisible`；
   - `libs/WorkspaceState/Project.fs` 经 `SidebarVS.BatchMode` / `SidebarVS.SelectedRowIds` 投影给侧栏；
   - `compose/SidebarDesk` 从投影读，多选动作只发意图
     （`SidebarDeskActions` 的 `enterSelection` / `toggleSelected` / `exitSelection` / `toggleSelectAllVisible`）；
   - `libs/BatchBar/SidebarSelection.fs` 不再记账，只剩「按可见行过滤已选项」这个派生读法。

   **可见范围的派生留在侧栏**：搜索框与归档开关都在侧栏，状态树看不到，
   所以批量条的计数、「全选 / 取消全选」、「置顶 / 取消置顶」由侧栏按自己看到的可见行算。

   画廊的 `SidebarDesk.batch` 条目走**真消息**：`EnterBatch` + `ToggleBatch` → `update` → `project` → 渲染。

2. **对话框与浮层的「打开」状态 —— 分工不同，不改。**
   `OverlayPhase.DialogOpen` 的投影没有生产者（`Update.fs` 里只有 `Escape` / `Dismiss` 会把它关掉）。
   但这不是两份互相打架的事实，而是**「投影是壳、真实控件在桌内」**的分工：
   - `compose/DialogsDesk` 直接 `overlay.ShowDialog(...)` 造真实控件；
   - `compose/OverlayDesk` 的 `OverlayVS.Dialog` 分支**只设 `currentOverlayPhase`、不造控件**。

   要让投影也带内容，就得把整套对话框内容搬进状态树——**那是新设计，不是缺陷修复**，所以不改。
   画廊的对话框条目因此直接调 `DialogsDesk`，走真实控件入口。

3. **错误卡 —— 已收成单一归属。**
   此前 `ChatPhase.Error` 无生产者（死分支），真实错误由 `compose/AppShell` 私读 `RunState.Idle err`，
   经 `compose/ChatScreen` 的 `Error: (GenerationError * (unit -> unit)) option` 字段传下去——
   错误卡没有单一归属。

   **现在**：生成错误是 `ChatVS.Error` 字段，由 `Workspace.project` 从 `RunState.Idle (Some err)` 投影；
   `compose/AppShell` 改从投影取（不再自己读 `RunState`）；`ChatPhase.Error` 那个死分支与其渲染分支一并删除
   （它画的是「加载更早消息失败」的横幅，与生成错误卡本就是两件事）。

   画廊的 `MessageCardView.error` 条目仍走卡片组的 `MessageCards.ErrorCard` 入口——
   那是**真实控件的入口**，不是伪造投影。

4. **输入区的附件 —— 已修。**
   此前 `ComposerUserMsg.Attach` / `.RemoveAttachment` 与 `InternalMsg.UploadProgress` / `.UploadFinished` /
   `.UploadFailed` 在 `WorkspaceState.Update` 里**没有处理分支**，附件托盘永远是空的；
   而且 `Send` **只把 `text` 放进消息，附件被静默丢掉**——那是真 bug，不是「画廊画错了」。

   **现在**：
   - `ComposerUserMsg.Attach` 带一个 `attachmentId`——**由发起方给**（客户端开始上传时生成）。
     它同时是这条草稿的身份与线协议 `attachment.begin` / `chunk` / `complete` 的 id，两侧必须是同一个，
     上传进度消息才对得上；
   - `RemoveAttachment` 按 id 移除；`UploadProgress` / `UploadFinished` / `UploadFailed` 按 id 推进阶段
     （完成置 `Ready=true`；失败置 `Failed=true` 且 `Ready=false`）；
   - 同一批还补上了 `ComposerUserMsg.CaretChanged` / `EnterSendsToggled` / `ModelPick` 的处理分支；
   - **`Send` 丢附件的 bug**：`buildMessageJson` 按 `compose/Outbox` 的 `MessageOutbox.BuildMessage`
     同一份形状构造 `{"role":"user","contents":[{"text":…},{"type":"attachment",…}]}`；
   - **发送门控在状态主人手里**：有附件在上传中、或有附件上传失败，**整条发送被挡住**
     （与控件层 `libs/AttachmentTray/ComposerSendState.fs` 的 `blockReason` 同一口径），
     不发明「只带部分附件」的消息；草稿与附件都留着，等用户处理完再发。

   `ComposerBox.attachments` 的用例因此**转绿**：画廊测试 **11 条全过**。

5. **2026-10-21 新增的 16 条控件库条目 —— 状态来源逐条登记。**
   判据（什么算控件库）见根 `AGENTS.md` §5《控件库交付约定（沿用）》；
   「每个控件库至少一条条目」由 `./x check` 机器核对（不设人工名单）。

   **走真状态树的条目**（视图状态来自 `Workspace.project`）：
   - `ChatHeader.header`：喂 `vs.Header` 给 `ctx.Chat.Header.Render`；
   - `ToastStrip.toast`：喂真消息 `SidebarUserMsg.DeleteConversation` → 投影里的 `vs.Toasts` → `ctx.Overlay.RenderToasts`；
   - `ApprovalPanel.approval`：喂真线协议事件 `WireEvent.ToolApprovalRequested` → 投影里的 `vs.Approval` → `ctx.Approval.Render`。

   **走该控件自己的真实入口的条目**（这些状态本来就属于控件库自己，状态树里没有它的投影，
   手搓 `WorkspaceViewState` 才是错的）：
   `ActionStrip.actions`、`AttachmentTray.items`、`BatchBar.selection`、`BrandMark.tile`、
   `ConfirmPane.confirm`、`DevicesPane.devices`、`ExportSheet.export`、`FieldCheck.invalid`、
   `FocusOrigin.programmatic`、`IconSet.glyphs`、`ModelSwitchPanel.models`、`PalettePanel.palette`、
   `PopupMenu.menu`。

   这些条目喂给控件的是**库自己的输入类型**（`ActionStripVS` / `TrayItem` / `BatchBarVS` /
   `DevicesPaneState` / `ExportSheetState` / `ModelChoice` / `PaletteRow` / `PopupEntry`），
   不是工作区的投影记录——两者是不同的东西，不构成「手搓视图状态」。

## 与旧实现的差异
无（旧仓库没有画廊这一物）。
