# DirLock

目录独占锁。不依赖任何其他库。AXIOM 见项目根目录 AGENTS.md。

# LEMMA
1. [AXIOM: 1, 10] 同一数据目录同一时刻只有一个写者。
    - 推理：两个进程同时追加同一日志，会交错写出互相矛盾的提交，事后无法分辨哪条该留，属于不可撤销的手滑，必须防呆（AXIOM 10）。
2. [AXIOM: 9, 10; LEMMA: 1] 锁的生命期绑定在操作系统句柄上，进程死亡即释放。
    - 推理：用「存在锁文件 = 已上锁」的做法，崩溃后会留下陈旧锁，用户得自己理解并删掉它（违反 AXIOM 9）。句柄锁没有这个状态。

# SPEC
1. [LEMMA: 1] 同进程内，持锁期间再次 `TryAcquire` 返回 Error（消息带持有者 pid）；`Dispose` 后可再次获取。
    - 证明：tests/Tests.fs `second acquire fails until first is disposed`。
2. [LEMMA: 1, 2] 其他进程持锁时获取失败；该进程被杀后无需清理即可获取。
    - 证明：`lock held by another process is released when it dies`（Linux/macOS 上用 `flock` 子进程）。
3. [LEMMA: 1, 2] 已打开锁文件但写 `.lock.pid` 失败时，释放句柄并返回不同于「被占用」的错误。
    - 证明：`pid write failure is reported distinctly and releases the lock`。
4. [TODO] Windows 上的跨进程行为（FileShare.None 是强制锁）尚无测试。
