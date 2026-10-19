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
