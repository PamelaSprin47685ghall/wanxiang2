# LocalDraftControls

设置页「本机草稿」区与外壳之间的桥（迁自旧 `Model/HostHealth.fs` 末尾）。旧的可设置回调 `ClearRequested` 是端口式注入，改为事件 `ClearRequested` 加方法 `RequestClear()`：设置页调用 `RequestClear()`，宿主把 `ClearRequested <- f` 改为 `ClearRequested.Add(fun () -> f ())`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **只在概况真的变化时通知。** [根 AXIOM: 2]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- 无旧测试。
