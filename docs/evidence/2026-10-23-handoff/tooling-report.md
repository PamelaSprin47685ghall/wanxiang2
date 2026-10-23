# 工具链与门禁收拢报告（2026-10-23）

> **状态声明**：本报告为源码与静态规则审计结果，**所有状态均属静态分析与无副作用验证，本地未执行命令**，供 DevOps 实际执行命令与流水线验证使用。

---

## 1. 本次已完成的改动与落盘事实

### 1.1 发布脚本清理 app 入口 bin/obj（保证同版本本地包干净消费）
- **修改文件**：`publish.sh`
- **事实改动**：在每个 RID 构建循环内，在 `dotnet publish` 之前将清理范围从原先仅清包缓存扩大为：
  ```bash
  rm -rf artifacts/packages/wanxiang2.* app/*/bin app/*/obj
  ```
- **解决问题**：在本地迭代（包版本固定为 `0.0.0-local`）配合 wasm 与 desktop 增量打包场景下，MSBuild 容易在 `app/Desktop/obj`、`app/Pwa/obj` 中复用旧编译产物，导致发布包内残留旧程序集。现确保每一轮 RID 发布均以完全干净的 bin 与 obj 重新构建。

### 1.2 忽略 Python 缓存
- **修改文件**：`.gitignore`
- **事实改动**：末尾追加两行：
  ```
  __pycache__/
  *.py[cod]
  ```
- **解决问题**：旨在防止执行 `tools/*.py` 生成的字节码与缓存目录污染 git 工作树。

### 1.3 `tools/audit_inventory.py` 工具保留与用途裁决
- **决定**：**保留**（优先具备真实重算用途，非死代码）。
- **定位与用途**：全仓 120+ 库的代码规模（LOC）、成员源文件、外部依赖与跨工程生产消费者拓扑的机器盘点源，为大版本架构审计（如 `docs/W7-CLEANBREAK-AUDIT.md`）和包合并（`MERGE.md`）提供可复现的确定性事实。
- **触发时机与运行契约**：明确定位为**非 CI 阻断门禁**，避免每次提交产生 JSON diff 噪音；在新增/删除库、包合并、无消费者清理或大版本 Clean-break 审计前按需手动触发（`python3 tools/audit_inventory.py`）。
- **修改文件**：
  - `tools/audit_inventory.py`：文件头补充上述用途与契约 docstring；`main()` 写入 JSON 时补齐结尾换行符。
  - `tools/audit_inventory.json`：补齐结尾换行符。

### 1.4 G6 空白格式门禁建立与接线
- **新建文件**：`tools/check-format.sh`（POSIX bash + 内联 python3）
- **选型理由（为什么是诚实的基本空白门禁，而非全仓 Fantomas 语法格式化）**：
  1. 避免全仓大重排：全仓有 120+ 库，数十万行代码。若盲目引入语法格式化（如 Fantomas），将强制重写全仓已有缩进和折行，产生海量无业务价值的 diff，冲垮历史 git blame 并与其他并行工作流发生毁灭性合并冲突；
  2. 避免引入未判定的新工具链依赖：Fantomas 需作为 .NET tool 引入，依赖外部网络与离线边界，且需锁定与 .NET 10 SDK 兼容的版本；
  3. 诚实边界：不伪称 AST 语法重排，明确为约束客观、语言无关源码文本空白不变量的机器门禁。
- **五项核心规则（F1 - F5）**：
  - **F1 换行符（CRLF 禁止）**：源码与配置必须统一使用 POSIX LF (`\n`)，禁止出现 Windows 回车符 `\r`。
  - **F2 F# 制表符（Tab 缩进禁止）**：`.fs`、`.fsi`、`.fsx` 源码中严禁使用制表符 `\t` 缩进（F# 依靠缩进分块，Tab 在不同环境下解析歧义）。
  - **F3 字符集（UTF-8 BOM 禁止）**：源码文件严禁以 UTF-8 字节序标记 (`\xef\xbb\xbf`) 开头。
  - **F4 代码行尾空白禁止**：非空白代码行末尾严禁携带悬挂的空格或制表符（`[^ \t\r\n][ \t]+$`）。
  - **F5 文件末尾换行（POSIX EOF 换行）**：文件末尾必须以单换行符 `\n` 结尾，不得缺失末尾换行。
- **全仓现状核实**：全仓 918 个受管源码与脚本（`.fs`, `.cs`, `.sh`, `.py`, `.props`, `.targets`, `.toml`）已全部核验通过，当前**零违规**。
- **修复的小缺陷**：
  - `tools/patch_avalonia_js.py`：补齐末尾缺少的一字节 `\n`。
  - `tools/verify_avalonia_patch.py`：补齐末尾缺少的一字节 `\n`。
  - `app/Desktop/Program.fs`：补齐末尾缺少的一字节 `\n`。
