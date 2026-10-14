# ToolsPane

设置 → 工具分区的界面，以及「添加 / 编辑 MCP 服务」「授权文件夹」两个对话框的内容（迁自旧 `Views/SettingsTools.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `ToolsPane(look)`：`View`；`Render ToolsPaneState`（工具、授权文件夹、沙箱说明、调试开关值、MCP 服务）。空清单时显示空态卡。
  - 事件：`PolicyClicked(toolId, 键)`、`RemoveFolderClicked path`、`DebugToggled 新值`（只在用户拨动时发；`Render` 写值不发）、`AddFolderClicked`、`AddServerClicked`、`EditServerClicked id`、`ServerMoreClicked(id, 键)`。
  - 自动化：`tool-policy-<id>`（读屏名「<名> 的审批方式：<方式>」）、`tools-add-folder`、「移除文件夹授权 <路径>」。
- `McpEditor(look, existing, validationFor)`：六个输入框（`IdBox` … `TimeoutBox`、`Boxes`）、启用开关（`Enabled`）、错误摘要（`Summary`，assertive、可聚焦）、取消 / 添加（或保存）键。
  - 单行框回车、任意处 Ctrl/⌘+Enter 发 `SaveRequested`；参数框回车是换行。保存往返中 `SetPending true`：保存键换「正在保存…」并禁用、取消也禁用、不再发 `SaveRequested`。
  - 页脚左右方向键在两键间移动。`InitialFocus`：新建是标识，编辑是名称（标识只读）。`SetMaxHeight` 给对话框内容的上限。
  - `validationFor` 是每个框的校验行（组合者的 FieldCheck.MessageFor），放进对应字段组，可见时说明让位。
- `FolderEditor(look, validationFor)`：路径框（`PathBox`）、取消 / 授权；回车或「授权」发 `SaveRequested 去空白的路径`。
- 校验（McpForm、路径规则）、写回（ToolsPolicy.payload、McpForm.payload）、审批与服务菜单、对话框开关、Esc 守卫都由组合者接。

## 外观

- `ToolsPaneLook`：公共字段加 `SpaceXXs`、`TextAreaMinHeight`、`EmptyLogoSize`、`PendingMinWidth`。`Glyph` 用到的名字：`server`、`wrench`、`pencil`、`more`、`trash`、`logo`。
- 样式类：按钮 `surface button-* hover-*`、图标按钮 `surface icon-button hover-plain`、卡 `grouping-card`、标签 `tag`、分隔 `hairline`、开关 `toggle-track` / `toggle-knob` / `on`、分节标签 `section-label`、输入壳 `surface hover-raised focus-soft`。

## 证据与旧测试

- SettingsViewTests：`SettingsTools SetCatalog renders tool list, mcp list and sandbox notes`（呈现部分）、`SettingsTools ShowEditor validates id, command/url, timeout and saves valid config`（界面与键盘部分；校验与载荷在组合者）
- W3ToolApprovalUiTests：`工具设置：每个工具有审批方式入口，授权文件夹有入口；空授权时文案指向入口`（呈现与事件部分；载荷在组合者）
- SettingsSurfaceTests：`disabled mcp card dims its text while keeping row actions alive`、`built-in tool row title shares the row-title body font`、`switch row consumes the shared settings row height and toggle semantics`
- 新增：`editing an existing mcp server locks its id and focuses the label`、`folder editor sends the trimmed path on enter`

## 与旧实现的差异

- 空态卡是本库自己的简化形态（标志 + 说明，无标题、无动作），与旧 `Ui.emptyState` 在这两处的用法一致。
- 调试开关行只建一次，`Render` 写值；旧实现每次 SetCatalog 重建该行。焦点因此不会在目录刷新时丢失。
