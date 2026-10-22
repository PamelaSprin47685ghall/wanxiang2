# AppConfig

万象的配置模型与 TOML 格式：`AppConfig` 及其各节、默认值，`TomlCodec.tryParse / serialize / ensureInstanceId`。是具体的领域库，不做泛型（根 LEMMA 8：只有一个使用方，格式就是领域）。依赖 Tomlyn，不依赖任何兄弟库。命名空间沿用 `Wanxiang.Config`，旧仓库的调用处不必改。根规则见项目根目录 AGENTS.md。

来源：从旧仓库 `src/Wanxiang.Config/ConfigModel.fs` 与 `TomlCodec.fs` 原样搬来（旧仓库决策 41–44、182、183 等的语义不变）。

# LEMMA
1. **用户手写的配置文件必须照旧能读。** [根 AXIOM: 1, 5]
    - 推理：和账本不同，配置文件是用户自己编辑、自己保管的东西，没有「旧格式没有使用方」这回事。所以本次是搬迁，不重新设计格式；`configVersion` 1 到 4 照旧接受，读到 v1 时显式升级。
2. **未知字段拒绝整份配置；写回时按当前模型完整重写。** [根 AXIOM: 5, 6]
    - 推理：拼错的键被悄悄忽略，用户会以为设置生效了。完整重写让文件和内存只有一种形状。
3. **格式是纯函数，不碰文件；一处例外是 TLS 路径的存在性检查。** [根 LEMMA: 3, 4]
    - 推理：读写文件、监视、何时采纳归别的对象（AtomicTextFile 与 ConfigStore）。TLS 证书/私钥路径的存在性检查留在校验里（2026-10-18 复判后保留，Chesterton's Fence 立得住）：它只回答「路径在不在」，不读内容；目标是启动前把「填了路径但文件不在」这种配置事故报出来——放到监听器处才发现，失败会推到用户看不见的地方。三条规则（只填一半、文件不存在、齐备可往返）由 `只填证书或只填私钥都被拒绝`、`证书文件不存在时拒绝启动`、`证书路径齐备时被接受并能往返 TOML` 锁住。

# SPEC
1. [LEMMA: 1, 2] 各节解析、往返、未知字段、版本 1 升级、不支持的版本、各节边界校验、TLS 配对规则、providers/mcp/auth 校验、复杂 extra 往返，以及辅助函数。
    - 证明：`tests/Tests.fs` 从旧仓库 `tests/Wanxiang.Tests/ConfigTests.fs` 中不涉及 ConfigStore 和 Auth 的部分原样搬来，全部通过。搬迁时删掉了两处从未被使用的变量（`bad`、`topPLow`），是旧测试里的死代码。
2. [LEMMA: 1, 2, 3] `TomlCodec.fs` 中 `tryParseV1` 与 `tryParseV2` 按配置节拆成小私有函数，入参统一带 `errors: ResizeArray<string>`，顶层只保留按顺序调用节函数、顺序校验与组装 `AppConfig`，错误追加顺序与文字不变。
    - 节函数清单：共用 `parseRuntime`、`parsePairing`、`parseAuthClients`、`parseToolPolicy`、`validateAuth`；v1 专有 `parseNetworkV1`、`parseProvidersV1`、`parseMcpV1`、`validateV1`；v2 专有 `parseNetwork`、`parseGeneration`、`parseTools`、`parseProviderEntry` / `parseProviders`、`parseMcp`、`parsePresets`、`validateNetwork`、`validateGeneration`、`validateTools`、`validateProvidersAndMcp`。
    - 证明：`./x test AppConfig` 原样通过；重新打包后 `ConfigStore.Tests`（11 个测试）与 `ConfigMutation.Tests`（1 个测试）全部通过。另做了独立对照（2026-10-18）：同一份「一次踩 18 条错」的 TOML 分别喂给重构前的 `TomlCodec.fs`（`git worktree` 里 HEAD 的版本）与重构后的版本，错误清单逐条、逐序相同（18 条一致）。
3. [LEMMA: 2; 根 AXIOM: 5] `[pairing]` 的三个限流参数省略时使用既有默认值；显式填写时必须是 `1..Int32.MaxValue` 的正整数。`int32` 的正数分钟乘 60 秒后仍远小于 `TimeSpan` 上限（约 153.7 亿分钟），主机侧 `TimeSpan.FromMinutes` 不会溢出，因此不再设更小的分钟上界。**但窗口大到 `now - window` 越出 `DateTimeOffset` 早期边界时，减法本身会抛异常**；那一侧由 `libs/FailureLimiter` 按「永不滑出」兜住（见其 SPEC 6），所以这里不重复设限。错误类型、非正数与越界整数拒绝整份配置，不截断、不静默回退。v1 与当前格式使用相同校验。
    - 证明：`tests/Tests.fs` 的 `配对限流显式非法值拒绝整份配置`、`v1 配对限流显式非法值同样被拒绝` 与 `配对三个限流参数都接受 Int32.MaxValue 这一上界`，通过。
4. [LEMMA: 2; 根 AXIOM: 1] `network.chunkSizeBytes` 有上下界：不得小于客户端上传的固定块大小（256 KiB），也不得大于 1 MiB。线协议没有通告分块大小的字段，客户端固定按 256 KiB 分块；主机把该值当上传块上限，配小了就等于每次上传都被拒。**下界之外还要有上界**：主机下载附件时按这个值 `Array.zeroCreate<byte>`，没有上界就等于让一份配置决定单次分配多大（`2000000000` ≈ 1.86 GB）。越界时按原值拒绝整份配置，让主机启动即报出原因，而不是等用户传附件才静默失败。
    - 证明：`tests/Tests.fs` 的 `分块大小小于客户端固定块时被拒绝`、`分块大小过大时被拒绝，避免下载时单次分配巨量缓冲`、`分块大小超出 int32 范围时按原值拒绝而不被截断` 与 `分块大小按取值分别判定：越界与非正数报正值，低于下界报下界，恰在下界被接受`，通过；合同见 `docs/contracts/resource-budgets.md` §6。
5. [LEMMA: 2, 3; 根 AXIOM: 5] 整数取值一律先看原值再判范围，不先截断成 `int32`。`int64` 字段（`maxAttachmentBytes`）用 `asInt64Exact` 取原值；`int32` 字段经 `getInt32` 读取，越出 `int32` 就记 `must be a 32-bit integer` 并返回 `None`（由调用方回退默认值），不截断成「看起来合法」的数。会截断的旧 `asInt` 已删除，避免再被复用。
    - 理由：`4294967360`（4 GiB + 64）截断成 `int32` 是 64，`64 > 0` 通过校验——运维以为给了 4 GiB，主机只收 64 字节且不报错；`4294967297` 截断成 1，会让 `timeoutSeconds` 静默变成 1 秒、`maxRetries` 静默变成 1 次。这类截断不改变字段合法性，只把用户的意图换成另一个值，所以必须在取值处暴露原值。
    - 证明：`tests/Tests.fs` 的 `附件上限超出 int32 范围时保留原值而不被截断`（`maxAttachmentBytes` 取到 `4294967360`）与 `模型服务整数参数越界时不被截断成合法值`（`timeoutSeconds` / `maxRetries` 越界报错），通过。
