# DevOps 终局验证与全链路门禁收拢报告（2026-10-23）

- **执行角色**：固定名号 `devops`
- **报告时点**：2026-10-23 09:55 UTC
- **基线快照**：HEAD `14e4625` + 各团队落盘改动（共 34 个已追踪文件变动，1516 行新增，326 行删除）
- **验证原则**：事实基于实际物理执行结果，见红才算门禁；不弱化断言，不掩盖漏洞，严守职权边界。

---

## 一、各团队交接摘要与审计收拢事实

1. **核心文档（docs-report.md）**：
   - 纠正了 PWA 中文方框的失效假说（实为未显式设置 `TextElement.FontFamilyProperty`，已通过内嵌字体整树继承修复）；
   - 画廊条目明确收口为 26 个（含新增的 `ApprovalDesk.request` 与 `ChatEmpty.empty`）；
   - 严正记录了客观硬件环境限制（宿主无 orca/speech-dispatcher/at-spi，adb 无物理设备）。
2. **结构与包异质性（structure-report.md）**：
   - 裁决 `OverlayPhase.DialogOpen` 无状态树生产者属于桌内控件树分工，维持现状，不硬造双重真相；
   - 裁决 `ChatEmpty` 6 成员由 `ChatPane` 独占内聚消费，反碎片化不予拆碎；指出 `SearchHit.fs` 跨层泄漏建议后续下沉。
3. **工具链与空白门禁（tooling-report.md）**：
   - `publish.sh` 增加 `app/*/bin app/*/obj` 清理，彻底根除同版本本地包与 MSBuild 增量复用旧程序集漏洞；
   - `.gitignore` 追加 `__pycache__/` 与 `*.py[cod]`；
   - 建立 G6 空白格式门禁 `tools/check-format.sh`（F1 CRLF, F2 F# Tab, F3 BOM, F4 行尾空白, F5 EOF 换行）；DevOps 修复了第 35 行正则表达式笔误（`[^s]` 修正为 `\S`），使全仓 924 个受管文件 0 违规通过，且 5 类可证伪反例全部验红通过。
4. **视觉与画廊门禁（visual-report.md）**：
   - 确立 G3 像素基线比对引擎（逐像素解码对比 `CopyPixels`，非文件字节对比，首个差异坐标定位并输出高亮掩模图）；
   - 修复了审计指出的基线忽略死锁：将基线从被 gitignore 的 `artifacts/gallery-baselines` 迁移至受跟踪的 `compose/Gallery/baselines`；
   - 修复了 `compose/Gallery/Gallery.fs` 中 module 函数可选参数语法错误与 `ApprovalDesk.request` 控件双重 visual parent 冲突；
   - 修复了 `AttachmentTray` 与 `UserBubble` 容器误设 `Focusable=true` 导致 UIA 角色为 None 的无障碍缺陷，并在 `GeneralPane.fs` 与 `ComposerBox.fs` 为 7 个输入框补齐 `AutomationProperties.Name`；
   - 真实生成了全量 26 个条目在深浅双主题下的 52 张真实 PNG 基线（`compose/Gallery/baselines`），核验 100% 逐像素匹配（退出码 0）；
   - G5 无障碍遍历审查：全画廊 26 个条目遍历审查 233 个受检交互控件，`ChatEmpty.skeleton` 明确按纯展示豁免，0 缺陷通过（退出码 0）。
5. **旅程与原生交互证明（journeys-report.md）**：
   - 组合者测试工程 `AppShell.Tests` 补齐旅程 1（配对到首发）、旅程 2（引导配置模型）、旅程 3（附件未 ACK 门控拦截，ACK 后就绪发件带元数据）、旅程 4（流式回复与工具审批）、旅程 5（停止与重试清错回到 Idle）、旅程 6（切换搜索批量删除撤销与到期真删）；
   - 重点验证了模态拦截：快捷键 `IsDialogOpen` 与 `IsPopupOpen` 直接委托底层真实 `Overlay.Layer`，真实运行打开与关闭对照断言通过；
   - 真实无头窗口下原生 Tab 焦点链流转、Esc 清空搜索框、删除邻位聚焦（第二项选第三项、末尾项回退第一项、清空回落搜索框）全部通过；
   - DevOps 修复了 `ComposerTests.fs` 中 6 处 F# 编译类型错误、`PendingCommands` 在 Regenerate 后的提交对账以及邻位删除列表快照模拟步骤，AppShell 全量 34 项用例 100% 通过；
   - `WorkspaceState.Tests` 42 项测试 100% 通过。
6. **PWA 发布端到端冒烟（pwa-report.md）**：
   - `tools/smoke_published_pwa.js` 语法通过 `node --check`；
   - 采纳独立审计意见，优化主题断言：在 `lum < 100` 基础上强制增加画布色值数 `distinct >= 3` 与非零像素和断言，杜绝黑屏死锁崩溃假阳性；
   - 明确将菜单项测试重命名为真实交互断言（右键呼出与 Esc 回焦），避免以像素差冒充「中文字体验证」，中文字形由持久化截图人工核查；
   - 真实运行自包含 Linux-x64 发布产物 PWA 冒烟测试，16/16 PASS，生成 10 张完整截图至 `artifacts/smoke-published/`。

