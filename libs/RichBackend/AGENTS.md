# RichBackend

富文本渲染所需的脚本能力接口与注册处（迁自旧 `src/Wanxiang.UI/Rich/RichBackend.fs`，原样迁入）。`IRichBackend` 抽象 hljs 代码高亮与 KaTeX 公式的「文本进、HTML 出」；`RichBackend.install`/`current` 是模块级槽位。命名空间沿用 `Wanxiang.UI`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **没装时功能缺失但绝不崩。** `current` 返回 `None`，调用方（CodeTint、MarkdownView 的宿主适配）自行退化为纯文本。
2. **两端同一接口。** 桌面端由 Jint（`compose/RichJint`）提供，浏览器端由宿主 JS 引擎（`compose/RichBrowser`）提供；布局与绘制全在 Avalonia 侧共享，两端的公式与代码长得完全一样。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 覆盖 `install`/`current` 槽位：安装后返回同一实例、install 覆盖语义、current 返回的后端可调用接口方法。

# 迁入或覆盖的旧测试
无。旧仓库无 RichBackend 专项测试；宿主适配（App/Pwa 给 `CodeTint`/`MarkdownView` 传后端）见根 AGENTS.md「UI 正文第二批」。
