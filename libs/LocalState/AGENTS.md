# LocalState

宿主的位置边界迁入 wanxiang2：库只接收显式路径，不发现或同步 Git 中的运行数据。IO 保持真实——这是 IO 壳库的职责。命名空间沿用 `Wanxiang.UI`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **运行数据绝不落进 Git 元数据。** 路径含 `.git` 段（大小写不敏感）直接拒绝；已存在的符号链接解析后也不能指回 Git 元数据。
2. **库只接收显式路径，不自己发现工作区。** `rootFor`/`initialize`/`configHome` 都从调用方给的路径出发；浏览器分支只保留路径文本，不执行 Git 或磁盘操作。
3. **只写自身。** `initialize` 只创建 `.Herdchestra` 并写内容为 `*` 的 `.gitignore`，不改 `.git/info/exclude`、config 或 hooks。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `空路径被拒绝`、`含 .git 段的路径被拒绝（大小写不敏感）`、`普通路径返回规范化绝对路径` 覆盖 `requireOutsideGit` 判定：空路径、`.git` 段、符号链接指回 `.git` 内部、符号链接解析为真实路径。临时目录取 `LocalApplicationData` 下的子目录，不取 `Path.GetTempPath()`：macOS 的 `/var` 是指向 `/private/var` 的符号链接，解析后的期望值会与未解析的输入不同（Linux 与 macOS 通过）。
2. [LEMMA: 2, 3] `rootFor`/`initialize`/`configHome` 的 Git 拓扑查询与磁盘写入属 IO 部分（路径边界由 `普通路径返回规范化绝对路径`、`符号链接指回 .git 内部被拒绝` 校验），留宿主与 Long Stroke 验证。

# 迁入或覆盖的旧测试
无。本库为 wanxiang2 新建，逻辑迁自旧 `src/Wanxiang.UI/Model/LocalState.fs`。
