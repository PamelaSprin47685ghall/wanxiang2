# __NAME__

[TODO] 写明本控件的职责、状态归属、事件。根规则见项目根目录 AGENTS.md《界面与前端组装（UI）》。

## 外观

- 不声明 `*Look`，不用 Follow 逐属性订阅：外观值由 compose/Theme 资源字典（`Palette.*`/`Metric.*`/`Space.*` DynamicResource）与样式类下发。
- 用到的样式类：`grouping-card`（模板示例；按实际增删并在此登记）。

## 契约

- 单入口：对外只有幂等 `Render(__NAME__VS)`；无带调用顺序依赖的 `Set*`。
- 单出口：用户意图经 CLIEvent 上报，跨组件副作用归组合者（宿主 Effect Runner 执行）。
- 不持 DispatcherTimer，不散落 Dispatcher.UIThread.Post。

## 证据

- `Tests.fs`：headless 验证行为与 Render 幂等投影（可见性/文本来自 VS）。
