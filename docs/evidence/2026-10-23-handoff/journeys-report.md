# 2026-10-23 SPEC 7 旅程与原生交互证明补齐交接报告（静态 / 未运行）

- **报告性质**：Engineer 源码与测试实现交接报告（未执行构建、测试或外部命令，测试尚未运行，不据此冒充动态运行证据）
- **承接任务**：《展望.md》§2 补齐配对后首发、旅程内附件 ACK 上线、真实失败重试、到期删除行为与回归源码；在 AppShell 真实控件树验证原生 Tab/Esc/删除邻位焦点与可访问名称角色
- **涉及模块**：`compose/AppShell`（组合者与测试）、`libs/WorkspaceState`（状态树与测试）、相关文档

---

## 一、实现与修改摘要

1. **测试台真实控件树根暴露（`compose/AppShell/tests/Rig.fs`）**：
   - 在 `type Rig` 记录中新增 `Root: Grid` 字段，并在 `buildViews ()` 时捕获最外层 `root` Grid。
   - 使得测试能够直接将真实控件树（含 Workspace 主工作区、SidebarDesk、ChatScreen、ComposerDesk、OverlayDesk）挂入顶层 `Avalonia.Controls.Window` 进行原生窗口级焦点与键盘交互验证。

2. **组合者核心旅程与原生交互测试补齐（`compose/AppShell/tests/ComposerTests.fs`）**：
   - 补充引用 `Avalonia`、`Avalonia.Controls`、`Avalonia.Input`、`Avalonia.Threading`、`Avalonia.Automation`。
   - **旅程 1（配对后首发）**：由认证态继续推进，装载可用目录快照，输入首条消息，触发发送，断言发出 `CreateConversation` + `SendUserMessage` + `ObserveConversation` 命令，台账记录两条在途命令，输入框清空；模拟主机快照到达，断言会话进入活跃态并呈现首发消息与标题。
   - **旅程 3（旅程内附件 ACK 上线）**：改为经真实生产端口 `rig.Shell.AttachFiles` 驱动，断言线路发出 `AttachmentBegin`、`AttachmentChunk`、`AttachmentComplete` 事件序列与缩略图端口调用；输入伴随文字后断言在主机未 ACK 前发送受门控拦截（无命令发出、文本与托盘保留且 Ready=false）；收到主机 `attachment.committed` ACK 后就绪，再次发送断言发出带文本及完整附件元数据（sha256/fileName/size/type）的 `SendUserMessage` 命令载荷，发送后托盘清空。
   - **旅程 5（真实失败重试）**：停止生成并收到上报超时错误后，断言 `Chat.Error` 展示错误卡；通过 `ChatUserMsg.Regenerate` 触发重试，断言在线路上发出 `ClientCommand.RegenerateResponse`；模拟主机新生成 `GenerationStarted` 到成功 `GenerationFinished`，断言错误被彻底清除，状态回归 `Idle None`。
   - **旅程 6（到期真删）**：保留原有撤销恢复断言，新增未撤销到期真删分支。批量删除后推进时间触发 `MsgTick`，断言待删台账清空，并在在线线路上逐个发出针对目标会话的 `DeleteConversation` 命令。
   - **原生交互证明（新增用例）**：挂入顶层无头 Window，验证真实控件树的分隔条、搜索框、新建按钮、设置按钮、发送按钮、附件按钮、模型胶囊的可访问名称（`AutomationProperties.Name`）与 `Focusable` 属性；断言输入框聚焦与原生 Tab 键后焦点沿导航链迁移；断言 Esc 键触发清空搜索词；断言删除第二项后邻位选第三项、删除末尾项后邻位选第一项、列表清空后回落搜索框。

3. **底层纯状态机回归测试补齐（`libs/WorkspaceState/tests/WorkspaceStateTests.fs`）**：
   - 新增 3 条单元测试，分别覆盖配对到首发生命周期纯函数状态转移、到期 MsgTick 自动提交 DeleteConversation 命令、失败携带 lastError 并在 Regenerate 重试完成后清错的纯逻辑判定。

4. **文档同步与缺口闭环**：
   - `libs/WorkspaceState/AGENTS.md`：在证据栏同步新增的 3 条状态机用例。
   - `compose/AppShell/AGENTS.md`：在证据栏同步 SPEC 7 旅程 1–6 与原生交互证明用例。
   - `展望.md`：§2 由「部分证据」更新为「2026-10-23 源码与测试已补齐」，逐条记录补齐证据源码位置与剩余边界。
   - `docs/frontend-dev-process.md`：更新 §1.2、§4.4、§4.5 处置记录表格、§5.3 已知边界。
   - `AGENTS.md`：根 SPEC 7 证据栏同步更新。

---

## 二、测试清单与可打红反例（Killable Mutations）

