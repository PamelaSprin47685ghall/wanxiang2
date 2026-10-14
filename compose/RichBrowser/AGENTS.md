# RichBrowser（组合）

旧 `Wanxiang.Pwa.BrowserRichBackend`：PWA 用浏览器自带 JS 引擎跑 hljs 与 KaTeX。协作者：BrowserRich、RichAssets。同样不再实现 IRichBackend，PWA 入口转接。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **着色与排版的输出与旧实现逐字节相同。** [根 AXIOM: 5]
2. **加载次序、按需语言、失败一律退化为 None 的规则归本组合者。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests 通过。
2. 浏览器之外 JSImport 调用会抛：测试钉住「绝不抛给界面，一律 None」。真正的浏览器内输出由旧仓库 PWA 的端到端脚本覆盖（tools/parity_pwa.js），不在本仓库 CI。

# 迁入或覆盖的旧测试
本组合测试：
- `浏览器之外调用退化为 None，不抛`
