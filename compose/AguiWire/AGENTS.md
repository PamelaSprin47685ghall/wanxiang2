# AguiWire（组合）

把 Wire（语义事件和编解码）与 Agui（版本常量和宽容读取）组合成旧 `Wanxiang.Protocol.WireAgui`：`toAguiEvent`、`encode`、`encodeCommand`、`tryDecode`。命名空间和公开面与旧代码相同。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **AG-UI 文本是客户端看到的字节，必须与旧实现一致。** [根 AXIOM: 5]
    - 推理：PWA 和其他设备的旧版本只认这些字节。
2. **已知的信息丢失原样保留，改不改等协议升级再定。** [根 AXIOM: 2]
    - 推理：RUN_FINISHED 不带 durationMs 和 cachedTokens；标准事件解回时缺少对话 id，或用全零 id 补位；畸形错误文本里带 .NET 异常原文。客户端已经适应了这些行为。
3. **源码与旧实现相同，只有 generation.delta 的载荷改用 Wire 的具名记录 `GenerationDeltaData`。** [根 AXIOM: 3]
    - 推理：匿名记录不能跨程序集写类型标注（FS0001）。改用具名记录后，customDelta 照旧按记录传参，线上字节不变，金样可以证明。

# SPEC
1. [LEMMA: 1, 2, 3] `golden/agui.txt` 由旧 WireAgui.fs、Agui/*.fs 跑同一 Scenario 生成（`tests/GoldenTests.fs` 的 `AG-UI bytes and decode results equal the old implementation`），覆盖出站事件、命令、往返和 20 种入站文本，通过。
2. 迁自旧仓库 WireAguiTests（去掉对客户端工程的引用）：`tests/WireAguiTests.fs` 的 `generation started maps to RUN_STARTED`、`text delta maps to TEXT_MESSAGE_CHUNK`、`non text delta maps to CUSTOM`、`completed generation maps to RUN_FINISHED` 等通过，共 35 项。
