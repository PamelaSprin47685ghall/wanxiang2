module BreadcrumbActionTests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``unavailable parent shows text but does not navigate`` () =
    let b = BreadcrumbAction<int>()
    let v = b.Set(Some("来自已删除", "已删除", false, 3))
    Assert.True v.visible
    Assert.False v.clickable
    Assert.Equal(None, b.Activate())
    b.Set(Some("来自 A", "回到 A", true, 4)) |> ignore
    Assert.Equal(Some 4, b.Activate())
    b.Set None |> ignore
    Assert.Equal("", b.Text)
    Assert.Equal(None, b.Activate())
