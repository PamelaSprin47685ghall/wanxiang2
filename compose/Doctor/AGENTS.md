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

# 迁入或覆盖的旧测试
- DoctorInspectTests.fs 整个文件（原样迁入 tests/）。

# 按检查项拆分（待拆项完成）
- 检查项拆成库：ProviderCheck（provider-config）、McpCheck（mcp-config）、SnapshotCheck（snapshot）、AttachmentAudit（missing/orphan-attachments）。
  库只拿基本类型，探测磁盘、读配置、映射存储类型留在组合者；检查顺序、名字与文字不变，`golden/doctor.txt` 照旧逐字节通过。
- 支持包的配置脱敏拆成库 SecretText（已知凭据精确替换 + 键值兜底打码），替换顺序与旧实现相同。
  stderr 的精确替换在 JsonLinesLog 里：库不能引用兄弟库，所以各持一份同样的规则，唯一差别是 stderr 按长度降序替换（一个密钥是另一个子串时才有区别）。
- 有意改动：宿主运行中（HostOwned）看到更新版本写的日志时，以前报「damaged…请 doctor --fix」，现在报与 UnsupportedFormat 相同的文字，不再建议 --fix；支持包 diagnosis 同样记为 `unsupported-format`（以前是 `repair-required`）。
