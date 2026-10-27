# WsConnection（组合）

旧 `WsConnection`（握手、认证、配对、观察、追赶/快照、命令、附件、搜索、设备、配置写入）与 `ConnectionRegistry`（连接表与广播）。
协作者：EventStore、ChatOrchestrator、Attachments、PairingDesk、HostStatus、ServerModel、ConfigMutation、ProviderCatalog、Search、HostLog、AguiWire、
ClientToken、DesktopClient、DeviceSightings、DeviceRules。命名空间沿用 Wanxiang.Server。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **线上行为与旧实现一致**，下列有意改动除外。 [根 AXIOM: 5]
2. **用户拥有配置文件；认证绝不写它。** 最近认证时间记在 DeviceSightings（主机自有状态）；构造参数多一个 `sightings`。
3. **设备改名/吊销规则有独立语义，抽成库 DeviceRules**；查设备、写配置、回执是协作，留下。 [根 AXIOM: 3]
4. 配对失败计数走 `PairingDesk.RecordFailure`（邀请码路径）与 `Attempt`（配对码路径），冻结状态只在 PairingDesk 里。

# SPEC
1. [LEMMA: 2] `AuthNoConfigWriteTests.fs`：`auth produces zero config writes` —— 真实组合（真实 TOML + ConfigStore、EventStore、ToolRegistry、
   ChatOrchestrator、AttachmentStore、PairingDesk、DeviceSightings），经本机 WebSocket 认证 3 次（3 个连接）：updateConfig 调用 0 次，
   配置文件原文与 mtime 不变，无临时文件，device-sightings.json 有记录且重开可读回。连跑三次通过。同一测试顺带钉住 `HostVersion` 注入：认证后的 host.capabilities 带宿主给的版本。
2. 有意改动：未用的 `PairingRequested d` 改为 `_`（警告即错误，见 `auth produces zero config writes`）。
3. `hostVersion` 由宿主注入（`HostVersion` 属性，ServerApp 设置，见 `auth produces zero config writes`）；不再取本程序集版本。
4. 待拆（路线图，[TODO]）：设备列表 JSON 形状、目录快照 JSON 可再拆；追赶/快照游标状态机与发送队列绑在一起，暂留。

# 迁入或覆盖的旧测试
- 旧仓库的 WsConnection 测试都经 ServerApp 起整台主机（TestServer），随 8.8 ServerApp 迁入并在那里逐条列名。
- 新增（旧仓库无对应）：`auth produces zero config writes`，取代 extract-ledger 上 watcher 测试间歇失败的根因。

# 拆分（待拆项完成）
- 设备条目的 JSON 形状拆成库 DeviceListJson；查配置、在线表、当前设备、能否邀请留在组合者。
- 目录快照没有可再拆的：它已经只调用 ProviderCatalog.providers / generation 加工具目录，形状归 ProviderCatalog 与 Wire。
