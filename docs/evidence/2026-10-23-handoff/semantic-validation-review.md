# 验收语义区分度与 DevOps 修复成果审查报告（2026-10-23）

> 历史快照：本文件是 2026-10-23 交接时点的结论，可能与后续实测不一致；权威口径以 docs/evidence/2026-10-24-verified-release/ 与根 AGENTS.md〈2026-10-24 本轮交付验证收口证据〉为准。

- **审查性质**：只读静态代码语义核对（**不改动源码**，不以动态“全绿”掩盖代码事实）
- **核对目标**：核实前轮报告（`structure-validation-audit.md`）指出的 4 类实质缺陷在 DevOps 落盘代码中的解决情况：
  1. PWA 具体语义与黑屏拒绝
  2. 基线跟踪与 CI 死锁
  3. a11y 空树恒空通过
  4. a11y PopupRoot 浮层穿透盲区
- **归档位置**：`docs/evidence/2026-10-23-handoff/semantic-validation-review.md`（留存同证据目录）

---

## 一、四类缺陷核对总览

| 缺陷类别 | 涉及文件与位置 | 状态判定 | 核心事实依据 |
| :--- | :--- | :--- | :--- |
| **1.1 PWA 黑屏拒绝** | `tools/smoke_published_pwa.js:420-435` | **已解决** | 增加了 `checkCanvasAlive` 校验（色值数 ≥ 3 且像素和 > 1000），黑屏崩溃不再能通过亮度断言。 |
| **1.2 PWA 菜单具体语义** | `tools/smoke_published_pwa.js:230-245` | **部分解决 / 转人工核验** | 断言文案去除了“验证中文字体”伪标签；但代码断言未变，仍凭像素差判定，无法自动甄别豆腐块或高亮误触。 |
| **2. 画廊基线跟踪与死锁** | `tools/gallery-baseline.sh`, `compose/Gallery/baselines` | **已彻底解决** | 基线迁移至 `compose/Gallery/baselines`，52 张基线实打实落盘，CI 配置同步更新，彻底消除缺失死锁。 |
| **3. a11y 空树恒空假通过** | `compose/Gallery/Program.fs:200-215` | **已解决** | 显式拦截 `items.IsEmpty && not isStaticExempt` 并报缺陷，仅对纯骨架屏做单点展示豁免，杜绝空转。 |
| **4. a11y PopupRoot 浮层穿透** | `compose/Gallery/A11yAudit.fs:75-85` | **未解决** | `auditTree` 仍使用 `VisualExtensions.GetVisualDescendants(root)`，仍无法穿透扫描独立的 `PopupRoot` 浮层。 |

---

## 二、逐项源码审查事实与技术细节

### 1. PWA 具体语义 / 黑屏拒绝

#### (1) 黑屏拒绝（已解决）
- **修复前状态**：深色模式仅断言 `getCanvasLuminance() < 100`。若主题切换导致崩溃黑屏（`lum = 0`），会产生假阳性通过。
- **DevOps 修复事实**：
  在 `tools/smoke_published_pwa.js:420-435` 中注入了活跃检测：
  ```javascript
  const checkCanvasAlive = () => page.evaluate(() => {
      // 采样 64x64 区域像素集合
      return { alive: set.size >= 3 && sum > 1000, distinct: set.size };
  });
  const darkLuminance = await until(async () => {
      const lum = await getCanvasLuminance();
      const alive = await checkCanvasAlive();
      return (lum < 100 && alive.alive && pageErrors.length === 0) ? lum : false;
  }, 6000, 200);
  ```
- **审查结论**：**已解决**。崩溃黑屏时色值数仅为 1，像素和为 0，`alive.alive` 必为 false，测试将坚决超时并标红，不再放行。

#### (2) 菜单语义区分度（部分解决 / 转人工核验）
- **修复前状态**：断言声称验证“弹出层中文字体样式”，但其实只是检查局部像素和变化 > 5000 且颜色数 ≥ 4。
- **DevOps 修复事实**：
  在 `tools/smoke_published_pwa.js:235-245` 中修改了断言输出文案：
  ```javascript
  check('菜单：输入框右键弹出菜单出现并支持Esc关闭（字形见截图 artifacts/smoke-published/smoke-menu-font.png 人工核验）', Boolean(menuAppeared)...);
  ```
