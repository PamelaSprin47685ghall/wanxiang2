# Sse

Server-Sent Events 分帧（W3C）：把字节流切成（事件名, data）。不懂任何事件的含义。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`SseEvent`、`Sse.read stream ct`。

# LEMMA
1. **只共享分帧，不共享语义。** [根 AXIOM: 3]
    - 推理：Anthropic 和 Gemini 都走 SSE，但事件含义完全不同。把两套语义塞进同一个解析器，只会得到一堆 if。
2. **按规范只去掉冒号后的一个空格。** [根 AXIOM: 5]
    - 推理：模型回答里的代码缩进就在 data 正文里。多去一个空格，用户看到的代码就错位了。

# SPEC
1. [LEMMA: 1] 空行分帧，多行 data 用换行拼接，末尾没有换行的帧也算，注释跳过，事件名在每帧之后重置：`tests/Tests.fs`，通过。
2. [LEMMA: 2] 缩进保留：`only the single space after the colon is stripped...`，通过。
