# DirLock

目录独占锁对象。私有状态：一个独占打开的文件句柄。不依赖任何兄弟库。根规则见项目根目录 AGENTS.md。

公开面：`DirLock.TryAcquire(dir) : Result<DirLock, string>`、`Path`、`Dispose()`（释放）。

# LEMMA
1. **同一数据目录同一时刻只有一个写者。** [根 AXIOM: 1, 6]
    - 推理：两个进程同时追加同一日志，会交错写出互相矛盾的提交，事后分不清哪条该留。这是不可撤销的误操作，属于该防呆的范围。
2. **锁的生命期绑在操作系统句柄上，进程死亡即释放。** [根 AXIOM: 5, 6; LEMMA: 1]
    - 推理：「锁文件存在即上锁」的做法在崩溃后留下陈旧锁，用户得理解并手动删除它。句柄锁没有这个状态。
3. **「被占用」与「拿到了锁但写诊断信息失败」是两种结果。** [根 AXIOM: 5]
    - 推理：把后者报成前者是歪曲事实，用户会去找一个并不存在的占用者。

# SPEC
1. [LEMMA: 1] 同进程持锁期间再次 `TryAcquire` 返回 Error（带持有者 pid）；`Dispose` 后可再次获取。
    - 证明：`tests/Tests.fs` `second acquire fails until first is disposed`，通过。
2. [LEMMA: 1, 2] 其他进程持锁时获取失败；该进程被杀后无需清理即可获取。
    - 证明：`lock held by another process is released when it dies`（Linux/macOS 用 `flock` 子进程），Linux 通过。
3. [LEMMA: 3] 写 `.lock.pid` 失败时释放句柄，返回不同于「被占用」的错误。
    - 证明：`pid write failure is reported distinctly and releases the lock`，通过。
4. [LEMMA: 1] 真实组合中，第二个存储打不开同一目录。
    - 证明：`compose/LedgerStore/tests/Tests.fs` `second store on the same directory is refused`。
5. [TODO] Windows 上的跨进程行为（FileShare.None 是强制锁）没有测试。

6. [根 SPEC 3] 失败的 `TryAcquire` 不惊动持有者：诊断 pid 不变、锁仍被拒；不同目录的锁是独立实例，释放一个不影响另一个。
    - 证明：`failed acquires leave the holder intact; locks in different directories are independent`，通过。
    - 剩余边界：输入只有 `string`；`TryAcquire` 要么交出锁对象本身（所有权转移），要么给出错误消息，没有可共享的可变别名。
