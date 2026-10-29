# 源码依赖与本机状态

2026-10-15 用户决议：wanxiang 与 wanxiang2 按 wanxiangshu / wanxiangli 的职责边界管理，
不照搬其 npm 或 Fable 实现。本文件覆盖旧目录说明，历史交接保留为历史记录。

## 源码

`wanxiang2/` 是根目录的正式 Git submodule，`.gitmodules` 声明跟踪 `main`。升级时取远端
最新 `main`，只允许 fast-forward；不能丢弃脏工作区、分叉或未发布的本地提交。
普通构建、运行和 CI 消费宿主记录的确切 gitlink，不自动联网换版本。

操作入口为 `tools/wanxiang2-feed.sh --update`。它不构建、不暂存、不提交、不推送。
开发者确认更新后用 `git add wanxiang2` 记录 gitlink，再执行原有 feed 准备与应用验收。
任何被消费的 gitlink 都应先在新库远端可取；本机拥有对象不证明其他 clone 可以获取。

应用仍以 PackageReference 消费 `wanxiang2/artifacts/feed`，不增加反向依赖、跨仓源码引用
或第二份构建器。`vendor/wanxiang2` 已退役，不提供 fallback。包集、摘要和构建回执继续
作为干净消费证据，源码检查不能代替这些证据。

`.tgz`、`.nupkg`、`.snupkg` 任何大小都不得进入任一仓库的 Git 索引。宿主的
`--source-check`、包准备入口和 CI 检查两仓索引；忽略规则不能代替索引检查。
独立新库的 `./x check` 已检查 Git 索引：`.nupkg`、`.snupkg`、`.tgz` 任一大小进入索引即失败。忽略规则不能代替这项检查。

## 运行状态

默认根为主工作树 `.Herdchestra/`，其中保存配置、客户端状态、草稿和 `data/`。linked
worktree 共用主工作树的本机状态；独立仓库和 submodule 自己定位其主工作树。无 Git 的
目录使用当前工作目录，Git 拓扑无法确定而又存在仓库标记时拒绝猜测。

入口在读取 TOML 前检查配置、数据和诊断包路径；拒绝 `.git` 和指向其内部的符号链接。
`.Herdchestra` 不能是符号链接或文件，其 `.gitignore` 必须是排除全部状态的普通文件。
纯只读 doctor 不因这项改动而创建状态目录；实际写入仍属于各存储 owner。

显式 `--config`、`--data`、`--bundle`、`WANXIANG_HOME` 保留为选择独立本机位置的方式，
但不允许指定 Git 元数据。默认启动不再读取 XDG 历史目录；不会删除、迁移或扫描已有
用户配置、密钥、对话或附件。旧 `.git/wanxiang*` 即使存在也不作为恢复候选。

PWA 继续使用浏览器存储，不虚构浏览器中的项目磁盘目录。现有 C/S 网络访问继续成立；
这不是 Git 数据同步，也不是本次要删除的产品能力。

## 清理边界

一次性清理可删除已确认的应用专属旧状态目录、同步配置、hooks 和数据 refs。
不得删除源码分支、tags、objects、子模块 Git 元数据或其他工具的 hooks；不重写历史，
不进行强制 GC。旧包退出当前树不等于历史对象已经物理消失。

## 验证入口与范围

- **历史背景说明（2026-10-17 收束更新）**：旧宿主仓已于 2026-10-17 物理退役，子模块挂载解除，本仓库（`wanxiang2`）成为唯一产品本体入口（`app/Desktop` 与 `app/Pwa`）。上文关于旧仓 submodule 与 `tools/wanxiang2-feed.sh` 的条款保留作历史架构对照。
- **当前持续门禁证据**：
  - Git 索引二进制禁入：由 `./x check` 自动检查 `.nupkg`、`.snupkg`、`.tgz`，任一大小进入索引即失败。
  - 本机状态路径与 Git 元数据拒绝：由 `libs/LocalState/tests/Tests.fs`（主/linked worktree、独立工作区、只读解析、旧目录隔离、`.git` 路径及符号链接拒绝）与 `compose/UiPrefsFile` 持续覆盖，并随 `./x all` 实跑验证通过。

路径预检防止误配，不宣称抵抗另一个恶意进程在检查后替换目录的文件系统竞态。
