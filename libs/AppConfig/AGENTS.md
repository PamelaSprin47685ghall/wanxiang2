# AppConfig

万象的配置模型与 TOML 格式：`AppConfig` 及其各节、默认值，`TomlCodec.tryParse / serialize / ensureInstanceId`。是具体的领域库，不做泛型（根 LEMMA 8：只有一个使用方，格式就是领域）。依赖 Tomlyn，不依赖任何兄弟库。命名空间沿用 `Wanxiang.Config`，旧仓库的调用处不必改。根规则见项目根目录 AGENTS.md。

来源：从旧仓库 `src/Wanxiang.Config/ConfigModel.fs` 与 `TomlCodec.fs` 原样搬来（旧仓库决策 41–44、182、183 等的语义不变）。

# LEMMA
1. **用户手写的配置文件必须照旧能读。** [根 AXIOM: 1, 5]
    - 推理：和账本不同，配置文件是用户自己编辑、自己保管的东西，没有「旧格式没有使用方」这回事。所以本次是搬迁，不重新设计格式；`configVersion` 1 到 4 照旧接受，读到 v1 时显式升级。
2. **未知字段拒绝整份配置；写回时按当前模型完整重写。** [根 AXIOM: 5, 6]
    - 推理：拼错的键被悄悄忽略，用户会以为设置生效了。完整重写让文件和内存只有一种形状。
3. **格式是纯函数，不碰文件。** [根 LEMMA: 3, 4]
    - 推理：读写文件、监视、何时采纳归别的对象（AtomicTextFile 与 ConfigStore）。例外：TLS 证书路径的存在性检查会读文件系统，这是从旧代码保留下来的启动前校验（Chesterton's Fence），记为 [TODO] 待重新判断。

# SPEC
1. [LEMMA: 1, 2] 各节解析、往返、未知字段、版本 1 升级、不支持的版本、各节边界校验、TLS 配对规则、providers/mcp/auth 校验、复杂 extra 往返，以及辅助函数。
    - 证明：`tests/Tests.fs` 共 26 个测试，从旧仓库 `tests/Wanxiang.Tests/ConfigTests.fs` 中不涉及 ConfigStore 和 Auth 的部分原样搬来，全部通过。搬迁时删掉了两处从未被使用的变量（`bad`、`topPLow`），是旧测试里的死代码。
2. [TODO] `TomlCodec.fs` 有 1000 行，按节拆成小函数会更好读；这次只搬不改，等迁移落地后再说。
