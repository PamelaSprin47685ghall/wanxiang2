module GenerationDraftSectionMemoryTests

open Xunit
open Wanxiang.UI

[<Fact>]
let ``切走记位置，切回恢复；没变不动`` () =
    let m = SectionMemory<string, obj>("providers")
    Assert.Equal(Some 0.0, m.Select("tools", Some 120.0))
    Assert.Equal(120.0, m.Offset "providers")
    Assert.Equal(Some 120.0, m.Select("providers", Some 30.0))
    Assert.Equal(30.0, m.Offset "tools")
    Assert.Equal(None, m.Select("providers", Some 999.0))
    Assert.Equal(120.0, m.Offset "providers")

[<Fact>]
let ``位置为 0 不覆盖已记下的位置（内容还没布局好时读到的 0）`` () =
    let m = SectionMemory<string, obj>("a")
    m.ApplyOffset("a", 50.0)
    m.Select("b", Some 0.0) |> ignore
    Assert.Equal(50.0, m.Offset "a")
    m.Select("a", Some 0.0) |> ignore
    Assert.Equal(0.0, m.Offset "b")

[<Fact>]
let ``每个分区只构建一次，失效后重建`` () =
    let m = SectionMemory<string, obj>("a")
    Assert.True((m.TryBuilt "a").IsNone)
    let x = obj ()
    m.Keep("a", x)
    Assert.Same(x, (m.TryBuilt "a").Value)
    m.Invalidate [ "a" ]
    Assert.True((m.TryBuilt "a").IsNone)
