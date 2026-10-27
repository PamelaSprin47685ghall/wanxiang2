# DeviceListJson

合并包：DeviceListJson、DeviceRules、DeviceSightings。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `DeviceListJson`

## DeviceListJson

设备列表里一台设备的线上 JSON 形状（从旧 WsConnection.SendDevices 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`DeviceListJson.item id name createdAt lastSeen (facts: Set<DeviceFact>) : JsonObject`；`DeviceFact = Revoked | Online | Current | Desktop`（RequireQualifiedAccess，四个正交运行时事实用集合表达，零裸布尔）。线上 JSON 字节契约不变：`online` 仍只对未吊销设备成立。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `设备条目形状与旧实现一致` 通过；端到端由 compose/ServerApp 迁入的 DevicesTests钉住。

## 迁入或覆盖的旧测试
以下旧测试已原文迁入 wanxiang2，端到端经过本库：
- `device list shows names online state and only desktop may invite`（compose/ServerApp/tests/DevicesTests.fs）
- `revoking a device disconnects it immediately and marks revoked in toml and list`（同上）
- `rename persists to toml and rejects empty reserved and desktop targets`（同上，列表中的名字）

本库自己的行为测试（边界与失败路径）见 `tests/DeviceListJson.Tests.fs`。

## 成员 `DeviceRules`

## DeviceRules

设备改名与吊销的规则（从旧 WsConnection.ChangeDevice 拆出）。公开面：`DeviceChange`（RequireQualifiedAccess）、`DeviceRules.check isReservedName targetName targetIsCurrent targetRevoked change`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **错误文字与判定顺序与旧实现一致。** [根 AXIOM: 5]
2. **规则只看名字与几个布尔，独立于连接与配置。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `改名与吊销规则与旧实现一致` 通过。

## 迁入或覆盖的旧测试
- 旧设备管理测试经 ServerApp 端到端，随 8.8 列在 compose/ServerApp。

## 成员 `DeviceSightings`

## DeviceSightings

主机自有的设备记账（令牌哈希 → 最近认证时间），存在数据目录下主机独占的 JSON 文件里。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`DeviceSightings(path, ?throttle)`：`Seen(hash, now) : Result<unit,string>`、`LastSeen(hash) : DateTimeOffset option`、`Seed(entries) : Result<unit,string>`、`Path`。

## LEMMA
1. **用户拥有配置文件；主机只在用户明确改配置时写它。** [根 AXIOM: 用户拥有配置；不要求兼容]
    - 推理：旧实现每次认证都对用户编辑的 TOML 做读-改-写记 lastSeen，与用户/测试的外部编辑竞争（WriteIfUnchanged 只缩小窗口）。记账挪到主机自有状态，竞争从根上消失。
2. **节流行为与旧实现一致**：同一设备 1 分钟内不重复落盘。 [根 AXIOM: 5]
3. **记账丢失只影响「最近连接」显示**：文件缺失或损坏当空表，落盘失败返回 Error 由调用方记日志。

## SPEC
1. [LEMMA: 1] `认证记账只写主机自己的文件，配置文件一个字节都不碰` 通过。
2. [LEMMA: 2] `一分钟内重复认证只改内存，跨进程重开后读回已落盘的时间` 通过。
3. [LEMMA: 1] `迁移只补缺、取较新者，无变化不写盘；损坏文件当空表` 通过。
4. AppConfig：TomlCodec.serialize 不再写 `lastSeen`（仍读，供迁移）。

## 迁入或覆盖的旧测试
- 无整文件；修复的是 extract-ledger 上「无效外部编辑……文件监视」间歇失败的根因（集成步骤见根 AGENTS.md 路线图）。
