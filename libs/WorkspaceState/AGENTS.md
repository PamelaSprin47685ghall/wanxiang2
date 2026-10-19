# WorkspaceState

工作区单一状态树内核库（纯逻辑，无 Avalonia 依赖）。
收拢连接生命周期、目录快照、对话切换/生成、命令在途台账、浮层退栈、Toast 与定时器调度。

## 职责
- `FocusTarget` / `ScrollCommand` / `Effect` / `Msg` 契约定义；
- `WorkspaceModel` 单一互斥 DU 状态树；
- `Workspace.init` / `Workspace.update` 纯函数状态转移（无 IO、无可变静态）；
- `Workspace.project` 纯投影输出 `WorkspaceViewState` 供前端原生控件与单入口 `Render` 消费。

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
- `tests/WorkspaceState.Tests.fsproj` 单元与旅程测试通过。
