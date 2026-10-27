# RichAssets（组合）

旧 `Wanxiang.UI.RichAssets`：内嵌脚本与语言解析，公开面不变。协作者：RichScripts、HljsCatalog。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **着色与排版的输出与旧实现逐字节相同。** [根 AXIOM: 5]
2. **加载次序、按需语言、失败一律退化为 None 的规则归本组合者。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 的 `language annotations resolve through hljs aliases`、`an unknown language annotation resolves to nothing`、`按需语言经别名解析后都能取到脚本`、`基座语言不需要额外加载` 通过。

# 迁入或覆盖的旧测试
迁自旧 PolishTests 与本组合补充：
- `language annotations resolve through hljs aliases`
- `an unknown language annotation resolves to nothing`
- `按需语言经别名解析后都能取到脚本`
- `基座语言不需要额外加载`