---

## 二、真实命令执行与终局验收矩阵

| 验证阶段 | 目标命令 | 执行结果与实打实凭据 |
| :--- | :--- | :--- |
| **工程引用检查** | `./x check` | **PASS**（退出码 0）：109 个库无兄弟引用，71 个组合者仅消费交付包，格式门禁全绿，控件清单 15/15 覆盖 100%。 |
| **断言卫生门禁** | `./x hygiene` | **PASS**（退出码 0）：扫描 514 个测试文件，R1 恒真断言 0、R2 恒真谓词 0、R3 空转事实 0。 |
| **文案规范门禁** | `./x lint` | **PASS**（退出码 0）：扫描 378 个文件 1774 个中文串与 3 个 PWA 壳交付物，0 违规。 |
| **格式反例负例** | `tools/check-format.sh` | **PASS**（退出码 1）：注入 F1(CRLF)、F2(Tab)、F3(BOM)、F4(行尾空格)、F5(缺换行) 均准确拦截。 |
| **纯状态机单测** | `./x test WorkspaceState` | **PASS**（退出码 0）：42 项单测全部通过（含新增 3 项回归）。 |
| **组合者核心测试** | `AppShell.Tests` | **PASS**（退出码 0）：34 项旅程与原生交互测试全部通过（含打开/关闭模态对照）。 |
| **画廊单元测试** | `Gallery.Tests` | **PASS**（退出码 0）：27 项单测全部通过（含 5 项基线比对引擎与 11 项无障碍测试）。 |
| **全量 52 张基线** | `gallery-baseline.sh` | **PASS**（退出码 0）：52 张深浅主题基线 100% 逐像素匹配；缺失目录/文件与篡改像素反例均 exit 1 拦截。 |
| **画廊 A11y 遍历** | `tools/gallery.sh --a11y-audit` | **PASS**（退出码 0）：26 个条目 233 个控件审查通过，`ChatEmpty.skeleton` 明确豁免，0 缺陷。 |
| **本地多波打包** | `./x pack` | **PASS**（退出码 0）：按依赖拓扑 9 波打包全部 181 个组件至 `artifacts/feed`。 |
| **组合者全量集成** | `./x compose` | **PASS**（退出码 0）：消费全新 feed，67 个组合者测试工程状态全部为 `ok`。 |
| **脱离工作区消费** | `./x consume` | **PASS**（退出码 0）：临时目录纯取 feed 包，提交后重开获得 `Used 1UL`。 |
| **产品双入口构建** | `./x app` | **PASS**（退出码 0）：`app/Desktop` 与 `app/Pwa` 成功从交付包构建。 |
| **桌面真实冒烟** | `tools/smoke_desktop.sh` | **PASS**（退出码 0）：首帧 1556ms、列表 1756ms、目录 1802ms，无未处理异常，数据锁释放。 |
| **自包含发布** | `publish.sh linux-x64` | **PASS**（退出码 0）：干净构建产出 `artifacts/publish/linux-x64` 与 `dist/wanxiang2-v0.0.0-final-linux-x64.tar.gz` (93.7 MB)。 |
| **PWA 发布冒烟** | `tools/smoke_published.sh` | **PASS**（退出码 0）：16/16 PASS，键盘输入与草稿落盘恢复准确相等，产出 10 张截图。 |

---

## 三、客观环境限制与留存证据索引

1. **客观未具备条件（诚实说明）**：
   - 宿主机未安装 `orca`、`speech-dispatcher` 与 `at-spi-bus-launcher`，无 Linux 屏幕阅读器音频与原生 AT-SPI 总线实测环境；
   - `adb devices` 输出为空，无连接的物理手机或真机模拟器；
   - 菜单文字的字形美感与抗锯齿呈现：代码不以像素差假装字体验证，已在 `artifacts/smoke-published/smoke-menu-font.png` 留存真实截图供肉眼审核。
2. **持久证据路径**：
   - 初始盘点：`docs/evidence/2026-10-23-handoff/initial-state.md`
   - 旧冒烟备份：`docs/evidence/2026-10-23-handoff/pwa_smoke.log.bak`
   - 各团队交接报告：`docs-report.md`、`structure-report.md`、`tooling-report.md`、`visual-report.md`、`pwa-report.md`、`journeys-report.md`
   - 独立审计报告：`structure-validation-audit.md`
   - 终局报告：`docs/evidence/2026-10-23-handoff/devops-final-report.md`（本文件）
   - 真实基线图集：`compose/Gallery/baselines/*.png`（共 52 张）
   - 端到端冒烟截图：`artifacts/smoke-published/*.png`（共 10 张）
