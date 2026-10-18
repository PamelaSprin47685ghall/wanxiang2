# SingleInstance

决策 251：单实例转发。主实例在配置目录下监听一个本机 Unix 套接字（Windows 10+ 同样支持）；第二次启动（系统打开链接时就是这样）先连它：连上就把链接（或空行 = 只是唤起）交过去后退出，不再去抢数据锁；连不上才当主实例。命名空间沿用 `Wanxiang.UI`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **转发成功即退出。** `tryForward` 返回 `true` 表示调用方应退出；返回 `false` 才当主实例。
2. **套接字路径固定。** `socketPath configDir = Path.Combine(configDir, "ui.sock")`。

# SPEC
1. [LEMMA: 2] `tests/Tests.fs` 覆盖 `socketPath` 的路径拼接。
2. [LEMMA: 1] `tests/Tests.fs` 用真实 Unix 套接字与真实监听循环覆盖：`listen 收到转发的链接并回 ok；空行只当唤起；Dispose 后不再有人接`（转发链接逐字、空行只唤起不产生链接、换行被剥掉、Dispose 清掉套接字文件后 tryForward 一律 false）、`没有监听者时 tryForward 一律 false`（文件不存在与陈旧残留两种）。

# 迁入或覆盖的旧测试
无。旧仓库宿主保留单实例套接字（见根 AGENTS.md「UI 非视图余项」）；本库为 wanxiang2 新建，逻辑迁自旧宿主保留部分。