- **审查结论**：**代码逻辑仍存语义盲区，口径降级为人工核验**。
  - **仍存缺陷反例**：自动化测试本身仍只执行 `Math.abs(now.sum - beforeMenu.sum) > 5000 && now.distinct >= 4`。若出现文本框鼠标选区拖拽高亮、或字体缺失渲染出带黑色边框的方框豆腐块（`□□□`），其像素差与色彩数同样满足此条件，该自动化用例依然会绿灯通过。
  - **最小修复要求（供后续参考，当前不改源码）**：
    由于 Avalonia WebAssembly 将界面完全绘制在 Canvas 上无原生 DOM 节点，自动化断言若需彻底闭环，需引入基于固定模板特征的像素级差分对比，或在无头 C# 控件测试中由 Peer 深度校验菜单项的存在性。当前作为端到端冒烟，由截图持久化留证转人工审查属于务实妥协，但须明确其并非机器全自动语义门禁。

---

### 2. 基线跟踪与 CI 死锁（已彻底解决）

- **修复前状态**：基线默认存在 `artifacts/gallery-baselines`，受 `.gitignore` 忽略且未入库，干净 CI 上运行必然因目录不存在而直接报错退出。
- **DevOps 修复事实**：
  1. 路径规范：`tools/gallery-baseline.sh:26, 32` 将基线路径修改为 `compose/Gallery/baselines`；
  2. 真实基线落盘：DevOps 真实执行生成并在 `compose/Gallery/baselines/` 目录下提交了全部 26 个画廊条目在深浅双主题下的 **52 张真实 PNG 基线图片**；
  3. CI 步骤更新：`.github/workflows/ci.yml:162` 同步改为 `bash tools/gallery-baseline.sh compose/Gallery/baselines`；
  4. 缺失确拒绝：基线目录或文件缺失时，脚本依然能严格 exit 1 拦截。
- **审查结论**：**已彻底解决**。代码与配置无缝契合，基线已被版本控制系统可靠跟踪。

---

### 3. a11y 空树恒空假通过（已解决）

- **修复前状态**：若条目受检控件数为 0，`items` 为空列表，旧代码打印 `[PASS] 控件树 0 个受检控件审查通过` 并以退出码 0 返回，掩盖空转与未挂载。
- **DevOps 修复事实**：
  在 `compose/Gallery/Program.fs:200-215` 中重构了判定分支：
  ```fsharp
  let isStaticExempt = entry.Widget = "ChatEmpty" && entry.Name = "ChatEmpty.skeleton"
  if not defects.IsEmpty then
      // 记录缺陷
      allDefects <- allDefects @ defects
  elif items.IsEmpty && not isStaticExempt then
      eprintfn "  [DEFECT] 交互性条目 %s 审查到 0 个受检控件（禁止恒空通过）" entry.Name
      allDefects <- allDefects @ [ A11yDefect.MissingAccessibleName(entry.Name, AutomationControlType.Custom, "交互性条目未扫描到任何受检可交互控件") ]
  elif isStaticExempt then
      printfn "  [PASS (EXEMPT)] %-34s 纯展示性条目（不适用交互控件审查）" entry.Name
  else
      printfn "  [PASS] %-34s 控件树 %d 个受检控件审查通过" entry.Name items.Length
  ```
- **审查结论**：**已解决**。
  - 对没有交互控件的骨架屏（`ChatEmpty.skeleton`）给予明确的展示性豁免标记（不再谎报“0 个通过”）；
  - 对其余所有声明具有交互能力的条目，一旦受检集合为空即直接判定为缺陷并退出非零码，彻底根除了空树恒真假安全。

---

### 4. a11y PopupRoot 浮层穿透盲区（未解决）

- **修复前状态**：`auditTree` 仅遍历传入控件的视觉树，无法穿透至 Avalonia 独立的 `PopupRoot`，导致动态下拉菜单项（`MenuItem`）全部漏检。
- **DevOps 修复事实**：
  `compose/Gallery/A11yAudit.fs:75-85` 的 `auditTree` 代码**未作任何修改**，依然仅执行：
  ```fsharp
  let descendants =
      VisualExtensions.GetVisualDescendants(root)
      |> Seq.choose (function :? Control as c -> Some c | _ -> None)
      |> Seq.filter isCandidate
      |> Seq.toList
  ```
- **审查结论**：**未解决**。
  - **机制原因**：在 Avalonia 框架中，所有 `MenuFlyout`、`ContextMenu`、`Popup` 在打开时会被宿主挂载至独立的顶层浮层根（`PopupRoot`）中，根本不属于被测条目根控件的视觉后代（`VisualDescendants`）。
  - **触发反例**：若对话列表右键操作菜单、模型切换下拉浮层在弹出后存在无障碍名称缺失或角色错误，目前的 `auditTree` 依然完全无法感知，所有 `MenuItem` 仍然是 100% 漏检状态。
  - **最小修复要求（供后续施工参考，当前不改源码）**：
    将审查入口从 `auditTree (root: Visual)` 扩展为接收承载窗口 `auditWindow (win: Window)`。在窗口布局完成后，不仅遍历主内容树，同时遍历属于该窗口的活动浮层或 `OverlayLayer` 子项，将弹出的浮层视觉树一并提取并喂入审查器。

