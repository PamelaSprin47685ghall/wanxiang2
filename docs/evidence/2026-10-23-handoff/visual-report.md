# 画廊自动化与视觉/无障碍覆盖收拢报告（2026-10-23）

> **状态声明**：本报告为源码实现、静态规则与测试用例落地事实。**当前全部改动均由 Engineer 静态完成，本地尚未运行命令，不据此冒充动态运行证据**。全部测试、覆盖脚本、基线生成与核对命令需由 DevOps 在标准环境中实际执行并留证收口。

---

## 1. 本次已完成的实现与落盘事实

承接《展望.md》§3 G3（像素基线）与 G5（无障碍整树遍历）自动化要求及控件画廊覆盖：

### 1.1 像素基线比对与生成引擎（G3）
- **文件**：`compose/Gallery/Baseline.fs`
- **机制与选型**：
  - 使用仓库现有的 Avalonia 与 Skia（`Avalonia.Skia`、`Bitmap`、`RenderTargetBitmap`、`WriteableBitmap`）能力，**零新增依赖**；
  - 核心类型：`DiffResult`（`Match`、`SizeMismatch`、`PixelMismatch`）与 `BaselineCheck`（`Passed`、`MissingBaseline`、`SizeMismatch`、`PixelChanged`、`RenderError`）；
  - 支持深色（`Dark`）与浅色（`Light`）两套主题；`Gallery.setMode` 同步设置 `ThemeManager` 活画笔与 `Application.Current.RequestedThemeVariant` FluentTheme 控件模板变体；
  - 像素比对时，若存在像素差异，除了计算差异像素数与百分比、定位首个差异点坐标 `(x, y)` 外，自动在 `<基线目录>/diff/` 输出高亮差分图（相同像素压暗衰减，差异像素品红 `#FFFF00FF` 高亮标注）。
- **门禁契约**：基线必须由 DevOps 在实机环境中真实生成，严禁伪造假图进仓库；缺基线或像素存在差异**必须红灯阻断**（退出码 1）。

### 1.2 全画廊真实控件树无障碍遍历与原生 Peer 审查（G5，含 PopupRoot / OverlayLayer 浮层穿透）
- **文件**：`compose/Gallery/A11yAudit.fs`、`compose/Gallery/Program.fs`
- **机制与选型**：
  - **承载窗口与浮层全树穿透**：根据独立语义审计报告（`semantic-validation-review.md` 第 4 项），Avalonia 的 `MenuFlyout`、`ContextMenu`、`Popup` 打开时会作为独立的 `PopupRoot` 或挂载在 `OverlayLayer`，不在主内容根控件的 `VisualDescendants` 中。本次将审查入口扩为 `A11yAudit.auditWindow win`（及 `activeRoots win`），穿透扫描承载 `Window`、Avalonia 原生 `OverlayLayer` 与所有活动状态下的 `PopupRoot` 浮层；
  - 在控件与浮层挂载完成后，遍历全量根节点可视树，甄别所有可见的可聚焦（`Focusable = true`）与实体交互控件（`Button`, `TextBox`, `ToggleSwitch`, `CheckBox`, `MenuItem`, `ComboBox`, `ListBoxItem`）；
  - **拒绝只读属性冒充**：严禁使用只读 `AutomationProperties.GetName` 绕过 peer（原生控件如 `Button(Content = "保存")` 或 `MenuItem(Header = "复制")` 靠 Content/Header 由 peer 计算可访问名称，只读属性为 null 但读屏能真实获取；反之仅挂属性而不具备有效 peer 属于虚假无障碍）；
  - 必须通过原生 `ControlAutomationPeer.CreatePeerForElement` 获取原生 peer，校验 `peer.GetAutomationControlType() <> AutomationControlType.None` 且 `peer.GetName()` 具有非空白有效名称。

