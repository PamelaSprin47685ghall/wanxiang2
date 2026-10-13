# TextMatch

多词全匹配和命中片段：分词、所有词都出现才算命中、以命中为中心截取片段。不懂消息和协议。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`TextMatch.terms`、`firstMatch`、`snippetAround total text start length`。

# LEMMA
1. **所有词都出现才算命中，大小写不敏感。** [根 AXIOM: 5]
    - 推理：沿用旧搜索和侧栏标题过滤的同一套规则，用户在两处看到的结果一致。
2. **片段不切开代理对，换行一对一换成空格。** [根 AXIOM: 5]
    - 推理：切开代理对会显示乱码；一对一替换保证高亮下标仍然准确。

# SPEC
1. [LEMMA: 1] `terms ...`、`every word must appear...`，通过。
2. [LEMMA: 2] `snippet keeps 30 chars...`、`snippet never splits a surrogate pair`，通过。
