# LinkInbox

带锁的收件箱 `Inbox<'item>`，与 `wanxiang://` 链接的收件规则（迁自旧 `Model/DesktopLinks.fs` 的 `ExternalLinks`）。旧实现是模块级单例；这里是对象，宿主自己持有一个实例。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **不丢、不重、按序。** 订阅前投递的条目排队，订阅时一次按序交付；之后直接交付。交付在锁外进行，订阅者可以再投递而不死锁。
2. **空白链接不收**（旧 `post` 的判断）。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 的 `251 外部链接投给当前订阅者`、`订阅前的链接排队，订阅时按序一次交付；空白链接不收`、`多线程投递不丢条目`  通过，含 200 条并发投递。

# 迁入或覆盖的旧测试
- PartialPersistAndUrlSchemeTests：`251 外部链接投给当前订阅者`
