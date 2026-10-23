# 2026-10-23 核心文档静态一致性核验与交接报告（静态 / 未运行）

- **报告性质**：静态事实核对、契约修正与交接备忘（未执行构建、测试或外部命令）
- **涉及文档**：`AGENTS.md`、`docs/frontend-dev-process.md`、`docs/W7-CLEANBREAK-AUDIT.md`、`展望.md`
- **工作区基线**：HEAD `14e4625`（与 `origin/main` 一致，status/diff 干净）

---

## 一、已核对并修正的核心历史陈述

1. **PWA 中文豆腐块（CJK 根字体修复）**：
   - **推翻旧失效假说**：先前将中文方框推测为「本机 headless Chrome 走软件 WebGL / SwiftShader 限制」的说法已被证伪并纠正。
   - **真实根因与修复**：真实根因为 Avalonia PWA 根可视树此前未显式设置 `TextElement.FontFamilyProperty`，浏览器缺乏默认中文字体导致整片方框。已在 `app/Pwa/Program.fs` 根控件上显式注入内嵌字体 `SharedTheme.manager.FontFamily.Value`，整树继承。
   - **佐证事实**：截图中文逐字清晰可读，PWA 冒烟非纯色色彩数由 12 提升至 18（字形真实绘制），`compose/EntryShells/tests/Tests.fs` 中新增的 `EmbeddedFontTests`（3 条）已通过。
   - **文档回填**：已在 `AGENTS.md` SPEC 7 证据栏、`docs/W7-CLEANBREAK-AUDIT.md` §9 与 `展望.md` §1/§7 中完整对齐，消除各处自相矛盾。

2. **组件画廊条目数与夹具收口状态**：
   - **条目数核对**：静态核查 `compose/Gallery/Gallery.fs` 与 `compose/Gallery/AGENTS.md`，确认条目数确为 **24 条**（ComposerBox 4 条、MessageCardView 5 条、SidebarDesk 4 条、SettingsScreen 6 条、OverlayDesk 4 条、ChatEmpty 1 条），与 `tools/gallery.sh --list` 保持一致。
   - **夹具收口确认**：旧文档中记录的「夹具仍在收口」已更新为「**已收口**」——输入区附件补齐状态树处理分支与 `Attach`/`Send` 闭环（11 条画廊单测全过）；侧栏多选走真状态树消息 `SidebarUserMsg.EnterBatch` / `ToggleBatch`；错误卡收归 `ChatVS.Error` 投影并走卡片组真实控件入口。
   - **门禁定性**：明确画廊是人工查看状态的确定性工具（固定时间戳 `Scenes.fixedNow`），**不是**像素级回归门禁（G3 像素 diff 仍未建）。

3. **历史用户旅程测试缺口明确化**：
   - 核验 `compose/AppShell/tests/ComposerTests.fs` 现有旅程断言，并在 `docs/frontend-dev-process.md`（§1.2、§4.4、§5.3）与 `AGENTS.md` 中明确记录客观盲区：
     - **旅程 1**：仅到认证态，未走完配对后的首条消息。
     - **旅程 2**：引导流程配置模型此前仅有负向断言，现已增强。
     - **旅程 3**：附件上线在旅程内未完整闭环（依赖独立用例断言主机 ACK）。
     - **旅程 5**：仅断言错误状态落位与取消派发，未走真实重试路径。
     - **旅程 6**：仅断言撤销与批次清空，未断言到期真删。
   - SPEC 7 要求的原生交互证明（焦点流转、无障碍角色原生断言）在整树层面仍缺，不把局部断言夸大为全量覆盖。

4. **工程职权边界澄清（SWE = DevOps 文案收束）**：
   - 对 `docs/frontend-dev-process.md` §5 与 `AGENTS.md` 中的「所有权原则：SWE = DevOps」文案进行限定说明：
   - 所有权原则强调交付界面的人对其端到端交付闭环与验证证据全权负责，不把验证推给下游；但**绝不代表** Engineer 角色可越权执行 bash 命令或自行跑构建冒烟。
   - Engineer 负责源码与测试实现及文档契约，真实编译、构建、测试与冒烟执行全归 DevOps。

5. **工作树提交状态修正**：
   - 依据 DevOps 初始盘点事实，修正 `展望.md` §8.1 中过期的「未提交」陈述，确认 2026-10-21 当轮改动已以提交 `14e4625` 全量入库，当前工作树完全干净。


6. **工具链与 G6 格式门禁的收敛与 DevOps 真实运行留证（2026-10-23）**：
   - **语法与忽略规则真实通过**：DevOps 真实执行 `bash -n x publish.sh tools/check-format.sh` 及 Python `py_compile` 对所有脚本语法检查全部通过；`git check-ignore` 对四条 Python 缓存路径测试通过。
   - **格式门禁脚本修复与验证**：`tools/check-format.sh` 首次执行出现 13 处误报，根因为正则 `([^s])` 笔误，DevOps 现场修复为 `(\S)`；修复后全仓扫描 924 个受管文件全部通过（退出码 0）；对 F1-F5 五个注入负例逐一测试，均准确拦截并以退出码 1 退出。
   - **严格守住留证律令**：依据「留证后再写通过，报告转述不替代证据文件」之要求，格式门禁在 DevOps 真实执行并留存反例凭据后方行认定通过。
   - **发布与审计工具定位**：`publish.sh` 清理 `app/*/bin` 与 `app/*/obj`；`audit_inventory.py` 经核准保留作为架构与包合并按需审计工具，非 CI 阻断门禁。
   - **架构与边界裁决对接**：
     - 包异质性：`ChatEmpty` 与 `BatchBar` 维持现状不拆分，完全符合当前单一消费面实际与反碎片化原则（详见 `structure-report.md`）。
     - 跨层依赖：`SearchHit` 跨层依赖属于结构优化点而非当前阻断项，不扩张迁移。
     - 对话框打开态与快捷键穿透修复：`compose/AppShell/AppShell.fs` 中的 `ShellCommands.IsDialogOpen` 与 `IsPopupOpen` 已修复为直接委托查询真实控件层 `views().Overlay.Layer.IsDialogOpen` 与 `views().Overlay.Layer.HasActivePopup()`，根除了因状态树 `model.Overlay` 无生产者恒为 false 导致弹窗/菜单打开时全局快捷键意外穿透的缺陷（待 DevOps 运行验证）。

---

## 二、运行验证与客观环境限制（待验证事项）

1. **真实设备与无障碍限制（客观事实）**：
   - 宿主机未安装 `orca`、`speech-dispatcher`、`at-spi-bus-launcher`，缺乏原生真实屏幕阅读器端到端实测环境。
   - `adb devices` 输出为空，无物理手机或模拟器连接。
   - 严禁伪造读屏与手机端通过记录，相关条款在文档中严格保持 `[待验证]`。

2. **本轮运行结果统一待验证与收口约定**：
   - 本轮全部构建、测试（逐库测试、组合者测试、断言卫生、界面文案、G6 空白门禁）、桌面冒烟及 PWA 端到端交互冒烟结果统一标注为 **`[待验证]`**。
   - **PWA 视觉证据边界**：新增 16 项端到端断言纯属源码待运行；截图存在或画布色彩/像素差异**绝不证明**中文逐字可读，必须由 DevOps 真实跑测并对截图进行人工目检。
   - **收口归属确定**：不向未确定的未来人员转交，由本会话持续推进，先保持待验证，等待接收 DevOps 的最终实跑结果与正式留证后，由本会话按最终证据直接收口。