---

## 三、最终建议与收束

1. **当前状态评估**：
   DevOps 的修复工作非常扎实有效：
   - 彻底解开了“基线跟踪受 gitignore 屏蔽导致 CI 必红”的死锁；
   - 补齐了“深色模式黑屏死锁导致假阳性”的防护；
   - 封堵了“无障碍门禁受检列表为空时静默通过”的漏洞；
   - 全库 52 张基线 100% 匹配与全链路验证全绿具备客观凭据支持。
2. **遗留两处边界事实**：
   - PWA 菜单自动化断言受限于浏览器 Canvas 架构，仍依赖像素差，中文字形需以人工核对截图为准；
   - 画廊 A11y 遍历目前仅覆盖常规控件树，未覆盖动态 `PopupRoot` 浮层内控件。
3. **操作指导**：
   当前无需在全库验证期间紧急改动源码，上述两处遗留点可作为已知架构边界记录在案，待后续专门迭代加固。


---

## 四、DevOps 后续修复追加核对（2026-10-23 末轮）

- **核对对象**：最新版 `tools/smoke_published_pwa.js` 与 `docs/evidence/2026-10-23-handoff/devops-final.md`。
- **核对重点**：是否守住“真实行为后果、不走内部状态机/假通道、保留黑屏拒绝”，未为变绿退回像素差或删断言。

### 1. 核对总览

| 核查项 | 状态 | 事实与证据 |
| :--- | :--- | :--- |
| **真实行为后果** | **守住且显著加固** | 菜单改用预置 `CLIP_TOKEN` 剪贴板 + IndexedDB 精确落盘断言；命令面板与设置开关改用 `localStorage` 持久化真值断言；主题切换校验 `wanxiang.local:ui.json` 真实变更。 |
| **不走内部状态机 / 假通道** | **守住** | 未注入任何隐藏测试钩子或内部状态旁路；DevOps 明确声明“未添加内部测试通道冒充 e2e”，全部经由真实产品 UI 入口与物理/键盘事件。 |
| **保留黑屏拒绝** | **守住了结构，但引入一处变量名笔误** | `checkCanvasAlive`（色值数 ≥ 3 且像素和 > 1000）与 `lum < 100` 联合判定逻辑完整保留；但实际引用了未定义的 `pageErrors` 变量（应为 `errors`），导致该断言在 Node 层抛出 `ReferenceError`。 |
| **不为变绿删断言 / 退回像素差** | **守住** | 命令面板、设置开关、主题轮转 4 项均以 `FAIL` 真实呈现，未删除、未放宽、未回退为像素指纹；菜单粘贴改为真实行为断言，属净增强。 |

### 2. 关键新发现缺陷（需修复）

- **`tools/smoke_published_pwa.js:384` 变量名笔误（`pageErrors` 未定义）**：
  - **代码现状**：
    ```javascript
    const darkLuminance = await until(async () => {
        const lum = await getCanvasLuminance();
        const alive = await checkCanvasAlive();
        return (lum < 100 && alive.alive && pageErrors.length === 0) ? lum : false;
    }, 6000, 200);
    ```
  - **实情**：本文件中页面错误数组在 `tools/smoke_published_pwa.js:73` 定义为 `const errors = []`，并不存在 `pageErrors`。
  - **触发反例**：
    由于 `until` 的 `try { ... } catch (_) {}` 会静默吞掉 `ReferenceError: pageErrors is not defined`，该谓词在 Node 层永远抛错并被吞掉，`darkLuminance` 必然超时返回 `false`。
    即使 `Ctrl+Shift+S` 真实切到深色模式，主题切换物理调色板变动断言依然必然失败。
  - **DevOps 当前误诊**：`devops-final.md` 将 `darkLum=false` 归因为“快捷键未穿透”，未发现该变量笔误才是根因之一。
  - **最小修复要求**：将 `pageErrors` 改为 `errors`（与 :73 与 :448 保持一致），恢复黑屏拒绝与深色亮度联合断言的真实可达性。

### 3. 结论

DevOps 本轮修复在“真实行为后果、不走假通道、不为变绿退回像素差或删断言”三个维度上守住了门禁底线，菜单粘贴断言属实质性语义加固；
但 `pageErrors` 笔误使“保留黑屏拒绝”断言在当前版本中实际不可达，需要修复该单字符笔误方可完全兑现黑屏拒绝契约。