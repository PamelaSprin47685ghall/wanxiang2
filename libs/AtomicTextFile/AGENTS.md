# AtomicTextFile

一个由人和程序共同编辑的文本文件对象。私有状态是路径、最后一次本对象读到或写出的原文、监视器和去抖计时器。不理解文本内容，不依赖任何兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`new AtomicTextFile(path, ?debounce)`：`Read() : Result<string,string>`、`Write(text) : Result<unit,string>`、`ExternalEdit() : string option`、`Watch()`、`Changed` 事件（去抖后的「可能变了」）、`Exists`、`RestrictToOwner()`、`Path`、`Dispose()`。

# LEMMA
1. **读者只会看到完整的旧文本或完整的新文本。** [根 AXIOM: 1]
    - 推理：配置写到一半，下次启动就会读到坏文件。临时文件加 flush 加同目录改名，把半写排除在可见状态之外。
2. **文件只给当前用户读写。** [根 AXIOM: 1]
    - 推理：配置里有 API key 和令牌哈希。
3. **「外部编辑」是本对象自己就能断言的事实：磁盘原文不等于最后已知原文。** [根 LEMMA: 3, 4]
    - 推理：监视器的通知会晚到，也分不清是不是自己写的。最后已知原文归本对象，所以由它回答；采不采纳由组合者决定。
4. **`Changed` 只说「可能变了」，去抖后发出一次。** [根 AXIOM: 2; 根 LEMMA: 4]
    - 推理：编辑器保存一次会触发多个文件系统事件。本对象不读、不判断，只通知；接下来做什么由组合者决定。

# SPEC
1. [LEMMA: 1, 2] 写后读等于原文，不留临时文件，权限是 0600：`tests/Tests.fs` `write then read round-trips, leaves no temp file, owner-only`，通过。
2. [LEMMA: 3] 外部编辑一直报告，直到被读：`external edit is reported until read`，通过。
3. [LEMMA: 4] 连续 5 次外部写，去抖后只发出一次 `Changed`：`external changes raise Changed once after debounce`，通过。
4. [LEMMA: 1] 写失败（真实故障：目标被占成非空目录）时原文件不变、临时文件被删：`failed write leaves the original intact`，通过。
5. [LEMMA: 3] 文件不存在时读返回 Error，`ExternalEdit` 为 None：`missing file reads as error`，通过。
6. [LEMMA: 1, 2] 改名后对所在目录执行 POSIX `fsync`（`AtomicTextFileInterop.syncDir` 按平台与架构取 `O_DIRECTORY`，在真实目录上返回 `true`、缺失或普通文件路径返回 `false` 且不抛异常）：`repeated writes keep the latest content on disk` 在 macOS ARM64 与 Linux x64 容器验证通过。剩余边界：物理断电与 Windows 行为未实测；Windows 上不设 POSIX `0600` 权限。

- `WriteIfUnchanged`：临时文件落盘后、改名前检查磁盘原文仍是最后已知原文，否则放弃，不覆盖别人的改动：`write-if-unchanged refuses to clobber...`，通过。
