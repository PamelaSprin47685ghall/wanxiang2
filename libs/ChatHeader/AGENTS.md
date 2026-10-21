# ChatHeader

聊天顶栏（原生 Button / TextBox / Grid / StackPanel 实现）：侧栏开关、可改名标题、生成中状态点、「对话操作」按钮、分叉面包屑。

单入口幂等投影：`Render(ChatHeaderVS)`，废除 Look 记录、Follow.live 属性订阅与散落的 Dispatcher.UIThread.Post；所有按钮均为 Avalonia 原生 Button，文本框为原生 TextBox。

## 接口（给宿主与组合者）

- `ChatHeader()`：构造顶栏控件。
- `member Host: Border`：放进布局的根节点。
- `member TitleAction: Button`：标题点击改名按钮。
- `member TitleEditor: TextBox`：改名输入框。
- `member MoreButton: Button`：对话操作按钮。
- `member SidebarButton: Button`：侧栏切换按钮。
- `member GeneratingChip: Border`：生成中状态胶囊。
- `member CrumbAction: Button`：面包屑按钮。
- `member Title: string`：当前标题。
- `member IsEditing: bool`：是否正处于编辑改名中。
- `member CrumbText: string`：面包屑显示文本。
- `member Render(vs: ChatHeaderVS): unit`：单入口幂等渲染。
- `member Renamed: IEvent<string>`：标题修改完成意图。
- `member MorePressed: IEvent<Control>`：更多操作点击意图。
- `member SidebarPressed: IEvent<unit>`：侧栏切换意图。
- `member CrumbPressed: IEvent<unit>`：面包屑点击意图。

## 成员 `BreadcrumbAction`

## BreadcrumbAction（库）

旧 `Views/ChatView.fs` 的 SetBreadcrumb、BreadcrumbText 与 ActivateBreadcrumb 状态迁入，成为泛型对象 `BreadcrumbAction<'Id>`。`Set` 接收 (文字, 说明, 父对话可用, 父对话) 或 None，返回呈现；`Activate` 返回要回到的父对话。文字由 ForkLineage.breadcrumb 给（组合时传入）；控件与自动化属性仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一串操作，呈现与跳转目标和旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/BreadcrumbAction.Tests.fs` 通过：`unavailable parent shows text but does not navigate`。

#### 迁入或覆盖的旧测试
旧面包屑用例要起窗口，留在旧仓库。本库新增 1 条规则用例。

## 成员 `RevealFlash`

## RevealFlash（库）

旧 `Views/ChatView.fs` RevealAndHighlight 与 EndRevealHighlight 的高亮记账迁入：`Revealed`、`Begin`（返回要先复原的上一条）、`End`、`LastFlashed`、`Highlighted`，停留时长 `DwellMs` 为 1600。计时器、找高亮承载面、背景刷仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一串操作，记账与旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/RevealFlash.Tests.fs` 通过：`a new highlight restores the previous one first`。

#### 迁入或覆盖的旧测试
旧搜索命中高亮用例（第 211 节）要起窗口，留在旧仓库。本库新增 1 条规则用例。

#### 合并说明

2026-10-21（W7-CLEANBREAK）并入本包：源文件按原工程编译顺序落盘为本包源文件；
测试按 MERGE.md《测试工程的合并约定》改名 `tests/BreadcrumbAction.<原文件名>.fs`，顶层模块改名 `<成员名><模块名>`；
原 `Wanxiang2.BreadcrumbAction` 包不再交付，消费方改引 `Wanxiang2.ChatHeader`。命名空间与类型名不变，消费方源码零改。
