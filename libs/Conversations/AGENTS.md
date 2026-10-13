# Conversations

会话领域：事件（创建、分叉、改名、改配置、删除、标记、记录消息、删除消息）、投影（会话、消息、fork 继承）、折叠规则，以及事件和投影的 JSON 形状。按会话配置类型 `'C` 泛型化，只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：命名空间 `Wanxiang.Core`。类型 `EventData<'C>`、`Conversation<'C>`、`Projection<'C>`、`MessageRecord`。模块 `Conversations`（`empty`、`apply proj id at events`、`tryConversation`、`effectiveMessages*`、`conversationList`、`trailingModelMessages`）、`EventJson`（`eventType`、`Version`、`toJson`、`tryFromJson`）、`ProjectionJson`（`toJson`、`tryFromJson`）。

# LEMMA
1. **配置是不透明的。** [根 AXIOM: 3]
    - 推理：领域只保存配置，fork 时把父会话的配置原样复制过去，从不读里面的字段。所以用泛型 `'C`，配置的 JSON 形状由调用方以函数传入，本库不依赖线协议。这是语义上本来就有的边界，不是依赖注入：没有任何行为交给外部决定。
2. **折叠是纯函数：输入提交号、时间、事件，输出新投影或原因。** [根 AXIOM: 3, 4]
    - 推理：提交外壳（commandId、bootId、来源）属于账本和线协议，领域只需要三样东西。出错时返回人能读懂的原因，不返回错误类型：截尾、只读还是拒绝，由调用方决定。一条提交里任何一个事件失败，整条提交都不生效。
3. **事件和快照的 JSON 形状不能改。** [根 AXIOM: 1]
    - 推理：两者都写在用户磁盘上，提交也发给客户端。类型名、字段名、版本号、提交号用字符串还是数字，全部照旧；碰到不认识的版本就拒绝，不猜。
4. **fork 规则照旧，标题编号也照旧。** [根 AXIOM: 5]
    - 推理：子分支只继承父会话 fork 那一刻、forkAfterId 之前的消息，父会话后来的删改不影响子分支。同一个父会话分出多个分支时，标题依次是「（分叉）」「（分叉 2）」……

# SPEC
1. [LEMMA: 1, 4] fork 原样复制配置，按 fork 点继承消息，标题编号正确：`tests/Tests.fs` `fork copies the parent config untouched...`，通过。
2. [LEMMA: 4] 父会话后来的删除不影响子分支：`later changes in the parent do not leak into a fork`，通过。
3. [LEMMA: 2] 提交号不连续、目标不存在都会被拒绝；失败的提交不改变投影：`commit ids must be contiguous...`、`a failing event leaves the projection unchanged`，通过。
4. [LEMMA: 3] 换任意配置编码，事件和投影 JSON 往返后都不变；不认识的版本被拒绝，通过。和旧实现字节一致的证据在组合者 ChatDomain 的金样里，因为金样必须带上真实的 SessionConfig 形状。
