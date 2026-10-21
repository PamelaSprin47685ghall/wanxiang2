module ChangeWatchTests

open Xunit
open Wanxiang.UI

[<Fact>]
let ``视口宽度：首次读到算变化，相同不算，再变才算`` () =
    let w = ChangeWatch<float>()
    Assert.True(w.Observe 800.0)
    Assert.False(w.Observe 800.0)
    Assert.True(w.Observe 640.0)
    Assert.Equal(Some 640.0, w.Last)

[<Fact>]
let ``恢复代号：从初值 0 起，同值不触发重连`` () =
    let w = ChangeWatch<int>(Some 0)
    Assert.False(w.Observe 0)
    Assert.True(w.Observe 1)
    Assert.False(w.Observe 1)
