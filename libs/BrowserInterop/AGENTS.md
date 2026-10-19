# BrowserInterop

页面状态与生命周期（pageUrl、视口宽、恢复计数、首次成功、页面隐藏导出）的 JSImport 绑定（C#，从旧 Wanxiang.Interop/BrowserBridge.cs 按职责拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **与 wwwroot/main.js 的契约不变：每个方法绑定的 JS 函数名、模块名 "wanxiang"、参数与编组都与旧 BrowserBridge 相同。** [根 AXIOM: 5]
2. **是 C# 项目：JSImport/JSExport 的绑定由 source generator 生成，F# 不运行 generator。方法名去掉了旧的前缀（如 CredList → BrowserCredentials.List），调用方要跟着改。** [根 AXIOM: 3]
3. **JSExport 换了程序集与类名：main.js 要改为 `getAssemblyExports("BrowserInterop.dll")` 后调用 `Wanxiang.Interop.BrowserInterop.NotifyHidden`（旧为 Wanxiang.Interop.dll 的 BrowserBridge.NotifyPageHidden）；事件 `PageHidden` 改名 `Hidden`。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 测试只读元数据比对绑定名（浏览器之外不能实例化 JSImport 特性，更不能调用）：通过。

# 迁入或覆盖的旧测试
- 旧仓库没有针对 BrowserBridge 的测试。本库测试：
  - `JS 绑定名与旧 BrowserBridge 一致`
  - `NotifyHidden 是导出给 main.js 的入口，触发 Hidden`
  - `没有订阅者时 NotifyHidden 不抛`
