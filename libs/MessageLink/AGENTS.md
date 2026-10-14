# MessageLink

单条消息链接（`wanxiang://open?c=…&m=…` 与 PWA `#open?…`）的生成与识别，原样迁自旧 `Model/MessageLink.fs`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **链接文本不变。** [根 AXIOM: 1] 用户已复制、分享出去的链接必须还能打开。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 按字节钉住桌面写法，并覆盖拒绝情况。

# 迁入或覆盖的旧测试
- ContinueLinkUndoTests：`231 消息链接两种写法往返，普通搜索词不误认`
