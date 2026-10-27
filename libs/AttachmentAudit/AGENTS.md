# AttachmentAudit

合并包：AttachmentAudit、HostProbe、McpCheck、ProviderCheck、SecretText、SnapshotCheck。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `AttachmentAudit`

## AttachmentAudit

诊断检查 missing-attachments / orphan-attachments（从旧 Doctor 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`AttachmentAudit.run refs blobNames : (ok * detail) * (ok * detail)`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `附件缺失与孤儿判定与旧实现一致` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 DoctorTests、AttachmentTests钉住。

## 迁入或覆盖的旧测试
以下旧测试已原文迁入 wanxiang2，端到端经过本库：
- `orphan-attachments 检查发现未被引用的 blob`（compose/ServerApp/tests/DoctorTests.fs）
- `损坏日志：log-replay FAIL 且附件检查跳过`（同上；跳过由组合者决定）
- `doctor reports and recovery bundle equal the old implementation`（compose/Doctor/tests/GoldenTests.fs，金样由旧代码生成）

本库自己的行为测试（边界与失败路径）见 `tests/AttachmentAudit.Tests.fs`。

## 成员 `HostProbe`

## HostProbe

主机探测：端口是否被占（`portInUse`）、目录所在盘剩余字节（`freeBytes`，取不到时为 None）、目录是否对组或其他人可访问（`groupOrOtherAccessible`）。只问操作系统，不做判断。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **只回答事实，阈值和措辞归调用方。** [根 AXIOM: 3]
    - 推理：doctor 的判断文字是组合者的格式，探测本身与业务无关。

## SPEC
1. [LEMMA: 1] 测试覆盖占用端口、存在目录的剩余空间、0700 与 0755 权限，通过。

## 成员 `McpCheck`

## McpCheck

诊断检查 mcp-config（从旧 Doctor 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`McpCheck.run fileExists (id, enabled, command, url) list : ok * detail`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `mcp 检查文字与旧实现一致` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 DoctorTests钉住。

## 迁入或覆盖的旧测试
以下旧测试已原文迁入 wanxiang2，端到端经过本库：
- `mcp-config 检查发现启用的 command 文件不存在`（compose/ServerApp/tests/DoctorTests.fs）
- `健康数据目录：全部检查通过`（同上）
- `doctor reports and recovery bundle equal the old implementation`（compose/Doctor/tests/GoldenTests.fs，金样由旧代码生成）

本库自己的行为测试（边界与失败路径）见 `tests/McpCheck.Tests.fs`。

## 成员 `ProviderCheck`

## ProviderCheck

诊断检查 provider-config（从旧 Doctor 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ProviderCheck.run (id, enabled, hasApiKey, baseUrl) list : ok * detail`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `provider 检查文字与旧实现一致` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 DoctorTests钉住。

## 迁入或覆盖的旧测试
以下旧测试已原文迁入 wanxiang2，端到端经过本库：
- `provider-config 检查发现启用但无 apiKey`（compose/ServerApp/tests/DoctorTests.fs）
- `健康数据目录：全部检查通过`（同上）
- `doctor reports and recovery bundle equal the old implementation`（compose/Doctor/tests/GoldenTests.fs，金样由旧代码生成）

本库自己的行为测试（边界与失败路径）见 `tests/ProviderCheck.Tests.fs`。

## 成员 `SecretText`

## SecretText

文本脱敏规则：已知凭据值精确替换、键值行兜底打码（从旧 SupportBundle 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`SecretText.replaceKnown secrets text`、`SecretText.maskAssignments prefixes text`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `凭据替换与键值兜底打码` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 SupportBundleTests钉住。

## 迁入或覆盖的旧测试
以下旧测试已原文迁入 wanxiang2，端到端经过本库：
- `bundle 生成全部必需文件且不含密钥`（compose/ServerApp/tests/SupportBundleTests.fs）
- `doctor reports and recovery bundle equal the old implementation`（compose/Doctor/tests/GoldenTests.fs，金样由旧代码生成）

本库自己的行为测试（边界与失败路径）见 `tests/SecretText.Tests.fs`。

## 成员 `SnapshotCheck`

## SnapshotCheck

诊断检查 snapshot 的文字（从旧 Doctor 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`SnapshotState`（RequireQualifiedAccess）、`SnapshotCheck.run state : ok * detail`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] `snapshot 检查文字与旧实现一致` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 DoctorTests钉住。

## 迁入或覆盖的旧测试
以下旧测试已原文迁入 wanxiang2，端到端经过本库：
- `健康数据目录：全部检查通过`（compose/ServerApp/tests/DoctorTests.fs）
- `doctor reports and recovery bundle equal the old implementation`（compose/Doctor/tests/GoldenTests.fs，金样由旧代码生成）

本库自己的行为测试（边界与失败路径）见 `tests/SnapshotCheck.Tests.fs`。
