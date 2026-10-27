# ContinuableReply

合并包：ContinuableReply、TitleText。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ContinuableReply`

## ContinuableReply

半截回复是否可续写：助手角色且有非空白正文（从旧 ChatOrchestrator.PartialText 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ContinuableReply.hasText`。

## LEMMA
1. **行为与旧实现一致；形状不对一律为否，不抛。** [根 AXIOM: 5]
2. **这是独立的判定规则；保存与否、何时保存归组合者 ChatOrchestrator 与 PartialReplies。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] `tests/ContinuableReply.Tests.fs` 的 `助手且有正文才算可续写`、`任一段有正文即可，其他段可以是工具调用`、`形状不对一律为否且不抛`  通过。

## 迁入或覆盖的旧测试
- compose/ChatOrchestrator/tests/ProductizationOrchestrationTests.fs 中断言 `ChatOrchestrator.PartialText` 的用例（该静态成员转发到本库）；本库 `助手且有正文才算可续写` 是同一组断言。

## 成员 `TitleText`

## TitleText

对话标题的文字规则：限长、首行、去引号、首句、占位标题判定（从旧 TitleGenerator 拆出）。只依赖 BCL；Markdown 清洗归调用方。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：模型看到的上下文与用户看到的标题是外部可见行为。

## SPEC
1. [LEMMA: 1] `tests/TitleText.Tests.fs` 的 `clip, first line, quotes and first sentence`、`placeholder titles`  通过；端到端由 ChatOrchestrator 金样与迁入的旧测试钉住。

## 迁入或覆盖的旧测试
- 旧测试迁在 compose/ChatOrchestrator/tests（逐条列在 ChatOrchestrator AGENTS.md）。
