# 初始状态与环境事实盘点（2026-10-23）

> 历史快照：本文件是 2026-10-23 交接时点的结论，可能与后续实测不一致；权威口径以 docs/evidence/2026-10-24-verified-release/ 与根 AGENTS.md〈2026-10-24 本轮交付验证收口证据〉为准。

- **盘点时点**：2026-10-23 09:17 UTC
- **盘点范围**：Git 工作树、SDK/运行环境、遗留进程、/tmp 日志归档、辅助功能与真机测试条件
- **执行原则**：只读检查，不执行 build/test/pack，不改动受 Engineer 维护之源码，不执行 stash/commit，不推送远端。

---

## 一、Git 与工作树事实

1. **当前分支与 HEAD**：
   - 分支：`main`
   - HEAD 提交：`14e4625`（`W7 清洁断点：死目录清理、8 组包合并、状态树补齐、PWA 输入与中文字体修复`）
   - 追踪状态：`up to date with 'origin/main'`，与远端完全同步。
2. **工作树状态**：
   - `git status -s`：干净（无任何被修改文件，无未跟踪文件）。
   - `git diff --stat`：为空。
   - `git stash list`：为空。
3. **外部 Worktree 挂载**：
   - `/home/kunweiz/Desktop/vibe/wanxiang2` -> `14e4625 [main]`（当前主工作区）
   - `/tmp/wx-baseline` -> `82d5628 (detached HEAD)`（W7 clean-break 基线只读对比用，除 node_modules 外干净）

---

## 二、可用 SDK、工具链与显示环境

| 工具 / 运行时 | 物理路径 | 版本 / 标识 | 状态 |
| :--- | :--- | :--- | :--- |
| **.NET SDK** | `/usr/bin/dotnet` | `10.0.111` | 可用 |
| **Node.js** | `/usr/bin/node` | `v26.8.1` | 可用 |
| **npm** | `/usr/bin/npm` | `12.0.2` | 可用 |
| **Git** | `/usr/bin/git` | `2.55.0` | 可用 |
| **Google Chrome** | `/usr/bin/google-chrome` | `Google Chrome 153.0.8010.36` | 可用（支持 headless 及 headed） |
| **显示环境** | `DISPLAY=:0.0` | X11 (`XDG_SESSION_TYPE=x11`) | X11 显示环境在线 |

**硬件容量基线**：
- CPU：16 核
- 内存：物理内存 62 GiB，当前空闲 43 GiB available；Swap 8.8 GiB 完整可用。

---

## 三、遗留进程与 /tmp 日志审查

1. **遗留进程核查**：
   - 无任何残留的 `wanxiang` 桌面应用实例（`Wanxiang.Desktop` / `app/Desktop`）、测试宿主（`HeadlessUnitTestSession`）或服务端进程（`wanxiang host`）。
   - 现存与测试相关的外部进程仅有系统的 localspace node 常驻服务与用户当前运行的桌面 Chrome 浏览器。
2. **/tmp 历史日志核查与留档**：
   - 发现旧日志 `/tmp/pwa_smoke.log`（生成于 10月21日 08:57，大小 3047 字节）。
   - **安全与凭证审计**：全文扫描 `token|secret|password|bearer|auth|key`，除普通日志标记（如 `PASS 预置客户端凭据`）外，**无任何真实明文密钥、令牌或凭证泄漏**。
   - **历史语义**：该日志记录了 10月21日 PWA 冒烟测试中「文本上屏桥与草稿落盘」的失败现象，该现象已由后续提交 `14e4625` 修复；鉴于 Engineer 3 正在重构 PWA 冒烟脚本，该历史记录具有重要对比与证据价值，已完整备份至：
     `docs/evidence/2026-10-23-handoff/pwa_smoke.log.bak`。
   - /tmp 下其余测试遗留目录（如 `/tmp/wanxiang2-tests-*`、`/tmp/wanxiang-ledger`、`/tmp/wanxiang-si-*`）均为历史单元测试与单实例锁遗留的临时目录，未见异常占用。

---

## 四、真实手机与屏幕阅读器验证条件评估

根据对系统底层与外设接口的探测事实：
1. **Linux 屏幕阅读器与无障碍基础组件**：
   - `orca`：**未安装**（`which orca` 返回无）
   - `speech-dispatcher`：**未安装**（`which speech-dispatcher` 返回无）
   - `at-spi-bus-launcher`：**未安装**（`which at-spi-bus-launcher` 返回无）
   - **判定**：当前 Linux 宿主环境**不具备**原生真实屏幕阅读器（Orca + AT-SPI）的端到端发声与读屏实测条件。
2. **真实移动设备（手机）**：
   - `adb` 工具在线（`/usr/bin/adb`）
   - `adb devices` 查询输出：`List of devices attached`（为空）
   - **判定**：当前宿主机未连接任何真实 Android 手机，亦无运行中的 Android 模拟器，**不具备**真实移动端设备联机调试与实测条件。
3. **后续验证替代路径**：
   - 界面无障碍验证依靠 Avalonia 的 `AutomationProperties`、Headless 单元测试断言以及 `docs/frontend-dev-process.md` 规定的无障碍属性走查。
   - Web/PWA 端验证依靠 Puppeteer / Headless Chrome 自动化冒烟（`tools/smoke_published.sh` 与 `tools/smoke_published_pwa.js`）。

---

## 五、当前未决事项与后续步骤

- **未决事项**：五位 Engineer 目前正在各自独立的工作分支或修改面（AppShell/WorkspaceState、Gallery、PWA 冒烟 JS、x/CI/publish、MERGE）推进代码。
- **DevOps 待命状态**：本轮环境与工作树盘点完成，不产出构建工件，不干扰五路修改；待 Manager 合流后通知 resume，再行启动完整的依赖检查、打包、多阶段组合与端到端回归验证。
