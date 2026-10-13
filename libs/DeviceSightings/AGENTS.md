# DeviceSightings

主机自有的设备记账（令牌哈希 → 最近认证时间），存在数据目录下主机独占的 JSON 文件里。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`DeviceSightings(path, ?throttle)`：`Seen(hash, now) : Result<unit,string>`、`LastSeen(hash) : DateTimeOffset option`、`Seed(entries) : Result<unit,string>`、`Path`。

# LEMMA
1. **用户拥有配置文件；主机只在用户明确改配置时写它。** [根 AXIOM: 用户拥有配置；不要求兼容]
    - 推理：旧实现每次认证都对用户编辑的 TOML 做读-改-写记 lastSeen，与用户/测试的外部编辑竞争（WriteIfUnchanged 只缩小窗口）。记账挪到主机自有状态，竞争从根上消失。
2. **节流行为与旧实现一致**：同一设备 1 分钟内不重复落盘。 [根 AXIOM: 5]
3. **记账丢失只影响「最近连接」显示**：文件缺失或损坏当空表，落盘失败返回 Error 由调用方记日志。

# SPEC
1. [LEMMA: 1] `认证记账只写主机自己的文件，配置文件一个字节都不碰` 通过。
2. [LEMMA: 2] `一分钟内重复认证只改内存，跨进程重开后读回已落盘的时间` 通过。
3. [LEMMA: 1] `迁移只补缺、取较新者，无变化不写盘；损坏文件当空表` 通过。
4. AppConfig：TomlCodec.serialize 不再写 `lastSeen`（仍读，供迁移）。

# 迁入或覆盖的旧测试
- 无整文件；修复的是 extract-ledger 上「无效外部编辑……文件监视」间歇失败的根因（集成步骤见根 AGENTS.md 路线图）。
