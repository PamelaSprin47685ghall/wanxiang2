# DevicesModel（库）

旧 `Views/SettingsDevices.fs` 里的设备配对地址推断与二维码矩阵：`InviteTarget` 类型，以及 `DevicesModel` 模块的 `httpOrigin`（ws 到 http 的同源地址）、`isLoopbackHost`（回环判断）、`lanAddresses`（局域网 IPv4 地址）、`inviteTarget`（邀请地址）与 `qrMatrix`（QRCoder 二维码矩阵）。纯 BCL 的设备领域事实（`DeviceInfo`、`PairingInviteView` 及 `parse`、`countdown`、`groupCode`、`lastSeenText`、`createdText`、`inviteLifetime`）已下沉至 `libs/WorkspaceState/DevicesModel.fs`（同名模块在消费方按名合并）。不引用任何其他兄弟库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **地址推断和二维码矩阵与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`loopback host offers lan candidates on the same port`、`non websocket urls have no invite address`、`qr matrix is square and non-empty`（设备快照解析、倒计时、分组码与在线文案用例随事实下沉迁入 `libs/WorkspaceState/tests/DevicesModel.Tests.fs`）。
2. [LEMMA: 1] 与旧代码的差异：`InviteTarget` 加了 RequireQualifiedAccess，因为 Reachable、NoAddress 是泛化的名字（见 `loopback host offers lan candidates on the same port`、`non websocket urls have no invite address`）。UI 的调用点改成 `InviteTarget.Reachable` 这样的写法。

# 迁入或覆盖的旧测试
旧 DevicesUiTests 都要渲染控件，留在旧仓库。本库保留：回环地址给出局域网候选、非 ws 地址、二维码是方阵。