| 测试工程 | 测试名称 | 验证行为 | 能够打红该测试的具体变异（Killable Mutation） |
|---|---|---|---|
| `compose/AppShell/tests` | `旅程 1：首次配对到就绪` | 配对成功后装载目录，输入并发送首条消息，发出建会话/发件/观察命令，快照到达后转入 Active | 1. `ComposerUserMsg.Send` 在 `IdleEmpty` 下未发出 `CreateConversation`；<br>2. 首发文本内容未进入发件载荷；<br>3. 快照到达后活跃消息集合为空。 |
| `compose/AppShell/tests` | `旅程 3：发送含附件消息` | 经 `AttachFiles` 真实上传，未 ACK 时门控拦截发件，收到 `AttachmentCommitted` 后就绪，发件带附件元数据 | 1. 未收到 ACK 时提前放行发件；<br>2. `AttachmentCommitted` 到达未将托盘项标为 Ready；<br>3. 发送载荷中缺少附件项或 sha256 校验值不符。 |
| `compose/AppShell/tests` | `旅程 5：中途停止与失败重试` | 生成失败呈现错误卡，派发 `Regenerate` 发出重试命令，新生成成功后清除错误并回到 `Idle None` | 1. 触发重试未发出 `RegenerateResponse` 命令；<br>2. 新生成完成后错误卡未清除（`Chat.Error` 残留）。 |
| `compose/AppShell/tests` | `旅程 6：切换对话、搜索与批量删除恢复` | 验证撤销恢复，以及未撤销时时间推移触发 `MsgTick` 自动发出真删命令并清空待删台账 | 1. 撤销后待删批次未被清空；<br>2. 到期 `MsgTick` 到达后未触发 `DeleteConversation` 命令；<br>3. 到期后待删台账仍残留未删除项。 |
| `compose/AppShell/tests` | `原生交互证明：真实控件树下的 Tab 序、Esc 退栈、删除邻位焦点与可访问名称角色` | 真实无头 Window 挂载下，断言分隔条/搜索/操作按钮的可访问名称与 Focusable，验证 Tab 焦点迁移、Esc 清空搜索、删除邻位聚焦与列表变空回落 | 1. 原生控件的 `AutomationProperties.Name` 被清空或改错；<br>2. 原生输入框缺失 `Focusable = true`；<br>3. Tab 键后焦点被锁死在输入框内部；<br>4. Esc 键未清空搜索框文字；<br>5. 会话行删除后邻位焦点未指向相邻项，或列表清空未回落搜索框。 |
| `libs/WorkspaceState/tests` | `配对到首发：从 PairingStarted、PairingSucceeded 到 AuthPresent，再到目录就绪后发送首条消息` | 纯状态机：配对流程 -> Ready IdleEmpty -> Send 首发创建在途命令 -> 快照 Active | `Workspace.update` 处理首发消息时未产出 `CreateConversation` 命令。 |
| `libs/WorkspaceState/tests` | `到期提交：未撤销时到期 Tick 触发 DeleteConversation 命令并清除待删台账` | 纯状态机：未到期 Tick 不发删除命令，到期 Tick 触发删除命令并清空 PendingDeletions | 到期 Tick 判定条件被误改（如 `tickNow < b.DueAt`）或台账未清除。 |
| `libs/WorkspaceState/tests` | `失败重试：失败后携带 lastError，Regenerate 发出命令并在新生成完成后清除错误` | 纯状态机：失败带 lastError，Regenerate 发出命令，新完成清空 lastError | `GenerationFinished (error=None)` 到达后未能重置错误为 `None`。 |

---

## 三、需 DevOps 执行的命令清单

本次改动纯为代码编写，根据 Engineer 角色权限约束，不得私自执行真实命令。请交由 DevOps 执行以下命令以获取真实运行证据：

```bash
# 1. 检查代码规范与依赖完整性
./x check

# 2. 检查断言卫生与界面文案规范
./x hygiene
./x lint

# 3. 运行 WorkspaceState 单元测试（验证新增的 3 条纯状态机回归测试）
./x test WorkspaceState

# 4. 运行 AppShell 组合者测试（验证升级后的 4 条旅程与 1 条原生交互测试）
./x compose

# 5. （可选）全量验证
./x all
```

---

## 四、限制与未证明边界摘要

1. **动态执行凭据尚未生成**：本报告记录的是静态源码与测试实现的完成状态，所有测试用例尚未被执行。真实运行结果须待 DevOps 调度执行后回填。
2. **整树自动化无障碍遍历断言（G5）仍未建**：本次新增了关键交互路径上的原生可访问名称、Focusable 角色与 Tab/Esc 行为断言，但跨全仓整树遍历的扫描断言门禁仍属未建状态。
3. **真实屏幕阅读器与真机运行未覆盖**：宿主机器缺少 orca/speech-dispatcher/at-spi，adb 无连接设备，因此本机不具备真机读屏实测与移动端物理设备测试条件。
4. **像素级视觉回归（G3）仍未建**：组件画廊虽已收口，但没有基线与 diff 门禁，渲染变动不会自动引发 CI 报错。
