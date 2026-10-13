# ToolSummary

把工具调用（函数名加参数）写成一句人话。主机写审批请求、客户端画工具卡片用的是同一份，两边文案不会走样。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ToolSummary.describe`、`isBuiltin`、`userDeniedText`。

# LEMMA
1. **用户批准之前要看得懂工具要做什么。** [根 AXIOM: 5]
    - 推理：审批卡片只写函数名，用户等于盲批。内置工具各有专门的句子；MCP 和不认识的工具退回「调用 X（展开看参数）」，不会编造含义。

# SPEC
1. [LEMMA: 1] 内置工具、缺参数、MCP、未知工具的句子都与旧仓库一致：`tests/Tests.fs`，通过。
