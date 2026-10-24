# 验收语义区分度与门禁有效性独立审计报告（2026-10-23）

> 历史快照：本文件是 2026-10-23 交接时点的结论，可能与后续实测不一致；权威口径以 docs/evidence/2026-10-24-verified-release/ 与根 AGENTS.md〈2026-10-24 本轮交付验证收口证据〉为准。

- **审计性质**：独立只读静态审计（**未执行真实命令**，不修改正在验证的源码）
- **审计对象**：`tools/smoke_published_pwa.js`、`compose/Gallery/Baseline.fs`、`tools/gallery-baseline.sh`、`compose/Gallery/A11yAudit.fs` 及相关验收脚本
- **遵循准则**：Common Law（门禁必须真会红、抓得住反例、不拿行数与表象遮掩漏洞）、Kolmogorov 之书（区分本质与偶然复杂度、追查根因）与根规则 SPEC 7。

---

## 一、快捷键真实产品映射审计

### 1. 当前代码现状
在 `tools/smoke_published_pwa.js` 中共涉及 4 组端到端快捷键：
- **阶段 6**：`Ctrl+K` 打开命令面板
- **阶段 7**：`Ctrl+,` 打开设置大面板
- **阶段 8**：`Ctrl+Shift+S` 轮转主题
- **阶段 5/6/7**：`Escape` 关闭弹窗与浮层回焦

### 2. 映射核对结果
对照核心路由权威 `libs/ShortcutKeys/ShortcutKeys.fs:57-85`（`ShortcutRouter.resolveKey`）与用户帮助清单 `compose/DialogsDesk/DialogsDesk.fs:37-67`：
- `ctrl && key = "K"` 映射至 `ShortcutAction.OpenCommandPalette`，**符合真实产品映射**；
- `ctrl && key = "OemComma"` 映射至 `ShortcutAction.OpenSettings`，Puppeteer 派发 `Comma`（keyCode 188）在 Avalonia Browser 中映射为 `Key.OemComma`，**符合真实产品映射**；
- `ctrl && shift && key = "S"` 映射至 `ShortcutAction.ToggleTheme`，**符合真实产品映射**；
- `key = "Escape"` 映射至 `ShortcutAction.Escape`，**符合真实产品映射**。

### 3. 伴生缺陷与脆弱点
- **字符类型与注释口径不一**：
  `tools/smoke_published_pwa.js:146-147` 注释声称：`// B. 通过真实键盘物理击键输入 ASCII 分隔与字符（触发原生 keydown / textinput 路径）`，但紧接着的代码为 `await page.keyboard.type('｜键盘');`。
  字符 `'｜'`（`\uFF5C` 全角竖线）与 `'键盘'` 均非 ASCII 字符。虽然该步骤通过了字符串拼接与 IndexedDB 草稿往返校验，但注释与用例意图存在陈述偏差，未形成对纯 ASCII 物理击键路径的单独隔离验证。

---

## 二、像素变化不冒充菜单语义审计

### 1. 当前代码现状
在 `tools/smoke_published_pwa.js:205-235`（阶段 4）中：
```javascript
const beforeMenu = await sampleMenuRegion();
await page.mouse.click(composerCoord.x, composerCoord.y, { button: 'right' });
const menuAppeared = await until(async () => {
    const now = await sampleMenuRegion();
    return Math.abs(now.sum - beforeMenu.sum) > 5000 && now.distinct >= 4;
}, 6000, 150);
check('菜单：输入框右键弹出菜单出现（含弹出层中文字体样式）', Boolean(menuAppeared)...);
```

### 2. 实质缺陷与触发反例
**缺陷性质：典型的「以粗糙像素扰动冒充语义成功」，完全无法区分真实菜单、选区高亮与字体故障。**

- **反例 A：文本框选中高亮（Selection Highlight）冒充菜单弹出**
  - **触发场景**：右键点击输入框坐标时，若触发了光标重定位、文字选区拖拽，或者文本框自身产生获得焦点的边缘呼吸变色与光标渲染。
  - **后果**：局部区域的 RGB 像素和变化极易超过 5000，且抗锯齿边缘会产生 4 种以上不同颜色值。测试将一段文字高亮误判为“右键上下文菜单已成功弹出”。
