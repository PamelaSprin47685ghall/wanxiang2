# Attachments（组合）

把 AttachmentBlobs、MediaSniff、HostLog 和 ChatDomain（错误类型）组合成旧 `Wanxiang.Server.AttachmentStore`。命名空间、公开面和磁盘布局（`<data>/attachments/ab/cd/<hash>` 和 `.meta`）与旧代码相同。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **返回值、错误文字、元数据文件字节都与旧实现一致。** [根 AXIOM: 5]
    - 推理：已有数据目录要继续能用，错误会出现在线上。
2. **组合者可以消费别的组合者的包（HostLog、ChatDomain）。** [根 AXIOM: 3]
    - 推理：错误类型和主机日志本身就是组合的产物。`./x pack` 按依赖顺序打包。
3. **删掉旧代码里没人用的公开类型 `AttachmentUpload`。** [根 AXIOM: 3]
    - 推理：上传的运行时状态归 AttachmentBlobs 内部；UI 有自己的同名类型，删掉后不再有歧义。

# SPEC
1. [LEMMA: 1] `golden/attachments.txt` 由旧 AttachmentStore.fs、Stderr.fs、DataPaths.fs 跑同一 Scenario 生成（`tests/GoldenTests.fs` 的 `attachment store and stderr behave byte for byte like the old implementation`），覆盖上传、乱序、嗅探、元数据、下载、各种错误、断线清理，以及 stderr 行格式，通过。
