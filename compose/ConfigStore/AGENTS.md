# compose/ConfigStore

组合者 C：用 `AtomicTextFile` 与 `AppConfig` 组成「TOML 文件是唯一权威来源」的配置存储。只经交付包使用这两个库。本目录打成包 `Wanxiang2.ConfigStore`。命名空间与旧仓库相同（`Wanxiang.Config.ConfigStore`），公开面与旧版一致，旧仓库的调用处不必改。根规则见项目根目录 AGENTS.md。

公开面：`ConfigStore.Open(path, onReloaded, onRejected) : Result<ConfigStore,string>`；`Current`、`Path`、`Rewrite(cfg)`、`Update(mutate)`、`TriggerReload()`、`Dispose()`。

# LEMMA
1. **C 拥有合作和「当前生效配置」。** [根 LEMMA: 4]
    - 推理：字节、原子写、外部编辑这一事实归文件对象；格式和校验归 AppConfig；何时读、何时写、外部改动采不采纳、通知谁，归本对象。
2. **只有完整有效的配置才替换当前配置；无效或文件被删时保留上一份，并报告。** [根 AXIOM: 1, 5]
3. **写盘前先看有没有外部编辑：有效就采纳，无效就拒绝写。** [根 AXIOM: 1]
    - 推理：绝不能拿旧快照覆盖用户刚改的文件（旧仓库决策 462）。
4. **监视器只是加速。** [根 AXIOM: 2]
    - 推理：本进程写盘也会触发通知；通知到达时用 `ExternalEdit` 判断，没有外部编辑就什么也不做。所以不需要旧版那套自管的去抖计时器和 `lastKnownText`。

# SPEC
（证明在 `tests/Tests.fs`，用真实文件和真实的两个库，`./x compose` 运行，Linux 通过。）
1. [LEMMA: 2] 无效文件被拒，保留上一份：`config store reload keeps last valid configuration on invalid file`。
2. [LEMMA: 1] 重写后持久并重新加载：`config store rewrite persists and reloads complete configuration`。
3. [LEMMA: 2] 重写失败返回 Error，保留旧配置：`rewrite 在 reload 失败时返回 Error 并保留旧配置`。
4. 首次创建的 instanceId 是 UUIDv7；缺 instanceId 时补上：`首次创建的 instanceId 是 UUIDv7`、`hand-written config without instanceId gets one generated instead of failing`。
5. 权限是 0600：`TOML 配置文件权限为 0600`。
6. [LEMMA: 1] Update 是原子的读-改-写：`ConfigStore Update 原子读改写与 Path 属性覆盖`。
7. [LEMMA: 3] 应用内更新不覆盖还没处理的外部编辑：`app-side update never clobbers a pending external edit`。
8. [LEMMA: 2, 4] 监视器采纳有效的外部编辑；文件被删时报告并保留：`watcher adopts a valid external edit and reports deletion`。

1–7 是从旧仓库 `ConfigTests.fs` 原样搬来的 ConfigStore 测试，8 是新加的。
9. [LEMMA: 2] 每次重新加载都重读并重新判定，与旧实现一致；同一份无效原文再次触发时仍会报告：`every reload re-reads the file and reports an invalid file again`。
10. [LEMMA: 3] 外部编辑落在读改写的「检查」与「替换」之间时，放弃写入，不覆盖用户的原文；随后的重新加载照常报告：`an external edit landing between check and replace is never clobbered`。这是旧仓库满载时「无效外部编辑……文件监视」超时的根因（认证后的 lastSeenUtc 写回在 fsync 期间覆盖了测试刚写的无效配置）。