- **反例 B：中文字体缺失（豆腐块方框字）反向通过测试（假阳性）**
  - **触发场景**：测试显式声称验证“弹出层中文字体样式”，旨在守住「剪切」「复制」「粘贴」不出现方框豆腐块。
  - **后果**：若字体加载失败，弹出层呈现为灰色背景、黑色边框以及 3 个白色方框豆腐块（`□□□`）。这种失效图像天然拥有灰色底、阴影过渡、边框黑、豆腐块白等 4~8 种以上颜色，且像素差远超 5000。
  - **实质**：**真正出现豆腐块退化时，该断言依然返回 PASS**！门禁沦为纸糊，丧失了对中文字形是否真正渲染出来的甄别能力。
- **反例 C：假关闭假回焦（连环失效）**
  - **触发场景**：在阶段 5 中，断言 `closed = Math.abs(now.sum - beforeMenu.sum) < 2500 && bridgeFocused()`。
  - **后果**：若阶段 4 的右键根本没有唤出任何弹层（纯粹误触），则像素与原先几乎无差异，且输入框本来就持焦。阶段 5 会瞬间通过，从而掩盖菜单未退栈的真实事实。

---

## 三、对比前后抓住真实退化审计

### 1. 当前代码现状
- **中心弹窗检测（阶段 6 & 7）**：
  命令面板（Ctrl+K）与设置大弹窗（Ctrl+,）均以画布中央区域 `(clientWidth/2 - 200, 150, 400, 300)` 的像素差 `abs(now.sum - beforeCenter.sum) > 8000` 为判定标准。
- **主题轮转检测（阶段 8）**：
  深色模式通过 `getCanvasLuminance() < 100` 判定；浅色通过 `getCanvasLuminance() > 140` 判定。

### 2. 实质缺陷与触发反例
- **反例 A：弹窗互串与错误弹窗穿透**
  - **触发场景**：若快捷键映射退化，按下 `Ctrl+,` 实际打开了命令面板而非设置面板；或者界面遇到未捕获异常弹出了居中的全局错误报错卡片或断线提示。
  - **后果**：两者的检测范围完全相同，均只粗暴判断中心区域像素差异 > 8000。只要屏幕中央出现了任何大面积色块，测试即断言“设置大弹窗正常打开”，根本抓不住组件退化。
- **反例 B：黑屏崩溃冒充深色模式通过**
  - **触发场景**：切换主题时若因资源字典键冲突或 WebGL 上下文丢失导致画布彻底清空崩溃为纯黑屏（RGBA 全 0）。
  - **后果**：此时平均亮度 `lum = 0.0 < 100`。由于阶段 8 缺少了像阶段 1 那样的 `distinct >= 3` 非纯色断言，**一个彻底死锁黑屏的产物会被断言为“深色主题外观验证通过”**！

---

## 四、基线缺失确拒绝审计

### 1. 当前代码现状
- `tools/gallery-baseline.sh:34-39`：
  ```bash
  if [[ ! -d "$BASELINE_DIR" ]]; then
    echo "[FAIL MISSING] 基线目录不存在: $BASELINE_DIR" >&2
    exit 1
  fi
  ```
- `compose/Gallery/Baseline.fs:110-114`：
  ```fsharp
  let checkEntry (baselineDir: string) (entry: Entry) (mode: PaletteMode) : BaselineCheck =
      let path = baselinePath baselineDir entry.Name mode
      if not (File.Exists path) then
          BaselineCheck.MissingBaseline(entry.Name, themeStr, path)
  ```
- `compose/Gallery/Program.fs:182-184`：只要存在 `MissingBaseline`，进程退出码即为 1。

### 2. 审计结论：确拒绝机制成立，但存在工程死锁
- **优点（拒绝确成立）**：
  基线缺失时的拦截逻辑严密可靠（Fail-Closed）。无论目录不存在、目录为空，还是某个新增条目缺少对应主题的 PNG，均明确报告 `[FAIL MISSING]` 并退出码 1，绝不放行。
- **严重工作流缺陷（CI 必红死锁）**：
  - 项目根目录 `.gitignore` 第 4 行配置了 `artifacts/`；
  - 默认基线存储路径被硬编码在 `artifacts/gallery-baselines`；
  - 在干净拉取的 Git 仓库或 CI 虚拟机构建机中，`artifacts/gallery-baselines` 根本不存在；
  - `.github/workflows/ci.yml:151` 中的 `gallery-baseline` 作业在 `actions/checkout@v4` 后未挂载任何基线制品，直接执行 `bash tools/gallery-baseline.sh artifacts/gallery-baselines`。
  - **后果**：任何干净的 CI 构建跑到此步骤必然直接红灯挂起！基线文件无法入库被跟踪。

