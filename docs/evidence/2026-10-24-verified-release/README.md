# 2026-10-24 交付验证归档证据

本文档记录 2026-10-24 wanxiang2 交付验证收口的全量实跑原始证据、环境指标与校验边界。
所有命令均使用独立管道/脚本捕获真实 stdout、stderr 与实际退出码（记录在每份日志尾部 `COMMAND_EXIT_CODE=<int>` 及 `MANIFEST.json`），禁止伪造日志。

> **关于 `artifacts/` 路径的入库边界说明（2026-10-26 审视补注）：**
> 仓库根 `.gitignore` 声明了 `artifacts/`，因此下文中以 `artifacts/` 开头的路径（以及 `docs/evidence/2026-10-24-verified-release/artifacts/pwa-smoke/`）均属当次实跑时的本机临时输出，**未进入 Git 索引**；已入库的持久化证据为同目录 `MANIFEST.json`、`search-package-smoke.log` 与 `logs/` 下的全部原始日志。若需在本机重现这些未入库产物，按下列命令再生成：
> 重新运行产生的是当前提交与当前环境下的新证据，不能恢复历史运行的原始截图、报告或字节内容；原始产物未入库的部分仍存在追溯缺口。
> - `artifacts/baseline_sha256.txt`：`shasum -a 256 compose/Gallery/baselines/*.png`（82 张 PNG 基线本体已入库于 `compose/Gallery/baselines/`）
> - `artifacts/baseline_shots/`：`bash tools/gallery.sh --render <条目名> <输出.png> --theme <dark|light>`
> - `artifacts/publish/linux-x64/`：`bash publish.sh linux-x64`
> - `artifacts/pwa-smoke/`（冒烟截图）：`SMOKE_OUT=/tmp/pwa-smoke bash tools/smoke_published.sh artifacts/publish/linux-x64`
> - `artifacts/lighthouse/report.report.{json,html}`：启动已发布服务后执行 §五.3 的 `npx lighthouse` 命令

---

## 一、验证环境基线（Host Environment）

- **操作系统**：Manjaro Linux (Kernel `linux 7.1.13-2-MANJARO x86_64`)
- **硬件架构**：AMD Ryzen 9 7940HS w/ Radeon 780M Graphics (16 vCPU), 62 GiB RAM
- **.NET SDK**：`10.0.111` (Runtime: `net10.0`, Workloads: `wasm-tools 10.0.101`)
- **浏览器与运行时**：
  - Google Chrome: `153.0.8010.36` (`/usr/bin/google-chrome`)
  - Node.js: `v26.8.1`
  - npm: `11.11.1`
  - Lighthouse: `13.5.0`
  - Tesseract OCR: `5.5.3` (Leptonica `1.87.0`, 语言包 `chi_sim`, `eng`)
- **系统字体**：系统已安装 800 个字体家族（含 Noto Sans/Serif CJK SC、DejaVu Sans 等标准开源字体）。
- **环境详情日志**：`logs/00-env.txt`

---

## 二、画廊像素基线门禁（G3）实跑记录

### 1. 平台口径与规则
- **平台口径**：**优先 Linux 本机（与 CI `gallery-baseline` 作业的 ubuntu-latest runner 同款环境），退而用 macOS 本机；不使用容器环境。**
- **基线总数**：41 个画廊条目 × 深浅双主题（dark/light） = 共计 82 张 PNG 基线。
- **校验工具**：`tools/gallery-baseline.sh`

### 2. Linux 本机生成（--generate）
- **执行命令**：
  ```bash
  bash tools/gallery-baseline.sh --generate compose/Gallery/baselines
  ```
- **退出码**：`0`
- **结果**：全量 82/82 张基线在 Linux 本机实机重新渲染并写入 `compose/Gallery/baselines/`。
- **原始日志**：`logs/01-gallery-generate.log`

### 3. Linux 本机核验（逐像素对比）
- **执行命令**：
  ```bash
  bash tools/gallery-baseline.sh compose/Gallery/baselines
  ```
- **退出码**：`0`
- **结果**：82/82 逐像素对比通过，`0 failed`，`0 missing`，`0 mismatched`。
- **原始日志**：`logs/02-gallery-check.log` 及最终复核 `logs/final-gallery-check.log`
- **PNG 产物指纹**：82 张 PNG 文件的 SHA256 完整清单记录在 `artifacts/baseline_sha256.txt`。
- **关键基线视觉样本**：抽样归档 22 张关键条目深浅主题截图于 `artifacts/baseline_shots/`，包括：
  - `ComposerBox` (idle/typing/attachments/generating × dark/light)
  - `MessageCardView` (user/assistant/error/streaming × dark/light)
  - `BatchBar` (selection × dark/light)
  - `ConfirmPane` (confirm × dark/light)
  - `DevicesPane` (devices × dark/light)
  - `ModelCard` (default × dark/light)
  - `TitleBar` (window × dark/light)

---

## 三、远端 CI 与提交关联边界说明

