# ToolApproval（组合）

把 Approvals（等待表）、Wire（审批事件）、AppConfig（工具策略）和 ToolSummary（人话摘要）组合成旧 `Wanxiang.Server.ToolApprovalBroker`。命名空间和公开面与旧代码相同。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **审批状态只在内存里。** [根 AXIOM: 4]
    - 推理：沿用旧决策。主机重启后，旧的审批编号会得到「已失效」。
2. **没有能审批的客户端在线就立即拒绝，宽限时间内出现的照常发起审批。** [根 AXIOM: 4]
    - 推理：旧客户端不能被挂住；分叉后马上生成时，发起操作的客户端还没来得及观察新对话。
3. **线上字节、拒绝原因的文字和「首次询问」的语义都与旧实现一致。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1, 2, 3] `golden/approval.txt` 由旧 ToolApproval.fs 跑同一 Scenario 生成（`tests/GoldenTests.fs` 的 `approval events and outcomes equal the old implementation`），覆盖批准、拒绝、已处理、已失效、取消、清空对话、无人可批、超时，通过。
2. 迁自旧仓库 W3ToolPolicyTests 的审批代理部分（`tests/ToolApprovalTests.fs` 的 `[AC-2] 每次询问：挂起等待，期间没有任何人决定就一直不返回；批准后继续`、`[AC-3] 两个客户端同时决定：恰有一个被接受，另一个收到「已处理」` 等），9 项通过。端到端部分依赖主机，留在旧仓库。
