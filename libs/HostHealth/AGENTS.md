# HostHealth

主机可见状态：doctor 检查项解析与用户词、HostHealth 对象（配置有误、检查结果），迁自旧 `Model/HostHealth.fs`。HostHealth 自己拥有状态并发 Changed；旧的可设置回调 `RequestCheck` 是端口式注入，改为事件 `CheckRequested`（与 Changed 同一机制，对象只宣告「要检查」，谁去发请求由订阅的宿主决定）；宿主把 `health.RequestCheck <- f` 改为 `health.CheckRequested.Add(fun () -> f ())`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **断线时进行中的检查转成说明，不留在「检查中」。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- 无纯逻辑旧测试；ComposerReliabilityTests 里的 DoctorCheck 用例要 Avalonia，留在旧仓库。
