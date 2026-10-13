# Doctor（组合）

旧 `Wanxiang.Server.Doctor` 与 `SupportBundle`。协作者：EventStore、Attachments、AppConfig、ChatDomain、DirLock、HostProbe、AttachmentRefs、Agui、HostLog。端口、磁盘、权限探测改走 HostProbe，附件引用改走 AttachmentRefs，判断文字不变。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **诊断条目、结论与文字与旧实现一致。** [根 AXIOM: 5]
    - 推理：用户和支持包读这些文字。
2. **主机探测与附件引用抽成库，不再依赖 ServerModel。** [根 AXIOM: 3]
    - 推理：ServerModel 还在旧仓库；doctor 只需要引用提取这一件事。

# SPEC
1. [LEMMA: 1] `golden/doctor.txt` 由旧 Doctor 跑同一 Scenario 生成（端口、provider/MCP 配置、快照、日志重放、缺失与孤立附件），通过。
2. [LEMMA: 1] 旧 DoctorInspectTests 原样迁入，通过。
