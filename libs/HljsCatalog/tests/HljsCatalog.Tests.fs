module HljsCatalogTests
open Xunit
open Wanxiang.UI

let private cat = HljsCatalog.parse """{"builtin":["bash","python","shell"],"onDemand":["fsharp"],"aliases":{"sh":"bash","py":"python","f#":"fsharp","fs":"fsharp"}}"""

[<Fact>]
let ``别名优先，再认基座语言，大小写与空白不敏感`` () =
    Assert.Equal(Some "fsharp", HljsCatalog.resolve cat " F# ")
    Assert.Equal(Some "bash", HljsCatalog.resolve cat "SH")
    Assert.Equal(Some "shell", HljsCatalog.resolve cat "shell")

[<Fact>]
let ``按需语言只经别名到达（与旧实现相同）`` () =
    Assert.Equal(None, HljsCatalog.resolve cat "fsharp")
    Assert.True(HljsCatalog.requiresLoad cat "fsharp")
    Assert.False(HljsCatalog.requiresLoad cat "bash")

[<Fact>]
let ``空白与未知标注为 None`` () =
    for s in [ ""; "   "; "沒有這種語言" ] do Assert.Equal(None, HljsCatalog.resolve cat s)

[<Fact>]
let ``目录格式不对时退化为空目录`` () =
    for j in [ ""; "{"; "[]"; """{"builtin":[]}""" ] do
        Assert.Equal(HljsCatalog.empty, HljsCatalog.parse j)
