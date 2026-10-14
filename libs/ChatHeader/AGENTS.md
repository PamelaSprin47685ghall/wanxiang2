# ChatHeader

聊天顶栏（迁自旧 `Views/ChatView.fs` 的 headerBar、titleAction/titleEditShell、generatingChip、moreButton、sidebarToggleButton、breadcrumbBar）：侧栏开关、可改名标题、生成中状态点、「对话操作」按钮、分叉面包屑。根规则见项目根目录 AGENTS.md。

- 菜单内容、改名去向、面包屑父对话由宿主决定：本库只发 `Renamed`、`MorePressed(anchor)`、`SidebarPressed`、`CrumbPressed`。
- 面包屑数据是 `CrumbView`（宿主用 BreadcrumbAction 折出）。
- 旧顶栏模型芯片按决策 350 恒隐藏，没有迁移；模型只在输入区选。

## 外观

- `ChatHeaderLook`：`Ink`、`Gauge`、`BodyFont`、`Glyph`（侧栏、更多、分叉）、`DressIcon`、`DressPlain`、`DressField`。

## 证据与旧测试

覆盖旧 ChatView 的 SetTitle / BeginTitleEdit / CommitTitle / CancelTitleEdit、SetGenerating 的状态点、SetConversationChrome、SetCompactMode 的顶栏部分、SetBreadcrumb / ActivateBreadcrumb。无头行为用例三条（改名、改名途中标题到达、状态点 / 更多 / 面包屑）。

## 整树证据

顶栏与面包屑的整树金样在 compose/ChatScreen/tests/golden/screen.txt（真 Theme 外观才有意义，故放组合者层）。