---

## 五、可交互控件遍历不恒空 / 不忽略关键控件审计

### 1. 当前代码现状
在 `compose/Gallery/A11yAudit.fs:34-45` 与 `Program.fs:193-211` 中：
```fsharp
let isCandidate (c: Control) : bool =
    if not c.IsEffectivelyVisible then false
    else
        c.Focusable
        || c :? Button
        || c :? TextBox
        || c :? ToggleSwitch
        || c :? CheckBox
        || c :? MenuItem
        || c :? ComboBox
        || c :? ListBoxItem

let items = A11yAudit.auditTree control
let defects = A11yAudit.defectsOf items
if not defects.IsEmpty then
    // 报错
else
    printfn "  [PASS] %-34s 控件树 %d 个受检控件审查通过" entry.Name items.Length
```

### 2. 实质缺陷与触发反例
**缺陷性质：恒空通过假象（空转门禁）与视觉树穿透盲区。**

- **致命缺陷 A：空受检集合静默 PASS（恒真断言反例）**
  - **事实**：画廊包含纯展示性或骨架屏条目，如 `ChatEmpty.skeleton`。该条目由 `StackPanel` 与 `Border` 组成，无任何 `Button` 或 `Focusable` 控件。
  - **后果**：`items` 长度为 0，`defects` 必然为空列表 `[]`。门禁直接打印：
    `[PASS] ChatEmpty.skeleton                控件树 0 个受检控件审查通过`
    并以退出码 0 成功返回！
  - **危害**：如果某个复杂面板（如设置页或对话框）由于异步初始化失败或绑定异常，导致内部交互控件全未生成或 `IsEffectivelyVisible = false`，`auditTree` 同样会抓取到 0 个受检控件，并**静默判定为 0 缺陷通过**！完全违背了断言卫生准则。
- **致命缺陷 B：弹出层与浮层控件（Popup / Flyout / ContextMenu）被 100% 漏检**
  - **原理**：`auditTree` 调用 `VisualExtensions.GetVisualDescendants(root)` 遍历视觉树。
  - **现实**：在 Avalonia 体系中，所有 `MenuFlyout`、`ContextMenu`、`Popup`、`ComboBox` 下拉浮层均被挂载在独立的顶层 `PopupRoot` 或 `OverlayLayer` 中，**根本不是宿主界面的视觉后代**。
  - **后果**：条目中所有的快捷操作菜单项、模型选择下拉项，在控件树遍历时**一个都没有被扫描到**。`isCandidate` 中声明的 `c :? MenuItem` 沦为摆设。
- **缺陷 C：关键交互控件类型被忽略**
  - `isCandidate` 采取硬编码类型匹配，遗漏了：
    1. `TabItem` / `TabControl`（标签页切换器）；
    2. `ListBox` 容器本身（负责承载列表级可访问名称和键盘导航，漏检了列表本身的角色）；
    3. 自定义拖拽柄或手势承载件（如未设 `Focusable` 的分割条、手势区域）。

---

## 六、整改与加固建议（供后续窗口施工）

1. **PWA 冒烟像素断言替换为结构/文字证明**：
   - 菜单弹出：右键后，通过 CDP 或 Avalonia 探针核实顶层弹出了带有 `Role = Menu` 的浮层，或在 OCR/特征匹配中核验特定的中文轮廓向量，不再单凭像素和变化率判定；
   - 深色模式：在检查亮度 `lum < 100` 的同时，强制断言 `distinct >= 3`，杜绝黑屏死锁假阳性。
2. **画廊基线目录脱离 .gitignore**：
   - 将门禁基线移至 Git 跟踪的固定路径（如 `tests/gallery-baselines`），或在 `.gitignore` 中解除该目录忽略（`!artifacts/gallery-baselines/`），并由 DevOps 提交初版标准基线，使 CI 不再因缺目录而死锁。
3. **无障碍遍历消除恒空漏洞与穿透浮层**：
   - **禁止 0 控件静默通过**：对于声明包含可交互控件的条目，增加 `Assert.True(items.Length >= minExpected)` 下限门禁，杜绝空转；
   - **穿透浮层扫描**：在审查窗口时，同时扫描 `TopLevel.GetTopLevel(c)` 对应的浮层管理器，将弹出的 `PopupRoot` 视轨一并纳入无障碍审查。