### 1. 远端 CI 证据（历史提交）
- **关联提交 SHA**：`29f3b97187454800b8e7e7063b0e0addfb49ccdb`
- **提交时间**：2026-10-24T01:18:37Z
- **GitHub Actions Run ID**：`40049194275`
- **Runner 环境**：GitHub-hosted `ubuntu-latest` (Ubuntu 24.04.5 LTS, runner version 2.333.1)
- **作业名称**：`gallery-baseline`
- **核验结果**：82/82 像素基线比对全部通过，无差异，退出码 `0`。
- **CI 原始下载日志**：`logs/04-ci-gallery-baseline.log`

### 2. 证据边界与诚实声明
- **边界区分**：
  1. **历史远端提交 CI 绿**：指 commit `29f3b971` 在 GitHub Actions ubuntu-latest runner 上绿过。
  2. **本轮本机基线 82/82 实跑**：指当前未提交的工作区工作树在 Manjaro Linux x86_64 本机环境全量重新生成并逐像素比对通过。
- **特别注意**：未提交的代码变动与本地新生成的基线文件**不能也不得**冒充该远端 CI run 已覆盖；两者分属不同环境与时间线。

---

## 四、全仓构建与工程测试（./x all & ./x app）

### 1. `./x all` 门禁实跑
- **命令**：`./x all`
- **退出码**：`0`
- **原始日志**：`logs/final-x-all.log`
- **各阶段结果**：
  - **Libs 测试**：109 个工程全部通过（1081 项测试通过，0 失败，0 跳过）
  - **Pack 阶段**：9 波打包完成（共 9 波 solution 编译/打包）
  - **Compose 测试**：67 个组合者工程全部通过（1033 项测试通过，0 失败，0 跳过）
  - **Consume smoke**：通过
  - **全量测试汇总**：共 176 个测试工程，全量求和 2114 项测试全部通过（0 失败，0 跳过）
- **工程明细测试日志**：
  - 完整归档于 `logs/detailed-tests/`，包含 libs 109 项与 compose 67 项各工程原始输出。
  - 特别是 `logs/detailed-tests/wanxiang2-tests-zPzw5x/AppShell.log`，包含 50/50 项全通测试与 OpenSettings/MenuFlyout 恢复断言。

### 2. `./x app` 启动构建
- **命令**：`./x app`
- **退出码**：`0`
- **构建目标**：同时成功构建 Desktop (`app/Desktop/Wanxiang.App.fsproj`) 与 PWA (`app/Pwa/Wanxiang.Pwa.fsproj`) 双入口。
- **原始日志**：`logs/final-x-app.log`

### 3. 桌面实机 Smoke（tools/smoke_desktop.sh）
- **命令**：`tools/smoke_desktop.sh`
- **退出码**：`0`
- **原始日志**：`logs/final-smoke-desktop.log`
- **实际断言与输出指标**：
  - 启动打点：`startup.firstFrame uptime_ms=1448`
  - 列表就绪：`startup.listReady uptime_ms=1623`
  - 目录就绪：`startup.catalogReady uptime_ms=1667`
  - 进程退出码 0，stderr 无未处理异常或异常堆栈
  - 数据目录生成 ledger 存储文件（`ledger/log.jsonseq`，`ledger/.lock`，`ledger/.lock.pid`）
  - 数据目录生成 `attachments/` 目录
  - 数据目录锁已正常释放
  - 临时测试工作区已干净删除

### 4. 画廊像素基线复核
- **命令**：`bash tools/gallery-baseline.sh compose/Gallery/baselines`
- **退出码**：`0`
- **原始日志**：`logs/final-gallery-check.log`
- **结果**：82/82 逐像素比对通过（0 差异）

### 5. AppShell 菜单修复前后对比证据
- `logs/appshell-BEFORE-fix-2026-10-24.log`（修复前 48 passed, 2 failed）
- `logs/appshell-AFTER-fix-2026-10-24.log`（修复后 50 passed, 0 failed）
- **程序式关闭边界**：MenuFlyout Escape关闭/重开/快捷键恢复已验证；ContextMenu只验证打开拦截与Escape认领，程序式Open(anchor)后Close未证明完成，PWA smoke的Esc回焦是独立浏览器证据，不冒充该单测关闭。

---

## 五、发布产物打包与 PWA 冒烟验证

### 1. 发布构建（linux-x64）
- **执行命令**：
  ```bash
  bash publish.sh linux-x64
  ```
- **退出码**：`0`
- **产物位置**：`artifacts/publish/linux-x64/` 及压缩包 `dist/wanxiang2-v0.0.0-dev-linux-x64.tar.gz`
- **原始日志**：`logs/final-publish-linux-x64.log`

### 2. 发布产物 PWA 冒烟（smoke_published.sh）
- **执行命令**：
  ```bash
  bash tools/smoke_published.sh artifacts/publish/linux-x64
  ```
