# WorkspaceState

工作区单一状态树内核库（纯逻辑，无 Avalonia 依赖）。
收拢连接生命周期、目录快照、对话切换/生成、命令在途台账、浮层退栈、Toast 与定时器调度。

## 职责
- `FocusTarget` / `ScrollCommand` / `Effect` / `Msg` 契约定义；
- `WorkspaceModel` 单一互斥 DU 状态树；
- `Workspace.init` / `Workspace.update` 纯函数状态转移（无 IO、无可变静态）；
- `Workspace.project` 纯投影输出 `WorkspaceViewState` 供前端原生控件与单入口 `Render` 消费。

## 事实归属（W7-B3 补全）
- `Chrome.RevealTarget : (Guid * string) option`：定位目标（对话 + 消息），来自搜索命中与消息链接。
  归属模型：消息加载后由 `update` 发 `Effect.ScrollChat (ScrollCommand.ToExchange 消息 id)`，
  宿主经统一 Effect 执行滚动与高亮；目标随即消费，15 秒未加载则到期清除。
- `Lineage : Map<Guid, ForkLineage>`：分叉谱系，出自同一份对话列表快照（`ConversationListSnapshot.items` 里带 `parentId` 的行），
  随 `SidebarVS.Lineage` 投影给侧栏；不再有旁路插槽，也不另造数据源。
- `Onboarding : OnboardingFacts`：引导卡的三项在途事实（所选预设 / 本机发现的模型 / 兼容端点建议）。
  归属模型：引导卡经 `OnboardingUserMsg` 回报，宿主按单一入口 `OnboardingDesk.Render` 投影回来；
  卡片自己的步进机（OnboardingFlow）仍归桌内。
- `Chrome.BatchMode : bool` 与 `Chrome.SelectedRowIds : Set<Guid>`：**侧栏多选的唯一事实**。
  归属模型：由 `SidebarUserMsg.EnterBatch` / `ToggleBatch` / `ExitBatch` / `ToggleBatchAllVisible` 四条消息驱动，
  经 `SidebarVS.BatchMode` / `SidebarVS.SelectedRowIds` 投影给侧栏；侧栏只发意图，不再自己记一份选择集合。
  取消最后一项即退出多选；「全选可见」会换掉不可见的旧选择；状态树**不清理**看不见的已选项
  （可见行是侧栏的事实——搜索框与归档开关都在侧栏——由侧栏按可见范围派生计数与文案）。
- 死消息已删（零派发、零消费）：`SettingsUserMsg.FormChanged / SaveProvider / DeleteProvider / ProbeProvider`，
  `OnboardingUserMsg.StartDiscovery / ConnectDirect / CompleteOnboarding`——这些路径实际走设置页/引导卡的端口。
- 侧栏的品牌行与页脚各有唯一投影：`SidebarVS.Brand`（`SidebarBrandVS`：`PresetsVisible` / `Compact`）与
  `SidebarVS.Host`（`HostSwitcherVS`：`ServerUrl` / `Connected` / `StatusText`）。
  `PresetsVisible` 取自主机目录快照的预设列表（非空即有预设）；`Brand.Compact` 由 `SidebarVS.Compact` 派生。
  两处都由组合者经各自控件的 `Render` 单入口消费，状态树不再旁路写控件。
  命令面板键是否出现不是每帧事实（宿主自己知道），走组合者插槽 `HostSwitcher.SlotPalette`。

## 第四批命令与游标契约
- 用户写操作只产出独立命令 Effect，由入口调用命令编码通道；不得作为入站事件回灌。
- 权威快照与增量先应用到状态，再确认实际应用的全局水位；命令提交回执不是投影应用证据。
- 重命名、置顶与父对话导航必须形成意图、状态更新、Effect 与投影闭环。
- 验证状态：已验证（`WorkspaceState.Tests` 45 条、`EntryShells.Tests` 8 条、`AppShell.Tests` 36 条、全量 109 库 + 67 组合者、`./x consume`、`./x app`、`tools/smoke_desktop.sh` 全部通过）。

## LEMMA
1. **单一状态树与互斥 DU 消除非法状态**：`ConnPhase`、`CatalogPhase`、`WorkspacePhase`、`RunState`、`OverlayPhase` 编译期互斥，非法状态不可表示。
2. **纯函数与确定性**：`update` 与 `project` 为无副作用纯函数，同一 `(model, msg)` 两次运算严格相等。
3. **退避与归零防锁死（Anti-Wedge）**：`ConnectionDown` 清空在途未决命令；定时器与等待皆有显式闭环。

## SPEC
1. 不变式 1：`RunState.Idle` 时投影里 `Streaming = false`，且无停止按钮字段可见。
2. 不变式 2：`ConnPhase <> Authed` 时投影不存在 `Active` 对话数据。
3. 不变式 3：任何 `Effect.After` / `Every` 都有配对的到达消息或显式停止；`ConnectionDown` 后在途计数归零。
4. 不变式 4：`update` 无 IO、无可变跨调用状态；同一 `(model, msg)` 两次调用结果严格相等。

## 证据
- `tests/WorkspaceState.Tests.fsproj` 单元与旅程测试通过（含「对话列表快照同时带出分叉谱系」与
  「定位目标：搜索命中进模型，快照到达且消息已加载时统一发滚动 Effect」两条接线用例，
  以及 9 条侧栏多选用例：`EnterBatch 选中第一项并进入多选`、`EnterBatch 在多选中只并入这一项`、
  `ToggleBatch 取消最后一项时退出多选`、`ToggleBatchAllVisible 全选可见行；已全选则退出多选`、
  `ToggleBatchAllVisible 换一批可见行会清掉旧的不可见选择`、`ExitBatch 清空选择并退出多选`、
  `多选投影：SidebarVS.SelectedRowIds 与批量条同源于 Chrome`、`多选投影：全部选中的行都置顶时 AllPinned 为真`、
  `多选投影：状态树不清理看不见的已选项`，
  以及 2026-10-23 补齐的状态机闭环用例：
  `配对到首发：从 PairingStarted、PairingSucceeded 到 AuthPresent，再到目录就绪后发送首条消息`、
  `到期提交：未撤销时到期 Tick 触发 DeleteConversation 命令并清除待删台账`、
  `失败重试：失败后携带 lastError，Regenerate 发出命令并在新生成完成后清除错误`）。
