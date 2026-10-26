# 收束后整理与包合并账目（2026-10-18 / 2026-10-19）

> 历史账目，只读。由 `_审视` 的 ISSUE-0001 修复从根 `AGENTS.md` 迁入；根文档只留指针。
> 原文出自 `30eb138:AGENTS.md` 第 243–264 行，未改动内容。

## 收束后整理（2026-10-18，抽取路线收束之后的第一次全仓整理）

1. **孤儿库退役（8 个）。** 全仓扫出 8 个零消费者的库：`CopyConfirm`、`RovingIndex`、`SidebarRowState`、`StartLayout`、`SendKey`、`UiPrimitives`、`ProbeCounter`、`BrowserProbe`。前五个是早期迁出的纯逻辑库，其规则在控件库重写时被内联进真正的行为拥有者（`CopyButton`、`PopupMenu`/`SettingsFrame`、`ConversationRows`、`ShellLayout`、`ComposerBox`），原库成了同一事实的第二份；后三个分别是被控件库取代的历史原语库与从未接线的探针链（`?wxprobe=1` / `WANXIANG_PARITY_PROBE`，见 `docs/ux/r28-pwa-parity.md`）。处置：删除，不建兼容层；被删库的旧用例其等价断言在各行为拥有者的 headless 测试里（`SendKey.sends` 的 8 种组合并入 `ComposerBox` 的 Enter 矩阵；`StartLayout` 两条规则在 `ShellLayout` 测试；`SidebarRowState` 三条规则在 `ConversationRows` 测试）。
2. **`ComposerBox` 的发送判定修正。** 内联判定原先漏了 `not shift`：Enter 发送档里 Ctrl+Shift+Enter 会误发送；已改为与旧 `SendKey.sends` 相同的 `not shift && (enterSends <> ctrl)`，并由测试钉住两种模式的全部组合。
3. **UiKit 建立与退役。** 早期合并 36 份复制件为临时基库，后随全仓原生化改造完成全面退役清零。
4. **工具与 CI。** `./x check` 增加两条：每个库必须有生产消费者（`UNWIRED.md` 是登记例外，模板 `tools/widget-template` 自带该文件），文档点名的 `libs/<名>` / `compose/<名>` 必须存在；`./x pack` 改为库与组合者统一按依赖顺序打包（`UiKit` 自然排在被依赖方前）；`./x all` 的逐库测试与组合者测试并行（`XJOBS`）；CI 的库测试矩阵与 widget-template 任务改为消费 pack 出的 feed 工件，新增 `published-smoke` 任务（自包含发布产物 + headless Chrome 冒烟，`release-publish.yml` 的 linux-x64 上传前也跑）；仓库根新增 `package.json`（只给 `tools/*.js` 用，`npm ci` 装 puppeteer-core），`.gitignore` 忽略 `node_modules/`。
5. **例外清单同步。** `VersionOverride="0.0.0-local"` 一行补上新的使用方（库对 `UiKit` 的包引用）。
6. **`ControlStroke`：输入类控件的边界描边过 WCAG 1.4.11。** `docs/ux/a11y-audit.md` §2 一直记着「以描边作唯一边界的输入框」不达标（`border` 浅 1.33~1.47、深 1.46~1.92）。实测确认边界只靠描边（`FieldSurface` 与 `Surface` 同值，没有填充差），于是 `libs/ThemePalette` 补 `ControlStroke`（浅 `#8D8A7E`、深 `#76767F`；对 Surface / SurfaceRaised / SurfaceContainer / Canvas 两套主题都 ≥ 3:1），22 处 `FieldEdge` 与 `ComposerBox` 的 `BoxInk.ControlStroke` 改用它，聚焦态仍是 `Accent`。机械断言 `compose/Theme/tests/A11yRegression.fs` 的 `控件边界描边在四种底上都过 1.4.11`；`ComposerDesk` / `Workspace` 整树金样里 7 行壳描边按新值改（有意偏离，已在这两个组合者的 AGENTS.md 记明）。`border` / `line` 数值不变，仍作装饰性分割。

7. **看过的、决定不动的：测试工程的 `Headless.fs` / `Probe.fs` / `Dump.fs` 副本**（libs 与 compose 的 tests 目录共约 74 / 18 / 7 份，逐字节相同的部分约 4–5 千行）保留按库复制，不进基础库。理由：它们是测试脚手架，各库测试自足（SPEC 2「只凭本库与声明依赖」）；副本漂移只影响本库且会立刻在本地测试里响，没有 `UiKit` 那种「36 份行为必须一致」的正确性压力；合并需要再开一类「测试支撑包」依赖并为此在 `./x check` 再开一个口子，代价大于收益（Axiom 2、LEMMA 8）。将来若真出现跨库同步的测试基建需求，再按 LEMMA 1 重新判断。

