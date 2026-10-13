# JsonPick

从不可信的 JSON 里按键取值：键缺失、类型不对、值为 null 一律得 None，从不抛异常。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`JsonPick.tryString/tryInt/tryBool/tryObject/tryArray`、`schemaOf`。

# LEMMA
1. **上游返回的 JSON 不可信，取值不能让流程崩掉。** [根 AXIOM: 5]
    - 推理：提供方会改字段、给 null、给字符串数字。取不到就得 None，由调用方决定默认值。

# SPEC
1. [LEMMA: 1] 各种类型取得到；缺失、null、类型不对、节点不是对象时都是 None：`tests/Tests.fs`，通过。

说明：Anthropic、Gemini 两个库各有一份私有的同类取值函数，这是有意的：库与库之间不互相依赖（根 AGENTS.md）。三份都只有几十行；第四个使用者出现时再考虑别的办法。