### 1.3 画廊组合者扩展与状态覆盖（24 → 26 条）
- **修改文件**：
  - `compose/Gallery/Gallery.fs`：公开 `setMode`、`currentMode`，`createContext (?mode)` 支持指定主题；补充 `ApprovalDesk.request`（工具调用待审批态）与 `ChatEmpty.empty`（新对话空态欢迎卡与起手式芯片）两个真实状态条目，使条目覆盖扩展至 26 条。
  - `compose/Gallery/Program.fs`：CLI 扩充支持 `--theme <light|dark>`、`--check-baselines`、`--generate-baselines` 与 `--a11y-audit`。
  - `compose/Gallery/Gallery.fsproj`：编译清单纳入 `Baseline.fs` 与 `A11yAudit.fs`。

### 1.4 自动化门禁脚本与清单规范
- **新建文件**：
  - `tools/gallery-baseline.sh`：深浅双主题像素基线比对与生成门禁外壳脚本；
  - `tools/gallery-coverage.sh`：控件库显式清单与画廊覆盖审计脚本；
  - `docs/ux/widget-coverage.md`：全仓 15 个控件库与 14 个 UI 组合者的显式认定标准、清单、画廊条目映射矩阵与四条覆盖规则。
- **修改文件**：
  - `tools/gallery.sh`：更新文档与参数透传说明；
  - `compose/Gallery/AGENTS.md`：同步更新 26 条状态矩阵、G3/G5 门禁与命令规范。

---

## 2. 自动化测试名一览

本次在 `compose/Gallery/tests/` 下新增 2 个测试文件共 16 条确定性行为与门禁测试（原 `Tests.fs` 11 条完整保留，总测试数达 27 条）：

### 2.1 像素基线比对引擎测试（`compose/Gallery/tests/BaselineTests.fs`，5 条）
1. `缺少基线文件时核查必须红（MissingBaseline）`
2. `同图比较必定 100% 匹配（Match）`
3. `像素改变时核查必须红并指出坐标（PixelMismatch）`
4. `尺寸不匹配时核查必须红（SizeMismatch）`
5. `深浅两套主题渲染出的位图背景与像素真实不同`

### 2.2 无障碍全树遍历与原生 Peer 审查测试（`compose/Gallery/tests/A11yAuditTests.fs`，13 条）
1. `画廊真实控件树遍历：输入区所有可聚焦可交互控件均有有效原生 peer 与非空名称`
2. `画廊真实控件树遍历：对话列所有卡片按钮与动作均有有效原生 peer 与非空名称`
3. `画廊真实控件树遍历：侧栏行与批量条所有操作控件均有有效原生 peer 与非空名称`
4. `画廊真实控件树遍历：设置页面板控件均有有效原生 peer 与非空名称`
5. `画廊真实控件树遍历：对话框确认取消键均有有效原生 peer 与非空名称`
6. `活动弹窗与浮层审查：真实打开的 ContextMenu 弹出层中至少一个 MenuItem 被扫描且名称角色正确`
7. `活动弹窗与浮层审查：真实打开的 MenuFlyout 弹出层中至少一个 MenuItem 被扫描且名称角色正确`
8. `原生 peer 角色与名称证明：peer.GetName() 优于只读 AutomationProperties`
9. `无名负例 1：无内容且无名称的按钮被审查判定为 MissingAccessibleName 缺陷`
10. `无名负例 2：无提示且无名称的输入框被审查判定为 MissingAccessibleName 缺陷`
11. `无名负例 3：可聚焦无名自定义元素被审查判定为 MissingAccessibleName 缺陷`
12. `无名负例 4：纯空白名称被审查判定为缺陷，不能蒙混过关`
13. `无名负例 5：真实打开的弹窗浮层中无名 MenuItem 必须被检出缺陷并亮红灯`
14. `负例审查失败判定：存在缺陷时 assertClean 必须抛出异常亮红灯`

---

## 3. 可打红反例（可验证见红的实据）