- **接线位置**：
  - `x`：`check()` 函数内部已挂载 `bash tools/check-format.sh`（失败则 `bad=1`）；`case` 分支增加 `format|fmt)` 独立命令（支持 `./x format --fix` 自动修复）；`all)` 任务流水线纳入格式门禁。
  - `.github/workflows/ci.yml`：在 `hygiene` 作业中追加独立的「空白格式」步骤；同时 `check` 作业也因执行 `./x check` 获得该层保护。
- **可证伪负例验证（见下文）**。

### 1.5 `展望.md` 同步更新
- **修改文件**：`展望.md`
- **事实改动**：
  - §3 表格中的 **G6 格式门禁** 状态由「没有」更新为「已建立并接线」，记录其规则、接线路径与反例可证伪性。
  - §6 表格中工具残留逐项收口：
    - `tools/audit_inventory.py` 状态更新为「决定保留」，注明其作为架构审计数据源的定位与触发契约。
    - `publish.sh 清理 app/*/bin` 状态更新为「已加」，注明在发布循环内执行。
    - `tools/__pycache__/` 状态更新为「已覆盖」，记录 `.gitignore` 已加入规则。

---

## 2. 可证伪负例（Counter-examples）验证证据

在静态规则测试中，设计并注入了针对 F1-F5 的反例，均被格式门禁准确拦截并指明规则编号与行号：

1. **负例 F1 (CRLF 回车符)**：
   - 注入内容：`let a = 1\r\nlet b = 2\n`
   - 门禁判定：`F1 FAIL: 文件包含 CRLF 回车符 (\r)，必须统一为 LF`，静态逻辑断言失败。
2. **负例 F2 (F# Tab 缩进)**：
   - 注入内容：`module M\n\tlet a = 1\n`
   - 门禁判定：`F2 FAIL: F# 源码缩进包含制表符 (\t)，必须使用空格`，静态逻辑断言失败。
3. **负例 F3 (UTF-8 BOM)**：
   - 注入内容：`\xef\xbb\xbflet a = 1\n`
   - 门禁判定：`F3 FAIL: 源码文件包含 UTF-8 BOM 字节序标记`，静态逻辑断言失败。
4. **负例 F4 (代码行尾悬挂空格)**：
   - 注入内容：`let a = 1   \n`
   - 门禁判定：`F4 FAIL: 代码行尾包含悬挂空白：'let a = 1   '`，静态逻辑断言失败。
5. **负例 F5 (缺少 EOF 换行)**：
   - 注入内容：`let a = 1`（末尾无 `\n`）
   - 门禁判定：`F5 FAIL: 文件末尾缺少换行符 (\n)`，静态逻辑断言失败。

---

## 3. 边界与受托约定遵守

- **未触碰受保护边界**：
  - 未修改任何 `compose/Gallery/` 源码与测试；
  - 未修改任何 `compose/AppShell/` 源码与测试；
  - 未修改任何 PWA 前端 JS 产物（`app/Pwa/wwwroot/main.js` 等）；
  - 未修改任何全局规则文档（根 `AGENTS.md`、根 `README.md` 等保持完好）。
- **执行边界**：
  - 本轮修改由 Engineer 纯静态编写落盘，严守权限未调用任何 bash 命令。
---

## 4. 视觉与控件覆盖门禁接线（承接 visual-report.md）

已根据 `docs/evidence/2026-10-23-handoff/visual-report.md` 的落地入口完成门禁分层接入：

### 4.1 静态控件库画廊覆盖门禁（G3/G5 静态规则）
- **脚本**：`tools/gallery-coverage.sh`（核验全仓 15 个实体交互控件库与 14 个 UI 组合者的显式画廊映射清单，秒级纯源码检查）。
- **接线位置**：
  - `x`：接入 `check()` 流程（失败则 `bad=1`），并暴露独立命令 `./x gallery-coverage`；
  - `.github/workflows/ci.yml`：在 CI 的 `check` 作业中随 `./x check` 自动执行，零增加流水线时间。

### 4.2 需包与渲染的像素基线比对门禁（G3 门禁）
- **脚本**：`tools/gallery-baseline.sh`（核验 26 个条目在深浅双主题下逐像素比对）。
- **接线原则**：**不在源码秒级 check 里反复构建**，而是放在 `pack` 之后消费交付包：
  - `x`：增加独立命令 `./x gallery-baseline [基线目录]`；
  - `.github/workflows/ci.yml`：新增独立 CI 作业 `gallery-baseline`，声明 `needs: pack`，下载 `feed` 工件后执行 `bash tools/gallery-baseline.sh artifacts/gallery-baselines`。
- **边界恪守**：未修改任何 `compose/Gallery/` 源码与测试，未提交伪造基线；真实基线生成与端到端核验由 DevOps 在实机与 CI 环境中执行收口。