- **初次实跑与根因记录**（保留真实失败历史）：
  - 原始日志：`logs/final-smoke-published.log`
  - 结果：22 项 PASS，6 项 FAIL，退出码 1。
  - **真实根因**：初次运行时本地 Tesseract 环境缺少 `$TESSDATA_PREFIX/configs/tsv` 配置文件，导致 Tesseract 执行时报 `read_params_file: Can't open tsv`，OCR TSV 输出为空，使所有依赖文字识别定位的检查项失败（并非 Canvas 渲染或中文可读性故障）。
- **配置补齐后复测**：
  - 补齐配置：在 `$TESSDATA_PREFIX/configs/tsv` 配置 `tessedit_create_tsv 1` 及 `chi_sim`/`eng` 语言包后重跑。
  - 原始日志：`logs/final-smoke-published-pwa.log`
  - 退出码：`0`
  - **结果**：全部 28 项检查完整通过（28 PASS, 0 FAIL），耗时 29.20 秒：
    - 中文截图门禁拒绝缺字方框与空白：PASS
    - Avalonia 画布与输入桥元素出现：PASS
    - DPR 2 画布像素 = CSS × 2：PASS
    - 画布有实际内容（非纯色，18 distinct colors）：PASS
    - 预置客户端凭据与输入框点聚焦：PASS
    - 真实键盘输入与上屏桥增量落盘：PASS（`"万象ok｜键盘草稿往返"`）
    - 菜单真实后果：右键菜单「粘贴」剪贴板标记入库：PASS
    - Esc 回焦恢复：PASS
    - 命令面板真实交互后果：PASS
    - 设置弹窗真实交互后果（localStorage 变更）：PASS
    - 设置弹窗 Esc 回焦：PASS
    - 主题切换与持久化（light -> dark -> system）：PASS
    - 主题切换物理调色板变动：PASS
    - localStorage 访问计数递增：PASS
    - 认证态：IndexedDB 草稿落盘：PASS
    - 认证态：重开页面草稿回到输入框并支持追加：PASS
    - 侧栏点「中文冒烟」后主内容区出现该对话正文：PASS
    - 界面正文有可测的中文（自校准基准）：PASS
    - 悬停「新建对话」出现提示条：PASS
    - 弹出层 tooltip 出现：PASS
    - 弹出层 tooltip 中文文字可读：PASS
    - 弹出层 contextmenu 出现：PASS
    - 弹出层 contextmenu 中文文字可读：PASS
    - 弹出层 flyout 出现：PASS
    - 弹出层 flyout 中文文字可读：PASS
    - 无未捕获页面异常：PASS
  - **产物同步边界**：当次记录称 25 张运行截图同步至 `docs/evidence/2026-10-24-verified-release/artifacts/pwa-smoke/`，但该目录受 `.gitignore` 排除，未进入 Git 索引；仓库无法提供这批历史截图供复核。本机重跑生成的是当前运行证据，不是恢复当次截图。

### 3. Lighthouse 无障碍审计（PWA Host）
- **执行命令**：
  ```bash
  npx lighthouse http://127.0.0.1:<PORT>/ --chrome-flags="--headless=new --no-sandbox" --only-categories=accessibility --output=json,html
  ```
- **退出码**：`0`
- **原始日志**：`logs/final-lighthouse.log`
- **报告产物**：`artifacts/lighthouse/report.report.json` 与 `artifacts/lighthouse/report.report.html`
- **审计得分**：`100.0%`（Host DOM Shell 无障碍审计全部 9 项基准检测通过，0 fail）
- **工具局限声明**：
  - Avalonia Web 应用基于 Skia Canvas/WebAssembly 渲染。浏览器宿主 DOM 仅包含顶层容器，真实的 UI 控件、文本、图标均绘制在 Canvas 内。
  - Lighthouse 得分 100% 仅代表外部宿主 HTML 骨架符合无障碍规范，**不代表 Canvas 内部所有控件已达到可访问性标准**，不可冒充屏幕阅读器等辅助技术可用性。

---

## 六、SearchHit 独立包消费 Smoke

- **验证内容**：SearchHit 从 BatchBar 下沉至 WorkspaceState 后的独立依赖消费与跨包引用验证。
- **原始日志**：`search-package-smoke.log`（退出码 `0`，成功构建并消费）。

---

## 七、实测边界与环境局限

1. **Orca 读屏**：
   - 当前 Linux 环境缺少物理音频硬件与活跃的 Orca 读屏守护进程，无法进行读屏音频回放测试；如实保留该边界，不伪造已验证。
2. **移动浏览器真机**：
   - 无物理 iOS Safari / Android Chrome 移动端物理设备连接，如实保留边界说明。
3. **远端 CI 边界**：
   - 当前工作区为本地未提交状态；远端 GitHub Actions Run 40049194275 仅代表历史提交 `29f3b97187454800b8e7e7063b0e0addfb49ccdb`。当前树不能冒充该远端 run 已覆盖。
4. **上下文菜单关闭行为**：
   - MenuFlyout Escape关闭/重开/快捷键恢复已验证；ContextMenu只验证打开拦截与Escape认领，程序式Open(anchor)后Close未证明完成，PWA smoke的Esc回焦是独立浏览器证据，不冒充该单测关闭。