本次测试设计严守「见红才算门禁，抓不住反例的门禁是纸糊的」原则：
- **G3 反例 1（缺少基线）**：当指定目录缺少基线文件时（如尚未生成的目录），核查必定返回 `BaselineCheck.MissingBaseline`，CLI 打印 `[FAIL MISSING]` 并以退出码 1 失败。
- **G3 反例 2（像素改变）**：在 `BaselineTests` 中，对一张 720×900 的真实渲染图篡改 `(100, 100)` 处 1 个像素，门禁比对必定返回 `BaselineCheck.PixelChanged`，报告 `diffCount >= 1`、首个差异坐标为 `(100, 100)`，并产出高亮 diff 掩模图。
- **G3 反例 3（尺寸改变）**：输入尺寸不同的位图，比对必定返回 `DiffResult.SizeMismatch`。
- **G5 反例 1（无名按钮）**：一个 `Button(Width = 32.0, Height = 32.0)`（无文字 Content 且未设名称），审查必定判定出 `A11yDefect.MissingAccessibleName`。
- **G5 反例 2（无名输入框）**：一个空的 `TextBox()`，审查必定判定出 `A11yDefect.MissingAccessibleName`。
- **G5 反例 3（可聚焦无名元素）**：一个 `Border(Focusable = true)`，审查必定判定出 `A11yDefect.MissingAccessibleName`。
- **G5 反例 4（纯空白名称）**：一个 `Button(Content = "    ")`，审查必定判定出 `A11yDefect.MissingAccessibleName`。
- **G5 反例 5（无名菜单项在活动浮层中必红）**：在真实 `Window` 上挂载并真实 `Open` 包含无名 `MenuItem(Header = "")` 的真实 `ContextMenu`（非伪造树），`auditWindow` 穿透到活动 `PopupRoot` 浮层后必定捕获其 `MissingAccessibleName` 缺陷，`assertClean` 坚决抛异常阻断。
- **G5 反例 6（门禁阻断）**：当存在任一缺陷时，调用 `A11yAudit.assertClean` 必定抛出带有详细定位信息的异常，无法静默通过。

---

## 4. 需 DevOps 实际执行命令（动态验证与留证指引）

请 DevOps 协助在包含 SDK 与图形依赖的标准宿主环境下执行以下命令并收集动态验证日志：

```bash
# 1. 编译并运行画廊组合者全部 27 项单测（验证状态差异、基线比对引擎与无障碍遍历审查）
dotnet test --project compose/Gallery/tests/Gallery.Tests.fsproj -p:RestorePackagesPath="$PWD/artifacts/packages"

# 2. 运行控件库显式清单与覆盖规则审计脚本
bash tools/gallery-coverage.sh

# 3. 运行画廊真实控件树无障碍全量遍历审查（遍历全量 26 个条目的真实可视控件）
bash tools/gallery.sh --a11y-audit

# 4. 生成全量真实深浅双主题 PNG 像素基线（DevOps 在目标机器环境生成基线）
bash tools/gallery-baseline.sh --generate artifacts/gallery-baselines

# 5. 执行深浅双主题像素基线核查门禁（核验 26 个条目在 light 与 dark 下共 52 张基线 100% 匹配）
bash tools/gallery-baseline.sh artifacts/gallery-baselines
```

---

## 5. 限制摘要与待决事项

1. **尚未执行命令**：根据 Engineer 职权约束，本次全部改动均为静态编写与代码审计，本地未执行真实 `dotnet test` 或 `bash` 脚本，动态运行结果目前标记为 **`[待验证]`**，交由 DevOps 实际验证。
2. **基线未伪造入库**：根据「基线由 DevOps 后续实际生成，不能伪造」的硬性要求，代码仓库内目前未提交伪造的 PNG 基线图片；DevOps 执行命令 4 生成真实基线后，门禁（命令 5）方能由红转绿。
3. **平台渲染环境一致性**：Headless Skia 光栅化依赖字体排版度量，跨平台（如 Linux glibc/FreeType vs Windows DirectWrite vs macOS CoreText）可能存在亚像素平滑微差，DevOps 建立基线时需在标准 CI runner（Ubuntu linux-x64）或指定生产环境下生成与核对。