8. **散落 TODO 的收口（同批）。** 全仓扫 `TODO/待办/未接线` 后，能做的当场做掉：`SingleInstance` 的套接字转发补上真实 Unix 套接字测试（转发链接、空行唤起、Dispose 后不再接、无监听者/陈旧文件两类拒绝）、`Canonical` 的大整数改写约束锁进测试、`Gemini` 跨提供方 `functionResponse.name` 的旧缺陷修掉（金样 1 行有意偏离）、`AppConfig` 的 TLS 路径存在性检查复判后保留并写明理由、`PairingDesk`「持久化失败不发令牌」改为指向现有证据（`DevicesTests` 的只读目录真实故障 + `DesktopPairing` 的无效配置用例）。做不了或要产品决策的写清边界：Windows 上的 `DirLock`/`AtomicTextFile` 行为、断电级别的目录 fsync、超大日志的流式 `ReadFrom`、`Ledger` 幂等索引与 `Checkpoint` 的保留策略、`ServerApp` 的附件 GC。

9. **发布产物冒烟扩到认证态：草稿往返有运行时实测了。** `tools/smoke_published.sh` 现在给测试主机预置一条客户端凭据（哈希进配置），`tools/smoke_published_pwa.js` 第二段把浏览器接进已认证状态，真跑「输入 → 800 ms 自动保存进 IndexedDB → 重开页面恢复」。2026-10-18 本机 33 秒全绿：画布、DPR 2、上屏桥、草稿落盘、草稿恢复、无页面异常。CI 的 `published-smoke` 与发布流程的 linux-x64 都跑这一套（见 `docs/ux/r28-pwa-parity.md` 的证据基础）。

**证据（2026-10-18，本机，冻结树后一次跑完）：** `XJOBS=10 ./x all` —— `check ok: 247 libs with no sibling references; 72 composer(s) using packages only`；逐库测试 247 个工程全部通过；`./x compose` 67 个工程全部通过；`./x consume` 通过（仓库外消费者提交后重开得到 `Used 1UL`）。`./x app` 两个入口构建成功（Debug）。`WX_SKIP_PACK=1 ./publish.sh linux-x64 v0.0.0-final`（自包含 + PWA 包 + avalonia.js 补丁）后 `bash tools/smoke_published.sh artifacts/publish/linux-x64` 9 项全绿：画布、DPR 2、非纯色、上屏桥、预置凭据、localStorage 访问计数递增、草稿落进 IndexedDB、重开恢复草稿、无页面异常。新检查的负例也跑过：`./x new-widget CiSmoke` 生成后 `./x check` 通过（模板自带 `UNWIRED.md`），删掉该文件即失败并给出指引，库引用兄弟包（`Wanxiang2.BatchBar`）被拒；`./x test BatchBar` 在 feed 里没有 UiKit 包时会先自动 pack；`TomlCodec` 拆节用「18 条错」新旧对照逐条核过；`SingleInstance`/`Canonical`/`Gemini` 各自的新用例随逐库测试通过（见各自 AGENTS.md）。本轮删掉的 8 个库、`UiKit` 的 37 个受影响库、`Con…

## 包合并（2026-10-19，41 组合并为一个包）

10. **162 个库按消费者集合合并为 41 个包。** 判据是机械的：生产消费者集合完全相同、外部依赖相同、Wanxiang2 依赖相同，三个都满足才合并；消费者集合相同意味着它要么全要、要么全不要，合并后任何消费者都不会多拿到它本来不用的 API。`libs` 242 → **123** 个包，组件工程 316 → 196（123 个库 + 73 个组合者）。合并组清单、测试合并约定与两处既有缺陷的修复见 `MERGE.md`。被合并掉的库的 `AGENTS.md` 按成员分节并入新包（前提与测试名都还在），文档与各 `AGENTS.md` 里的库名引用同步改成新包名。

**证据（2026-10-19，本机）：** `./x all` —— `check ok: 123 libs with no sibling references; 72 composer(s) using packages only`；逐库测试 123 个工程全部通过；pack 分 9 波（`111 / 44 / 18 / 14 / 2 / 2 / 2 / 2 / 1`）；`./x compose` 67 个工程全部通过；`./x consume` 通过（仓库外消费者提交后重开得到 `Used 1UL`）。`./x app` 两个入口从交付包构建成功；PWA 侧 `getAssemblyExports` 只引用 `BrowserInterop.dll`（不在任何合并组里），程序集名未变，`app/Pwa/wwwroot/main.js` 无需改。
