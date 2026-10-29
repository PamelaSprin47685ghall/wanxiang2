# Agui

AG-UI 协议的本端常量和宽容读取：协议版本 1.0、扩展命名空间 `wanxiang.dev/`，以及把入站文本分成「已知、未识别、自定义、畸形」四种结果。不懂万象的语义事件。依赖外部包 AGUI.Abstractions。根规则见项目根目录 AGENTS.md。

公开面：`Capabilities.ProtocolVersion/Namespace/extensions`、`ParsedEvent`、`TolerantReader.parse`。

# LEMMA
1. **未识别的 type 要忽略，不能当成错误。** [根 AXIOM: 4, 5]
    - 推理：AG-UI 规定未知事件必须忽略，但官方 .NET SDK 遇到未知事件会抛异常。对端升级不应该导致断连，所以要自己先分流。
2. **只有已知类型但字段坏了才算畸形。** [根 AXIOM: 4]
    - 推理：这种情况才真的是对端违规。
3. **已知类型的清单取自 SDK 常量，不手写。** [根 AXIOM: 3]
    - 推理：SDK 增加事件类型时，这里会自动跟上。

# SPEC
1. [LEMMA: 1, 2, 3] 迁自旧仓库 AguiTests、AguiMappingTests：`tests/AguiTests.fs` 的 `Capabilities protocol version is 1.0`、`Capabilities namespace prefix is wanxiang.dev/` 与 `tests/AguiMappingTests.fs` 的 `a run started event is recognized`、`an unknown event type is tolerated rather than thrown on` 等 29 项通过。
