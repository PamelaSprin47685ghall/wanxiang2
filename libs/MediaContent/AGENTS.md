# MediaContent

合并包：MediaContent、MessageSerde。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `MediaContent`

## MediaContent

按传输能力（MediaSupport）把一个附件（sha256、媒体类型、文件名、大小）变成 Provider 能消费的 AIContent：图片/PDF/音频走 DataContent，文本内联成代码块，其余给一行说明。从旧 AttachmentContent 拆出，不认识消息格式。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：模型看到的文字是行为的一部分。

## SPEC
1. [LEMMA: 1] 测试覆盖能力拒收、丢失、文本内联：`tests/MediaContent.Tests.fs` 的 `images go as data when the transport accepts them, otherwise a note explains why`、`missing blobs and text files`、`ofProviderKind maps each transport to its capabilities` 通过；完整输出由 AgentContent 金样钉住。

## 迁入或覆盖的旧测试
- 旧测试迁在 compose/AgentContent/tests（逐条列在 AgentContent AGENTS.md）。

## 成员 `MessageSerde`

## MessageSerde

ChatMessage 与 JSON 互转（Microsoft.Extensions.AI 的 JSON 配置），宽松解析客户端手写 JSON，抽取 `{"type":"attachment"}` 附件引用。源码原样迁自旧 `Wanxiang.Agent.MessageSerde`，去掉了没用到的 Wanxiang.Core 引用。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：落盘消息要继续能读，送给 Provider 的 JSON 不能变。

## SPEC
1. [LEMMA: 1] 测试覆盖宽松解析、附件引用、往返：`tests/MessageSerde.Tests.fs` 的 `loose client json keeps text and skips attachments, refs are extracted`、`tagged messages round trip` 通过；完整字节由 AgentContent 金样钉住。

## 迁入或覆盖的旧测试
- 旧测试迁在 compose/AgentContent/tests（逐条列在 AgentContent AGENTS.md）。
