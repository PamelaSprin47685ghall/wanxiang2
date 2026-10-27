# Approvals

等待中的审批表：登记后等待结果，先到的决定生效，处理过的结果记住一段时间。不懂工具、对话和线协议。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ApprovalDesk<'Item,'Outcome,'Memo>(memo, recentLimit)`，成员 Open、Resolve、Recent、Pending、Count。

# LEMMA
1. **每个审批只结束一次。** [根 AXIOM: 4]
    - 推理：两台设备同时点「批准」和「拒绝」时，必须恰好有一个生效，另一个被告知「已处理」。
2. **「移出等待」和「记入最近」不可分开。** [根 AXIOM: 4]
    - 推理：如果分两步做，落败者可能在两步之间既查不到等待中的审批，也查不到结果。
3. **调用方的副作用先于等待者拿到结果。** [根 AXIOM: 5]
    - 推理：旧实现先记下「首次询问已批准」再放行调用。顺序反过来，下一次调用可能再问一遍。
4. **最近结果有上限。** [根 AXIOM: 4]
    - 推理：长时间运行的主机不能无限增长。

# SPEC
1. [LEMMA: 1, 2] `first decision wins; later ones see the memo`，通过。
2. [LEMMA: 3] `beforeComplete runs before the waiter sees the result`，通过。
3. [LEMMA: 4] `recent memory is bounded`，通过。
