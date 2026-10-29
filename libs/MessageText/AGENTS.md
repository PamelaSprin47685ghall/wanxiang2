# MessageText

合并包：MessageText、TextMatch。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `MessageText`

## MessageText

从消息 JSON（ChatMessage 形状）取出用户看得见的正文：text 字段和 contents 里的 text 项，跳过思维链。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`MessageText.visible maxChars payload`。

## LEMMA
1. **思维链、工具参数和工具结果不算正文。** [根 AXIOM: 5]
    - 推理：搜索只匹配用户看得见的内容，沿用旧决策 201。
2. **有上限。** [根 AXIOM: 4]
    - 推理：超长消息不能拖慢整次扫描。

## SPEC
1. [LEMMA: 1, 2] `tests/MessageText.Tests.fs` 的 `text items are joined by newline; reasoning and tool parts are skipped`、`top level text and arrays are read; output is capped` ，通过。

## 成员 `TextMatch`

## TextMatch

多词全匹配和命中片段：分词、所有词都出现才算命中、以命中为中心截取片段。不懂消息和协议。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`TextMatch.terms`、`firstMatch`、`snippetAround total text start length`。

## LEMMA
1. **所有词都出现才算命中，大小写不敏感。** [根 AXIOM: 5]
    - 推理：沿用旧搜索和侧栏标题过滤的同一套规则，用户在两处看到的结果一致。
2. **片段不切开代理对，换行一对一换成空格。** [根 AXIOM: 5]
    - 推理：切开代理对会显示乱码；一对一替换保证高亮下标仍然准确。

## SPEC
1. [LEMMA: 1] `terms split on whitespace and drop duplicates`、`every word must appear; the earliest one is reported`，通过。
2. [LEMMA: 2] `snippet keeps 30 chars before the hit, marks cuts and flattens newlines`、`snippet never splits a surrogate pair`，通过。
