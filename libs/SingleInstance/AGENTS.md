# SingleInstance

决策 251：单实例转发。主实例在配置目录下监听一个本机 Unix 套接字（Windows 10+ 同样支持）；第二次启动（系统打开链接时就是这样）先连它：连上就把链接（或空行 = 只是唤起）交过去后退出，不再去抢数据锁；连不上才当主实例。命名空间沿用 `Wanxiang.UI`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **转发成功即退出。** `tryForward` 返回 `true` 表示调用方应退出；返回 `false` 才当主实例。
2. **套接字路径固定。** `socketPath configDir = Path.Combine(configDir, "ui.sock")`。

# SPEC
1. [LEMMA: 2] `tests/Tests.fs` 覆盖 `socketPath` 的路径拼接。
2. [LEMMA: 1] `tryForward`/`listen` 需要真实 Unix 套接字与运行中的监听进程，属 IO 部分，测试留 TODO，待 Long Stroke 或宿主集成测试覆盖。

# 迁入或覆盖的旧测试
无。旧仓库宿主保留单实例套接字（见根 AGENTS.md「UI 非视图余项」）；本库为 wanxiang2 新建，逻辑迁自旧宿主保留部分。
