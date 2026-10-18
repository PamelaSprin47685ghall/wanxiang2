# ExternalLinks（组合）

进程内唯一的 `wanxiang://` 链接收件箱（决策 251），迁自旧 `src/Wanxiang.UI/Model/DesktopLinks.fs` 的 `ExternalLinks` 模块（旧仓库 bfdda85 删除；同一旧文件的另两个模块落在 `libs/UrlSchemeRegistration` 与 `libs/SingleInstance`）。排队与交付规则在 `Wanxiang2.LinkInbox` 库（旧 `Inbox<'item>` 带锁收件箱），本组合者经交付包引用它，只留与旧代码相同的公开面：`inbox`、`post`、`subscribe`。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

消费点：`app/Desktop`（启动参数 `cli.openUrl`、第二实例转发、macOS `OpenUri` 激活都投到 `Wanxiang.UI.ExternalLinks.post`）。

# LEMMA
1. **进程内单例仍归宿主。** [根 AXIOM: 3]
    - 推理：旧实现是模块级单例；LinkInbox 库是对象，宿主自己持有一个实例。这里保留旧的三函数公开面，旧调用点不用改。
2. **投递不丢、不重、按序；空白链接不收。** [LinkInbox LEMMA: 1, 2]

# SPEC
1. [LEMMA: 2] 旧用例 `251 外部链接投给当前订阅者` 迁入 `libs/LinkInbox/tests/Tests.fs`（同名），通过；`./x test LinkInbox` 可重跑。
2. 本组合者无独立测试工程：`./x compose` 跳过它（`x` 的 `compose()` 注释把无 tests/ 的组合者逐一点名，与 `gen_sln` 口径一致：不要求每个组合者都有测试工程）。

# 迁入或覆盖的旧测试
- 旧 `PartialPersistAndUrlSchemeTests`：`251 外部链接投给当前订阅者` → `libs/LinkInbox/tests/Tests.fs`（以 `Inbox<string>()` 对象形式重写，断言相同）；库内另有排队 / 空白过滤、并发投递两个新用例。
- 旧 `251 第二实例把链接转发给主实例，主实例不在时转发失败` 不属本组合者（同一旧文件的 `SingleInstance` 模块），其新落点 `libs/SingleInstance` 的 AGENTS.md 记录了该用例为何没有随库迁（要真实 Unix 套接字与监听进程）。

# 拆分与复核
- 旧 `Model/DesktopLinks.fs` 一拆为三：`ExternalLinks` 归本组合者；`UrlSchemeRegistration` 的纯文本归 `libs/UrlSchemeRegistration`、IO 归 `compose/UrlSchemeRegistrationIo`；`SingleInstance` 归 `libs/SingleInstance`。拆分线是「纯规则」与「碰 IO / 认系统」。
- 旧用例 `251 外部链接投给当前订阅者` 随拆分落到 `libs/LinkInbox`；库内两个新用例补上旧实现没有的排队与并发交付断言。
