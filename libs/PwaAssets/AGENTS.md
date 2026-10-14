# PwaAssets

PWA 静态资源目录的查找顺序（从旧 Program.fs 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`PwaAssets.locate appBaseDir`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **查找顺序与旧实现相同：发布包 pwa → Debug AppBundle → Release AppBundle → wwwroot。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
- 旧仓库没有覆盖 Wanxiang.App 的测试（测试工程不引用 App）。本库测试是新写的行为测试：
  - `什么都没有时为 None`
  - `发布包内的 pwa 目录最优先`
  - `开发模式：Debug 产物先于 Release，先于 wwwroot`
