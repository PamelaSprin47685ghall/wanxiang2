# UrlSchemeRegistration

`wanxiang://` 链接注册的纯文本部分（决策 251）：桌面文件名、Linux 桌面文件内容 `desktopEntry`、路径加引号 `quote`，迁自旧 `Model/DesktopLinks.fs`。写桌面文件、写 Windows 注册表、求当前进程启动命令（`launchCommand`/`registerCurrentUser`）、`ExternalLinks` 收件箱与 `SingleInstance` 套接字都碰系统，留给桌面宿主；同名模块合并，旧调用方不用改。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **磁盘字节不变。** [根 AXIOM: 1] 写出的桌面文件与旧程序集逐字节相同。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`251 Linux 桌面文件声明 x-scheme-handler 并以 %u 接收链接`。

# 迁入或覆盖的旧测试
- PartialPersistAndUrlSchemeTests：`251 Linux 桌面文件声明 x-scheme-handler 并以 %u 接收链接`
