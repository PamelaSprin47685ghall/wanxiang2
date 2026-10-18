# DevicesModel（库）

旧 `Views/SettingsDevices.fs` 里的设备模型：DeviceInfo、PairingInviteView、InviteTarget 三个类型，以及 `DevicesModel` 模块（设备快照解析、倒计时、分组码、在线文案、ws 到 http 的同源地址、回环判断、邀请地址、局域网地址、QRCoder 二维码矩阵）。DeviceActions（回调）、QrCodeView 和 SettingsDevices 视图留在 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **解析、文案、地址和二维码矩阵与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/devices.txt` 锁解析结果与文案。设备时间字段用不变文化的 roundtrip（`o`），不锁 `%A`；`%A` 的日期格式随当前文化区变，不是契约。`createdText` 仍是本地日历日，金样按 UTC 机器记录，非 UTC 机器只跳过含 `lastSeenText` 的行。
2. 与旧代码的差异：`InviteTarget` 加了 RequireQualifiedAccess，因为 Reachable、NoAddress 是泛化的名字。UI 的调用点要改成 `InviteTarget.Reachable` 这样的写法。

# 迁入或覆盖的旧测试
旧 DevicesUiTests 都要渲染控件，留在旧仓库。本库新增：金样、倒计时取整、分组码、回环地址给出局域网候选、非 ws 地址、残缺条目、吊销优先、二维码是方阵。
