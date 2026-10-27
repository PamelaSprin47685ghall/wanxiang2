# Wire

主机和客户端之间的线协议：事件外壳 `WireEvent`、客户端命令 `ClientCommand`、编解码 `WireCodec`，以及只为线上传输而存在的值类型（`CommitId`、`SessionConfig`、`GenerationError`、`GenerationUsage`、`ProviderRateLimit`）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：命名空间 `Wanxiang.Protocol`（`WireEvent`、`WireCodec.encode/tryDecode/encodeCommand/tryDecodeCommand`、导出与搜索的查询/页类型和限额）与 `Wanxiang.Core`（上述值类型、`ClientCommand`、`ClientCommand.businessPayload`、`SessionConfig.toJson/fromJson`）。命名空间沿用旧仓库，让旧调用点不用改。

# LEMMA
1. **线格式字节兼容，不重新设计。** [根 AXIOM: 1, 5]
    - 推理：线格式的另一端是用户手里别的设备和缓存的旧版本客户端，它们不随主机一起升级。改字段名、改事件名或改数字表示，用户看到的是「连不上」或静默丢数据，而且主机这边没法替他们修。所以事件类型名、字段名（包括历史上的 `threadId`/`runId`）、可选字段的省略规则全部原样保留，并用金样钉住。真要改，只能走 `protocolVersion` 提升，旧客户端拿到明确的升级提示。
2. **只搬真正属于线协议的东西。** [根 AXIOM: 3]
    - 推理：旧 `Core/Types.fs` 里的 `Constants`（日志格式版本、命令前缀、bootId）和 `WanxiangError` 是账本和领域的事，留在旧仓库，等路线第 5 项。`SessionConfig` 的 JSON 形状被线和日志共用，形状只能有一份，放在这里，旧 `CommitCodec.configToJson/configFromJson` 改为转调。
3. **规范化不是线协议的事。** [根 AXIOM: 3]
    - 推理：commandId = SHA-256(前缀 + invocationId + 类型 + JCS(业务载荷))，是主机的幂等规则，客户端不计算。旧 `canonicalPayload` 把 JCS 也揉进来，迫使线协议依赖 Canonical。现在线协议只交出 `businessPayload`（JsonObject），规范化和哈希由领域方做；字节结果不变，由金样钉住。
4. **commitId 用 JSON 数字承载，超过 2^53 时 JavaScript 客户端会丢精度。** [根 AXIOM: 5]
    - 推理：这是现有格式的已知缺陷，不能单方面改成字符串（见 LEMMA 1）。实际上提交序号到 2^53 不可达，风险只在理论上。[TODO] 如果出现 JS 客户端（当前浏览器侧是 F# 编到 browser-wasm 的 `app/Pwa`，没有 JS 客户端），随下一次 `protocolVersion` 提升改为字符串。

# SPEC
1. [LEMMA: 1] 9 类代表事件（含 generation.delta）、一条命令的编码等于金样字节，金样能解码回同一事件：`tests/WireGoldenTests.fs` 的 `encode matches golden bytes`、`golden bytes decode back to the same event`、`command golden bytes` ，通过。
2. [LEMMA: 1] 旧仓库 `WireCodecTests.fs`（27 个，含 `CreateConversation command roundtrips perfectly` 等）、`SessionConfigTests.fs`（6 个，含 `empty 配置：provider/model 必填均报错` 等）原样搬来，通过。旧 `CodecTests.fs` 测的是领域事件和 commandId，不属本库；它后来随领域接线原样搬到 `compose/ChatDomain/tests/CodecTests.fs`（ChatDomain SPEC 3）。
3. [LEMMA: 2] `SessionConfig.toJson/fromJson` 往返相等，空对象读出默认值：`session config json round trip`，通过。
4. [LEMMA: 3] `businessPayload` 字节等于金样：`business payload golden bytes (domain hashes this after JCS)`，通过。领域方对它做 JCS 后的 commandId 与旧实现相同：`compose/ChatDomain/CommandEngine.fs` 的 `canonicalPayload` 就是 `JCS(businessPayload)`，金样在 `compose/ChatDomain/tests/CompositionTests.fs` 的 `command id equals the one the old implementation computed`（值由旧实现算出），通过。旧仓库接线后的同一路径见旧仓库 8a506ca 新增的 `src/Wanxiang.Core/CanonicalPayload.fs`；旧仓库及其 `CodecTests` 已物理退役（16904f0 删除 `src/` 与 `tests/`）。
5. [LEMMA: 1] `GenerationDelta` 的载荷是具名记录 `GenerationDeltaData`，组合者可以跨程序集写类型标注；字节不变，见 SPEC 1（`encode matches golden bytes`）和 `compose/AguiWire` 金样。
